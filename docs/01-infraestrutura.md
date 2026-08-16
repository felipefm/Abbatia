# Infraestrutura

## Ambiente-alvo

| Item | Valor |
|---|---|
| Host | CasaOS rodando em Debian 13 (trixie), homelab pessoal |
| Orquestração | Docker Compose (2 serviços) |
| Banco de dados | SQLite (arquivo único, em volume Docker) |
| IA de tradução | LM Studio, rodando em OUTRA máquina da rede local (pode estar desligada) |

## Topologia dos containers

O `docker-compose.yml` (na raiz do monorepo) define **dois serviços**,
construídos a partir do **mesmo `Dockerfile`** (multi-stage build, ver
seção abaixo), cada um usando um `target` diferente:

| Serviço | Imagem base final | Papel | Porta exposta |
|---|---|---|---|
| `scriptorium-api` | `aspnet:8.0` | Processo Kestrel; responde HTTP; **só lê** o SQLite | `8110:8080` (host:container) |
| `scriptorium-worker` | `runtime:8.0` | `BackgroundService`; roda de madrugada; **escreve** no SQLite | nenhuma (não recebe tráfego HTTP) |

Os dois containers compartilham o **mesmo arquivo SQLite** através de um
volume Docker **nomeado** (`scriptorium-data`, montado em `/data` dentro de
cada container). Um volume nomeado (em vez de um bind mount para um
caminho fixo do host) foi escolhido por ser mais portátil entre diferentes
instalações do CasaOS — não depende de nenhum caminho absoluto do
filesystem do host existir de antemão. Se você preferir acessar o arquivo
`.db` diretamente pelo File Manager do CasaOS, basta trocar por um bind
mount, por exemplo:

```yaml
volumes:
  - /DATA/AppData/Abbatia/dados-sqlite:/data
```

## O Dockerfile (multi-stage build)

Arquivo: `Scriptorium/Dockerfile`.

Um único Dockerfile compila a solução **uma vez** e gera **duas imagens
finais diferentes**, uma por `target`:

```
FROM sdk:8.0 AS build                 ← compila tudo (pesado, ~800MB, não vai pra imagem final)
  │
  ├─▶ FROM aspnet:8.0 AS api-final    ← runtime ASP.NET Core (~220MB) + binário da API
  │
  └─▶ FROM runtime:8.0 AS worker-final ← runtime puro (~190MB, sem Kestrel) + binário do Worker
```

Motivo de ser um único Dockerfile: API e Worker compartilham 100% do
código das camadas `Domain`, `Application` e `Infrastructure` — só o ponto
de entrada (`Program.cs`) muda. Um estágio de build compartilhado garante
que os dois processos sejam sempre compilados a partir do **mesmo snapshot
de código-fonte**.

Detalhes de segurança/performance embutidos no Dockerfile:

- **Cache de camadas otimizado**: os arquivos `.csproj`/`.sln` são copiados
  e restaurados (`dotnet restore`) **antes** do código-fonte. Isso faz o
  Docker só refazer o `restore` (que baixa pacotes NuGet da internet)
  quando uma dependência muda de verdade, não a cada alteração de um
  arquivo `.cs`.
- **Usuário não-root**: ambas as imagens finais rodam como o usuário `app`
  (já vem pré-criado nas imagens oficiais do .NET 8, UID 64198) em vez de
  `root` — reduz a superfície de ataque caso algum dia haja uma
  vulnerabilidade explorável dentro do container.
- **`/data` com dono correto**: o diretório `/data` é criado e tem seu dono
  ajustado (`chown app:app`) **antes** do `USER app` — necessário porque,
  quando um volume Docker nomeado é montado pela primeira vez sobre um
  diretório vazio da imagem, o Docker copia as permissões desse diretório
  para dentro do volume.

## `docker-compose.yml`

Localização: raiz do monorepo (`/DATA/AppData/Abbatia/docker-compose.yml`).

### Variáveis de ambiente injetadas em cada serviço

