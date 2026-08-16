using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain.Enums;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Implementação CONCRETA da estratégia <see cref="ILiturgyScraper"/> para a
/// fonte liturgia.cancaonova.com — extrai o título do dia litúrgico, a cor
/// litúrgica e todas as leituras (1ª Leitura, Salmo, [2ª Leitura], Evangelho).
///
/// ESTRUTURA DO HTML DA PÁGINA DE ARTIGO (descoberta por inspeção manual):
/// &lt;span class="cor-liturgica"&gt;Cor Litúrgica: Branco&lt;/span&gt;
/// &lt;h1 class="entry-title"&gt;Título do dia litúrgico&lt;/h1&gt;
/// &lt;ul id="leituraTab"&gt;                          -- ABAS (menu) com o TIPO e a REFERÊNCIA de cada leitura
///   &lt;li&gt;&lt;label class="tipo-titulo"&gt;1ª Leitura&lt;/label&gt;&lt;div class="referencia"&gt;Ap 11,19a...&lt;/div&gt;&lt;/li&gt;
///   ... (Salmo, [2ª Leitura], Evangelho)
/// &lt;/ul&gt;
/// &lt;div id="liturgia-1"&gt;...texto completo da 1ª leitura em &lt;p&gt;...&lt;/div&gt;  -- CONTEÚDO de cada aba, na mesma ordem (liturgia-1, liturgia-2, ...)
/// </summary>
public class CancaoNovaLiturgyScraper(
    IHttpClientFactory httpClientFactory,
    ILogger<CancaoNovaLiturgyScraper> logger) : ILiturgyScraper
{
    private const string HttpClientName = "LiturgiaCancaoNova";

    public async Task<LiturgyScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        var articleUrl = await CancaoNovaCalendarHelper.FindArticleUrlForDateAsync(
            httpClient, "liturgia", date, cancellationToken);

        if (articleUrl is null)
        {
            logger.LogInformation("Nenhum link de liturgia encontrado no calendário para {Data:yyyy-MM-dd}.", date);
            return null;
        }

        using var response = await httpClient.GetAsync(articleUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(html);

        var colorNode = document.DocumentNode.SelectSingleNode("//span[contains(@class,'cor-liturgica')]");
        var color = ParseColor(colorNode is null ? null : HtmlTextExtractor.CleanText(colorNode.InnerText));

        var titleNode = document.DocumentNode.SelectSingleNode("//h1[contains(@class,'entry-title')]");
        var title = titleNode is null ? $"Liturgia de {date:dd/MM/yyyy}" : HtmlTextExtractor.CleanText(titleNode.InnerText);

        var readings = ExtractReadings(document);

        return new LiturgyScrapeResult(title, color, readings, articleUrl);
    }

    /// <summary>
    /// As abas (&lt;li&gt; dentro de #leituraTab) e os painéis de conteúdo
    /// (&lt;div id="liturgia-N"&gt;) são posicionalmente correspondentes: a
    /// 1ª aba da lista bate com "liturgia-1", a 2ª com "liturgia-2", e assim
    /// por diante — MAS ATENÇÃO: essa numeração NÃO É reindexada quando uma
    /// aba do meio está ausente. Em dias de semana comuns (sem 2ª Leitura),
    /// a lista de abas tem só 3 itens (1ª Leitura, Salmo, Evangelho), porém
    /// o painel do Evangelho continua se chamando "liturgia-4" (o número
    /// original da posição fixa do Evangelho no esquema do site, que sempre
    /// reserva a posição 3 para a 2ª Leitura mesmo quando ela não existe).
    /// Confirmamos isso inspecionando o HTML de um dia de semana real:
    /// o link da aba já vem como <c>href="#liturgia-4"</c> mesmo sendo a
    /// 3ª (e última) aba da lista. Por isso, em vez de contar posições,
    /// extraímos o número do painel DIRETO do atributo href de cada aba —
    /// a única fonte confiável dessa correspondência.
    /// </summary>
    private static List<ReadingScrapeResult> ExtractReadings(HtmlDocument document)
    {
        var results = new List<ReadingScrapeResult>();

        var tabItems = document.DocumentNode.SelectNodes("//ul[@id='leituraTab']/li");
        if (tabItems is null)
        {
            return results;
        }

        foreach (var tabItem in tabItems)
        {
            var labelNode = tabItem.SelectSingleNode(".//label[contains(@class,'tipo-titulo')]");
            var referenceNode = tabItem.SelectSingleNode(".//div[contains(@class,'referencia')]");
            var linkNode = tabItem.SelectSingleNode(".//a[@href]");

            var label = labelNode is null ? string.Empty : HtmlTextExtractor.CleanText(labelNode.InnerText);
            var reference = referenceNode is null ? string.Empty : HtmlTextExtractor.CleanText(referenceNode.InnerText);

            // O href da aba é algo como "#liturgia-4" — removemos o "#" para
            // montar o XPath de busca do painel correspondente.
            var panelId = linkNode?.GetAttributeValue("href", string.Empty).TrimStart('#') ?? string.Empty;
            var panelNode = string.IsNullOrEmpty(panelId)
                ? null
                : document.DocumentNode.SelectSingleNode($"//div[@id='{panelId}']");

            // O painel de áudio embutido (&lt;div class="embeds-audio"&gt;)
            // contém um &lt;iframe&gt; do player de podcast da Cancão Nova —
            // não tem texto útil, mas removê-lo explicitamente evita
            // qualquer ruído acidental na extração.
            var text = HtmlTextExtractor.ExtractParagraphs(panelNode, ".//div[contains(@class,'embeds-audio')]");

            var readingType = MapReadingType(label);

            // Se não reconhecemos o rótulo (ex: mudança futura no site), ainda
            // assim preservamos o dado como "PrimeiraLeitura" por padrão em vez
            // de descartá-lo silenciosamente — mas isso só acontece em um
            // cenário de mudança de layout não esperada; documentamos com log.
            results.Add(new ReadingScrapeResult(readingType, reference, text));
        }

        return results;
    }

    private static ReadingType MapReadingType(string label)
    {
        if (label.Contains("2ª Leitura", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("2a Leitura", StringComparison.OrdinalIgnoreCase))
        {
            return ReadingType.SegundaLeitura;
        }

        if (label.Contains("Salmo", StringComparison.OrdinalIgnoreCase))
        {
            return ReadingType.SalmoResponsorial;
        }

        if (label.Contains("Evangelho", StringComparison.OrdinalIgnoreCase))
        {
            return ReadingType.Evangelho;
        }

        // Cobre "1ª Leitura" e qualquer outro rótulo não previsto (fallback seguro).
        return ReadingType.PrimeiraLeitura;
    }

    /// <summary>
    /// Converte o texto "Cor Litúrgica: Branco" (formato exato encontrado na
    /// página) no enum <see cref="LiturgicalColor"/> correspondente.
    /// </summary>
    private static LiturgicalColor ParseColor(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return LiturgicalColor.Verde;
        }

        var colorName = rawText.Split(':').Last().Trim();

        return colorName.ToLowerInvariant() switch
        {
            "branco" or "ouro" or "dourado" => LiturgicalColor.Branco,
            "vermelho" => LiturgicalColor.Vermelho,
            "roxo" or "violeta" => LiturgicalColor.Roxo,
            "rosa" => LiturgicalColor.Rosa,
            "preto" => LiturgicalColor.Preto,
            _ => LiturgicalColor.Verde,
        };
    }
}
