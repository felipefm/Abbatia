using Scriptorium.Domain.Enums;

namespace Scriptorium.Domain.Entities;

/// <summary>
/// Representa UMA leitura bíblica da liturgia do dia (1ª Leitura, Salmo,
/// 2ª Leitura ou Evangelho).
///
/// PORQUÊ UMA TABELA SEPARADA (E NÃO 4 COLUNAS SOLTAS NO DailyDevotional)?
/// Um <see cref="DailyDevotional"/> pode ter de 3 a 4 leituras dependendo do
/// dia (dias de semana normalmente não têm 2ª Leitura, só domingos e
/// solenidades têm). Se modelássemos como colunas fixas
/// (PrimeiraLeitura, Salmo, SegundaLeitura, Evangelho todas na mesma tabela),
/// teríamos colunas nulas o tempo todo E dificultaria adicionar, no futuro,
/// leituras extras (ex: Vigília Pascal tem várias leituras do AT).
/// Modelar como uma coleção (relação 1-para-N) é a solução mais flexível e
/// segue o princípio de "normalização" de bancos de dados relacionais.
/// </summary>
public class Reading
{
    public int Id { get; set; }

    /// <summary>Chave estrangeira para o devocional do dia.</summary>
    public int DailyDevotionalId { get; set; }

    /// <summary>Propriedade de navegação EF Core para o devocional "pai".</summary>
    public DailyDevotional? DailyDevotional { get; set; }

    /// <summary>Qual papel esta leitura desempenha na liturgia (1ª Leitura, Salmo, etc).</summary>
    public required ReadingType Type { get; set; }

    /// <summary>
    /// Referência bíblica abreviada, ex: "Ap 11,19a;12,1.3-6a.10ab" ou
    /// "Lc 1,39-56". Guardamos como veio da fonte, sem tentar "parsear"
    /// livro/capítulo/versículo — não é necessário para o escopo do app.
    /// </summary>
    public required string Reference { get; set; }

    /// <summary>
    /// Texto completo da leitura, já limpo de tags HTML (apenas o texto
    /// corrido dos parágrafos, extraído pelo HtmlAgilityPack).
    /// </summary>
    public required string Text { get; set; }
}
