// Placeholder para desenvolvimento local — em produção (Docker), este
// arquivo é REESCRITO pelo entrypoint do container a partir da variável de
// ambiente ORATORIUM_API_BASE_URL (ver Oratorium/docker-entrypoint.d/40-oratorium-env.sh).
// Deixamos vazio aqui de propósito: em dev, o api/client.ts cai para
// import.meta.env.VITE_API_BASE_URL (definido em .env.development).
window.__ORATORIUM_CONFIG__ = {}
