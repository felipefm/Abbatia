# Pré-raspagem dos ciclos litúrgicos (A/B/C e I/II)

Status: **proposta em avaliação** — não implementada.

## O problema hoje

O `Scriptorium.Worker` (`DailyDevotionalWorker.cs`) roda todo dia de
madrugada e raspa os próximos `DaysAhead` dias (padrão: 7) **do zero**,
sempre, usando os 4 scrapers:

- `GCatholicCalendarScraper` — calendário litúrgico (cor, ciclo, tempo)
- `CancaoNovaSaintScraper` — santo do dia
- `CancaoNovaLiturgyScraper` — leituras do dia
- `VaticanHomilyScraper` — homilia do Papa

Isso significa bater nos mesmos sites externos repetidamente para conteúdo
que, na prática, **não muda** — as leituras litúrgicas e o santo do dia
seguem um calendário fixo e cíclico, não são notícia nova todo dia (ao
contrário da homilia do Papa, que é conteúdo genuinamente novo).

## Por que isso é cacheável (com ressalvas)

A ideia original — "raspar uma vez, guardar para sempre" — é válida, mas o
calendário litúrgico católico tem duas nuances importantes que mudam o
desenho da solução:

1. **Dois ciclos independentes, não um só.**
   - Leituras de **domingo**: ciclo de **3 anos** (Ano A, B, C).
   - Leituras de **dia de semana**: ciclo de **2 anos** (Ano I, II),
     independente do ciclo dominical. Um mesmo ano civil tem
     simultaneamente um ano dominical (ex: "B") e um ano ferial (ex: "I").

2. **Data civil ≠ posição fixa no calendário litúrgico.**
   A Páscoa muda de data todo ano (calculada pelo calendário lunar), o que
   desloca a Quaresma, a Páscoa e o início/fim do Tempo Comum. Ou seja,
   "17 de agosto" não corresponde necessariamente à mesma semana litúrgica
   de um ano para o outro. **Não dá para indexar o cache por data civil
   fixa** (`"08-17" → leitura X`) — é preciso indexar pela **posição
   litúrgica** (ex: "19º Domingo do Tempo Comum, Ano B") e recalcular, ano
   a ano, qual data civil corresponde a qual posição. Esse cálculo é
   determinístico (só depende da data da Páscoa daquele ano) e não requer
   scraping nenhum — só um algoritmo de calendário litúrgico.

3. **O santo do dia (santoral) é a parte mais simples.**
   Diferente das leituras, o santo do dia é fixado por **data civil** (1º
   de janeiro é sempre Santa Maria Mãe de Deus, por exemplo), com raras
   exceções de solenidades transferidas. Essa parte pode ser cacheada numa
   única raspagem, sem se preocupar com ciclo nenhum — é o item de menor
   esforço e maior retorno imediato.

## Desenho proposto (alto nível)

- Nova tabela (ex: `LiturgicalDayCache`) indexada por **posição
  litúrgica** (tempo + semana + dia da semana + ciclo A/B/C ou I/II), não
  por data civil.
- Um componente de **cálculo de calendário** (data da Páscoa → mapeamento
  data civil → posição litúrgica do ano corrente), sem depender de
  scraping.
- O Worker passa a, para cada dia da janela `DaysAhead`:
  1. Calcular a posição litúrgica da data.
  2. Checar se já existe no cache (`LiturgicalDayCache`).
  3. Se existir: reaproveita leitura + santo do cache.
  4. Se não existir: raspa normalmente e grava no cache.
  5. **Sempre** raspa a Homilia do Papa (`VaticanHomilyScraper`) à parte —
     esse conteúdo nunca é cacheável, é diário por natureza.

## Faseamento sugerido (conforme conversa)

Para não tentar preencher os 3 anos de uma vez (raspagem pesada e sites
externos que podem bloquear/limitar requisições em rajada):

1. **Fase 0** — raspagem única do santoral (366 dias, sem depender de
   ciclo). Ganho imediato, menor esforço.
2. **Fase 1** — pré-raspar o **Ano A** (dominical) e o **Ano I** (ferial),
   ou o que estiver "de vez" no ciclo atual.
3. **Fase 2** — Ano B / Ano II, no ano seguinte.
4. **Fase 3** — Ano C / Ano I novamente, completando o ciclo.

Cada fase pode ser um job manual/pontual (não precisa ser automático), e
uma vez completo, o Worker deixa de precisar raspar leitura/santo para
qualquer data futura — só a homilia diária.

## Esforço e riscos

- Esforço: médio-alto — não é só "salvar o resultado do scraper", é criar
  o modelo de posição litúrgica e o cálculo de data da Páscoa/calendário.
- Risco principal: bugs no cálculo do calendário (ano bissexto, semanas de
  transição do Tempo Comum ao redor da Quaresma/Páscoa) resultariam em
  leitura errada para o usuário — precisa de testes de calendário robustos
  (comparar contra anos conhecidos).
- Vantagem colateral: reduz drasticamente a dependência de disponibilidade
  dos sites de terceiros (Canção Nova, GCatholic) no dia a dia, deixando
  só a raspagem do Vaticano como ponto de falha externo diário.
