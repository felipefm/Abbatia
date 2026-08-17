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

### CQRS simplificado (separação Worker escreve / API lê) + cache-miss sob demanda

No CAMINHO NORMAL, a API nunca faz scraping nem chama a IA de tradução —
só lê o SQLite (rápido, sem depender da disponibilidade de sites
externos). O Worker é o único processo que escreve de forma agendada.
Essa separação de responsabilidades é uma aplicação enxuta do padrão CQRS
(Command Query Responsibility Segregation), sem a complexidade de um CQRS
"de livro-texto" (sem barramento de comandos, sem event sourcing) — só a
divisão de papéis, que já resolve o problema real do projeto.

Desde a navegação livre por calendário no Oratorium (ver seção "Navegação
livre por calendário e busca sob demanda", abaixo), essa regra ganhou UMA
exceção deliberada: se a API recebe um pedido para uma data que não está
no banco, ela tenta montar o devocional na hora (reusando
`DevotionalBuilderService`, o mesmo orquestrador do Worker) e salva o
resultado, em vez de simplesmente devolver 404. Continua sendo "CQRS
simplificado" no dia a dia (99% das leituras batem no cache que o Worker
já preparou), só que agora com um mecanismo de "preencher o cache sob
demanda" para o 1% de vezes em que o usuário pede algo fora da janela
que o Worker mantém quente.

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

### Bug #5 — IP errado copiado do LM Studio para o Oratorium

**Sintoma observado**: após corrigir o Bug #6 (abaixo), o front carregava
mas mostrava "NetworkError when attempting to fetch resource" — a aba
Rede do navegador mostrava uma tentativa de `GET` para
`192.168.0.2:8110/today` terminando em `NS_ERROR_CONNECTION_REFUSED`,
enquanto o usuário acessava o app em `192.168.0.11:8111`.

