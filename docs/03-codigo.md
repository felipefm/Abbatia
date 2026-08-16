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
    │   │   └── Homily.cs
    │   └── Enums/
    │       ├── LiturgicalColor.cs
    │       ├── ReadingType.cs
    │       └── TranslationStatus.cs
    │
    ├── Scriptorium.Application/     ← casos de uso e contratos (interfaces)
    │   ├── DTOs/ScrapeResults.cs    ← resultados brutos dos scrapers
    │   ├── Interfaces/
    │   │   ├── IScrapers.cs         ← ISaintOfTheDayScraper, ILiturgyScraper, ILiturgicalCalendarScraper, IHomilyScraper
    │   │   ├── ITranslationService.cs
    │   │   └── IDevotionalRepository.cs
    │   ├── Services/DevotionalBuilderService.cs  ← orquestra os 4 scrapers + tradução
    │   └── ServiceCollectionExtensions.cs
    │
    ├── Scriptorium.Infrastructure/  ← implementações concretas (EF Core, HTTP, scraping)
    │   ├── Data/
    │   │   ├── ScriptoriumDbContext.cs
    │   │   └── Migrations/
    │   ├── Repositories/DevotionalRepository.cs
    │   ├── Scrapers/
    │   │   ├── HtmlTextExtractor.cs           ← utilitário compartilhado
    │   │   ├── CancaoNovaCalendarHelper.cs     ← lógica compartilhada Santo/Liturgia
    │   │   ├── CancaoNovaSaintScraper.cs
    │   │   ├── CancaoNovaLiturgyScraper.cs
    │   │   ├── VaticanHomilyScraper.cs
    │   │   └── GCatholicCalendarScraper.cs
    │   ├── Translation/LmStudioTranslationService.cs
    │   ├── Options/LmStudioOptions.cs
    │   └── ServiceCollectionExtensions.cs
    │
    ├── Scriptorium.API/             ← processo Kestrel (Minimal APIs)
    │   ├── Program.cs
    │   ├── Endpoints/DevotionalEndpoints.cs
    │   └── DTOs/DevotionalResponse.cs
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
```

Tabelas correspondentes no SQLite: `DailyDevotionals`, `Readings`, `Saints`,
`Homilies` (nomes gerados automaticamente pelo EF Core a partir dos
`DbSet<T>` de `ScriptoriumDbContext`).

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
  │       ├─▶ Task.WhenAll: dispara os 4 scrapers EM PARALELO
  │       │     ├─ ISaintOfTheDayScraper       (Cancão Nova)
  │       │     ├─ ILiturgyScraper              (Cancão Nova)
  │       │     ├─ ILiturgicalCalendarScraper   (gcatholic.org)
  │       │     └─ IHomilyScraper                (vatican.va)
  │       ├─▶ combina os resultados (cor litúrgica: liturgia.cancaonova.com
  │       │     é a fonte primária, gcatholic.org é o fallback)
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
  "homily": null
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
