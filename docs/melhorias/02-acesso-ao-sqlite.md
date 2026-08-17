# Acesso direto ao arquivo SQLite

Status: **proposta em avaliação** — não implementada.

## Situação atual

O banco (`scriptorium.db`) vive dentro de um **volume Docker nomeado**
(`scriptorium-data`), declarado no `docker-compose.yml`:

```yaml
volumes:
  scriptorium-data:
    driver: local
```

montado em `/data` dentro dos containers `scriptorium-api` e
`scriptorium-worker`. Isso é ótimo para portabilidade (não depende de um
caminho absoluto existir de antemão no host), mas tem uma desvantagem
prática: **não aparece em nenhuma pasta do projeto**, então não dá para
navegar até o arquivo `.db` pelo File Manager do CasaOS ou copiá-lo
diretamente para backup.

O caminho físico real no host fica em algo como:

```
/var/lib/docker/volumes/<nome-do-projeto>_scriptorium-data/_data/scriptorium.db
```

(o prefixo `<nome-do-projeto>` depende do nome usado pelo Docker Compose
como "project name" — normalmente o nome da pasta onde o compose roda).
Para confirmar o caminho exato:

```bash
docker volume inspect <nome-do-projeto>_scriptorium-data
```
e ler o campo `"Mountpoint"` do JSON retornado.

## Alternativa: bind mount

O próprio `docker-compose.yml` já deixa comentado como trocar:

```yaml
volumes:
  - /DATA/AppData/Abbatia/dados-sqlite:/data
```

em vez do volume nomeado. Com isso, o arquivo `scriptorium.db` passaria a
viver literalmente em `/DATA/AppData/Abbatia/dados-sqlite/scriptorium.db`
— visível e navegável pelo File Manager do CasaOS, copiável para backup
com um `cp` simples, abrível com qualquer cliente SQLite (ex:
`sqlite3`, DB Browser for SQLite) sem precisar entrar no container.

## Trade-offs

| | Volume nomeado (atual) | Bind mount (proposta) |
|---|---|---|
| Portabilidade entre instalações | Alta (Docker gerencia o caminho) | Menor (depende do path `/DATA/...` existir) |
| Acesso via File Manager/backup manual | Não | Sim |
| Risco de permissão (usuário `app` não-root no container) | Baixo (Docker ajusta automaticamente) | Precisa garantir que a pasta do host tenha permissão de escrita para o UID usado pelo container (`app`, UID 64198) |
| Mudança necessária | — | Uma linha em cada serviço (`scriptorium-api` e `scriptorium-worker`) no `docker-compose.yml` |

## Recomendação

Troca de baixo risco e baixo esforço (uma linha por serviço), mas exige
criar a pasta `/DATA/AppData/Abbatia/dados-sqlite/` no host **antes** de
subir os containers e conferir que o usuário `app` (UID 64198) dentro do
container consegue escrever nela — senão o Worker falha ao tentar gravar o
banco. Se optar por isso, também vale considerar mover o `.db` já existente
do volume nomeado para o novo caminho antes de trocar (para não perder o
histórico já raspado).
