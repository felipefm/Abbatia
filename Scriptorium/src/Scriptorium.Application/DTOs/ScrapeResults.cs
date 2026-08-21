using Scriptorium.Domain.Enums;

namespace Scriptorium.Application.DTOs;

/// <summary>
/// Este arquivo concentra os pequenos objetos "de transporte" (DTOs — Data
/// Transfer Objects) que os SCRAPERS devolvem depois de raspar uma página.
///
/// PORQUÊ NÃO DEVOLVER AS ENTIDADES DO DOMAIN DIRETO (ex: SaintOfTheDay)?
/// As entidades de Domain (Scriptorium.Domain.Entities) representam o
/// registro JÁ PERSISTIDO no banco — elas têm Id, chaves estrangeiras
/// (DailyDevotionalId) e navegação para outras entidades. Um scraper não
/// sabe (e não deveria saber!) qual vai ser o Id do devocional ao qual seu
/// resultado será associado — essa é uma decisão de quem ORQUESTRA o
/// processo (o Worker), não do scraper em si.
///
/// Separar "resultado bruto do scraping" (este arquivo) de "entidade
/// persistida" (Domain.Entities) é uma aplicação do princípio de
/// Single Responsibility: o scraper só sabe extrair dados de HTML: e
/// converter esse resultado para uma entidade de banco é responsabilidade
/// de outra camada (o Worker, na hora de montar o DailyDevotional completo).
/// </summary>

/// <summary>Resultado bruto do scraping do santo do dia.</summary>
public sealed record SaintScrapeResult(
    string Name,
    string Biography,
    string? ImageUrl,
    string SourceUrl);

/// <summary>Resultado bruto do scraping de UMA leitura bíblica.</summary>
public sealed record ReadingScrapeResult(
    ReadingType Type,
    string Reference,
    string Text);

/// <summary>
/// Resultado bruto do scraping da liturgia diária: título do dia litúrgico,
/// a cor (já vem embutida na própria página do liturgia.cancaonova.com) e a
/// lista de leituras.
/// </summary>
public sealed record LiturgyScrapeResult(
    string LiturgicalTitle,
    LiturgicalColor Color,
    IReadOnlyList<ReadingScrapeResult> Readings,
    string SourceUrl);

/// <summary>
/// Resultado bruto do scraping do calendário litúrgico do gcatholic.org.
/// Serve como fonte de VERIFICAÇÃO/FALLBACK da cor litúrgica e também traz
/// o "ranking" da celebração (Solenidade, Festa, Memória Obrigatória,
/// Memória Facultativa, Comemoração ou dia comum) — informação que o site
/// de liturgia nem sempre deixa tão explícita.
/// </summary>
public sealed record LiturgicalCalendarScrapeResult(
    LiturgicalColor Color,
    string CelebrationName,
    string? Rank);

/// <summary>Resultado bruto do scraping de UM santo listado no Vatican News.</summary>
public sealed record OtherSaintScrapeResult(string Name, string ShortBiography);

/// <summary>Resultado bruto do scraping de UMA homilia do Papa.</summary>
public sealed record HomilyScrapeResult(
    string Title,
    DateOnly HomilyDate,
    string OriginalLanguage,
    string TextoOriginal,
    string SourceUrl);

/// <summary>
/// Resultado de uma tentativa de tradução via IA local. Modelado como um
/// "resultado" explícito (em vez de lançar exceção em caso de falha) porque
/// falha de tradução é um CASO ESPERADO e recorrente neste projeto (a
/// máquina do LM Studio pode estar desligada) — não uma condição
/// excepcional. Usar exceções para fluxo de controle esperado é considerado
/// má prática em C#/.NET (exceções são caras em performance e devem
/// representar o INESPERADO). Preferimos o padrão "Result Object".
/// </summary>
public sealed record TranslationAttemptResult(bool Success, string? TranslatedText, string? ErrorMessage)
{
    public static TranslationAttemptResult Ok(string translatedText) => new(true, translatedText, null);
    public static TranslationAttemptResult Fail(string errorMessage) => new(false, null, errorMessage);
}
