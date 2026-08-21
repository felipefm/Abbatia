using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scriptorium.Application.Services;

/// <summary>
/// Compara nomes de santo vindos de DUAS fontes independentes (CancaoNova e
/// Vatican News), que grafam o mesmo santo de formas diferentes ("São Pio X"
/// vs. "S. Pio X", com/sem acento, com/sem título completo). Usado para
/// excluir da lista de "outros santos" quem já é o santo principal do dia.
///
/// PURO E SEM I/O DE PROPÓSITO: não depende de HTTP, banco ou qualquer outra
/// coisa externa — só string in, bool/string out. Isso o torna trivial de
/// testar isoladamente.
/// </summary>
public static class SaintNameMatcher
{
    // Já em forma SEM ACENTO de propósito: são comparados contra o texto
    // DEPOIS da remoção de diacríticos em Normalize (abaixo), então "são "
    // aqui precisa ser "sao " — um prefixo acentuado nunca bateria com o
    // texto já normalizado.
    private static readonly string[] Prefixes =
        ["sao ", "santo ", "santa ", "s. ", "sta. ", "sto. ", "beato ", "beata ", "bem-aventurado ", "bem-aventurada "];

    private static readonly Regex WhitespaceCollapser = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Normaliza um nome para comparação: minúsculas, sem acento, sem
    /// títulos honoríficos comuns no início ("São ", "S. ", "Beato "...).
    /// </summary>
    public static string Normalize(string name)
    {
        var decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var stripped = new string(decomposed
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        stripped = WhitespaceCollapser.Replace(stripped, " ").Trim();

        bool removedAny;
        do
        {
            removedAny = false;
            foreach (var prefix in Prefixes)
            {
                if (stripped.StartsWith(prefix, StringComparison.Ordinal))
                {
                    stripped = stripped[prefix.Length..];
                    removedAny = true;
                }
            }
        } while (removedAny);

        return stripped.Trim();
    }

    /// <summary>
    /// Compara duas grafias de nome de santo de fontes independentes,
    /// tolerando prefixos/abreviações diferentes E o "sobrenome"/epíteto que
    /// cada fonte pendura depois de uma vírgula (ex: CancaoNova diz "Pio X,
    /// o Papa camponês", Vatican News diz "Pio X, papa" — bater as duas
    /// strings inteiras por conteção falharia aqui). Por isso comparamos
    /// primeiro o "nome núcleo" (tudo antes da primeira vírgula) e só caímos
    /// para a string completa se não houver vírgula em nenhum dos dois lados.
    /// </summary>
    public static bool IsLikelySamePerson(string nameA, string nameB)
    {
        var a = Normalize(nameA);
        var b = Normalize(nameB);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        var coreA = CoreName(a);
        var coreB = CoreName(b);
        if (coreA.Length > 0 && coreB.Length > 0 && Contains(coreA, coreB))
        {
            return true;
        }

        return Contains(a, b);
    }

    private static string CoreName(string normalized)
    {
        var commaIndex = normalized.IndexOf(',');
        return (commaIndex < 0 ? normalized : normalized[..commaIndex]).Trim();
    }

    private static bool Contains(string a, string b) =>
        a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);
}
