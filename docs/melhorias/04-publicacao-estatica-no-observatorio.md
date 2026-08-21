# Publicação estática do devocional no Observatório (Hugo)

Status: **proposta em avaliação** — não implementada.

## A ideia

O Observatório (`/DATA/AppData/observatorio`) é o blog pessoal do usuário,
gerado com **Hugo** (site estático) a partir de arquivos Markdown, tema
PaperMod, publicado em `https://observatorio.felipefm.com/`. A proposta é
levar o devocional diário (ou parte dele) para dentro desse blog, em vez
de (ou além de) servi-lo só pelo Oratorium.

Essa ideia só faz sentido **depois** da [pré-raspagem dos ciclos
litúrgicos](01-pre-raspagem-ciclos-liturgicos.md) estar completa (A/B/C +
I/II), porque é o que torna o conteúdo de leitura/santo **estático e
previsível** — exatamente o tipo de conteúdo que um gerador de site
estático como o Hugo foi feito para lidar bem.

## Por que isso combina com a pré-raspagem

Hugo não roda nada em tempo real: ele transforma arquivos Markdown em
HTML **no momento do build**, e o resultado é servido como arquivos
estáticos (é literalmente o que já acontece hoje com o Observatório, cujo
`public/` é HTML puro gerado a partir de `content/`).

Se as leituras litúrgicas + santo do dia já estão 100% cacheadas no
SQLite (sem depender de raspagem ao vivo), dá para **exportar esse cache
para arquivos Markdown** (um por posição litúrgica, ou pré-gerados para os
próximos anos civis) e deixar o Hugo gerar tudo como HTML estático. Nesse
cenário, essa parte do devocional deixaria de depender de um backend
rodando 24/7 — seria só arquivo estático, mais leve ainda que a API atual.

A única parte que **não** se encaixa nesse modelo é a **Homilia do
Papa**: como ela é conteúdo novo todo dia (sem ciclo, sem cache possível),
publicá-la via Hugo exigiria um mecanismo de atualização diária — não dá
para gerar "de uma vez só" como as leituras.

## Dois caminhos possíveis para a Homilia dentro do Hugo

1. **Rebuild diário completo**: um cron job (rodando onde hoje roda o
   `Scriptorium.Worker`, ou um script Python simples) raspa a homilia do
   dia, gera/atualiza um arquivo `.md` dentro de `content/` do
   Observatório, roda `hugo build` e publica o `public/` atualizado.
   Simples de entender, mas o site inteiro é "re-buildado" todo dia só por
   causa de uma seção.
2. **Ilha dinâmica dentro de página estática**: a página do devocional é
   estática (leitura + santo, gerados uma vez), mas a seção da homilia
   busca o conteúdo via JavaScript de um endpoint pequeno e separado (ex:
   mantendo só essa fatia do `Scriptorium.API` viva, ou uma function/
   endpoint bem mais simples). Mantém o build do Hugo raro/estável, mas
   reintroduz uma dependência de backend rodando, só que bem menor que a
   atual.

## Decisão em aberto: o que acontece com o Oratorium?

Essa proposta força uma pergunta maior, que vale decidir antes de
implementar qualquer coisa:

- **(a) Coexistência**: manter o Oratorium (React/PWA) como está hoje e
  *também* publicar uma versão espelhada no Observatório. Duplica o
  conteúdo em dois lugares, mas não descontinua nada que já funciona.
- **(b) Substituição**: migrar o devocional inteiro para dentro do
  Observatório e aposentar o Oratorium + `Scriptorium.API` (mantendo talvez
  só um pequeno gerador/worker para alimentar o Hugo e, se optar pela
  "ilha dinâmica", servir a homilia). Reduz bastante a superfície do
  projeto (menos containers, menos manutenção), mas descarta o trabalho já
  feito no frontend React/PWA (instalável, navegação entre datas, etc.).

Esses dois caminhos têm esforço e impacto bem diferentes — vale
amadurecer separadamente da decisão de pré-raspar os ciclos, já que essa
parte (a pré-raspagem) é útil nos dois cenários.

## Vantagens gerais de ir para estático

- Menor consumo de recursos na homelab: HTML estático servido por nginx
  não compete por CPU/memória do jeito que um backend .NET rodando 24/7
  compete (relevante para a preocupação original de custo de recursos que
  motivou toda essa conversa).
- Resiliência: se o SQLite/API cair, o conteúdo já publicado continua no
  ar normalmente (é só arquivo estático).
- Reaproveita infraestrutura que já existe e já funciona (Observatório já
  está publicado e com pipeline de build rodando).

## Riscos e pontos em aberto

- Duplicar conteúdo (cenário "a") tem custo de manutenção — dois lugares
  para manter sincronizados.
- O tema/propósito do Observatório é um blog pessoal de escrita; misturar
  um devocional diário automatizado nele muda o escopo do site — vale
  decidir se entra como uma seção separada (ex: `/devocional/`) ou fica de
  fora.
- Nenhuma decisão de implementação foi tomada aqui — este documento existe
  só para registrar a ideia e as opções, para retomar quando a
  pré-raspagem dos ciclos estiver decidida/concluída.
