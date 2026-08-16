# Inteligência de código

Este documento reúne o **raciocínio** por trás do código: os padrões de
projeto escolhidos e por quê, o trabalho de engenharia reversa feito em
cada fonte de scraping, os bugs reais encontrados durante o desenvolvimento
(e como foram corrigidos), e as limitações conhecidas e assumidas
conscientemente. É o tipo de conhecimento que normalmente se perde depois
que o código é escrito — por isso está registrado aqui, e não só espalhado
em comentários.

---

## 1. Padrões de projeto usados

### Strategy (nos scrapers)

Quatro fontes de dados completamente diferentes (`santo.cancaonova.com`,
`liturgia.cancaonova.com`, `vatican.va`, `gcatholic.org`) precisam ser
tratadas de forma intercambiável pelo resto do sistema. A camada
`Application` define **interfaces** (`ISaintOfTheDayScraper`,
`ILiturgyScraper`, `ILiturgicalCalendarScraper`, `IHomilyScraper` — em
`Scriptorium.Application/Interfaces/IScrapers.cs`) que descrevem **o quê**
precisa ser feito ("buscar o santo de uma data"); cada classe concreta em
`Scriptorium.Infrastructure/Scrapers/` decide **como** fazer isso.

Consequência prática: se `santo.cancaonova.com` sair do ar um dia, basta
escrever uma nova classe implementando `ISaintOfTheDayScraper` e trocar uma
linha de registro de DI em `ServiceCollectionExtensions.cs` — nada mais no
sistema precisa mudar.

### Repository (`IDevotionalRepository`)

A camada `Application` não conhece Entity Framework Core — ela só conhece
a interface `IDevotionalRepository`. Isso mantém a "Regra da Dependência"
da Clean Architecture (camadas internas nunca dependem de detalhes técnicos
externos) e permitiria, em teoria, trocar SQLite por outro banco sem tocar
em nenhuma linha de `Application` ou `API`.

### Options Pattern (`LmStudioOptions`, `WorkerScheduleOptions`)

Em vez de ler variáveis de ambiente "na mão" espalhadas pelo código
(`Environment.GetEnvironmentVariable("...")`, frágil a erros de digitação),
a configuração é centralizada em classes fortemente tipadas, vinculadas a
seções do `appsettings.json`/variáveis de ambiente pelo próprio ASP.NET
Core. É esse mecanismo que permite `LmStudio__BaseUrl` (variável de
ambiente) sobrescrever `LmStudio:BaseUrl` (JSON) automaticamente, sem
nenhum código extra — usado diretamente pelo `docker-compose.yml` (ver
[01-infraestrutura.md](01-infraestrutura.md)).

### Result Object em vez de exceções para falhas esperadas

`TranslationAttemptResult` (`Success`/`TranslatedText`/`ErrorMessage`) e o
retorno `null` dos scrapers em caso de falha de scraping são usados **de
propósito** em vez de lançar exceções. O motivo: falha do LM Studio estar
desligado, ou de um site fora do ar, são **cenários esperados e
recorrentes** neste projeto — não são "o inesperado" que uma exceção
deveria representar. Usar exceções para fluxo de controle esperado é
custoso em performance e torna o código de chamada mais difícil de ler.

### CQRS simplificado (separação Worker escreve / API lê)

A API nunca faz scraping nem chama a IA de tradução — só lê o SQLite
(rápido, sem depender da disponibilidade de sites externos). O Worker é o
único processo que escreve. Essa separação de responsabilidades é uma
aplicação enxuta do padrão CQRS (Command Query Responsibility
Segregation), sem a complexidade de um CQRS "de livro-texto" (sem barramento
de comandos, sem event sourcing) — só a divisão de papéis, que já resolve
o problema real do projeto.

### Injeção de Dependência via `IServiceScopeFactory` no Worker

`DailyDevotionalWorker` é registrado como `Singleton` (todo
`IHostedService` é), mas `ScriptoriumDbContext` **não é thread-safe** e não
deve viver pela duração inteira da aplicação. Por isso, cada rodada de
raspagem cria seu próprio `IServiceScope` (`scopeFactory.CreateScope()`) —
replicando manualmente, uma vez por dia, o mesmo padrão que o ASP.NET Core
aplica automaticamente a cada requisição HTTP.

---

## 2. Engenharia reversa das 4 fontes de scraping

