# Código

## Estrutura de pastas

```
Scriptorium/
├── Scriptorium.sln
├── Dockerfile
├── .dockerignore
└── src/
    ├── Scriptorium.Domain/          ← entidades e enums puros, zero dependências externas
    │   ├── Entities/
    │   │   ├── DailyDevotional.cs   ← raiz do agregado
    │   │   ├── Reading.cs
    │   │   ├── SaintOfTheDay.cs
    │   │   ├── OtherSaintOfDay.cs
    │   │   ├── Homily.cs
    │   │   └── DiaryEntry.cs        ← independente de DailyDevotional
    │   ├── Enums/
    │   │   ├── LiturgicalColor.cs
    │   │   ├── ReadingType.cs
    │   │   └── TranslationStatus.cs
    │   └── ScrapableDateRange.cs    ← limites de data pro scraping sob demanda
    │
    ├── Scriptorium.Application/     ← casos de uso e contratos (interfaces)
    │   ├── DTOs/ScrapeResults.cs    ← resultados brutos dos scrapers
    │   ├── Interfaces/
    │   │   ├── IScrapers.cs         ← ISaintOfTheDayScraper, ILiturgyScraper, ILiturgicalCalendarScraper, IHomilyScraper, IOtherSaintsScraper
    │   │   ├── ITranslationService.cs
    │   │   ├── IDevotionalRepository.cs
    │   │   └── IDiaryRepository.cs
    │   ├── Services/
    │   │   ├── DevotionalBuilderService.cs  ← orquestra os 5 scrapers + tradução
    │   │   └── SaintNameMatcher.cs          ← dedup entre santo principal e "outros santos"
    │   └── ServiceCollectionExtensions.cs
    │
    ├── Scriptorium.Infrastructure/  ← implementações concretas (EF Core, HTTP, scraping)
    │   ├── Data/
    │   │   ├── ScriptoriumDbContext.cs
    │   │   └── Migrations/
    │   ├── Repositories/
    │   │   ├── DevotionalRepository.cs
    │   │   └── DiaryRepository.cs
    │   ├── Scrapers/
    │   │   ├── HtmlTextExtractor.cs           ← utilitário compartilhado
    │   │   ├── CancaoNovaCalendarHelper.cs     ← lógica compartilhada Santo/Liturgia
    │   │   ├── CancaoNovaSaintScraper.cs
    │   │   ├── CancaoNovaLiturgyScraper.cs
    │   │   ├── VaticanHomilyScraper.cs
    │   │   ├── VaticanNewsOtherSaintsScraper.cs
    │   │   └── GCatholicCalendarScraper.cs
    │   ├── Translation/LmStudioTranslationService.cs
    │   ├── Options/LmStudioOptions.cs
    │   └── ServiceCollectionExtensions.cs
    │
    ├── Scriptorium.API/             ← processo Kestrel (Minimal APIs)
    │   ├── Program.cs
    │   ├── Endpoints/
    │   │   ├── DevotionalEndpoints.cs
    │   │   ├── CalendarEndpoints.cs
    │   │   └── DiaryEndpoints.cs
    │   └── DTOs/
    │       ├── DevotionalResponse.cs
    │       ├── MonthCalendarResponse.cs
    │       └── DiaryEntryResponse.cs
    │
    └── Scriptorium.Worker/          ← BackgroundService
        ├── Program.cs
        ├── DailyDevotionalWorker.cs
        └── Options/WorkerScheduleOptions.cs
```

## Camadas da arquitetura (Clean Architecture)

```
       ┌─────────────┐     ┌─────────────┐
       │ Scriptorium │     │ Scriptorium │
       │    .API     │     │   .Worker   │   ← "delivery mechanisms": só decidem QUANDO/COMO expor os casos de uso
       └──────┬──────┘     └──────┬──────┘
              │                   │
              └─────────┬─────────┘
                         ▼
              ┌────────────────────┐
              │ Scriptorium         │
              │ .Infrastructure     │        ← implementações concretas: EF Core, HTTP, HtmlAgilityPack
              └──────────┬──────────┘
                         ▼
              ┌────────────────────┐
              │ Scriptorium         │
              │ .Application        │        ← casos de uso + INTERFACES (contratos)
              └──────────┬──────────┘
                         ▼
              ┌────────────────────┐
              │ Scriptorium         │
              │ .Domain             │        ← entidades puras, zero dependências externas
              └────────────────────┘
```

