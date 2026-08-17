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
        return ExtractFromContainer(container, includeListItems: false, xpathsToRemove);
    }

    /// <summary>
    /// Igual a <see cref="ExtractParagraphs"/>, mas TAMBÉM inclui o texto de
    /// itens de lista (&lt;li&gt;) — não só &lt;p&gt;. Necessário porque
    /// algumas seções de conteúdo (ex: "Outros santos e beatos celebrados
    /// em [data]" e "Fontes" na página de cada santo do
    /// santo.cancaonova.com) usam &lt;ul&gt;/&lt;li&gt; em vez de parágrafos
    /// — <see cref="ExtractParagraphs"/> as ignorava silenciosamente (nenhum
    /// erro, o texto simplesmente nunca aparecia no resultado), um bug real
    /// reportado pelo usuário (ver Bug #8 em
    /// docs/04-inteligencia-de-codigo.md). Cada item de lista é prefixado
    /// com "• " no texto final para deixar claro, mesmo em texto puro sem
    /// HTML, que aquele trecho é um item de uma lista, não um parágrafo
    /// corrido.
    ///
    /// Não usamos isso como comportamento PADRÃO de <see cref="ExtractParagraphs"/>
    /// (em vez de um método novo) de propósito: os outros dois usos deste
    /// utilitário (leituras litúrgicas e homilias do Papa) nunca precisaram
    /// de listas, e mudar o comportamento de um método já testado e em
    /// produção só para o caso do santo do dia arriscaria introduzir ruído
    /// inesperado (ex: menus/links relacionados que também usam
    /// &lt;li&gt;) nesses outros dois scrapers sem necessidade.
    /// </summary>
    public static string ExtractParagraphsAndListItems(HtmlNode? container, params string[] xpathsToRemove)
    {
        return ExtractFromContainer(container, includeListItems: true, xpathsToRemove);
    }

    private static string ExtractFromContainer(HtmlNode? container, bool includeListItems, string[] xpathsToRemove)
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

        // A união XPath ".//p | .//li" preserva a ORDEM DO DOCUMENTO (não a
        // ordem de cada sub-busca separadamente) — é assim que garantimos
        // que, por exemplo, o cabeçalho "Outros santos..." (um <p>) apareça
        // ANTES da lista de nomes que o segue (vários <li>), na mesma ordem
        // em que aparecem na página original.
        var nodes = container.SelectNodes(includeListItems ? ".//p | .//li" : ".//p");
        if (nodes is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            var text = CleanText(node.InnerText);

            // Ignora parágrafos vazios e "separadores decorativos" (comuns em
            // páginas do Vaticano, ex: uma linha de puros underscores usada
            // como filete visual entre o cabeçalho e o corpo do texto).
            if (string.IsNullOrWhiteSpace(text) || IsDecorativeSeparator(text))
            {
                continue;
            }

            if (node.Name == "li")
            {
                text = "• " + text;
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
