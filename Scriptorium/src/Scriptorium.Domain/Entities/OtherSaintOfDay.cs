namespace Scriptorium.Domain.Entities;

/// <summary>
/// Um santo ADICIONAL comemorado no mesmo dia do <see cref="DailyDevotional.Saint"/>
/// principal, raspado do Vatican News (santoral fixo por mês/dia, independente
/// de ano). Diferente do santo principal (que vem do CancaoNova com biografia
/// completa), aqui guardamos só nome + um parágrafo curto — é o que a fonte
/// oferece.
/// </summary>
public class OtherSaintOfDay
{
    public int Id { get; set; }

    /// <summary>Chave estrangeira para o devocional do dia.</summary>
    public int DailyDevotionalId { get; set; }

    /// <summary>Propriedade de navegação EF Core para o devocional "pai".</summary>
    public DailyDevotional? DailyDevotional { get; set; }

    /// <summary>Nome como aparece no Vatican News, ex: "S. Timóteo, mártir romano, na via Ostiense".</summary>
    public required string Name { get; set; }

    /// <summary>Biografia curta (um parágrafo), extraída da página do dia.</summary>
    public required string ShortBiography { get; set; }
}