**Regra da Dependência**: as setas de referência de projeto (`ProjectReference`)
sempre apontam para dentro/para baixo. `Domain` não referencia nada.
`Application` referencia só `Domain`. `Infrastructure` referencia
`Domain` + `Application` e implementa as interfaces que `Application`
declarou. `API` e `Worker` referenciam tudo, mas são os únicos lugares onde
a composição final acontece (registro de DI nos respectivos `Program.cs`).

Isso significa, na prática: **trocar SQLite por outro banco, ou trocar um
site de scraping por outro, nunca exige tocar em `Domain` ou `Application`**
— só a implementação concreta em `Infrastructure` muda, e o registro de DI.

## Modelo de domínio

```
DailyDevotional (raiz do agregado)
 ├── Date: DateOnly                     (única — índice UNIQUE no banco)
 ├── LiturgicalTitle: string
 ├── Color: LiturgicalColor (enum)
 ├── Readings: List<Reading>            (1-para-N, cascade delete)
 ├── Saint: SaintOfTheDay?              (1-para-1, cascade delete)
 ├── OtherSaints: List<OtherSaintOfDay> (1-para-N, cascade delete)
 └── Homily: Homily?                    (1-para-1 opcional, SetNull no delete)

Reading
 ├── Type: ReadingType                  (PrimeiraLeitura | SalmoResponsorial | SegundaLeitura | Evangelho)
 ├── Reference: string                  (ex: "Lc 1,39-56")
 └── Text: string

SaintOfTheDay
 ├── Name: string
 ├── Biography: string
 ├── ImageUrl: string?
 └── SourceUrl: string?

Homily
 ├── Title: string
 ├── HomilyDate: DateOnly
 ├── OriginalLanguage: string           ("pt" | "en")
 ├── TextoOriginal: string              (nunca sobrescrito)
 ├── TextoTraduzido: string?
 ├── Status: TranslationStatus          (NaoRequerida | Pendente | FalhouTentativa | Concluida)
 ├── SourceUrl: string                  (índice UNIQUE — evita duplicar a mesma homilia)
 └── TranslationAttempts: int

OtherSaintOfDay
 ├── Name: string                       (grafia do Vatican News, ex: "S. Timóteo, mártir romano...")
 └── ShortBiography: string             (um parágrafo)

DiaryEntry                              (independente de DailyDevotional — sem FK)
 ├── Date: DateOnly                     (única — índice UNIQUE no banco)
 ├── Text: string
 ├── CreatedAtUtc: DateTime
 └── UpdatedAtUtc: DateTime
```

Tabelas correspondentes no SQLite: `DailyDevotionals`, `Readings`, `Saints`,
`OtherSaints`, `Homilies`, `DiaryEntries` (nomes gerados automaticamente
pelo EF Core a partir dos `DbSet<T>` de `ScriptoriumDbContext`).

## Fluxo de escrita (Worker)

```
DailyDevotionalWorker.ExecuteAsync()
  │
  ├─▶ aplica migrations pendentes (idempotente)
  ├─▶ (se RunImmediatelyOnStartup) roda uma rodada imediatamente
  └─▶ loop: espera até HourUtc, roda uma rodada, repete

RunOnceAsync() [uma rodada]
  │
  ├─▶ para cada um dos próximos N dias (DaysAhead):
  │     DevotionalBuilderService.BuildAsync(data)
  │       ├─▶ Task.WhenAll: dispara os 5 scrapers EM PARALELO
  │       │     ├─ ISaintOfTheDayScraper       (Cancão Nova)
  │       │     ├─ ILiturgyScraper              (Cancão Nova)
  │       │     ├─ ILiturgicalCalendarScraper   (gcatholic.org)
  │       │     ├─ IHomilyScraper                (vatican.va)
  │       │     └─ IOtherSaintsScraper           (vaticannews.va)
  │       ├─▶ combina os resultados (cor litúrgica: liturgia.cancaonova.com
  │       │     é a fonte primária, gcatholic.org é o fallback; "outros
  │       │     santos" exclui, via SaintNameMatcher, quem já é o principal)
  │       └─▶ se houver homilia em idioma != "pt": tenta traduzir via LM Studio
  │             (sucesso → Status=Concluida; falha → Status=FalhouTentativa,
  │              texto original preservado)
  │     repository.UpsertAsync(devotional)  → grava/atualiza no SQLite
  │
  └─▶ RetryPendingTranslationsAsync(): varre TODAS as homilias com
        Status Pendente/FalhouTentativa (não só as processadas nesta rodada)
        e tenta traduzir de novo — é o mecanismo que faz uma tradução que
        falhou ontem (LM Studio desligado) funcionar hoje, sem re-raspar nada.
```