**Causa raiz**: o valor padrão de `ORATORIUM_API_BASE_URL` no
`docker-compose.yml` foi escrito como `http://192.168.0.2:8110` — um
copy-paste do IP usado como padrão de `LM_STUDIO_BASE_URL` (âncora
`x-lm-studio-url`, ver Bug #2), que é o endereço de uma máquina
DIFERENTE (a que roda o LM Studio) do IP real do CasaOS do usuário
(`192.168.0.11`).

**Correção**: valor corrigido para `http://192.168.0.11:8110`. Diferente
do Bug #2 (duas cópias do MESMO valor fora de sincronia), aqui era um
único valor, só que ERRADO desde o início — âncora YAML não ajuda nesse
caso, porque LM Studio e CasaOS são propositalmente endereços diferentes,
não deveriam compartilhar a mesma fonte de verdade.

**Como foi encontrado**: o usuário reportou o erro com prints do DevTools
do navegador (aba Rede) mostrando exatamente qual endereço a requisição
tentou alcançar — sem essa evidência visual, a causa (IP de outra
máquina) não seria óbvia só a partir da mensagem genérica "NetworkError".

### Bug #6 — CORS ausente bloqueava o front de ler a resposta da API

**Sintoma observado**: mesmo depois do Bug #5 corrigido (IP certo), o
front continuava mostrando "NetworkError". A aba Rede mostrava a chamada
para `today`/`{data}` com status `200 OK` e o corpo JSON correto visível
no painel de Resposta — mas com o aviso "O corpo da resposta não está
disponível para scripts (motivo: CORS Missing Allow Origin)".

**Causa raiz**: `Scriptorium.API` nunca enviava cabeçalhos
`Access-Control-Allow-*`. Isso não é um erro de servidor (por isso o
status HTTP era 200 e os dados vinham certos) — é a *Same-Origin Policy*
do PRÓPRIO NAVEGADOR bloqueando o JavaScript do Oratorium de LER uma
resposta vinda de uma origem diferente (`192.168.0.11:8110`, a API, é uma
origem distinta de `192.168.0.11:8111`, o Oratorium, mesmo estando no
mesmo host — porta diferente já conta como origem diferente).

**Correção**: adicionado `builder.Services.AddCors(...)` com
`AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()` e `app.UseCors()` em
`Scriptorium.API/Program.cs` (antes do mapeamento dos endpoints).
`AllowAnyOrigin` (em vez de uma lista de origens específicas) foi escolha
deliberada: a API só expõe leitura pública sem autenticação/cookies, não
há dado sensível em jogo, e evita introduzir mais um endereço para manter
sincronizado (a mesma classe de problema do Bug #2/#5) — `AllowCredentials()`
não é usado, o que é coerente (o navegador proíbe combiná-lo com
`AllowAnyOrigin`, e a API não usa cookies/sessão mesmo).

**Como foi encontrado**: o usuário reportou "a API está respondendo, mas
a tela não mostra nada" com dois prints do DevTools — um da aba Rede
mostrando o `200 OK`, outro do painel de Resposta mostrando o aviso de
CORS explicitamente. Sem essas duas evidências juntas (resposta 200 +
aviso específico de CORS), o sintoma isolado ("não mostra nada") seria
indistinguível de um erro de IP (Bug #5) ou de um bug de renderização no
React.

**Lição geral**: erros de CORS são silenciosos do lado do servidor — não
aparecem em nenhum log da API, só no DevTools do navegador de quem está
acessando. Isso reforça o valor de pedir print da aba Rede/Console
sempre que um frontend "não mostra nada" sem mensagem de erro clara.

### Bug #7 — "Hoje" virava amanhã 3 horas antes da meia-noite (UTC vs. Brasília)

**Sintoma observado**: o usuário reportou, às 22h40 de um domingo (hora de
Brasília), que o app já mostrava o devocional de **segunda-feira**.

**Causa raiz**: tanto `GET /api/devotional/today` quanto o cálculo de
"quais dias processar" no `DailyDevotionalWorker` usavam
`DateOnly.FromDateTime(DateTime.UtcNow)` para decidir qual é "o dia de
hoje". Containers Docker rodam em UTC por padrão, e Brasília está 3 horas
ATRÁS do UTC — então, a partir das 21h em Brasília, o relógio UTC do
container já virou a data seguinte. Confirmado ao vivo neste ambiente: às
22h41 de 16/08 (domingo) em Brasília, `DateTime.UtcNow` já marcava
17/08 (segunda) 01h41.

**Correção**: criado `Scriptorium.Domain.LiturgicalClock`, uma classe
estática com um único método `Today()` que converte
`DateTime.UtcNow` para o fuso `America/Sao_Paulo` (via
`TimeZoneInfo.FindSystemTimeZoneById` + `ConvertTimeFromUtc`) antes de
extrair o `DateOnly`. Os dois pontos que decidiam "hoje" (endpoint
`/today` na API e o laço de processamento do Worker) passaram a chamar
esse método único, em vez de repetir a lógica — evitando, de novo, a
classe de bug de "dois lugares que deveriam concordar mas não são a mesma
fonte" (Bug #2, #5). Timestamps de auditoria (`UpdatedAtUtc`) e o
agendamento do Worker (`WorkerSchedule__HourUtc`) permanecem em UTC de
propósito — só "que dia é hoje para o usuário" precisa do fuso local,
tudo o mais (logs, ordenação, agendamento de tarefa) continua correto e
mais simples em UTC.

**Por que `TimeZoneInfo` explícito em vez de configurar o fuso do SO do
container**: depender da variável `TZ`/configuração do sistema operacional
do container criaria mais um valor de configuração para manter
sincronizado entre API, Worker e qualquer ambiente futuro — e falharia
silenciosamente (voltando para UTC) se alguém esquecesse de definir a
variável em um dos dois serviços. Resolver o fuso explicitamente uma
única vez, em código, é uma fonte única de verdade que não depende de
nenhuma configuração externa.

**Como foi encontrado**: relato direto do usuário testando o app em
produção no horário exato em que o bug se manifesta (perto das 21h de
Brasília) — o tipo de bug de fuso horário que só aparece em uma janela
específica de 3 horas por dia, fácil de não perceber testando em outros
horários.

### Bug #8 — Listas (`<ul>`/`<li>`) da biografia do santo eram ignoradas silenciosamente

**Sintoma observado**: o usuário colou o texto completo da página real de
Santo Estêvão da Hungria (santo.cancaonova.com) e apontou que várias
seções não apareciam no Oratorium — entre elas a lista "Outros santos e
beatos celebrados em 16 de agosto" (18 nomes) e a lista de "Fontes"
(bibliografia).

**Causa raiz**: `HtmlTextExtractor.ExtractParagraphs` (usado pelo
`CancaoNovaSaintScraper`) só selecionava nós `<p>` via XPath (`.//p`). No
HTML real do site, os títulos de cada seção ("Origens", "Vida",
"Exemplo de Caridade" etc.) estão dentro de `<p><b>...</b></p>` — por
isso ESSES apareciam normalmente —, mas as duas últimas seções da página
(a lista de outros santos do dia e a bibliografia) são estruturadas como
`<ul><li>...</li></ul>`, não parágrafos. Como o XPath nunca pedia
elementos `<li>`, esse conteúdo era descartado sem nenhum aviso/erro — o
scraper "funcionava" normalmente (sem exceção, sem log de falha), só que
devolvia menos texto do que a página realmente tinha.

**Correção**: adicionado `HtmlTextExtractor.ExtractParagraphsAndListItems`,
que estende a mesma lógica para também selecionar `.//li` (via XPath de
união `.//p | .//li`, que preserva a ORDEM do documento original — o
cabeçalho "Outros santos..." continua vindo antes da lista que o segue).
Cada item de lista é prefixado com "• " no texto puro salvo no banco, para
o item continuar reconhecível como lista mesmo sem HTML. Optou-se por um
método NOVO em vez de mudar o comportamento de `ExtractParagraphs`
existente: os outros dois usos desse utilitário (leituras litúrgicas e
homilias do Papa) nunca precisaram de listas, e mudar o padrão ali sem
necessidade arriscaria puxar ruído inesperado (menus, links relacionados)
para dentro de conteúdo já testado e funcionando em produção — só
`CancaoNovaSaintScraper` foi migrado para o novo método.

**Validação**: rodado contra o HTML real da página (baixado ao vivo do
site durante a investigação) usando a mesma versão do HtmlAgilityPack
usada em produção — confirmado que as 18 entradas de "Outros santos" e as
4 entradas de "Fontes" passam a ser extraídas, na ordem correta, com o
prefixo "• ".

**Como foi encontrado**: o usuário colou o conteúdo completo da página
real ao lado do que o app mostrava, tornando a comparação trivial. Sem
esse tipo de evidência lado a lado, um "scraper que às vezes captura
menos texto que o esperado" é difícil de notar — não há exceção, não há
log de erro, o resultado só é silenciosamente incompleto.

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
- Mecanismo de backup automatizado do SQLite (hoje é um procedimento
  manual — ver [01-infraestrutura.md](01-infraestrutura.md#backup-do-banco-de-dados)).
- Um mecanismo de LOCK por data para a busca sob demanda (ver seção
  "Navegação livre por calendário e busca sob demanda" abaixo) — hoje, se
  duas requisições pedirem a MESMA data ainda não cacheada ao mesmo tempo
  (ex: duplo clique), as duas disparam uma raspagem completa em paralelo.
  Não implementado de propósito: é um app de um usuário só, a chance real
  de colisão é baixíssima, e mesmo colidindo o pior caso é só trabalho
  duplicado (não corrompe dado nenhum — `UpsertAsync` lida bem com a
  segunda gravação chegando depois da primeira).

---

## 6. Decisões de projeto do Oratorium (Frontend)

### Configuração de runtime aplicada PROATIVAMENTE, por causa do Bug #2

O Oratorium (React/Vite) precisa saber o endereço da API do Scriptorium. O
caminho "óbvio" do Vite seria uma variável `VITE_API_BASE_URL` resolvida em
**build-time** — mas isso reproduziria exatamente a classe de problema do
**Bug #2** (URL do LM Studio duplicada e fora de sincronia no
`docker-compose.yml`, relatada pelo usuário): mudar o endereço da API
exigiria reconstruir a imagem Docker inteira do frontend, e um valor
"gravado" no bundle é fácil de esquecer que existe.

Por isso o Oratorium foi desenhado, desde a primeira versão, com um padrão
de configuração de RUNTIME: um arquivo `public/env-config.js`, carregado
antes do bundle React, populado por um script de entrypoint do nginx a
partir de uma variável de ambiente do container
(`ORATORIUM_API_BASE_URL`). Ver o passo a passo completo em
[01-infraestrutura.md](01-infraestrutura.md#configuração-da-api-em-runtime-não-em-build-time).
Essa é uma aplicação direta da "Lição geral" registrada no Bug #2: em vez
de esperar o mesmo tipo de bug se repetir no frontend, o padrão de "fonte
única de configuração, editável sem rebuild" foi aplicado de propósito
antes mesmo de existir um problema.

### Testando um app React sem navegador disponível

O ambiente onde este projeto foi desenvolvido não tinha um navegador
gráfico nem permissão para instalar as bibliotecas de sistema exigidas por
um Chromium headless (uma tentativa real com Playwright falhou por
`libatk-1.0.so.0` ausente, que exigiria `apt-get install` — sem sudo
disponível). Em vez de declarar a UI "não testável" e seguir em frente sem
verificação, a solução foi usar a ferramenta padrão do próprio ecossistema
React para esse cenário: **Vitest + Testing Library**, que renderiza
componentes React reais num DOM simulado (`jsdom`) sem precisar de um
motor de navegador de verdade.

O detalhe importante: os testes em `src/App.smoke.test.tsx` fazem
requisições HTTP **reais** contra uma instância genuína do
`Scriptorium.API` (com dados reais, raspados de verdade das fontes do
projeto) — não usam mocks. Isso significa que o teste valida a integração
real entre frontend e backend (contratos de DTO batendo, tratamento de
erro HTTP 400/404 correto), não apenas a lógica isolada dos componentes.
Ver detalhamento completo, incluindo a limitação honesta sobre o que esse
tipo de teste NÃO cobre (aspectos puramente visuais), em
[03-codigo.md](03-codigo.md#como-o-oratorium-foi-testado-sem-navegador).

---

## 7. Navegação livre por calendário e busca sob demanda

Pedido do usuário: poder abrir QUALQUER data pelo Oratorium (não só os 7
dias que o Worker mantém atualizados), com um seletor de calendário no
topo da página — e, se a data escolhida ainda não estiver no banco, o
sistema deveria tentar buscá-la ao vivo em vez de simplesmente dizer "não
encontrado".

### Frontend: `<input type="date">` nativo, sem biblioteca extra

`DateNav.tsx` ganhou um `<input type="date">` entre os botões "Anterior"/
"Próximo". Foi uma escolha deliberada NÃO usar uma biblioteca de
calendário (ex: react-datepicker): o navegador já desenha um seletor de
mês/ano completo de graça, inclusive otimizado para toque no celular (onde
o PWA roda de verdade) — adicionar uma dependência só duplicaria algo que
a plataforma já oferece. O único ajuste necessário foi
`dark:[color-scheme:dark]`, para o POPUP nativo do calendário (não só a
caixa de texto) respeitar o tema escuro do app — sem isso, o popup do
seletor de data aparece sempre no estilo claro do sistema operacional,
independente do tema escolhido no Oratorium.

### Backend: cache-miss vira busca ao vivo, não um 404 imediato

`DevotionalEndpoints.FetchAndRespondAsync` (ver também a nota sobre CQRS
na seção 1) agora, ao não achar a data no banco:

1. Confere se a data está dentro de um intervalo de sanidade
   (`2000-01-01` até 5 anos no futuro a partir de hoje) — fora disso,
   devolve 404 IMEDIATO, sem tentar raspar nada (nenhuma das 4 fontes
   publica calendário litúrgico fora dessa janela; tentar seria
   desperdício de tempo e de requisições aos sites externos).
2. Dentro do intervalo, chama `DevotionalBuilderService.BuildAsync` — o
   MESMO orquestrador que o Worker usa de madrugada, já preparado para
   isso desde o início (ver comentário original na classe: "permite,
   por exemplo, expor essa mesma lógica futuramente por um endpoint HTTP
   manual... sem duplicar nada").
3. Se pelo menos UMA das 4 fontes trouxe algo (santo, leitura ou
   homilia), salva no banco (`UpsertAsync`) e devolve 200 — a partir daí,
   qualquer consulta futura a essa mesma data (de qualquer dispositivo)
   vem do cache, instantânea.
4. Se as 4 fontes vierem vazias mesmo assim, devolve 404 (a data existe
   dentro do intervalo suportado, mas nenhuma fonte tinha conteúdo real
   para ela).

**Por que isso é seguro dentro dos timeouts já existentes**: cada um dos 4
scrapers já tem seu próprio timeout de 30s (rodam em paralelo via
`Task.WhenAll`, então o pior caso é ~30s, não 4×30s), e a tradução via LM
Studio tem seu próprio timeout de 120s configurável — E, mais importante,
**nenhum desses timeouts propaga uma exceção não tratada**: cada scraper é
isolado por `SafeScrapeAsync` (falha de um não derruba os outros) e o
serviço de tradução já captura sua própria `TaskCanceledException`
internamente (ver Bug #3), sempre devolvendo um resultado válido
(`Success=false` + texto original preservado) em vez de propagar o erro.
Ou seja: o endpoint sob demanda reaproveita 100% do tratamento de erro que
já existia para o Worker — não precisou de nenhum try/catch novo na API
para ficar seguro contra timeout/site fora do ar.

**Validado ao vivo** (não só lido no código): rodei a API localmente e
pedi uma data bem no futuro, fora da janela do Worker — a primeira
consulta levou ~4s e trouxe uma biografia real de uma santa raspada na
hora (Santa Beatriz da Silva); a segunda consulta à mesma data, já em
cache, levou ~90ms. Também confirmei que uma data de 1899 (fora do
intervalo suportado) retorna 404 imediatamente, sem tentar raspar nada.

### Frontend: UX para uma busca que pode demorar

Como a PRIMEIRA consulta a uma data nova pode levar de alguns segundos a
cerca de um minuto (scraping + tradução), `useDevotional` passou a expor
uma flag `slow` (fica `true` se o carregamento passar de 4s) — o
`LoadingState` troca a mensagem genérica "Carregando…" por uma explicando
que a API pode estar buscando aquele dia ao vivo pela primeira vez, para
o app não parecer travado numa espera mais longa que o normal. A mensagem
de "não encontrado" (404) também foi atualizada para deixar claro que a
busca ao vivo JÁ foi tentada (não é mais "ainda não publicado, tente mais
tarde" — agora significa "nem ao vivo achamos nada").

### Tipografia das leituras: mais próxima da fonte original

O usuário comparou a leitura do Oratorium com a formatação do site-fonte
(liturgia.cancaonova.com) e pediu para melhorar a legibilidade. Duas
mudanças em `ReadingsList`/`SaintCard`/`HomilyCard`:

- Fonte um pouco maior e mais espaçada (`text-[17px] leading-relaxed` →
  `text-[18px] leading-[1.8]`), mais perto do conforto de leitura do
  site-fonte.
- Números de versículo destacados em negrito dentro do texto corrido
  (ex: "Naqueles dias, **39** Maria partiu…"), como o site original já
  faz visualmente. Como o backend guarda só TEXTO PURO (sem a formatação
  HTML original — ver `HtmlTextExtractor`), essa formatação não existe
  mais no dado por si só; foi recriada no frontend com uma heurística em
  `Paragraphs.tsx`: qualquer número de 1 a 3 dígitos isolado por espaços
  vira `<strong>`. Aplicado SÓ nas leituras (`highlightVerseNumbers`
  passado explicitamente), não na biografia do santo nem na homilia —
  nesses dois, um número solto raramente é um versículo, e o risco de
  negritar algo errado (uma data, uma idade) não vale a pena.
