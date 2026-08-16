using System.Globalization;
using Scriptorium.API.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.API.Endpoints;

/// <summary>
/// Define as rotas HTTP relacionadas a devocionais usando MINIMAL APIS
/// (o estilo de roteamento introduzido no .NET 6, sem a necessidade de
/// classes Controller tradicionais do MVC).
///
/// PORQUÊ MINIMAL APIS EM VEZ DE CONTROLLERS AQUI?
/// Para uma API pequena e focada como esta (2 endpoints, ambos de LEITURA
/// simples do banco), Minimal APIs eliminam a cerimônia de criar uma classe
/// Controller + herdar de ControllerBase + decorar métodos com atributos
/// [HttpGet] — o roteamento e a implementação ficam juntos, num estilo mais
/// direto e funcional. Para APIs maiores/mais complexas, Controllers ainda
/// fazem sentido (melhor para organizar MUITOS endpoints relacionados) —
/// mas aqui, com o escopo enxuto do projeto, Minimal APIs são a escolha
/// mais simples e igualmente robusta.
///
/// PORQUÊ ESSA API SÓ LÊ DO BANCO (NUNCA FAZ SCRAPING NA HORA)?
/// Fazer o usuário do app esperar 3-4 requisições HTTP para sites externos
/// (que podem estar lentos ou fora do ar) toda vez que ele abre a tela do
/// devocional seria uma péssima experiência e um ponto de falha
/// desnecessário. A API é DELIBERADAMENTE burra e rápida: ela só lê o que o
/// Worker já deixou pronto no SQLite de madrugada. Essa separação entre
/// "quem escreve" (Worker) e "quem lê" (API) é uma aplicação simplificada
/// do padrão CQRS (Command Query Responsibility Segregation).
/// </summary>
public static class DevotionalEndpoints
{
    public static IEndpointRouteBuilder MapDevotionalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/devotional").WithTags("Devotional");

        // GET /api/devotional/today
        // IMPORTANTE: este mapeamento precisa vir ANTES do mapeamento de
        // "/{date}" logo abaixo. Se estivesse depois, o roteador tentaria
        // casar a palavra literal "today" com o parâmetro {date} da outra
        // rota (que aceitaria "today" como texto e falharia ao tentar
        // converter para DateOnly). O ASP.NET Core resolve rotas mais
        // específicas (sem parâmetro) com prioridade sobre rotas com
        // parâmetro quando registradas nessa ordem, mas é uma boa prática
        // deixar isso explícito pela ORDEM de declaração.
        group.MapGet("/today", GetTodayAsync)
            .WithName("GetTodayDevotional")
            .WithSummary("Devolve o devocional do dia atual (UTC).")
            .Produces<DevotionalResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // GET /api/devotional/{date}  (formato esperado: yyyy-MM-dd, ex: 2026-08-16)
        group.MapGet("/{date}", GetByDateAsync)
            .WithName("GetDevotionalByDate")
            .WithSummary("Devolve o devocional de uma data específica, no formato yyyy-MM-dd.")
            .Produces<DevotionalResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetTodayAsync(
        IDevotionalRepository repository,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await FetchAndRespondAsync(repository, today, cancellationToken);
    }

    private static async Task<IResult> GetByDateAsync(
        string date,
        IDevotionalRepository repository,
        CancellationToken cancellationToken)
    {
        // Fazemos o parsing MANUALMENTE com TryParseExact (em vez de deixar
        // o binding automático do Minimal APIs converter direto para
        // DateOnly no parâmetro do método) por um motivo pedagógico E
        // prático: TryParseExact nos dá controle total sobre o formato
        // aceito (estritamente "yyyy-MM-dd", como pedido no requisito da
        // rota) e nos permite devolver um 400 Bad Request com uma mensagem
        // amigável quando o formato estiver errado, em vez de um erro 400
        // genérico e pouco informativo gerado automaticamente pelo framework.
        var isValidDate = DateOnly.TryParseExact(
            date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDate);

        if (!isValidDate)
        {
            return Results.BadRequest(new
            {
                erro = $"Formato de data inválido: '{date}'. Use o formato yyyy-MM-dd (ex: 2026-08-16).",
            });
        }

        return await FetchAndRespondAsync(repository, parsedDate, cancellationToken);
    }

    /// <summary>
    /// Lógica compartilhada pelos dois endpoints: busca no repositório e
    /// converte o resultado em 200 OK + DTO, ou 404 Not Found quando o
    /// Worker ainda não processou aquele dia (ex: uma data muito no futuro,
    /// além da janela de 7 dias que o Worker mantém atualizada).
    /// </summary>
    private static async Task<IResult> FetchAndRespondAsync(
        IDevotionalRepository repository,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var devotional = await repository.GetByDateAsync(date, cancellationToken);

        if (devotional is null)
        {
            return Results.NotFound(new
            {
                erro = $"Nenhum devocional encontrado para {date:yyyy-MM-dd}. " +
                       "O Worker processa os próximos 7 dias a partir de hoje; " +
                       "datas fora dessa janela (ou muito no passado) podem não estar disponíveis.",
            });
        }

        return Results.Ok(DevotionalResponse.FromEntity(devotional));
    }
}
