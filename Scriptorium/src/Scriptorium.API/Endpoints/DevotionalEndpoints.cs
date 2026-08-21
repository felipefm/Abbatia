using System.Globalization;
using Microsoft.Extensions.Logging;
using Scriptorium.API.DTOs;
using Scriptorium.Application.Interfaces;
using Scriptorium.Application.Services;
using Scriptorium.Domain;

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
/// SOBRE A API "SÓ LER DO BANCO" — E O CACHE-MISS SOB DEMANDA:
/// O CAMINHO NORMAL continua sendo ler o que o Worker já deixou pronto no
/// SQLite de madrugada (rápido, sem esperar sites externos) — essa
/// separação entre "quem escreve" (Worker) e "quem lê" (API) é uma
/// aplicação simplificada do padrão CQRS (Command Query Responsibility
/// Segregation). MAS, quando o usuário navega para uma data que o Worker
/// ainda não processou (fora da janela padrão de 7 dias, ou o Worker caiu
/// naquele dia), em vez de simplesmente devolver 404, a API tenta montar o
/// devocional NA HORA, chamando o mesmo <see cref="DevotionalBuilderService"/>
/// que o Worker usa — e SALVA o resultado, para que a próxima consulta
/// àquela data (deste ou de qualquer outro dispositivo) seja instantânea.
/// Isso é uma pequena flexibilização deliberada do CQRS "puro" (um caso de
/// "cache-miss sob demanda"): o ganho de poder navegar livremente pelo
/// calendário compensa a API deixar de ser 100% burra/rápida NESSE caso
/// específico (que só acontece uma vez por data — depois fica em cache no
/// banco para sempre). Ver
/// docs/04-inteligencia-de-codigo.md, seção "Navegação livre por
/// calendário e busca sob demanda", para o raciocínio completo.
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
            .WithSummary("Devolve o devocional do dia atual (hora de Brasília).")
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
        DevotionalBuilderService builder,
        ILogger<DevotionalBuilderService> logger,
        CancellationToken cancellationToken)
    {
        var today = LiturgicalClock.Today();
        return await FetchAndRespondAsync(repository, builder, logger, today, cancellationToken);
    }

    private static async Task<IResult> GetByDateAsync(
        string date,
        IDevotionalRepository repository,
        DevotionalBuilderService builder,
        ILogger<DevotionalBuilderService> logger,
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

        return await FetchAndRespondAsync(repository, builder, logger, parsedDate, cancellationToken);
    }

    /// <summary>
    /// Lógica compartilhada pelos dois endpoints: busca no repositório e
    /// converte o resultado em 200 OK + DTO. Se a data não estiver no banco
    /// (cache-miss — ver comentário na classe), tenta montar o devocional
    /// NA HORA via <see cref="DevotionalBuilderService"/> (o mesmo
    /// orquenstrador usado pelo Worker) antes de desistir com 404.
    /// </summary>
    private static async Task<IResult> FetchAndRespondAsync(
        IDevotionalRepository repository,
        DevotionalBuilderService builder,
        ILogger<DevotionalBuilderService> logger,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var devotional = await repository.GetByDateAsync(date, cancellationToken);

        if (devotional is null)
        {
            if (!ScrapableDateRange.Contains(date))
            {
                return Results.NotFound(new
                {
                    erro = $"Nenhum devocional encontrado para {date:yyyy-MM-dd}, e essa data está fora do " +
                           $"intervalo suportado para busca ao vivo ({ScrapableDateRange.MinScrapableDate:yyyy-MM-dd} a {ScrapableDateRange.MaxScrapableDate:yyyy-MM-dd}).",
                });
            }

            // CACHE-MISS: essa data ainda não foi processada pelo Worker.
            // Tenta montar na hora, ao vivo, chamando os mesmos 4 scrapers +
            // tradução que o Worker usaria de madrugada. Pode demorar (cada
            // scraper tem até 30s de timeout, e a tradução via LM Studio até
            // 120s se a IA estiver ligada mas lenta) — mas o resultado fica
            // salvo, então só a PRIMEIRA consulta a essa data é lenta.
            logger.LogInformation(
                "Devocional de {Data:yyyy-MM-dd} não encontrado no banco; tentando montar sob demanda.",
                date);

            devotional = await builder.BuildAsync(date, cancellationToken);

            // Só vale a pena salvar/devolver se REALMENTE achamos alguma
            // coisa em pelo menos uma das 4 fontes — caso contrário
            // salvaríamos um registro "vazio" (só com o título de fallback
            // "Feria do dia ..." que DevotionalBuilderService sempre gera)
            // que poluiria o banco sem nenhuma informação real.
            var encontrouAlgumaCoisa = devotional.Saint is not null
                || devotional.Readings.Count > 0
                || devotional.Homily is not null;

            if (!encontrouAlgumaCoisa)
            {
                logger.LogWarning("Busca sob demanda de {Data:yyyy-MM-dd} não encontrou nada em nenhuma fonte.", date);
                return Results.NotFound(new
                {
                    erro = $"Não encontramos informações para {date:yyyy-MM-dd} em nenhuma das fontes, " +
                           "mesmo tentando buscar ao vivo agora. Essa data pode estar fora do que os sites publicam.",
                });
            }

            await repository.UpsertAsync(devotional, cancellationToken);
            logger.LogInformation("Devocional de {Data:yyyy-MM-dd} montado e salvo sob demanda com sucesso.", date);
        }
        else if (devotional.Readings.Count == 0 && ScrapableDateRange.Contains(date))
        {
            // REGISTRO INCOMPLETO: a data já foi processada (existe no banco),
            // mas sem leituras — sinal de que o scraper de liturgia falhou
            // naquele dia especificamente (site fora do ar, artigo ainda não
            // publicado no instante em que o Worker rodou, etc), enquanto os
            // outros scrapers (santo, homilia) foram bem. Como o Worker só
            // reprocessa os PRÓXIMOS dias a partir de "hoje" (ver
            // DailyDevotionalWorker.RunOnceAsync), uma vez que a data vira
            // passado ela nunca mais seria revisitada — então é a API quem
            // precisa tentar de novo aqui, na leitura. Só tentamos completar
            // (nunca substituímos Santo/Homilia já salvos): UpsertAsync só
            // sobrescreve esses campos quando o resultado da nova raspagem
            // não é nulo, então uma falha repetida do scraper de liturgia não
            // apaga dado nenhum que já estava certo.
            logger.LogInformation(
                "Devocional de {Data:yyyy-MM-dd} está no banco sem leituras; tentando completar sob demanda.",
                date);

            var rebuilt = await builder.BuildAsync(date, cancellationToken);

            if (rebuilt.Readings.Count > 0)
            {
                await repository.UpsertAsync(rebuilt, cancellationToken);
                devotional = await repository.GetByDateAsync(date, cancellationToken) ?? devotional;
                logger.LogInformation("Leituras de {Data:yyyy-MM-dd} completadas sob demanda com sucesso.", date);
            }
            else
            {
                logger.LogWarning(
                    "Tentativa de completar leituras de {Data:yyyy-MM-dd} sob demanda não encontrou nada; devolvendo registro como está.",
                    date);
            }
        }

        return Results.Ok(DevotionalResponse.FromEntity(devotional));
    }
}
