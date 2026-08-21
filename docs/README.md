# Abbatia — Documentação

Este diretório reúne a documentação completa do ecossistema **Abbatia**, o
monorepo do app de Devocional Católico Diário. Ele existe para que você (ou
qualquer pessoa que volte a este projeto depois de um tempo) consiga
entender **o quê**, **como** e **por quê** sem precisar reconstituir tudo lendo
código do zero.

## Estrutura do monorepo

```
Abbatia/
├── Scriptorium/        ← Backend em .NET 8 (API + Worker)
├── Oratorium/           ← Frontend: React + Vite, PWA
├── docker-compose.yml   ← Orquestração dos 3 containers, na raiz do monorepo
└── docs/                ← Você está aqui
```

## Índice dos documentos

| Documento | Conteúdo |
|---|---|
| [01-infraestrutura.md](01-infraestrutura.md) | Docker, docker-compose, CasaOS, volumes, limites de recursos, deploy |
| [02-tecnologias.md](02-tecnologias.md) | Stack tecnológica completa (backend e frontend), com versões e justificativas |
| [03-codigo.md](03-codigo.md) | Estrutura de pastas, camadas da arquitetura, entidades, endpoints, componentes, como rodar/depurar localmente |
| [04-inteligencia-de-codigo.md](04-inteligencia-de-codigo.md) | O "porquê" por trás das decisões: padrões de projeto, engenharia reversa dos scrapers, bugs encontrados e corrigidos, limitações conhecidas |

## Visão geral em 60 segundos

O **Scriptorium** é a API + Worker que alimentam o devocional diário. Todo
dia de madrugada, um processo em background (`Scriptorium.Worker`) sai
raspando 5 sites diferentes (Santo do Dia, Liturgia Diária, Homilias do
Papa, Calendário Litúrgico e Outros Santos do Dia), traduz o que precisar
via uma IA local (LM Studio) e salva tudo num banco SQLite. Uma API
separada (`Scriptorium.API`) só **lê** esse banco e expõe as rotas de
devocional, calendário mensal e diário espiritual (ver
[03-codigo.md](03-codigo.md#referência-dos-endpoints-http)).
O **Oratorium** é a interface de leitura: um app React (PWA, instalável no
celular) que consome essa API e exibe o devocional do dia num layout de 3
colunas (sumário + calendário à esquerda, widgets à direita), com
navegação entre datas. Os três processos rodam em containers Docker separados; API e
Worker compartilham o mesmo arquivo SQLite via um volume Docker, e o
Oratorium fala com a API por HTTP comum através da rede local.

```
                    ┌─────────────────────────┐
   madrugada        │  Scriptorium.Worker      │
   (agendado) ─────▶│  (BackgroundService)     │──┐
                    └─────────────────────────┘  │ escreve
                                                   ▼
   sites externos ◀── raspa ──┐            ┌──────────────┐
   (Cancão Nova,               │            │   SQLite     │
    Vaticano,                  │            │ (volume      │
    GCatholic) ─────────────────┘           │  Docker)     │
                                             └──────────────┘
   LM Studio (IA local) ◀── traduz              ▲
                                                 │ lê
                    ┌─────────────────────────┐ │
   navegador ───────▶│  Oratorium               │
   do usuário        │  (React + PWA, nginx)    │
                    └───────────┬─────────────┘
                                 │ HTTP (fetch)
                                 ▼
                    ┌─────────────────────────┐
                    │  Scriptorium.API         │─┘
                    │  (Minimal APIs/Swagger)  │
                    └─────────────────────────┘
```

Para o passo a passo de cada peça, siga os documentos do índice acima.
