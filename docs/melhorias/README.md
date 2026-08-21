# Melhorias em avaliação

Esta subpasta reúne **propostas de melhoria** discutidas para o Abbatia, mas
ainda **não decididas nem implementadas**. Diferente do resto de `docs/`
(que descreve o sistema como ele É hoje), aqui documentamos o que ele
**poderia vir a ser**, com prós, contras e nível de esforço, para que a
decisão de atacar (ou não) cada item possa ser tomada com calma, sem
depender de reconstituir o raciocínio do zero.

Quando uma proposta for implementada, o ideal é mover o conteúdo relevante
para os documentos principais (`01-infraestrutura.md`, `04-inteligencia-de-
codigo.md`, etc.) e apagar (ou arquivar) o arquivo daqui.

## Índice

| Documento | Resumo |
|---|---|
| [01-pre-raspagem-ciclos-liturgicos.md](01-pre-raspagem-ciclos-liturgicos.md) | Cachear leituras litúrgicas por ciclo (A/B/C dominical, I/II ferial) em vez de raspar tudo todo dia |
| [02-acesso-ao-sqlite.md](02-acesso-ao-sqlite.md) | Trocar o volume Docker nomeado por bind mount, para acessar o `scriptorium.db` direto pelo File Manager do CasaOS |
| [03-limitar-recursos-do-build.md](03-limitar-recursos-do-build.md) | Limitar CPU/memória do `docker build`/`buildx` para não sufocar a homelab durante o build do Scriptorium |
| [04-publicacao-estatica-no-observatorio.md](04-publicacao-estatica-no-observatorio.md) | Publicar o devocional (leituras/santo) como conteúdo estático no blog Hugo "Observatório", depois da pré-raspagem completa |
