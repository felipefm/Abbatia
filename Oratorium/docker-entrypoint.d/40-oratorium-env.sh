#!/bin/sh
# Este script roda AUTOMATICAMENTE na inicialização do container, ANTES do
# nginx subir — é um recurso nativo da imagem oficial nginx: qualquer script
# executável em /docker-entrypoint.d/ é executado em ordem alfabética pelo
# entrypoint padrão da imagem (por isso o prefixo "40-", para rodar depois
# dos scripts internos da própria imagem, que usam prefixos menores).
#
# Regenera public/env-config.js (já copiado para dentro da imagem em build
# time com um valor placeholder) com o valor REAL da URL da API, lido da
# variável de ambiente ORATORIUM_API_BASE_URL do container — permitindo
# trocar o endereço da API só reiniciando o container, SEM precisar
# reconstruir a imagem Docker inteira.
set -eu

: "${ORATORIUM_API_BASE_URL:=http://localhost:8110}"

cat > /usr/share/nginx/html/env-config.js <<EOF
window.__ORATORIUM_CONFIG__ = { apiBaseUrl: "${ORATORIUM_API_BASE_URL}" };
EOF

echo "[oratorium] env-config.js gerado com apiBaseUrl=${ORATORIUM_API_BASE_URL}"
