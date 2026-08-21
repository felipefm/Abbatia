using Scriptorium.API.DTOs;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain;
using Scriptorium.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Scriptorium.API.Endpoints;

/// <summary>
/// Endpoint de calendário mensal: devolve a cor/título litúrgico de cada dia
/// de um mês, usado pelo Oratorium para pintar o mini-calendário da barra
/// lateral. Dias já persistidos vêm do banco (rápido); dias que o Worker
/// ainda não processou usam <see cref="ILiturgicalCalendarScraper"/>
/// (gcatholic.org) AO VIVO, sem persistir — esse scraper já cacheia o HTML
/// do ano inteiro em memória (é Singleton), então isso é barato mesmo
/// perguntando por um mês inteiro de dias "novos". Não usamos o
/// <see cref="Services.DevotionalBuilderService"/> completo aqui de
/// propósito: rodar os 4 scrapers pra cada um dos ~31 dias só pra colorir um
/// calendário seria caro demais para uma consulta de leitura.
/// </summary>
public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/devotional")
            .WithTags("Devotional")
            .MapGet("/calendar/{year:int}/{month:int}", GetMonthAsync)
            .WithName("GetMonthCalendar")
            .WithSummary("Devolve a cor/título litúrgico de cada dia de um mês (ano/mês numéricos).")
            .Produces<MonthCalendarResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetMonthAsync(
        int year,
        int month,
        IDevotionalRepository repository,
        ILiturgicalCalendarScraper calendarScraper,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (month is < 1 or > 12)
        {
            return Results.BadRequest(new { erro = $"Mês inválido: {month}. Use um valor entre 1 e 12." });
        }

        DateOnly firstDay;
        try
        {
            firstDay = new DateOnly(year, month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.BadRequest(new { erro = $"Ano inválido: {year}." });
        }

        var firstDayNextMonth = firstDay.AddMonths(1);

        var persisted = await repository.GetByMonthRangeAsync(firstDay, firstDayNextMonth, cancellationToken);
        var persistedByDate = persisted.ToDictionary(d => d.Date);

        var days = new List<MonthCalendarDayResponse>();
        for (var date = firstDay; date < firstDayNextMonth; date = date.AddDays(1))
        {
            if (persistedByDate.TryGetValue(date, out var existing))
            {
                days.Add(new MonthCalendarDayResponse(date.ToString("yyyy-MM-dd"), existing.LiturgicalTitle, existing.Color.ToString()));
                continue;
            }

            if (!ScrapableDateRange.Contains(date))
            {
                days.Add(new MonthCalendarDayResponse(date.ToString("yyyy-MM-dd"), $"Feria do dia {date:dd/MM/yyyy}", LiturgicalColor.Verde.ToString()));
                continue;
            }

            // Isola falha do scraper ao vivo (rede instável, timeout, site fora
            // do ar) pra que UM dia problemático não derrube o mês inteiro —
            // mesmo espírito do SafeScrapeAsync em DevotionalBuilderService,
            // mas aqui não passa por lá (não queremos os OUTROS 3 scrapers
            // completos só pra colorir um calendário).
            LiturgicalCalendarScrapeResult? live;
            try
            {
                live = await calendarScraper.GetForDateAsync(date, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao buscar cor litúrgica ao vivo para {Data:yyyy-MM-dd}; usando feria genérica.", date);
                live = null;
            }

            days.Add(new MonthCalendarDayResponse(
                date.ToString("yyyy-MM-dd"),
                live?.CelebrationName ?? $"Feria do dia {date:dd/MM/yyyy}",
                (live?.Color ?? LiturgicalColor.Verde).ToString()));
        }

        return Results.Ok(new MonthCalendarResponse(year, month, days));
    }
}
