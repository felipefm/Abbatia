namespace Scriptorium.Domain;

/// <summary>
/// Limites de sanidade para o scraping ao vivo/sob demanda, compartilhados
/// por qualquer endpoint que precise decidir se vale a pena tentar raspar
/// uma data (devocional individual ou calendário mensal). Nenhuma das
/// fontes publica calendário litúrgico fora desta janela, então nem vale a
/// pena tentar.
/// </summary>
public static class ScrapableDateRange
{
    public static readonly DateOnly MinScrapableDate = new(2000, 1, 1);

    public static DateOnly MaxScrapableDate => LiturgicalClock.Today().AddYears(5);

    public static bool Contains(DateOnly date) => date >= MinScrapableDate && date <= MaxScrapableDate;
}