## Fluxo de leitura (API)

```
GET /api/devotional/today
GET /api/devotional/{date}     (formato: yyyy-MM-dd)
  │
  └─▶ IDevotionalRepository.GetByDateAsync(data)
        (SELECT com Include de Readings/Saint/Homily, AsNoTracking)
      │
      ├─ encontrado  → 200 OK + DevotionalResponse (JSON)
      └─ não encontrado → 404 Not Found + mensagem explicativa
```

A API **nunca** chama nenhum scraper nem o serviço de tradução — só lê o
que o Worker já deixou pronto. Essa separação (quem escreve vs. quem lê) é
uma aplicação simplificada de CQRS, e existe para que abrir o app nunca
fique refém da latência/disponibilidade de 4 sites externos.

## Referência dos endpoints HTTP

| Método | Rota | Descrição | Sucesso | Erros |
|---|---|---|---|---|
| `GET` | `/api/devotional/today` | Devocional do dia atual (UTC) | `200` + JSON | `404` se o Worker ainda não processou hoje |
| `GET` | `/api/devotional/{date}` | Devocional de uma data específica (`yyyy-MM-dd`, ex: `2026-08-16`) | `200` + JSON | `400` formato inválido · `404` não encontrado |
| `GET` | `/api/devotional/calendar/{year}/{month}` | Cor/título litúrgico de cada dia do mês (dias sem cache vêm ao vivo do gcatholic.org, sem persistir) | `200` + JSON | `400` ano/mês inválido |
| `GET` | `/api/diary/{date}` | Entrada do diário espiritual de uma data | `200` + JSON | `400` formato inválido · `404` sem entrada |
| `PUT` | `/api/diary/{date}` | Cria/atualiza a entrada do diário (`{"text": "..."}`) | `200` + JSON | `400` formato inválido ou texto vazio |
| `DELETE` | `/api/diary/{date}` | Remove a entrada do diário | `204` | `400` formato inválido · `404` não existia |
| `GET` | `/health` | Healthcheck simples (`{"status":"ok"}`) | `200` | — |
| `GET` | `/` | Swagger UI (interface visual de teste) | `200` (HTML) | — |
| `GET` | `/swagger/v1/swagger.json` | Especificação OpenAPI em JSON | `200` | — |

### Exemplo de resposta — `GET /api/devotional/2026-08-16`

```json
{
  "date": "2026-08-16",
  "liturgicalTitle": "Assunção da Bem-aventurada Virgem Maria | Solenidade | Domingo",
  "liturgicalColor": "Branco",
  "saint": {
    "name": "Santo Estêvão da Hungria: rei, diplomata e caridoso",
    "biography": "Origens\n\nSanto Estêvão da Hungria nasceu por volta de 969...",
    "imageUrl": "https://img.cancaonova.com/cnimages/.../Santo-Estevão-da-Hungria-2-300x225.jpg"
  },
  "readings": [
    { "type": "PrimeiraLeitura", "reference": "Ap 11,19a;12,1.3-6a.10ab", "text": "..." },
    { "type": "SalmoResponsorial", "reference": "Sl 44(45),10bc.11.12ab.16 (R. 10b)", "text": "..." },
    { "type": "SegundaLeitura", "reference": "1Cor 15,20-27a", "text": "..." },
    { "type": "Evangelho", "reference": "Lc 1,39-56 (Cântico de Maria)", "text": "..." }
  ],
  "homily": null,
  "otherSaints": [
    { "name": "S. Timóteo, mártir romano...", "shortBiography": "..." }
  ]
}
```

Quando há homilia, o campo `homily` vem como:

```json
"homily": {
  "title": "Santa Missa na Solenidade da Assunção...",
  "displayText": "...(texto traduzido, ou original se ainda pendente)...",
  "isAwaitingTranslation": false,
  "sourceUrl": "https://www.vatican.va/content/leo-xiv/pt/homilies/2026/documents/..."
}
```

## Rodando e depurando localmente (fora do Docker)

Pré-requisitos: .NET 8 SDK instalado (`dotnet --version` deve mostrar `8.x`).

