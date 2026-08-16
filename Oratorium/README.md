# Oratorium

Frontend do devocional católico diário da Abbatia — React + Vite + PWA,
consumindo a API do [Scriptorium](../Scriptorium).

Documentação completa (arquitetura, decisões, infraestrutura): [`../docs/`](../docs/).

## Rodando localmente

```bash
npm install
cp .env.example .env.development   # ajuste VITE_API_BASE_URL se necessário
npm run dev
```

## Scripts

| Comando | Descrição |
|---|---|
| `npm run dev` | Dev server com hot reload (`http://localhost:5173`) |
| `npm run build` | Type-check (`tsc -b`) + build de produção em `dist/` |
| `npm run preview` | Serve o build de produção localmente |
| `npm test` | Roda os testes (Vitest + Testing Library) — precisa da API real rodando |
| `npm run lint` | Lint com oxlint |

## Docker

Este projeto não é buildado isoladamente em produção — ver o
`docker-compose.yml` na raiz do monorepo, serviço `oratorium`.
