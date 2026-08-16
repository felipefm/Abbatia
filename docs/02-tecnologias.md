# Tecnologias

## Stack principal

| Tecnologia | Versão | Onde é usada | Por quê |
|---|---|---|---|
| .NET | 8.0 (LTS) | Todos os 5 projetos | Versão LTS (suporte de longo prazo) mais recente na época da criação do projeto; Minimal APIs, `DateOnly`, e o GC *cgroup-aware* usados no projeto exigem .NET 6+ |
| ASP.NET Core Minimal APIs | 8.0 | `Scriptorium.API` | Roteamento HTTP sem a cerimônia de classes `Controller` — adequado para uma API pequena e focada (2 rotas de negócio) |
| Entity Framework Core | 8.0.11 | `Scriptorium.Infrastructure` | ORM oficial da Microsoft; Code-First Migrations geram o schema SQLite a partir das classes C# |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.11 | `Scriptorium.Infrastructure`, `Scriptorium.API` | Provider do EF Core para SQLite |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.11 | `Infrastructure`, `API`, `Worker` | Ferramentas de design-time (`dotnet ef migrations add`, etc.) — precisa estar referenciado tanto no projeto de biblioteca quanto no projeto "de inicialização" usado pela CLI |
| HtmlAgilityPack | 1.12.4 | `Scriptorium.Infrastructure` | Biblioteca .NET padrão de mercado para parsing de HTML "do mundo real" (tolerante a tags malformadas), com API estilo DOM + XPath |
| `Microsoft.Extensions.Http` (`IHttpClientFactory`) | 8.0.1 | `Scriptorium.Infrastructure` | Gerenciamento correto do ciclo de vida de `HttpClient`, evitando esgotamento de sockets; permite clientes HTTP **nomeados** com `BaseAddress`/headers próprios por site raspado |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 8.0.0 | `Scriptorium.Infrastructure` | Suporte ao *Options Pattern* (`IOptions<T>`) usado por `LmStudioOptions` e `WorkerScheduleOptions` |
| `Microsoft.Extensions.Hosting` (Generic Host + `BackgroundService`) | 8.0.1 | `Scriptorium.Worker` | Infraestrutura de processo de longa duração com DI, configuração e logging prontos, sem o pipeline HTTP (que não faz sentido para um worker) |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.2 | `Scriptorium.Application` | Permite `DevotionalBuilderService` receber `ILogger<T>` via DI sem a camada Application depender de nenhuma implementação concreta de logging |
| Swashbuckle.AspNetCore (Swagger) | 6.9.0 | `Scriptorium.API` | Gera a especificação OpenAPI a partir dos Minimal APIs (via `AddEndpointsApiExplorer`) e serve a interface visual do Swagger UI para testes manuais, sem precisar de `curl`/Postman |

## Infraestrutura de execução

| Item | Versão/detalhe |
|---|---|
| SDK/Runtime usado no build local | .NET SDK `8.0.424` |
| Imagem Docker de build | `mcr.microsoft.com/dotnet/sdk:8.0` |
| Imagem Docker final da API | `mcr.microsoft.com/dotnet/aspnet:8.0` |
| Imagem Docker final do Worker | `mcr.microsoft.com/dotnet/runtime:8.0` |
| Banco de dados | SQLite (arquivo único) |
| Ferramenta de migrations | `dotnet-ef` (global tool) `8.0.30` |
| Orquestração | Docker Compose v2 (Compose Specification) |

## Por que essas escolhas (e não outras)

- **SQLite em vez de Postgres/MySQL**: requisito explícito do projeto —
  roda numa homelab pessoal de baixo volume (poucos registros por dia), e
  SQLite elimina a necessidade de um serviço de banco separado para
  gerenciar. A troca para outro banco no futuro exigiria mudar apenas a
  `Infrastructure` (ver [03-codigo.md](03-codigo.md) sobre a Regra da
  Dependência da Clean Architecture).
- **HtmlAgilityPack em vez de um navegador headless (Playwright/Selenium)**:
  todas as 4 fontes de dados devolvem HTML estático no primeiro request
  (nenhuma delas exige execução de JavaScript para renderizar o conteúdo
  relevante) — confirmado por inspeção manual durante o desenvolvimento
  (ver [04-inteligencia-de-codigo.md](04-inteligencia-de-codigo.md)). Um
  navegador headless seria ordens de magnitude mais pesado (CPU/memória) e
  desnecessário aqui.
- **Minimal APIs em vez de Controllers**: a API expõe apenas 2 rotas de
  negócio + `/health`. Controllers fazem mais sentido para APIs com dezenas
  de endpoints organizados em grupos — overhead desnecessário neste escopo.
- **Swagger sempre ativo (não só em Development)**: decisão deliberada
  documentada em `Scriptorium.API/Program.cs` — a Abbatia roda numa rede
  doméstica, não exposta à internet pública, então a conveniência de testar
  direto do navegador supera o risco de expor a spec da API. Reconsiderar
  se um dia o serviço for exposto externamente.
- **IHttpClientFactory em vez de `new HttpClient()`**: evita o problema
  clássico de esgotamento de sockets em aplicações .NET de longa duração
  (cada Worker roda continuamente, potencialmente por dias/semanas sem
  reiniciar).

## Onde ver a lista completa de pacotes

Os arquivos `.csproj` de cada projeto são a fonte da verdade definitiva
(mais confiável que este documento, que pode ficar desatualizado com o
tempo):

```
Scriptorium/src/Scriptorium.Domain/Scriptorium.Domain.csproj
Scriptorium/src/Scriptorium.Application/Scriptorium.Application.csproj
Scriptorium/src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj
Scriptorium/src/Scriptorium.API/Scriptorium.API.csproj
Scriptorium/src/Scriptorium.Worker/Scriptorium.Worker.csproj
```
