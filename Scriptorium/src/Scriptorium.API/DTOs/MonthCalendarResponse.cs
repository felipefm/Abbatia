namespace Scriptorium.API.DTOs;

/// <summary>Cor/título litúrgico de UM dia dentro do calendário mensal.</summary>
public sealed record MonthCalendarDayResponse(string Date, string LiturgicalTitle, string LiturgicalColor);

/// <summary>
/// Resposta do endpoint de calendário mensal — usado pelo Oratorium para
/// pintar um mini-calendário com a cor litúrgica de cada dia.
/// </summary>
public sealed record MonthCalendarResponse(int Year, int Month, IReadOnlyList<MonthCalendarDayResponse> Days);
