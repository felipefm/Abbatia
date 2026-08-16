# syntax=docker/dockerfile:1
#
# Este Dockerfile usa MULTI-STAGE BUILD para produzir DUAS imagens finais
# diferentes a partir do MESMO arquivo: uma para a API (Scriptorium.API) e
# outra para o Worker (Scriptorium.Worker). Isso é possível porque um
# Dockerfile pode ter vários estágios "FROM ... AS <nome>", e o
# docker-compose.yml (na raiz do monorepo) escolhe qual estágio final usar
# através da chave `target:` de cada serviço.
#
# PORQUÊ UM ÚNICO DOCKERFILE EM VEZ DE DOIS SEPARADOS?
# API e Worker compartilham 100% do código das camadas Domain, Application e
# Infrastructure — só o "ponto de entrada" (Program.cs) é diferente. Manter
# um único Dockerfile com um estágio de build COMPARTILHADO evita duplicar a
# lógica de restore/compilação em dois arquivos, e garante que os dois
# processos sejam sempre compilados a partir EXATAMENTE do mesmo snapshot de
# código-fonte (evitando o clássico bug de "funciona na API mas não no
# Worker porque foram buildados em momentos/versões diferentes").

# ============================================================================
# ESTÁGIO 1 — BUILD: compila a solução inteira usando a imagem do SDK
# completo do .NET (pesada, ~800MB — mas essa camada NUNCA vai parar na
# imagem final, só existe durante o processo de build).
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# TRUQUE DE CACHE DE CAMADAS DO DOCKER (muito importante para builds rápidos
# no dia a dia): copiamos primeiro APENAS os arquivos de projeto (.sln e
# .csproj) — sem nenhum código-fonte .cs ainda — e rodamos `dotnet restore`
# nessa camada isolada.
#
# O Docker cacheia cada instrução (RUN/COPY) baseado no hash dos arquivos
# copiados. Se copiássemos TODO o código-fonte de uma vez só, qualquer
# alteração em UM ÚNICO arquivo .cs (mesmo sem tocar em nenhuma dependência
# NuGet) invalidaria essa camada de cache e forçaria um `dotnet restore`
# completo (que baixa pacotes da internet) em TODO rebuild — lento demais
# para um ciclo de desenvolvimento típico. Copiando só os .csproj primeiro,
# o restore só roda de novo quando uma DEPENDÊNCIA realmente muda.
COPY Scriptorium.sln .
COPY src/Scriptorium.Domain/Scriptorium.Domain.csproj src/Scriptorium.Domain/
COPY src/Scriptorium.Application/Scriptorium.Application.csproj src/Scriptorium.Application/
COPY src/Scriptorium.Infrastructure/Scriptorium.Infrastructure.csproj src/Scriptorium.Infrastructure/
COPY src/Scriptorium.API/Scriptorium.API.csproj src/Scriptorium.API/
COPY src/Scriptorium.Worker/Scriptorium.Worker.csproj src/Scriptorium.Worker/
RUN dotnet restore Scriptorium.sln

# Agora sim copiamos todo o código-fonte restante e publicamos os dois
# projetos executáveis. `dotnet publish` (diferente de `dotnet build`) gera
# uma pasta "pronta para produção", já sem os arquivos intermediários de
# build, incluindo todas as DLLs de dependência necessárias para rodar sem
# precisar do SDK instalado — é isso que vamos copiar para as imagens finais.
COPY src/ src/
RUN dotnet publish src/Scriptorium.API/Scriptorium.API.csproj -c Release -o /app/api --no-restore
RUN dotnet publish src/Scriptorium.Worker/Scriptorium.Worker.csproj -c Release -o /app/worker --no-restore

# ============================================================================
# ESTÁGIO 2a — IMAGEM FINAL DA API: usa a imagem "aspnet" (runtime do
# ASP.NET Core, ~220MB) em vez da imagem "sdk" completa (~800MB) usada no
# build. Essa é a essência do multi-stage build: a imagem de PRODUÇÃO não
# precisa do compilador C#, do MSBuild nem do cache de pacotes NuGet — só do
# runtime necessário para EXECUTAR o binário já compilado. Isso reduz
# drasticamente o tamanho final da imagem (menos tempo de download/deploy no
# CasaOS da sua homelab, menor superfície de ataque de segurança).
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-final
WORKDIR /app

# Instala `curl`, usado apenas pelo HEALTHCHECK configurado no
# docker-compose.yml para o Docker verificar se a API está respondendo
# (chamando GET /health). A imagem base "aspnet" é minimalista de propósito
# e não vem com curl por padrão.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Cria a pasta onde o volume do SQLite será montado (ver docker-compose.yml)
# e garante que o usuário não-root "app" (já vem pré-criado nas imagens
# oficiais do .NET 8, UID 64198, por segurança — rodar containers como root
# é uma má prática) seja o dono dela. Isso é necessário porque, quando um
# volume Docker NOMEADO é montado pela primeira vez sobre um diretório que
# já existe na imagem, o Docker copia as permissões desse diretório da
# imagem para dentro do volume — sem isso, o processo rodando como usuário
# "app" não teria permissão de escrever o arquivo .db ali dentro.
RUN mkdir -p /data && chown -R app:app /data
USER app

COPY --from=build --chown=app:app /app/api .

# Porta em que o Kestrel (servidor HTTP embutido do ASP.NET Core) escuta
# DENTRO do container — mapeada para uma porta externa no docker-compose.yml.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Scriptorium.API.dll"]

# ============================================================================
# ESTÁGIO 2b — IMAGEM FINAL DO WORKER: mesma lógica do estágio anterior, mas
# usando a imagem "runtime" pura (ainda mais enxuta que "aspnet", ~190MB),
# já que o Worker é um BackgroundService puro — NUNCA recebe requisições
# HTTP, então não precisa de NENHUMA parte do stack Kestrel/ASP.NET Core.
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS worker-final
WORKDIR /app

RUN mkdir -p /data && chown -R app:app /data
USER app

COPY --from=build --chown=app:app /app/worker .

ENTRYPOINT ["dotnet", "Scriptorium.Worker.dll"]