Nenhuma das 4 fontes oferece uma API oficial — todo o acesso é feito
raspando o HTML público, exatamente como um navegador faria. Boa parte do
trabalho de implementação foi **descobrir** a estrutura real de cada site
antes de escrever o parser.

### `santo.cancaonova.com` e `liturgia.cancaonova.com`

Ambos os sites rodam o **mesmo tema/plugin WordPress**
(`cancaonova_calendar_widget`). A navegação por data no widget de
calendário visível na página **não usa uma URL simples** (tipo
`?mes=9&ano=2026`) — os links de "mês anterior"/"próximo mês" disparam
`javascript:void(0)`, ou seja, a navegação real acontece via **AJAX**.

Descoberta: lendo o arquivo JavaScript carregado pela página
(`cancaonova_calendar_widget/static/js/calendar.js`), encontramos a chamada
real:

```js
$.post(admin_ajax, {
  action: 'widget-ajax',
  sMes: mes, sAno: ano, title: title, type: type, ajax: true
}, ...)
```

Isso foi replicado diretamente com `HttpClient` em
`CancaoNovaCalendarHelper.FindArticleUrlForDateAsync`: um `POST` para
`/wp-admin/admin-ajax.php` com esses campos de formulário devolve um
pedaço de HTML com a tabela do calendário daquele mês, de onde extraímos o
`href` do dia pedido. O `type` muda entre os dois sites (`"santo"` vs.
`"liturgia"`), mas o mecanismo é idêntico — daí o helper compartilhado.

**Limitação assumida conscientemente**: cada consulta de data faz **2
requisições HTTP** (1 para achar o link do dia via AJAX, 1 para ler o
artigo). Aceitável para o volume do projeto (Worker roda 1x/dia, no máximo
7 dias por rodada); documentado no código como um ponto de melhoria futuro
(cachear o calendário do mês, como já é feito para o gcatholic.org).

#### Estrutura do artigo do Santo do Dia

```html
<h1 class="entry-title"><span>NOME DO SANTO</span></h1>
<div class="entry-content content-santo">
  <ul id="share-buttons">...</ul>  ← removido antes de extrair o texto
  <p>...biografia em parágrafos...</p>
</div>
```

#### Estrutura do artigo de Liturgia Diária

```html
<span class="cor-liturgica">Cor Litúrgica: Branco</span>
<h1 class="entry-title">Título do dia litúrgico</h1>
<ul id="leituraTab">
  <li><a href="#liturgia-1"><label class="tipo-titulo">1ª Leitura</label><div class="referencia">Ap 11,19a...</div></a></li>
  <li><a href="#liturgia-2"><label class="tipo-titulo">Salmo</label>...</a></li>
  <li><a href="#liturgia-4"><label class="tipo-titulo">Evangelho</label>...</a></li>  ← note o "4", não "3"!
</ul>
<div id="liturgia-1" class="tab-pane">...texto da 1ª leitura...</div>
<div id="liturgia-2" class="tab-pane">...texto do salmo...</div>
<div id="liturgia-4" class="tab-pane">...texto do evangelho...</div>
```

Ver **bug real #1** abaixo — essa numeração não-sequencial dos painéis foi
a causa de um bug encontrado durante os testes.

### `vatican.va` (homilias do Papa)

Estrutura em duas partes:

1. **Índice anual**: `/content/leo-xiv/pt/homilies/{ano}.index.html` lista
   todas as homilias do ano. O nome de arquivo de cada link sempre começa
   com a data no formato `YYYYMMDD`
   (`20260815-omelia-castelgandolfo.html` = 15/08/2026) — é esse prefixo
   que casa uma homilia com a data pedida, já que o site não organiza isso
   numa tabela de calendário como os sites da Cancão Nova.
2. **Página do artigo**: o corpo do texto fica em
   `<div class="text parbase vaticanrichtext">` — só que o **cabeçalho**
   (título litúrgico, local, data) usa as MESMAS classes **mais** a classe
   `abstract`. O XPath usado exclui explicitamente nós com `abstract` para
   pegar só o corpo real da homilia.

