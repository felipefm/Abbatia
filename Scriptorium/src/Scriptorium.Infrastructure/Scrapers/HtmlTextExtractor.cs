using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Pequeno utilitário compartilhado por todos os scrapers para converter um
/// nó HTML (que pode conter parágrafos, negritos, links, imagens, scripts de
/// compartilhamento social etc.) em texto corrido limpo, pronto para ser
/// salvo no banco e exibido no app Oratorium.
///
/// PORQUÊ CENTRALIZAR ISSO EM VEZ DE REPETIR EM CADA SCRAPER?
/// Os 3 sites de conteúdo textual (santo.cancaonova.com, liturgia.cancaonova.com
/// e vatican.va) têm a MESMA necessidade: pegar uma "div de conteúdo" cheia de
/// ruído (botões de compartilhar, iframes de áudio, imagens) e extrair só o
/// texto legível dos parágrafos. Repetir essa lógica em 3 lugares violaria o
/// princípio DRY (Don't Repeat Yourself) — se um dia percebermos um bug na
/// limpeza de texto, corrigimos em UM lugar só.
/// </summary>
internal static class HtmlTextExtractor
{
    // Colapsa qualquer sequência de espaços em branco (incluindo quebras de
    // linha vindas do HTML original, que não têm significado visual nenhum
    // em HTML) numa única barra de espaço — evita salvar texto com
    // espaçamento estranho no banco.
    private static readonly Regex WhitespaceCollapser = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Extrai o texto de todos os parágrafos (&lt;p&gt;) dentro de
    /// <paramref name="container"/>, removendo antes quaisquer nós
    /// "ruidosos" indicados em <paramref name="xpathsToRemove"/> (ex: a lista
    /// de botões de compartilhamento social, que também usa parágrafos/listas
    /// e poluiria o resultado se não fosse removida primeiro).
    /// </summary>
    public static string ExtractParagraphs(HtmlNode? container, params string[] xpathsToRemove)
    {
        if (container is null)
        {
            return string.Empty;
        }

        foreach (var xpath in xpathsToRemove)
        {
            var nodesToRemove = container.SelectNodes(xpath);
            if (nodesToRemove is null)
            {
                continue;
            }

            // Removemos de trás para frente por segurança, embora aqui não
            // seja estritamente necessário (SelectNodes já materializa a
            // lista antes de começarmos a remover).
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        var paragraphs = container.SelectNodes(".//p");
        if (paragraphs is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var text = CleanText(paragraph.InnerText);

            // Ignora parágrafos vazios e "separadores decorativos" (comuns em
            // páginas do Vaticano, ex: uma linha de puros underscores usada
            // como filete visual entre o cabeçalho e o corpo do texto).
            if (string.IsNullOrWhiteSpace(text) || IsDecorativeSeparator(text))
            {
                continue;
            }

            builder.AppendLine(text);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Decodifica entidades HTML (ex: &amp;aacute; vira á) e normaliza
    /// espaços em branco de um trecho de texto extraído do HTML.
    /// </summary>
    public static string CleanText(string rawInnerText)
    {
        var decoded = HtmlEntity.DeEntitize(rawInnerText) ?? string.Empty;
        return WhitespaceCollapser.Replace(decoded, " ").Trim();
    }

    private static bool IsDecorativeSeparator(string text)
    {
        var trimmed = text.Trim('_', '-', ' ', ' ', '.');
        return trimmed.Length == 0;
    }
}
