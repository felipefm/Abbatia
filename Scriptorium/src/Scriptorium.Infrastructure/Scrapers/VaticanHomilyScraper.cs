using System.Globalization;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Implementação CONCRETA da estratégia <see cref="IHomilyScraper"/> para o
/// site oficial do Vaticano (vatican.va), buscando homilias do Papa.
///
/// ESTRUTURA DO SITE (descoberta por inspeção manual):
///
/// 1) PÁGINA ÍNDICE (uma por ano): /content/leo-xiv/pt/homilies/{ano}.index.html
///    Contém uma lista de todas as homilias publicadas naquele ano:
///    &lt;div class="vaticanindex"&gt;
///      &lt;ul&gt;
///        &lt;li&gt;&lt;div class="item"&gt;
///          &lt;h2&gt;&lt;a href="/content/leo-xiv/pt/homilies/2026/documents/20260815-omelia-castelgandolfo.html"&gt;
///            Título da homilia (Local, DD de mês de AAAA)
///          &lt;/a&gt;&lt;/h2&gt;
///          &lt;div class="translation-field"&gt;&lt;span class="translation"&gt;
///            &lt;a href=".../de/...">DE&lt;/a&gt; - &lt;a href=".../en/...")EN&lt;/a&gt; - ... - &lt;a href=".../pt/...")PT&lt;/a&gt;
///          &lt;/span&gt;&lt;/div&gt;
///        &lt;/div&gt;&lt;/li&gt;
///        ...
///
///    IMPORTANTE: o nome do ARQUIVO de cada homilia sempre começa com a data
///    no formato YYYYMMDD (ex: "20260815-omelia-castelgandolfo.html" =
///    15 de agosto de 2026). É esse prefixo que usamos para "casar" a
///    homilia com a data pedida — o site não organiza isso numa tabela de
///    calendário como os sites da Cancão Nova, então a estratégia de
///    scraping aqui é DIFERENTE: baixamos o índice do ano inteiro (uma
///    página só) e procuramos, no HTML já carregado, o link cujo nome de
///    arquivo bate com a data.
///
/// 2) PÁGINA DO ARTIGO: o corpo do texto fica em
///    &lt;div class="documento"&gt;&lt;div class="testo"&gt;
///      &lt;div class="abstract text parbase vaticanrichtext"&gt;...cabeçalho/título litúrgico...&lt;/div&gt;
///      &lt;div class="text parbase vaticanrichtext"&gt;...CORPO REAL DA HOMILIA em vários &lt;p&gt;...&lt;/div&gt;
///    Repare que AMBOS os blocos têm as classes "text" e "vaticanrichtext" —
///    a diferença é que o cabeçalho TAMBÉM tem a classe "abstract". Por
///    isso o XPath abaixo explicitamente EXCLUI nós com essa classe.
///
/// REQUISITO DE TRADUÇÃO: como pedimos a página do ÍNDICE EM PORTUGUÊS
/// (/pt/homilies/...), o texto normalmente já vem em PT-BR quando a
/// tradução oficial já foi publicada pelo Vaticano. Quando a tradução
/// oficial AINDA não saiu (comum nos dias seguintes a uma celebração
/// recente), a página em PT existe mas o corpo do texto vem vazio/idêntico
/// ao índice — nesse caso, seguimos o link alternativo em inglês (presente
/// no bloco "translation-field" da própria página) e deixamos a tradução
/// para a nossa IA local, conforme pedido no escopo do projeto.
/// </summary>
public class VaticanHomilyScraper(
    IHttpClientFactory httpClientFactory,
    ILogger<VaticanHomilyScraper> logger) : IHomilyScraper
{
    private const string HttpClientName = "VaticanVa";

    public async Task<HomilyScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        var indexUrl = $"content/leo-xiv/pt/homilies/{date.Year}.index.html";
        var indexHtml = await FetchHtmlOrNullAsync(httpClient, indexUrl, cancellationToken);
        if (indexHtml is null)
        {
            return null;
        }

        var indexDocument = new HtmlDocument();
        indexDocument.LoadHtml(indexHtml);

        var targetFilePrefix = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var items = indexDocument.DocumentNode.SelectNodes("//div[contains(@class,'vaticanindex')]//li//div[@class='item']");
        if (items is null)
        {
            return null;
        }

        foreach (var item in items)
        {
            var h2Node = item.SelectSingleNode(".//h2");
            if (h2Node is null)
            {
                continue;
            }

            // O <h2> só é um link clicável (<a href>) quando o Vaticano JÁ
            // publicou pelo menos uma página em português para essa homilia
            // (mesmo que o corpo do texto ainda esteja vazio/pendente — ver
            // TryScrapeArticleAsync). Quando a tradução em PT NUNCA foi
            // sequer iniciada, o <h2> vem como TEXTO PURO, sem link algum —
            // confirmado inspecionando duas homilias reais (exéquias de
            // Cardeais) que não têm nenhuma entrada "PT" no bloco de
            // traduções. Nesses casos, extraímos o nome do arquivo de
            // QUALQUER OUTRO idioma disponível no bloco "translation-field"
            // (o nome do arquivo é idêntico entre todos os idiomas — só o
            // segmento /pt//en//it/.../ na URL muda) e montamos a URL da
            // versão em PT manualmente a partir do padrão conhecido do site,
            // tentando-a mesmo assim (ela pode retornar 404 ou vir vazia —
            // ambos os casos já são tratados por TryScrapeArticleAsync/
            // FetchHtmlOrNullAsync, então é seguro tentar sem checagem prévia).
            var linkNode = h2Node.SelectSingleNode(".//a[@href]");

            string title;
            string? fileName;

            if (linkNode is not null)
            {
                title = HtmlTextExtractor.CleanText(linkNode.InnerText);
                fileName = linkNode.GetAttributeValue("href", string.Empty).Split('/').LastOrDefault();
            }
            else
            {
                title = HtmlTextExtractor.CleanText(h2Node.InnerText);
                var anyTranslationHref = item
                    .SelectSingleNode(".//div[contains(@class,'translation-field')]//a[@href]")
                    ?.GetAttributeValue("href", string.Empty);
                fileName = anyTranslationHref?.Split('/').LastOrDefault();
            }

            // O nome do arquivo só nos interessa se começar exatamente com a
            // data pedida (YYYYMMDD) — evita, por exemplo, casar 15/08 com
            // 15/08 de um EVENTO diferente publicado sob outro prefixo.
            if (string.IsNullOrEmpty(fileName) || !fileName.StartsWith(targetFilePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // Tenta a versão em português: se o <h2> já era um link, usamos
            // o href dele diretamente (já aponta para /pt/...); senão,
            // montamos a URL em PT manualmente a partir do padrão do site.
            var portugueseHref = linkNode?.GetAttributeValue("href", string.Empty)
                ?? $"/content/leo-xiv/pt/homilies/{date.Year}/documents/{fileName}";

            var portugueseResult = await TryScrapeArticleAsync(httpClient, portugueseHref, title, date, "pt", cancellationToken);
            if (portugueseResult is not null)
            {
                return portugueseResult;
            }

            // Fallback: tradução oficial em PT ainda não publicada. Seguimos
            // o link em inglês listado no bloco "translation-field" da
            // própria página do índice e deixamos a tradução para a IA local.
            logger.LogInformation(
                "Homilia '{Title}' sem conteúdo em PT ainda; tentando versão em inglês para tradução via LM Studio.",
                title);

            var englishHref = item.SelectSingleNode(".//div[contains(@class,'translation-field')]//a[text()='EN']")
                ?.GetAttributeValue("href", string.Empty);

            if (string.IsNullOrEmpty(englishHref))
            {
                logger.LogWarning("Homilia '{Title}' não tem versão em PT nem em EN disponível.", title);
                return null;
            }

            return await TryScrapeArticleAsync(httpClient, englishHref, title, date, "en", cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Baixa a página de um artigo de homilia específico e extrai o corpo do
    /// texto. Devolve <c>null</c> quando a página existe mas não tem corpo de
    /// texto substancial (sinal de que a tradução para aquele idioma ainda
    /// não foi publicada pelo Vaticano).
    /// </summary>
    private async Task<HomilyScrapeResult?> TryScrapeArticleAsync(
        HttpClient httpClient,
        string relativeOrAbsoluteHref,
        string title,
        DateOnly homilyDate,
        string language,
        CancellationToken cancellationToken)
    {
        // Os hrefs no HTML do Vaticano começam com "/" (absolutos a partir da
        // raiz do domínio); removemos essa barra inicial para que o .NET
        // combine corretamente com o BaseAddress do HttpClient (que já
        // termina em "/"), do mesmo jeito que fazemos no helper da Cancão Nova.
        var path = relativeOrAbsoluteHref.TrimStart('/');

        var html = await FetchHtmlOrNullAsync(httpClient, path, cancellationToken);
        if (html is null)
        {
            return null;
        }

        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Seleciona o(s) bloco(s) de corpo de texto (classes "text" +
        // "vaticanrichtext"), EXCLUINDO o bloco de cabeçalho que também tem
        // classe "abstract".
        var bodyNodes = document.DocumentNode.SelectNodes(
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' text ') " +
            "and contains(@class,'vaticanrichtext') " +
            "and not(contains(@class,'abstract'))]");

        if (bodyNodes is null || bodyNodes.Count == 0)
        {
            return null;
        }

        var combinedText = string.Join(
            "\n\n",
            bodyNodes
                .Select(node => HtmlTextExtractor.ExtractParagraphs(node))
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        // Um corpo de texto "substancial" tem que ter um tamanho mínimo
        // razoável — páginas de tradução ainda não publicada às vezes
        // devolvem apenas uma frase padrão tipo "Tradução em breve" ou ficam
        // com o bloco de texto praticamente vazio.
        if (combinedText.Length < 100)
        {
            return null;
        }

        var absoluteUrl = new Uri(httpClient.BaseAddress!, path).ToString();

        return new HomilyScrapeResult(title, homilyDate, language, combinedText, absoluteUrl);
    }

    /// <summary>
    /// Busca o HTML de <paramref name="path"/>, devolvendo <c>null</c> (em vez
    /// de deixar a exceção propagar) em qualquer cenário de falha de rede —
    /// site fora do ar, DNS falhando, ou uma resposta que demore mais que o
    /// timeout configurado no HttpClient (ver <c>ConfigureDefaultHeaders</c>
    /// em <c>ServiceCollectionExtensions</c>).
    ///
    /// BUG REAL ENCONTRADO E CORRIGIDO: a versão original deste scraper só
    /// capturava <see cref="HttpRequestException"/> ao redor da busca do
    /// índice anual, e NÃO tinha nenhum tratamento de erro ao redor da busca
    /// de uma página de artigo específica. Isso foi descoberto testando o
    /// scraper contra duas homilias reais sem tradução oficial em português
    /// (exéquias de Cardeais) — uma delas expirou por timeout ao buscar o
    /// índice, e a exceção de timeout (<see cref="TaskCanceledException"/>,
    /// que o .NET lança tanto para timeout quanto para cancelamento
    /// explícito) NÃO é uma <see cref="HttpRequestException"/>, então
    /// escapava do catch e derrubava o processo inteiro sem nenhum log
    /// amigável. Centralizar a busca HTTP aqui, com tratamento único para os
    /// dois métodos que fazem requisição (índice e artigo), fecha essa
    /// lacuna nos dois lugares de uma vez.
    /// </summary>
    private async Task<string?> FetchHtmlOrNullAsync(HttpClient httpClient, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetStringAsync(path, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            // Cobre: DNS falhando, conexão recusada, status HTTP de erro
            // (404, 500 etc — GetStringAsync lança para qualquer status não-2xx).
            logger.LogInformation(ex, "Falha ao buscar '{Path}' em vatican.va.", path);
            return null;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Sem o "when" acima, esse catch também capturaria um cancelamento
            // EXPLÍCITO pedido por quem chamou este método (ex: shutdown do
            // Worker) — nesse caso queremos deixar a exceção propagar
            // normalmente, não tratá-la como "site indisponível". O "when"
            // garante que só timeouts DO PRÓPRIO HttpClient caem aqui.
            logger.LogInformation(ex, "Timeout ao buscar '{Path}' em vatican.va.", path);
            return null;
        }
    }
}
