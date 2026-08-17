# Limitar recursos do build (docker buildx build --memory=...)

Status: **proposta em avaliação** — não implementada.

## O problema observado

Ao construir a imagem do `scriptorium-api` (via `docker compose build
scriptorium-api`), o build bate CPU e memória em 100% no host. Isso não
acontece com os outros apps Python da homelab.

## Por que isso acontece (e por que não é um bug)

O estágio de build do `Scriptorium/Dockerfile` usa a imagem
`mcr.microsoft.com/dotnet/sdk:8.0` (~800MB) e roda:

```dockerfile
RUN dotnet restore Scriptorium.sln
...
RUN dotnet publish src/Scriptorium.API/Scriptorium.API.csproj -c Release -o /app/api --no-restore
RUN dotnet publish src/Scriptorium.Worker/Scriptorium.Worker.csproj -c Release -o /app/worker --no-restore
```

Isso é compilação de verdade: `dotnet restore` baixa pacotes NuGet,
`dotnet publish` roda o compilador Roslyn e o MSBuild para gerar os
binários finais. É um processo naturalmente intensivo em CPU, e por padrão
não tem nenhum teto — ele usa o quanto o host permitir.

Aplicações Python normalmente não passam por essa etapa (interpretadas ou,
no máximo, compiladas para bytecode leve), por isso não geram esse mesmo
pico durante o "build" (geralmente só um `pip install`, que é leve
comparado a compilar uma solução .NET inteira).

**Importante:** isso é um custo pontual de build, não de runtime. Uma vez
que a imagem final existe, os containers `scriptorium-api` e
`scriptorium-worker` usam a imagem `aspnet`/`runtime` (bem mais leve que a
`sdk`) e já têm limites de recursos aplicados via `deploy.resources` /
`mem_limit` no `docker-compose.yml` (256MB cada). O problema é
especificamente durante o comando de build, não durante a operação diária
do sistema.

## Opção avaliada: limitar recursos do build

Docker Buildx permite restringir CPU/memória do processo de build, do
mesmo jeito que já se faz para os containers em runtime:

```bash
docker buildx build \
  --memory=1g \
  --memory-swap=1g \
  --cpu-quota=100000 \
  -t abbatia/scriptorium-api:latest \
  --target api-final \
  ./Scriptorium
```

ou, se preferir manter o fluxo via `docker compose build`, algumas
versões do Compose aceitam configurar limites de build através de
variáveis de ambiente do BuildKit (`BUILDKIT_...`) ou executando o build
via `docker buildx bake` com um arquivo de configuração próprio — vale
testar qual caminho a versão instalada do Docker/Compose no CasaOS
suporta, já que o suporte a flags de recurso no build varia bastante entre
versões.

## Outras opções complementares (menor esforço, ganho parecido)

- **Cache de camada do NuGet entre builds** (`--mount=type=cache` no
  `RUN dotnet restore`, recurso do BuildKit) — não evita o pico de CPU do
  `dotnet publish`, mas evita repetir o download/restore de pacotes em
  builds subsequentes quando só o código-fonte muda.
- **Manter o escalonamento de build já documentado no `docker-
  compose.yml`** (buildar `scriptorium-api`, depois `scriptorium-worker`,
  depois `oratorium`, um comando por vez) — já mitiga builds *simultâneos*
  disputando recursos; limitar por `buildx` mitigaria também o pico de
  *um único* build.

## Recomendação

Testar `--memory`/`--cpu-quota` num build manual primeiro (fora do
Compose) para confirmar que a versão do Docker no CasaOS realmente respeita
esses limites no build (nem todo setup honra isso da mesma forma que
honra em runtime). Se funcionar bem, formalizar isso como parte do
processo de build documentado (talvez um script `build.sh` na raiz do
projeto, em vez de comandos soltos no `docker-compose.yml`).
