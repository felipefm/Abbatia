using System.Globalization;
using Scriptorium.API.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.API.Endpoints;

/// <summary>
/// CRUD do diário espiritual pessoal (uma entrada de texto livre por data).
/// App de usuário único, sem autenticação — não há conceito de "dono" da
/// entrada.
/// </summary>
public static class DiaryEndpoints
{
    public static IEndpointRouteBuilder MapDiaryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diary").WithTags("Diary");

        group.MapGet("/{date}", GetByDateAsync)
            .WithName("GetDiaryEntry")
            .WithSummary("Devolve a entrada do diário de uma data, no formato yyyy-MM-dd.")
            .Produces<DiaryEntryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{date}", SaveAsync)
            .WithName("SaveDiaryEntry")
            .WithSummary("Cria ou atualiza a entrada do diário de uma data.")
            .Produces<DiaryEntryResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapDelete("/{date}", DeleteAsync)
            .WithName("DeleteDiaryEntry")
            .WithSummary("Remove a entrada do diário de uma data.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool TryParseDate(string date, out DateOnly parsed) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);

    private static async Task<IResult> GetByDateAsync(string date, IDiaryRepository repository, CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var parsed))
        {
            return Results.BadRequest(new { erro = $"Formato de data inválido: '{date}'. Use o formato yyyy-MM-dd." });
        }

        var entry = await repository.GetByDateAsync(parsed, cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(DiaryEntryResponse.FromEntity(entry));
    }

    private static async Task<IResult> SaveAsync(
        string date,
        SaveDiaryEntryRequest request,
        IDiaryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var parsed))
        {
            return Results.BadRequest(new { erro = $"Formato de data inválido: '{date}'. Use o formato yyyy-MM-dd." });
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { erro = "O texto do diário não pode ficar vazio." });
        }

        var saved = await repository.UpsertAsync(parsed, request.Text, cancellationToken);
        return Results.Ok(DiaryEntryResponse.FromEntity(saved));
    }

    private static async Task<IResult> DeleteAsync(string date, IDiaryRepository repository, CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var parsed))
        {
            return Results.BadRequest(new { erro = $"Formato de data inválido: '{date}'. Use o formato yyyy-MM-dd." });
        }

        var deleted = await repository.DeleteAsync(parsed, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