```bash
cd Scriptorium

# build de tudo
dotnet build Scriptorium.sln

# rodar a API sozinha (lê de um SQLite local)
export ConnectionStrings__Default="Data Source=./scriptorium-dev.db"
dotnet run --project src/Scriptorium.API
# abra http://localhost:5000/ (ou a porta que o Kestrel anunciar) → Swagger UI

# rodar o Worker sozinho (mesmo banco, escreve nele)
export ConnectionStrings__Default="Data Source=./scriptorium-dev.db"
export WorkerSchedule__DaysAhead=1
export WorkerSchedule__RunImmediatelyOnStartup=true
dotnet run --project src/Scriptorium.Worker
```

### Gerando uma nova migration depois de alterar uma entidade

```bash
# instala a ferramenta global (uma vez só por máquina)
dotnet tool install --global dotnet-ef

# sempre a partir da pasta Scriptorium/ (raiz da solução):
dotnet ef migrations add NomeDescritivoDaMudanca \
  --project src/Scriptorium.Infrastructure \
  --startup-project src/Scriptorium.API \
  -o Data/Migrations

# aplica ao banco local (opcional em dev — em produção isso já é
# feito automaticamente no startup da API/Worker):
dotnet ef database update \
  --project src/Scriptorium.Infrastructure \
  --startup-project src/Scriptorium.API
```

Ver comentários detalhados sobre por que `--startup-project` aponta para a
API em `ScriptoriumDbContext.cs`.

---

## Oratorium (Frontend)

### Estrutura de pastas

```
Oratorium/
├── Dockerfile
├── nginx.conf
├── docker-entrypoint.d/40-oratorium-env.sh   ← gera env-config.js em runtime
├── index.html
├── public/
│   ├── env-config.js         ← placeholder em dev; reescrito no container
│   └── (ícones PWA: favicon.ico, pwa-*.png, apple-touch-icon-*.png)
├── vite.config.ts             ← plugins: react, tailwindcss, vite-plugin-pwa
├── vitest.config.ts           ← config separada de testes (jsdom + Testing Library)
└── src/
    ├── main.tsx                ← ponto de entrada, monta <BrowserRouter><App /></BrowserRouter>
    ├── App.tsx                 ← definição das rotas
    ├── App.smoke.test.tsx       ← teste de integração contra a API real
    ├── env.d.ts                 ← tipos de import.meta.env e window.__ORATORIUM_CONFIG__
    ├── api/
    │   ├── types.ts              ← espelha 1:1 os DTOs de Scriptorium.API
    │   └── client.ts             ← fetch wrapper (GET/PUT/DELETE), resolve a URL da API (runtime > build-time)
    ├── hooks/
    │   ├── useDevotional.ts      ← busca dados + estados de loading/error/retry
    │   ├── useMonthCalendar.ts   ← cor litúrgica de cada dia do mês
    │   └── useDiaryEntry.ts      ← busca/salva a entrada do diário de uma data
    ├── lib/
    │   ├── date.ts                ← aritmética de datas yyyy-MM-dd em UTC
    │   ├── liturgicalColor.ts     ← mapeia LiturgicalColor → cor hex/tema
    │   ├── readingLabels.ts       ← mapeia ReadingType → rótulo em PT-BR
    │   ├── excerpt.ts             ← corta texto longo pro pull-quote da sidebar
    │   ├── rosaryMysteries.ts     ← mistério do Rosário do dia (dia da semana)
    │   └── liturgicalSeasons.ts   ← Páscoa (Computus) + contagem pro próximo tempo litúrgico
    ├── components/
    │   ├── AppHeader.tsx, DateNav.tsx, LiturgicalHeader.tsx
    │   ├── SaintCard.tsx, ReadingsList.tsx, HomilyCard.tsx
    │   ├── Card.tsx                ← wrapper compartilhado (borda/padding/eyebrow) por todos os cards
    │   ├── TableOfContents.tsx, MonthCalendar.tsx  ← barra lateral esquerda
    │   ├── sidebar/                ← barra lateral direita: ColorMeaningCard, PullQuoteCard,
    │   │                              HomilySourceCard, RosaryMysteryCard, SeasonCountdownCard,
    │   │                              OtherSaintsCard, DiaryCard
    │   ├── StatusStates.tsx       ← LoadingState, ErrorState (distingue 404 de erro de rede)
    │   └── Paragraphs.tsx         ← converte texto com \n\n em <p> reais
    └── pages/
        └── DevotionalPage.tsx     ← layout de 3 colunas, liga os hooks aos componentes de UI
```

### Fluxo de dados

