# syntax=docker/dockerfile:1
#
# Multi-stage build: compila o app React/Vite numa imagem Node completa e
# serve os arquivos estáticos resultantes com nginx — a imagem final não
# carrega Node.js nem node_modules, só HTML/CSS/JS já prontos + o nginx.

# ============================================================================
# ESTÁGIO 1 — BUILD
# ============================================================================
FROM node:24-alpine AS build
WORKDIR /app

# Mesmo truque de cache de camadas usado no Dockerfile do Scriptorium:
# copiamos primeiro só os manifestos de dependência e rodamos `npm ci`
# nessa camada isolada, para que alterações no código-fonte não invalidem
# o cache de instalação de pacotes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# VITE_API_BASE_URL aqui é só o valor de FALLBACK embutido no bundle (usado
# caso o env-config.js de runtime não tenha sido gerado por algum motivo) —
# a configuração real de produção é injetada em RUNTIME pelo entrypoint
# (ver docker-entrypoint.d/40-oratorium-env.sh), não em build-time.
ARG VITE_API_BASE_URL=http://localhost:8110
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# ============================================================================
# ESTÁGIO 2 — IMAGEM FINAL (nginx servindo arquivos estáticos)
# ============================================================================
FROM nginx:alpine AS final

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY docker-entrypoint.d/40-oratorium-env.sh /docker-entrypoint.d/40-oratorium-env.sh
RUN chmod +x /docker-entrypoint.d/40-oratorium-env.sh

EXPOSE 80
