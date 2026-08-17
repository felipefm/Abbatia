using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Implementação CONCRETA da estratégia <see cref="ISaintOfTheDayScraper"/>
/// (padrão Strategy) para a fonte santo.cancaonova.com.
///
/// FLUXO DE SCRAPING (2 requisições HTTP):
/// 1) Descobre a URL do artigo do santo do dia pedido, via o helper de
///    calendário AJAX compartilhado (<see cref="CancaoNovaCalendarHelper"/>).
/// 2) Faz o GET nessa URL e usa HtmlAgilityPack para navegar pela árvore DOM
///    do HTML retornado (SelectSingleNode/SelectNodes com XPath) e extrair
///    o nome do santo e a biografia.
///
/// SOBRE O HtmlAgilityPack: é a biblioteca .NET mais usada para parsing de
/// HTML "sujo" do mundo real (com tags não fechadas, entidades malformadas
/// etc, como browsers reais toleram). Ela nos dá uma API estilo
/// "DOM + XPath", parecida com o que JavaScript oferece no navegador com
/// `document.querySelector`, mas em C# e sem precisar de um navegador de
/// verdade rodando (o que seria MUITO mais pesado para um Worker que roda
/// uma vez por dia em background).
/// </summary>
public class CancaoNovaSaintScraper(
    IHttpClientFactory httpClientFactory,
    ILogger<CancaoNovaSaintScraper> logger) : ISaintOfTheDayScraper
{
    // O nome usado aqui ("SantoCancaoNova") precisa bater exatamente com o
    // nome registrado em ServiceCollectionExtensions.AddScriptoriumInfrastructure,
    // onde configuramos o BaseAddress e os headers padrão deste HttpClient.
    private const string HttpClientName = "SantoCancaoNova";

    public async Task<SaintScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        var articleUrl = await CancaoNovaCalendarHelper.FindArticleUrlForDateAsync(
            httpClient, "santo", date, cancellationToken);

        if (articleUrl is null)
        {
            logger.LogInformation("Nenhum link de santo encontrado no calendário para {Data:yyyy-MM-dd}.", date);
            return null;
        }

        using var response = await httpClient.GetAsync(articleUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        // O nome do santo está dentro de: <h1 class="entry-title"><span>NOME</span></h1>
        var nameNode = document.DocumentNode.SelectSingleNode("//h1[contains(@class,'entry-title')]//span");
        var name = nameNode is null ? null : HtmlTextExtractor.CleanText(nameNode.InnerText);

        if (string.IsNullOrWhiteSpace(name))
        {
            logger.LogWarning("Não foi possível extrair o nome do santo em {Url}.", articleUrl);
            return null;
        }

        // O corpo da biografia está em: <div class="entry-content content-santo"> ... vários <p> ...
        // Essa div também contém um <ul id="share-buttons"> com os ícones de
        // compartilhar (Facebook, WhatsApp, etc) que PRECISA ser removido
        // antes de extrairmos os parágrafos, senão o texto dos botões de
        // compartilhamento entraria misturado com a biografia.
        //
        // Usamos ExtractParagraphsAndListItems (não ExtractParagraphs) porque
        // este site coloca duas seções importantes em listas <ul>/<li> em vez
        // de parágrafos: "Outros santos e beatos celebrados em [data]" e
        // "Fontes" (bibliografia). ExtractParagraphs (só <p>) as ignorava
        // silenciosamente — bug real reportado pelo usuário, ver Bug #8 em
        // docs/04-inteligencia-de-codigo.md.
        var contentNode = document.DocumentNode.SelectSingleNode("//div[contains(@class,'content-santo')]");
        var biography = HtmlTextExtractor.ExtractParagraphsAndListItems(contentNode, ".//ul[@id='share-buttons']");

        var imageNode = contentNode?.SelectSingleNode(".//img");
        var imageUrl = imageNode?.GetAttributeValue("src", string.Empty);

        return new SaintScrapeResult(name, biography, imageUrl, articleUrl);
    }
}
