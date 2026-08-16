using Scriptorium.Domain.Enums;

namespace Scriptorium.Domain.Entities;

/// <summary>
/// Representa uma homilia do Papa associada (ou não) ao dia do devocional.
///
/// NOTA IMPORTANTE SOBRE A RELAÇÃO COM DailyDevotional:
/// Diferente de Reading e SaintOfTheDay (que existem TODO santo dia), o Papa
/// nem sempre celebra uma missa pública com homilia publicada num dia
/// específico. Por isso <see cref="DailyDevotionalId"/> é nullable (int?) —
/// uma Homily pode existir "solta", já capturada do site do Vaticano, mas
/// ainda sem ser vinculada a nenhum dia específico do devocional, ou pode
/// ficar disponível para consulta avulsa mesmo que não seja "a homilia de
/// hoje".
///
/// CONTROLE DE STATUS DE TRADUÇÃO (requisito central do projeto):
/// As homilias do Papa Leão XIV às vezes só têm tradução oficial disponível
/// em outros idiomas (inglês, italiano) antes da versão em português ser
/// publicada pelo Vaticano. Quando isso acontece, guardamos o texto
/// original E delegamos a tradução para a IA local rodando no LM Studio.
/// Os campos <see cref="TextoOriginal"/> / <see cref="TextoTraduzido"/> e
/// <see cref="Status"/> trabalham juntos para modelar esse fluxo assíncrono
/// e tolerante a falhas (veja os comentários em <see cref="TranslationStatus"/>).
/// </summary>
public class Homily
{
    public int Id { get; set; }

    /// <summary>
    /// FK opcional para o devocional do dia. Nula quando a homilia foi
    /// capturada mas ainda não foi associada a nenhum DailyDevotional
    /// (por exemplo, o Worker rodou o scraper de homilias antes de criar
    /// os devocionais dos próximos 7 dias).
    /// </summary>
    public int? DailyDevotionalId { get; set; }

    public DailyDevotional? DailyDevotional { get; set; }

    /// <summary>Título da homilia, ex: "Santa Missa na Solenidade da Assunção...".</summary>
    public required string Title { get; set; }

    /// <summary>
    /// Data efetiva em que a homilia foi proferida (extraída do nome do
    /// arquivo na URL do Vaticano, formato YYYYMMDD).
    /// </summary>
    public required DateOnly HomilyDate { get; set; }

    /// <summary>
    /// Idioma em que <see cref="TextoOriginal"/> foi capturado (ex: "en", "it", "pt").
    /// Usamos código ISO 639-1 (duas letras) por ser um padrão amplamente
    /// reconhecido e compacto.
    /// </summary>
    public required string OriginalLanguage { get; set; }

    /// <summary>
    /// Texto original tal como veio da fonte, no idioma indicado por
    /// <see cref="OriginalLanguage"/>. NUNCA é sobrescrito — mesmo depois de
    /// traduzido, mantemos o original para fins de auditoria e para permitir
    /// re-tentativas de tradução com um modelo de IA melhor no futuro.
    /// </summary>
    public required string TextoOriginal { get; set; }

    /// <summary>
    /// Texto traduzido para português pela IA local (LM Studio). Fica nulo
    /// enquanto <see cref="Status"/> for <see cref="TranslationStatus.Pendente"/>
    /// ou <see cref="TranslationStatus.FalhouTentativa"/>.
    /// </summary>
    public string? TextoTraduzido { get; set; }

    /// <summary>Estado atual do processo de tradução. Veja <see cref="TranslationStatus"/>.</summary>
    public TranslationStatus Status { get; set; } = TranslationStatus.Pendente;

    /// <summary>URL de origem no site do Vaticano, para auditoria/link direto.</summary>
    public required string SourceUrl { get; set; }

    /// <summary>
    /// Quantas vezes já tentamos traduzir este texto sem sucesso. Usado
    /// pelo Worker para decidir se ainda vale a pena tentar de novo ou se
    /// deve desistir (evitando fila infinita de tentativas se o LM Studio
    /// estiver permanentemente incompatível com aquele texto).
    /// </summary>
    public int TranslationAttempts { get; set; }
}