| Variável (dentro do container) | Serviço | Propósito |
|---|---|---|
| `ConnectionStrings__Default` | api, worker | Caminho do arquivo SQLite dentro do volume (`Data Source=/data/scriptorium.db`) |
| `LmStudio__BaseUrl` | api, worker | Endereço do servidor LM Studio na rede local |
| `WorkerSchedule__HourUtc` | worker | Hora (UTC) em que a raspagem diária roda (padrão `6` = ~03h em Brasília) |
| `WorkerSchedule__DaysAhead` | worker | Quantos dias à frente manter atualizados (padrão `7`) |
| `WorkerSchedule__RunImmediatelyOnStartup` | worker | Se `true`, roda uma raspagem assim que o container sobe (não espera a próxima madrugada) |

A notação `Chave__Subchave` (com **dois underscores**) é a convenção do
ASP.NET Core para mapear variáveis de ambiente para a estrutura hierárquica
do `appsettings.json` (`Chave: { Subchave: valor }`) — é assim que o
[Options Pattern](02-tecnologias.md) do .NET lê essas configurações sem
nenhum código extra.

### Configurando o IP do LM Studio

Existe **uma única linha** no topo do `docker-compose.yml` que controla o
endereço do LM Studio para os dois serviços simultaneamente:

```yaml
x-lm-studio-url: &lm-studio-url "${LM_STUDIO_BASE_URL:-http://192.168.0.2:1234}"
```

- Isso é uma **âncora YAML** (`&lm-studio-url`) reutilizada via
  **referência** (`*lm-studio-url`) dentro de cada serviço — um recurso
  nativo do formato YAML, não é uma feature específica do Docker.
- Para trocar o IP: edite o valor depois de `192.168.0.2:1234` **nesta
  linha só**. Os dois serviços (`scriptorium-api` e `scriptorium-worker`)
  vão automaticamente usar o mesmo valor, porque tecnicamente é a mesma
  entrada de YAML, não duas cópias.
- **Alternativa via variável de ambiente do host**: se você preferir não
  editar o arquivo, defina `LM_STUDIO_BASE_URL` no shell (ou num arquivo
  `.env` ao lado do `docker-compose.yml`) antes de rodar
  `docker compose up` — o `${LM_STUDIO_BASE_URL:-...}` vai usar esse valor
  em vez do padrão depois dos dois-pontos.
