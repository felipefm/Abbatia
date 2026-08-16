using HtmlAgilityPack;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Lógica compartilhada entre <see cref="CancaoNovaSaintScraper"/> e
/// <see cref="CancaoNovaLiturgyScraper"/> — ambos os sites (santo.cancaonova.com
/// e liturgia.cancaonova.com) rodam o MESMO tema/plugin WordPress
/// ("cancaonova_calendar_widget"), então compartilham exatamente a mesma
/// forma de navegar por data.
///
/// COMO DESCOBRIMOS ISSO (a parte mais "detetive" do scraping real):
/// Inspecionando o HTML das páginas, percebemos que os links de "próximo
/// mês" / "mês anterior" do calendário nessas páginas são executados via
/// JavaScript (`javascript:void(0)`), ou seja, NÃO existe uma URL simples
/// tipo "?mes=9&ano=2026" que a gente possa simplesmente montar. Em vez
/// disso, o clique dispara uma chamada AJAX para o endpoint clássico do
/// WordPress `/wp-admin/admin-ajax.php`, enviando `action=widget-ajax`
/// junto com mês/ano/tipo desejados, e o servidor devolve de volta um
/// pedaço de HTML novo com a tabela do calendário daquele mês — inclusive
/// com o link direto (href) para o artigo de cada dia.
///
/// Encontramos esse comportamento lendo o arquivo JavaScript do plugin
/// (calendar.js) que a própria página carrega, e depois confirmamos
/// replicando a chamada POST manualmente. Essa é uma técnica valiosa de
/// engenharia reversa de scraping: quando um site usa AJAX para navegação,
/// muitas vezes é mais simples (e mais estável) chamar o mesmo endpoint
/// AJAX que o site usa internamente do que tentar simular cliques com um
/// navegador headless.
///
/// LIMITAÇÃO CONHECIDA (documentada de propósito): como essa API AJAX
/// devolve o calendário de UM MÊS por vez, e o link de cada dia é o
/// artigo/página completa daquele dia, este helper faz sempre 2 requisições
/// HTTP por data pedida (1 para achar o link do dia, 1 para ler o artigo).
/// Isso é aceitável no contexto deste projeto (o Worker roda 1x por dia,
/// buscando no máximo 7 dias), mas seria um gargalo se fosse escalado para
/// milhares de requisições — um bom exercício futuro seria cachear o
/// resultado do calendário do mês inteiro (como já fazemos em
/// <see cref="GCatholicCalendarScraper"/>) para evitar refazer essa
/// primeira chamada 7 vezes seguidas dentro da mesma janela de 30 dias.
/// </summary>
internal static class CancaoNovaCalendarHelper
{
    /// <summary>
    /// Consulta o widget de calendário AJAX do WordPress e devolve a URL
    /// completa do artigo correspondente ao dia pedido, ou <c>null</c> se o
    /// dia não constar na resposta (layout mudou, mês sem dados, etc).
    /// </summary>
    /// <param name="httpClient">
    /// HttpClient já configurado com o <c>BaseAddress</c> correto do site
    /// (ex: https://santo.cancaonova.com/ ou https://liturgia.cancaonova.com/pb/).
    /// </param>
    /// <param name="widgetType">
    /// Valor do campo "type" esperado pelo plugin: "santo" para o site de
    /// santos, "liturgia" para o site de liturgia diária.
    /// </param>
    public static async Task<string?> FindArticleUrlForDateAsync(
        HttpClient httpClient,
        string widgetType,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var formFields = new Dictionary<string, string>
        {
            ["action"] = "widget-ajax",
            ["sMes"] = date.Month.ToString(),
            ["sAno"] = date.Year.ToString(),
            ["title"] = string.Empty,
            ["type"] = widgetType,
            ["ajax"] = "true",
        };

        using var formContent = new FormUrlEncodedContent(formFields);

        // Caminho relativo: combinado com o BaseAddress do HttpClient
        // (que já termina em "/"), o .NET monta a URL absoluta correta
        // automaticamente (ex: BaseAddress "https://liturgia.cancaonova.com/pb/"
        // + "wp-admin/admin-ajax.php" = "https://liturgia.cancaonova.com/pb/wp-admin/admin-ajax.php").
        using var response = await httpClient.PostAsync("wp-admin/admin-ajax.php", formContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var calendarHtml = await response.Content.ReadAsStringAsync(cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(calendarHtml);

        // A tabela devolvida tem id="wp-calendar"; cada dia com conteúdo
        // publicado tem um <a href="...">{numero-do-dia}</a> dentro de uma
        // célula <td>. Dias sem link (raríssimo nestes sites) ficam de fora.
        var dayAnchors = document.DocumentNode.SelectNodes("//table[@id='wp-calendar']//a[@href]");
        if (dayAnchors is null)
        {
            return null;
        }

        var targetDayText = date.Day.ToString();
        foreach (var anchor in dayAnchors)
        {
            var anchorText = HtmlTextExtractor.CleanText(anchor.InnerText);
            if (anchorText == targetDayText)
            {
                return anchor.GetAttributeValue("href", string.Empty);
            }
        }

        return null;
    }
}