**Fallback PT → EN**: como a página consultada já é o índice em
português, o texto normalmente já vem traduzido oficialmente. Quando a
tradução oficial em PT ainda não foi publicada, a página existe mas o
corpo do texto vem vazio/curto demais (heurística: menos de 100
caracteres) — nesse caso, o scraper segue o link em inglês presente no
bloco `translation-field` da própria página e devolve o texto original em
inglês, deixando a tradução por conta do `LmStudioTranslationService`
(exatamente o cenário que o requisito "homilias em inglês devem ser
traduzidas" descreve).

**Caso extremo descoberto e corrigido**: quando a tradução em PT NUNCA foi
sequer iniciada (não é só "vazia", é **inexistente**), o Vaticano nem
publica um link no `<h2>` do índice — o título vem como texto puro, sem
`<a href>`. Testado com dois casos reais (homilias de exéquias de
Cardeais, 18/06/2026 e 15/05/2026, nenhuma com entrada "PT" no bloco de
traduções). Ver **bug real #4** abaixo.

### `gcatholic.org` (calendário litúrgico anual)

Uma **única página cobre o ano inteiro** — cada dia é uma linha de tabela
identificada por `<tr id="MMDD">` (ex: `id="0815"` = 15 de agosto).

```html
<tr id="0815">
  <td><span class="zdate">15</span></td>
  <td><span class="zdate">Sábado</span></td>
  <td><a class="feast" title="Solenidade">S</a></td>   ← rank, só existe se houver celebração especial
  <td><p class="indent">
    <span class="feastw"></span>                        ← COR (letra final: w/g/r/v/p/b)
    <span class="feast1">Assunção da Virgem Santa Maria</span>
  </p></td>
</tr>
```

**Mapeamento de cor descoberto por inspeção** (contando ocorrências de cada
classe no HTML real e cruzando com o contexto — ex: linhas de "Tempo
Comum" sempre usam `feastg`, linhas de solenidade sempre `feastw`):

| Classe CSS | Cor litúrgica |
|---|---|
| `feastw` | Branco |
| `feastg` | Verde |
| `feastv` | Roxo |
| `feastr` | Vermelho |
| `feastp` | Rosa |
| `feastb` | Preto |

**Cache em memória**: como a página cobre o ano inteiro (~170KB), o
`GCatholicCalendarScraper` é registrado como **Singleton** (diferente dos
outros scrapers, que são `Scoped`) e mantém um `ConcurrentDictionary<int,
Task<HtmlDocument?>>` — baixa a página do ano **uma única vez** e reutiliza
para todos os dias daquele ano consultados na mesma execução do Worker.

**Limitação assumida — ciclo dominical (A/B/C)**: o ciclo litúrgico
"oficial" muda no 1º Domingo do Advento (final de novembro), não em 1º de
janeiro. A URL usada (`/calendar/{ano}/General-{ciclo}-pt`) segue o mesmo
comportamento do próprio site: uma página por **ano civil**, rotulada com
um único ciclo dominante. `ComputeSundayCycle` replica essa aproximação
(ano civil, não ano litúrgico exato) — suficiente para cruzar/validar a
cor litúrgica do dia, mas não deveria ser usado como fonte de cálculo
litúrgico oficial preciso para as últimas semanas de dezembro.

---

## 3. Bugs reais encontrados durante o desenvolvimento

### Bug #1 — Numeração dos painéis de leitura não é sequencial

**Sintoma observado**: ao testar o Worker contra a internet real, o
Evangelho de um dia de semana comum (sem 2ª Leitura) foi salvo com
`"text": ""` (vazio), enquanto a Referência (`Mt 19,16-22`) veio correta.

**Causa raiz**: o código original de `CancaoNovaLiturgyScraper` assumia que
o N-ésimo item da lista de abas (`<li>` dentro de `#leituraTab`) sempre
correspondia ao painel `liturgia-N` (contador incremental: 1ª aba →
`liturgia-1`, 2ª aba → `liturgia-2`, ...). Isso é verdade em domingos/
solenidades (4 abas: 1,2,3,4), mas **falso** em dias de semana comuns: como
não há 2ª Leitura, a lista tem só 3 `<li>`, mas o painel do Evangelho
continua se chamando `liturgia-4` (a posição fixa reservada a ele no
esquema do site) — a numeração NÃO é reindexada.

**Correção**: em vez de contar posições, o scraper agora lê o número do
painel **diretamente do atributo `href`** de cada aba (`href="#liturgia-4"`),
a única fonte confiável dessa correspondência. Ver
`CancaoNovaLiturgyScraper.ExtractReadings`.

**Como foi encontrado**: rodando o Worker de ponta a ponta contra a
internet real (não só compilando) para dois dias reais — um domingo e uma
segunda-feira comum — e inspecionando o conteúdo salvo no SQLite linha por
linha. O bug só aparece em dias SEM 2ª Leitura, então testar só com um
domingo (que sempre tem 4 leituras) não o revelaria.

### Bug #2 — URL do LM Studio duplicada no `docker-compose.yml`

**Sintoma observado (relatado pelo usuário)**: editar o IP do LM Studio no
`docker-compose.yml` (via CasaOS) não surtia efeito — o valor antigo
continuava sendo usado.

**Causa raiz**: a primeira versão do `docker-compose.yml` declarava
`${LM_STUDIO_BASE_URL:-http://IP:PORTA}` **duas vezes**, uma em cada
serviço (`scriptorium-api` e `scriptorium-worker`), como duas strings
completamente independentes. O diff da edição do usuário confirmou
exatamente isso: só o valor do `scriptorium-worker` havia mudado; o da
`scriptorium-api` continuava com o valor antigo — porque nunca estiveram
de fato ligados.

**Correção**: introduzida uma **âncora YAML**
(`x-lm-studio-url: &lm-studio-url "..."`, no topo do arquivo) referenciada
nos dois serviços via `*lm-studio-url`. Agora existe uma única declaração
no arquivo inteiro; os dois serviços literalmente compartilham o mesmo
valor de YAML, não duas cópias que precisam ser mantidas manualmente em
sincronia. Detalhes de uso em
[01-infraestrutura.md](01-infraestrutura.md#configurando-o-ip-do-lm-studio).

**Lição geral**: qualquer valor de configuração que precise ser **idêntico**
em múltiplos lugares é, por definição, um único dado — duplicar sua
declaração (mesmo que "por conveniência" ou copy-paste) cria a
possibilidade estrutural de divergência. A âncora YAML resolve isso na
raiz, em vez de depender de disciplina manual para manter cópias
sincronizadas.

### Bug #3 — Timeout HTTP não tratado derrubava o scraper de homilias

**Sintoma observado**: ao testar deliberadamente o `VaticanHomilyScraper`
contra homilias reais sem tradução em PT, uma das chamadas (busca do
índice anual) excedeu o timeout de 30s configurado no `HttpClient` e o
processo **crashou** com uma `TaskCanceledException` não tratada, em vez de
simplesmente registrar a falha e devolver `null`.

**Causa raiz**: o método `GetForDateAsync` só capturava
`HttpRequestException` ao redor da busca do índice — e o método
`TryScrapeArticleAsync` (usado tanto para a versão em PT quanto em EN de
cada artigo) **não tinha nenhum tratamento de erro**. `TaskCanceledException`
(lançada tanto por timeout quanto por cancelamento explícito) não é uma
subclasse de `HttpRequestException`, então escapava de qualquer captura
existente.

**Correção**: extraído um método privado `FetchHtmlOrNullAsync` que
centraliza a chamada HTTP com tratamento para `HttpRequestException` (DNS,
conexão recusada, status HTTP de erro) **e** `TaskCanceledException` por
timeout (usando a cláusula `when (!cancellationToken.IsCancellationRequested)`
para não engolir um cancelamento explícito pedido por quem chamou o
método — mesmo padrão já usado em `LmStudioTranslationService`). Os dois
pontos de rede do scraper (índice e artigo) agora passam por esse método
único.

**Como foi encontrado**: testando deliberadamente o scraper contra datas
reais sem tradução em PT (ver Bug #4 abaixo) — o bug só se manifesta sob
condições de rede lentas/instáveis, então não apareceu nos testes
anteriores contra homilias já traduzidas (que sempre respondiam rápido).

### Bug #4 — Título sem link quando a tradução em PT nunca foi iniciada

**Sintoma observado**: ao testar o fallback PT→EN contra duas homilias
reais confirmadas sem NENHUMA entrada "PT" no bloco de traduções (exéquias
dos Cardeais Camillo Ruini e Emil Paul Tscherrig), o scraper devolvia
`null` — nem tentava a versão em inglês.

**Causa raiz**: o código original exigia `<h2><a href="...">` para
identificar uma homilia candidata. Quando a tradução em PT nunca foi sequer
iniciada, o Vaticano não publica NENHUM link no título dentro do índice em
português — o `<h2>` vem como texto puro. O `continue` do loop pulava esses
itens inteiramente, antes mesmo de checar se a data batia.

**Correção**: quando o `<h2>` não é um link, o scraper agora busca o nome
do arquivo em **qualquer outro idioma disponível** no bloco
`translation-field` (o nome do arquivo é idêntico entre todos os idiomas —
só o segmento de idioma da URL muda) e monta a URL da versão em PT
manualmente a partir do padrão conhecido do site
(`/content/leo-xiv/pt/homilies/{ano}/documents/{arquivo}`), tentando-a
mesmo assim — que pode legitimamente devolver 404 ou vir vazia, ambos os
casos já tratados por `TryScrapeArticleAsync`/`FetchHtmlOrNullAsync` (Bug #3).

**Como foi encontrado**: o usuário pediu explicitamente um teste do
fallback para inglês com uma homilia real sem tradução em PT. Isso exigiu
primeiro localizar, por inspeção manual do índice de 2026, quais entradas
realmente não tinham link "PT" — os dois primeiros bugs (#3 e #4) só
apareceram por causa desse teste direcionado, não teriam sido descobertos
testando só homilias já traduzidas.

**Confirmação final**: com os dois bugs corrigidos, o teste end-to-end
funcionou como esperado — encontrou a homilia de 18/06/2026, seguiu o
fallback para inglês, extraiu 6.679 caracteres de texto real, tentou
traduzir via LM Studio (desligado neste ambiente) e degradou
graciosamente (`Success=False`, mensagem de erro amigável, texto original
preservado) — exatamente o comportamento exigido pelo requisito do projeto.

---

## 4. Limitações conhecidas e assumidas conscientemente

| Limitação | Onde | Por quê é aceitável |
|---|---|---|
| 2 requisições HTTP por data nos scrapers da Cancão Nova | `CancaoNovaCalendarHelper` | Worker roda 1x/dia, no máximo 7 dias por rodada — volume baixo |
| Ciclo dominical (A/B/C) aproximado por ano civil, não ano litúrgico | `GCatholicCalendarScraper.ComputeSundayCycle` | Usado só como fallback/checagem de cor, não como cálculo litúrgico oficial |
| Casamento de homilia por prefixo de data no nome do arquivo | `VaticanHomilyScraper` | É a única correspondência disponível — o site não organiza homilias numa tabela de calendário |
| `GCatholicCalendarScraper` só captura a celebração **principal** do dia (não comemorações secundárias) | `GCatholicCalendarScraper.GetForDateAsync` | Suficiente para determinar a cor litúrgica e o rank do dia, que é o uso real dado ao dado |
| Nenhum teste automatizado (unitário/integração) ainda existe | todo o projeto | Validação feita via build limpo + execução real do Worker contra a internet + inspeção manual do SQLite/API durante o desenvolvimento; testes automatizados são uma melhoria futura natural |
| Build da imagem Docker não testado no ambiente onde o projeto foi gerado | `Dockerfile`/`docker-compose.yml` | Sandbox de desenvolvimento sem permissão no grupo `docker`; sintaxe validada via `docker compose config`, mas o primeiro build real deve ser observado no CasaOS |
| Scraper de homilias assume que a Homily encontrada deve ser vinculada ao dia da MISSA, não ao dia de publicação | `DevotionalBuilderService`/`Homily.HomilyDate` | Corresponde ao uso esperado (mostrar a homilia do dia litúrgico correspondente) |

## 5. Ideias de evolução futura (não implementadas)

- Cache do calendário mensal dos sites da Cancão Nova (reduzir de 2 para 1
  requisição HTTP por data, no mesmo espírito do cache já existente no
  `GCatholicCalendarScraper`).
- Testes automatizados: testes unitários para o mapeamento de cor/tipo de
  leitura (funções puras, fáceis de testar) e testes de integração contra
  fixtures de HTML salvas localmente (evitando dependência de rede nos
  testes).
- Endpoint administrativo na API para forçar o reprocessamento manual de um
  dia específico (reaproveitando `DevotionalBuilderService`, que já vive na
  camada `Application` e não depende do Worker).
- Mecanismo de backup automatizado do SQLite (hoje é um procedimento
  manual — ver [01-infraestrutura.md](01-infraestrutura.md#backup-do-banco-de-dados)).
