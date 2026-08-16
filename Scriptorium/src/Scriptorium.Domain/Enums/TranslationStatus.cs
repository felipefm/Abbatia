namespace Scriptorium.Domain.Enums;

/// <summary>
/// Representa o estado de tradução de um texto originalmente capturado em
/// outro idioma (por exemplo, uma homilia do Papa que às vezes só está
/// disponível em italiano/inglês antes da tradução oficial em português sair).
///
/// PORQUÊ ISSO EXISTE (contexto arquitetural):
/// A Abbatia depende de uma IA LOCAL (LM Studio) rodando num computador da
/// homelab que pode estar DESLIGADO na hora em que o Worker roda de madrugada.
/// Não podemos simplesmente "falhar" o processo inteiro se a tradução não
/// funcionar — isso violaria o requisito de resiliência do projeto.
///
/// Em vez disso, seguimos o padrão "Salvar o que temos, marcar o que falta"
/// (também chamado de "graceful degradation"): persistimos o texto original
/// no banco IMEDIATAMENTE, com o status abaixo indicando se a tradução
/// ainda precisa ser tentada. Um job futuro do BackgroundService pode
/// varrer o banco procurando por registros "PendenteDeTraducao" e tentar
/// de novo, sem perder o conteúdo original.
/// </summary>
public enum TranslationStatus
{
    /// <summary>
    /// O texto já nasceu em português (por exemplo, veio direto do site do
    /// Vaticano em PT-BR) e não precisa passar pela IA de tradução.
    /// </summary>
    NaoRequerida = 0,

    /// <summary>
    /// Texto capturado em outro idioma, mas a tradução via LM Studio AINDA
    /// NÃO foi tentada (por exemplo, texto acabou de ser salvo pelo scraper).
    /// </summary>
    Pendente = 1,

    /// <summary>
    /// A tradução foi tentada, mas falhou (LM Studio desligado, erro de
    /// rede, timeout, resposta inválida). O texto original em inglês/italiano
    /// permanece salvo em <c>TextoOriginal</c> para que o usuário não fique
    /// sem conteúdo enquanto aguardamos uma nova tentativa.
    /// </summary>
    FalhouTentativa = 2,

    /// <summary>
    /// Tradução concluída com sucesso. O campo <c>TextoTraduzido</c> está
    /// preenchido e pronto para ser exibido ao usuário final.
    /// </summary>
    Concluida = 3,
}
