# Tecnologias

## Stack do Backend (Scriptorium)

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
| CORS (`Microsoft.AspNetCore.Cors`, embutido no ASP.NET Core) | 8.0 (parte do runtime, sem pacote NuGet extra) | `Scriptorium.API` | Libera o navegador a ler respostas da API a partir da origem do Oratorium (porta diferente = origem diferente); ver Bug #6 em [04-inteligencia-de-codigo.md](04-inteligencia-de-codigo.md) |

## Stack do Frontend (Oratorium)

| Tecnologia | Versão | Onde é usada | Por quê |
|---|---|---|---|
| Node.js | 24 (LTS "Krypton") | Build/dev/testes | Versão LTS mais recente disponível na época; usada só para build — a imagem Docker final não carrega Node nenhum (ver [01-infraestrutura.md](01-infraestrutura.md)) |
| React | 19.2 | Toda a UI | Biblioteca de UI mais madura do ecossistema, com o ecossistema de testes (Testing Library) e roteamento (React Router) mais consolidado — escolha explícita do usuário |
| TypeScript | 6.0 | Todo o código-fonte | Tipagem estática — os tipos em `src/api/types.ts` espelham 1:1 os DTOs C# do backend, pegando divergências de contrato em tempo de compilação |
| Vite | 8.2 | Build/dev server | Bundler/dev-server padrão atual do ecossistema React — HMR quase instantâneo em dev, build de produção otimizado (tree-shaking, code-splitting) |
| Tailwind CSS | 4.3 | Estilização | Utilitários CSS direto no JSX, sem precisar manter arquivos `.css` separados por componente; v4 usa configuração "CSS-first" (`@theme` em `src/index.css`) em vez de `tailwind.config.js` |
| React Router | 7.18 | Roteamento (`/hoje`, `/dia/:date`) | Padrão de fato para roteamento client-side em React; API `Routes`/`Route` clássica, sem necessidade do "data router" mais complexo para as 2 rotas deste app |
| vite-plugin-pwa | 1.3 | Geração do Service Worker + manifest | Gera automaticamente o `sw.js` (via Workbox) e o `manifest.webmanifest` a partir de uma configuração declarativa — sem escrever um Service Worker à mão |
| Vitest + Testing Library | 4.1 / 16.3 | Testes (`src/App.smoke.test.tsx`) | Testa os componentes React reais (sem mocks) contra a API real do backend — ver [03-codigo.md](03-codigo.md#como-o-oratorium-foi-testado-sem-navegador) para o porquê dessa escolha neste projeto especificamente |
| nginx (`nginx:alpine`) | — | Servidor de produção | Serve os arquivos estáticos gerados pelo build; imagem final minúscula, sem runtime de aplicação nenhum |

## Infraestrutura de execução

| Item | Versão/detalhe |
|---|---|
| SDK/Runtime usado no build local | .NET SDK `8.0.424` |
| Imagem Docker de build | `mcr.microsoft.com/dotnet/sdk:8.0` |
| Imagem Docker final da API | `mcr.microsoft.com/dotnet/aspnet:8.0` |
| Imagem Docker final do Worker | `mcr.microsoft.com/dotnet/runtime:8.0` |
| Imagem Docker de build do Oratorium | `node:24-alpine` |
| Imagem Docker final do Oratorium | `nginx:alpine` |
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
- **PWA (Progressive Web App) em vez de app nativo**: decisão explícita do
  usuário — um único código-fonte roda no navegador, é instalável na tela
  inicial do celular (ícone próprio, tela cheia, funciona parcialmente
  offline via Service Worker) sem precisar de Android Studio/Xcode nem de
  publicação em lojas de aplicativo, adequado para um app pessoal de
  homelab.
- **Configuração da API em runtime, não em build-time**: ver
  [01-infraestrutura.md](01-infraestrutura.md#configuração-da-api-em-runtime-não-em-build-time)
  — decisão tomada diretamente por causa do Bug #2 encontrado no backend
  (URL duplicada e fora de sincronia no `docker-compose.yml`); o Oratorium
  foi desenhado desde o início para não repetir essa classe de problema.

## Onde ver a lista completa de pacotes

Os arquivos de manifesto de cada projeto são a fonte da verdade definitiva
(mais confiáveis que este documento, que pode ficar desatualizado com o
tempo):

```
Scriptorium/src/Scriptorium.Domain/Scriptorium.Domain.csproj
Scriptorium/src/Scriptorium.Application/Scriptorium.Application.csproj
Scriptorium/src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj
Scriptorium/src/Scriptorium.API/Scriptorium.API.csproj
Scriptorium/src/Scriptorium.Worker/Scriptorium.Worker.csproj
Oratorium/package.json
```
