using Microsoft.Extensions.Logging;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain.Entities;
using Scriptorium.Domain.Enums;

namespace Scriptorium.Application.Services;

/// <summary>
/// ORQUESTRADOR da montagem de um <see cref="DailyDevotional"/> completo
/// para uma data específica, combinando os 4 scrapers (Strategy pattern) e
/// o serviço de tradução.
///
/// PORQUÊ ESSA CLASSE FICA NA CAMADA APPLICATION (E NÃO NO WORKER)?
/// Esta classe representa uma REGRA DE NEGÓCIO/CASO DE USO: "para montar o
/// devocional de um dia, combine santo + liturgia + calendário litúrgico +
/// homilia (se houver), tentando traduzir textos pendentes". Essa lógica é
/// o "coração" do que a Abbatia faz — e por isso pertence à camada
/// Application, que concentra os casos de uso do sistema.
///
/// O projeto Scriptorium.Worker (camada mais externa, um "delivery
/// mechanism" assim como a API) é apenas o "gatilho" que decide QUANDO essa
/// lógica deve rodar (todo dia de madrugada, via BackgroundService). Essa
/// separação permite, por exemplo, expor essa mesma lógica futuramente por
/// um endpoint HTTP manual ("forçar reprocessamento de um dia") sem
/// duplicar nada.
///
/// Esta classe só depende de INTERFACES (ISaintOfTheDayScraper,
/// ILiturgyScraper, etc), nunca de implementações concretas — é Injeção de
/// Dependência em ação: o container de DI (configurado no Program.cs do
/// Worker) decide, em tempo de execução, quais classes concretas vão
/// preencher esses parâmetros do construtor.
/// </summary>
public class DevotionalBuilderService(
    ISaintOfTheDayScraper saintScraper,
    ILiturgyScraper liturgyScraper,
    ILiturgicalCalendarScraper calendarScraper,
    IHomilyScraper homilyScraper,
    ITranslationService translationService,
    ILogger<DevotionalBuilderService> logger)
{
    /// <summary>
    /// Monta (ou remonta) o devocional completo de uma data, chamando todos
    /// os scrapers em paralelo (quando possível) e tolerando falha
    /// individual de qualquer um deles.
    /// </summary>
    public async Task<DailyDevotional> BuildAsync(DateOnly date, CancellationToken cancellationToken)
    {
        // Task.WhenAll dispara as 4 chamadas de rede EM PARALELO ao invés de
        // sequencialmente. Como cada scraper acessa um site diferente, não
        // há nenhuma dependência entre eles — rodar em série seria
        // desperdiçar tempo esperando 4 respostas HTTP uma atrás da outra
        // quando poderíamos esperar todas ao mesmo tempo. Isso reduz
        // drasticamente o tempo total de execução do Worker.
        var saintTask = SafeScrapeAsync(() => saintScraper.GetForDateAsync(date, cancellationToken), nameof(saintScraper));
        var liturgyTask = SafeScrapeAsync(() => liturgyScraper.GetForDateAsync(date, cancellationToken), nameof(liturgyScraper));
        var calendarTask = SafeScrapeAsync(() => calendarScraper.GetForDateAsync(date, cancellationToken), nameof(calendarScraper));
        var homilyTask = SafeScrapeAsync(() => homilyScraper.GetForDateAsync(date, cancellationToken), nameof(homilyScraper));

        await Task.WhenAll(saintTask, liturgyTask, calendarTask, homilyTask);

        var saintResult = saintTask.Result;
        var liturgyResult = liturgyTask.Result;
        var calendarResult = calendarTask.Result;
        var homilyResult = homilyTask.Result;

        // A cor litúrgica "oficial" vem da página de liturgia diária (fonte
        // primária). Se por algum motivo ela não veio (site fora do ar,
        // mudança de layout), caímos para a cor informada pelo calendário
        // do gcatholic.org como FALLBACK — um exemplo prático de como
        // combinar múltiplas fontes torna o sistema mais resiliente do que
        // depender de uma fonte única.
        var color = liturgyResult?.Color ?? calendarResult?.Color ?? LiturgicalColor.Verde;
        var title = liturgyResult?.LiturgicalTitle ?? calendarResult?.CelebrationName ?? $"Feria do dia {date:dd/MM/yyyy}";

        var devotional = new DailyDevotional
        {
            Date = date,
            LiturgicalTitle = title,
            Color = color,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        if (liturgyResult is not null)
        {
            devotional.Readings = liturgyResult.Readings
                .Select(r => new Reading
                {
                    Type = r.Type,
                    Reference = r.Reference,
                    Text = r.Text,
                })
                .ToList();
        }

        if (saintResult is not null)
        {
            devotional.Saint = new SaintOfTheDay
            {
                Name = saintResult.Name,
                Biography = saintResult.Biography,
                ImageUrl = saintResult.ImageUrl,
                SourceUrl = saintResult.SourceUrl,
            };
        }

        if (homilyResult is not null)
        {
            devotional.Homily = await BuildHomilyWithTranslationAsync(homilyResult, cancellationToken);
        }

        return devotional;
    }

    /// <summary>
    /// Constrói a entidade Homily a partir do resultado do scraping,
    /// tentando traduzir o texto original SE ele não estiver em português.
    ///
    /// Este é o ponto exato onde o requisito "se a IA local falhar, salve
    /// o original com status Pendente" é implementado: independentemente
    /// do resultado da tradução (sucesso OU falha), SEMPRE retornamos uma
    /// entidade válida para ser salva no banco — nunca deixamos o dado se
    /// perder por causa de uma falha na tradução.
    /// </summary>
    private async Task<Homily> BuildHomilyWithTranslationAsync(
        Application.DTOs.HomilyScrapeResult homilyResult,
        CancellationToken cancellationToken)
    {
        var homily = new Homily
        {
            Title = homilyResult.Title,
            HomilyDate = homilyResult.HomilyDate,
            OriginalLanguage = homilyResult.OriginalLanguage,
            TextoOriginal = homilyResult.TextoOriginal,
            SourceUrl = homilyResult.SourceUrl,
        };

        // Se o texto já veio em português (o Vaticano às vezes já publica a
        // tradução oficial), não há trabalho de IA a fazer.
        if (string.Equals(homilyResult.OriginalLanguage, "pt", StringComparison.OrdinalIgnoreCase))
        {
            homily.Status = TranslationStatus.NaoRequerida;
            homily.TextoTraduzido = homilyResult.TextoOriginal;
            return homily;
        }

        homily.TranslationAttempts = 1;

        var translationResult = await translationService.TranslateAsync(
            homilyResult.TextoOriginal,
            homilyResult.OriginalLanguage,
            cancellationToken);

        if (translationResult.Success)
        {
            homily.TextoTraduzido = translationResult.TranslatedText;
            homily.Status = TranslationStatus.Concluida;
        }
        else
        {
            // AQUI é o "graceful degradation" em ação: a homilia é salva
            // MESMO SEM tradução, com o texto original preservado e o
            // status marcado para que uma execução futura do Worker tente
            // novamente (ver DevotionalBuilderService.RetryPendingTranslationsAsync).
            logger.LogWarning(
                "Falha ao traduzir homilia '{Title}': {Erro}. Texto original será salvo com status Pendente.",
                homilyResult.Title,
                translationResult.ErrorMessage);
            homily.Status = TranslationStatus.FalhouTentativa;
        }

        return homily;
    }

    /// <summary>
    /// Envolve a chamada de um scraper com tratamento de exceção, para que
    /// a falha de UM scraper (ex: santo.cancaonova.com fora do ar) nunca
    /// derrube o Task.WhenAll inteiro e impeça os outros 3 scrapers de
    /// completar. Cada fonte de dado é isolada das demais.
    /// </summary>
    private async Task<T?> SafeScrapeAsync<T>(Func<Task<T?>> scrapeFunc, string scraperName) where T : class
    {
        try
        {
            return await scrapeFunc();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Scraper '{ScraperName}' falhou; prosseguindo sem este dado.", scraperName);
            return null;
        }
    }
}
