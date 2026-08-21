using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Implementação concreta de <see cref="IOtherSaintsScraper"/> para
/// vaticannews.va — a página "Santo do Dia" desse site lista TODOS os
/// santos comemorados numa data (sem distinguir "principal" de "outros"),
/// cada um como uma seção própria:
///
///   &lt;section class="section section--evidence section--isStatic"&gt;
///     &lt;div class="section__head"&gt;&lt;h2&gt;S. Fulano, ...&lt;/h2&gt;&lt;/div&gt;
///     &lt;div class="section__wrapper"&gt;&lt;div class="section__content"&gt;
///       &lt;p&gt;Biografia curta...&lt;/p&gt;
///     &lt;/div&gt;&lt;/div&gt;
///   &lt;/section&gt;
///
/// A URL é organizada por MÊS/DIA, SEM ANO (ex: /pt/santo-do-dia/08/22.html)
/// — é um santoral fixo, o mesmo conteúdo vale para qualquer ano civil.
/// A classe "section--isStatic" é o discriminador: o parágrafo introdutório
/// da página ("O Santo do dia é uma resenha...") usa "section section--evidence"
/// SEM "section--isStatic", então já fica de fora da seleção abaixo sem
/// precisar de nenhum filtro extra.
///
/// Quem decide o que é "outro santo" (excluindo o santo já coberto pela
/// fonte principal, CancaoNova) é o chamador (<see cref="Services.DevotionalBuilderService"/>),
/// via <see cref="Services.SaintNameMatcher"/> — este scraper só devolve a
/// lista completa como a página apresenta.
/// </summary>
public class VaticanNewsOtherSaintsScraper(
    IHttpClientFactory httpClientFactory,
    ILogger<VaticanNewsOtherSaintsScraper> logger) : IOtherSaintsScraper
{
    private const string HttpClientName = "VaticanNewsSaints";

    public async Task<IReadOnlyList<OtherSaintScrapeResult>?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var path = $"pt/santo-do-dia/{date:MM}/{date:dd}.html";

        string html;
        try
        {
            html = await httpClient.GetStringAsync(path, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogInformation(ex, "Falha ao buscar '{Path}' em vaticannews.va.", path);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(ex, "Timeout ao buscar '{Path}' em vaticannews.va.", path);
            return null;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var sections = document.DocumentNode.SelectNodes(
            "//section[contains(concat(' ', normalize-space(@class), ' '), ' section--isStatic ')]");
        if (sections is null)
        {
            logger.LogInformation("Nenhuma seção de santo encontrada em '{Path}' (layout mudou?).", path);
            return [];
        }

        var results = new List<OtherSaintScrapeResult>();
        foreach (var section in sections)
        {
            var headingNode = section.SelectSingleNode(".//div[@class='section__head']//h2");
            var contentNode = section.SelectSingleNode(".//div[@class='section__content']");
            if (headingNode is null || contentNode is null)
            {
                continue;
            }

            var name = HtmlTextExtractor.CleanText(headingNode.InnerText);
            var shortBiography = HtmlTextExtractor.ExtractParagraphs(contentNode);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(shortBiography))
            {
                continue;
            }

            results.Add(new OtherSaintScrapeResult(name, shortBiography));
        }

        return results;
    }
}