```
DevotionalPage
  │
  ├─▶ useParams() lê :date da URL (undefined em "/hoje")
  ├─▶ useDevotional(date) dispara o fetch (AbortController por mudança de data)
  │     └─▶ api/client.ts → GET {API_BASE_URL}/api/devotional/today | /{date}
  │
  ├─ loading=true  → <LoadingState />
  ├─ error         → <ErrorState /> (mensagem diferente para 404 vs. erro de rede)
  └─ data          → coluna central: <LiturgicalHeader /> + <SaintCard /> (se houver)
                      + <ReadingsList /> + <HomilyCard /> (se houver)
                      sidebar esquerda: <TableOfContents /> + <MonthCalendar />
                        (useMonthCalendar() próprio, independente do fetch acima)
                      sidebar direita: os 7 cards de components/sidebar/ — cálculo
                        local (RosaryMystery/SeasonCountdown) ou derivado de `data`;
                        DiaryCard usa useDiaryEntry() à parte
```

A resolução da URL da API segue uma ordem de prioridade (ver
`src/api/client.ts`): `window.__ORATORIUM_CONFIG__.apiBaseUrl` (injetado em
runtime pelo container Docker) > `import.meta.env.VITE_API_BASE_URL`
(definido em `.env.development`, usado só em dev local) > um valor padrão
fixo. Ver [01-infraestrutura.md](01-infraestrutura.md#configuração-da-api-em-runtime-não-em-build-time)
para o porquê dessa camada extra existir.

### Rodando localmente (fora do Docker)

```bash
cd Oratorium
npm install
cp .env.example .env.development   # ajuste VITE_API_BASE_URL se necessário
npm run dev                         # abre em http://localhost:5173

# build de produção (gera dist/, valida TypeScript primeiro)
npm run build
npm run preview                     # serve o build de produção localmente

# testes (precisa da API do Scriptorium rodando de verdade — não usa mocks)
npm test
```

### Como o Oratorium foi testado sem navegador

Este projeto foi desenvolvido num ambiente sandbox sem acesso a um
navegador gráfico e sem permissão para instalar as bibliotecas de sistema
que um Chromium headless (Playwright/Puppeteer) exige — uma tentativa real
de rodar Playwright falhou com `error while loading shared libraries:
libatk-1.0.so.0`, que exigiria `apt-get install` (sem sudo disponível).

Em vez de pular a verificação, a validação foi feita com **Vitest +
Testing Library**, a forma padrão do ecossistema React de testar
componentes SEM precisar de um navegador de verdade: os componentes reais
são renderizados num DOM simulado (`jsdom`) e o teste faz requisições HTTP
**reais** (sem mocks) contra uma instância real do `Scriptorium.API`
rodando com dados genuínos (raspados de verdade das fontes do backend).
Isso está registrado permanentemente em `src/App.smoke.test.tsx` e cobre:

- `/hoje` renderiza com dados reais (Santo do Dia, todas as leituras,
  incluindo o texto completo).
- Uma data fora do intervalo suportado pela busca sob demanda (2000-01-01
  a 5 anos no futuro) mostra a mensagem amigável de "não encontrado" (erro
  404 da API, sem tentar raspar nada).
- Um formato de data inválido mostra a mensagem de erro correspondente
  (erro 400 da API).
- Uma data DENTRO do intervalo suportado mas fora do cache dispara a busca
  sob demanda de verdade (chamada real aos 4 scrapers) e ainda assim
  devolve o devocional completo — ver
  [04-inteligencia-de-codigo.md](04-inteligencia-de-codigo.md), seção 7,
  "Navegação livre por calendário e busca sob demanda". Esse teste usa um
  timeout maior (`testTimeout` de 30s no `vitest.config.ts`, e 50s neste
  teste específico) porque a chamada realmente sai para a internet.

Adicionalmente, uma inspeção manual do HTML renderizado (via
`container.textContent` e `outerHTML` num teste ad-hoc, depois descartado)
confirmou que o texto acentuado em português veio íntegro e que as classes
do Tailwind (cor litúrgica, tipografia serifada) foram aplicadas
corretamente — por exemplo, o badge de cor litúrgica renderizou com
`background-color: rgb(201, 168, 76)`, o tom dourado configurado em
`lib/liturgicalColor.ts` para representar "Branco".

**Limitação honesta**: isso valida a LÓGICA da aplicação (dados corretos
chegando aos componentes certos, roteamento funcionando, tratamento de
erro correto) e as classes CSS aplicadas, mas não é um substituto perfeito
para ver o app renderizado visualmente num navegador real — questões
puramente visuais (alinhamento, responsividade em telas pequenas,
comportamento do Service Worker offline) devem ser conferidas manualmente
no navegador antes do primeiro uso real do app.