- **Cuidado com editores gráficos**: painéis de "Environment Variables" de
  ferramentas como o CasaOS costumam reescrever o arquivo inteiro e podem
  "achatar" (expandir) a âncora YAML em duas cópias literais de novo. Para
  editar com segurança, prefira sempre a edição direta do arquivo
  `docker-compose.yml` (aba de "Compose File" do CasaOS, ou SSH). O motivo
  desse desenho — e o bug real que ele corrige — está detalhado em
  [04-inteligencia-de-codigo.md](04-inteligencia-de-codigo.md#bug-real-url-do-lm-studio-duplicada).

### Limites de recursos (memória e CPU)

Cada serviço tem um teto de **256MB de memória** (com 128MB reservados) e
**1 CPU**, declarado em dois formatos simultaneamente:

```yaml
deploy:
  resources:
    limits:
      cpus: "1.0"
      memory: 256M
    reservations:
      memory: 128M
mem_limit: 256m
mem_reservation: 128m
```

- `deploy.resources` é o formato moderno da *Compose Specification*,
  respeitado pelo `docker compose up` mesmo fora do modo Swarm nas versões
  atuais do Docker Compose.
- `mem_limit`/`mem_reservation` é o formato legado, que ferramentas de
  gerenciamento gráfico como o **CasaOS** costumam ler/exibir nos seus
  próprios painéis de limite de recursos.
- Os dois são declarados juntos para garantir que o limite valha
  independentemente de qual caminho (CLI pura ou UI do CasaOS) sobe o
  container.
- **Por que 256MB é aceitável**: nem a API nem o Worker fazem nada
  pesado em memória — a API só lê linhas do SQLite e serializa JSON; o
  Worker faz requisições HTTP e faz parsing de HTML com HtmlAgilityPack,
  cujo maior documento (o calendário anual do gcatholic.org) tem ~170KB.
  Na prática ambos os processos rodam confortavelmente com 80-120MB; 256MB
  dá uma margem de segurança generosa sem monopolizar a RAM do servidor
  (importante numa homelab onde o mesmo host roda vários outros apps do
  CasaOS ao mesmo tempo).
- **Nota técnica**: o Garbage Collector do .NET (desde o .NET Core 3.0) é
  *cgroup-aware* — ele detecta automaticamente o limite de memória do
  container (via cgroups do Linux) e dimensiona o heap gerenciado de
  acordo, sem precisar de nenhuma configuração adicional além do limite
  do Docker em si.

### Healthcheck

Só o serviço `scriptorium-api` tem `healthcheck` configurado, batendo no
endpoint `GET /health` (implementado em `Scriptorium.API/Program.cs`):

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
  interval: 30s
  timeout: 5s
  retries: 3
  start_period: 15s
```

O Worker não tem healthcheck porque não expõe nenhum endpoint HTTP —
sendo um processo de background puro, não há uma forma trivial e nativa do
Docker de perguntar "você está saudável?" a ele (o monitoramento de sua
saúde é feito pelos próprios logs — ver seção de troubleshooting abaixo).

### Migrations do banco de dados aplicadas automaticamente

Tanto o `Program.cs` da API quanto o `DailyDevotionalWorker` do Worker
chamam `dbContext.Database.MigrateAsync()` na inicialização — a operação é
**idempotente** (não faz nada se o banco já estiver atualizado), então é
seguro chamar nos dois processos. Isso significa que, ao rodar
`docker compose up -d` pela primeira vez, **nenhum passo manual de
migration é necessário** — qualquer um dos dois containers que subir
primeiro já cria o schema do banco dentro do volume.

## Passo a passo de deploy

```bash
# 1) (opcional) defina o IP do LM Studio via variável de ambiente,
#    OU edite diretamente a âncora x-lm-studio-url no docker-compose.yml
export LM_STUDIO_BASE_URL="http://192.168.0.2:1234"

# 2) a partir da raiz do monorepo (onde está o docker-compose.yml):
docker compose up -d --build

# 3) acompanhe os logs do Worker na primeira execução:
docker compose logs -f scriptorium-worker

# 4) teste a API:
curl http://localhost:8110/api/devotional/today
# ou abra no navegador: http://<ip-do-casaos>:8110/  → Swagger UI
```

Para atualizar depois de alterar código:

```bash
docker compose up -d --build
```

(o `--build` força a reconstrução das imagens a partir do código atual;
sem ele, o Compose reaproveitaria as imagens antigas já existentes).

## Backup do banco de dados

Como o SQLite vive dentro de um volume Docker nomeado, o backup mais
simples é copiar o arquivo de dentro do volume:

```bash
docker compose exec scriptorium-api sh -c "cat /data/scriptorium.db" > backup-scriptorium-$(date +%F).db
```

Ou, via Docker diretamente (sem precisar dos containers rodando):

```bash
docker run --rm -v abbatia_scriptorium-data:/data -v "$PWD":/backup alpine \
  cp /data/scriptorium.db /backup/backup-scriptorium.db
```

(o nome exato do volume pode variar com um prefixo do projeto — confirme
com `docker volume ls`).

## Limitações conhecidas desta configuração

- O build da imagem Docker **não foi testado dentro do ambiente de
  desenvolvimento sandbox** usado para gerar este projeto (o usuário do
  sandbox não tinha permissão no grupo `docker` nem sudo sem senha). A
  sintaxe do `docker-compose.yml` foi validada com `docker compose config`
  e os caminhos do `Dockerfile` foram conferidos manualmente contra a
  estrutura real de pastas — mas o primeiro `docker compose up -d --build`
  de verdade deve ser feito e observado no CasaOS.
- Não há, hoje, nenhum mecanismo automatizado de backup do SQLite — é uma
  tarefa manual (ver seção acima) ou a implementar futuramente.
