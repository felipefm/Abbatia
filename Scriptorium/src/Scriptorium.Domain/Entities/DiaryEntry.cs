namespace Scriptorium.Domain.Entities;

/// <summary>
/// Anotação espiritual pessoal para um dia específico. Independente de
/// <see cref="DailyDevotional"/> (sem FK) — pode existir mesmo para datas
/// sem devocional raspado, e sobrevive mesmo que o devocional daquele dia
/// seja reprocessado.
/// </summary>
public class DiaryEntry
{
    public int Id { get; set; }

    public required DateOnly Date { get; set; }

    public required string Text { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
