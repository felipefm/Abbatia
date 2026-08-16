using System.Collections.Concurrent;
using System.Globalization;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain.Enums;

namespace Scriptorium.Infrastructure.Scrapers;

/// <summary>
/// Implementação CONCRETA da estratégia <see cref="ILiturgicalCalendarScraper"/>
/// para gcatholic.org — usada principalmente como fonte de VERIFICAÇÃO/
/// FALLBACK da cor litúrgica (a fonte primária é a página de liturgia da
/// Cancão Nova, que já inclui a cor diretamente) e como fonte do "rank" da
/// celebração do dia (Solenidade, Festa, Memória Obrigatória/Facultativa).
///
/// ESTRUTURA DO HTML (uma ÚNICA página cobre o ANO INTEIRO):
/// URL: /calendar/{ano}/General-{ciclo}-pt  (ciclo = A, B ou C — ver
/// <see cref="ComputeSundayCycle"/> abaixo)
///
/// Cada dia do ano é uma linha de tabela identificada por um id no formato
/// "MMDD" (ex: id="0815" = 15 de agosto):
///   &lt;tr id="0815"&gt;
///     &lt;td&gt;&lt;span class="zdate"&gt;15&lt;/span&gt;&lt;/td&gt;             -- dia do mês
///     &lt;td&gt;&lt;span class="zdate"&gt;Sábado&lt;/span&gt;&lt;/td&gt;         -- dia da semana
///     &lt;td&gt;&lt;a class="feast" title="Solenidade"&gt;S&lt;/a&gt;&lt;/td&gt; -- RANK (só existe se houver celebração especial)
///     &lt;td&gt;&lt;p class="indent"&gt;
///       &lt;span class="feastw"&gt;&lt;/span&gt;                             -- COR LITÚRGICA (letra final da classe: w/g/r/v/p/b)
///       &lt;span class="feast1"&gt;Assunção da Virgem Santa Maria&lt;/span&gt; -- nome da celebração
///     &lt;/p&gt;&lt;/td&gt;
///   &lt;/tr&gt;
///
/// Mapeamento de cor descoberto por inspeção (contando ocorrências de cada
/// classe no HTML real e cruzando com o contexto — ex: "Tempo Comum" sempre
/// aparece com "feastg", e "Solenidade"/"Assunção" sempre com "feastw"):
///   feastw = Branco, feastg = Verde, feastv = Roxo, feastr = Vermelho,
///   feastp = Rosa, feastb = Preto.
///
/// CACHE EM MEMÓRIA: como a página cobre o ano inteiro (~170KB) e o Worker
/// pede, na mesma execução, vários dias seguidos (até 7, potencialmente no
/// mesmo ano), fazemos o download e o parse UMA VEZ por ano e reutilizamos o
/// HtmlDocument já carregado para todas as datas daquele ano dentro do
/// mesmo ciclo de vida deste scraper (registrado como singleton — veja
/// ServiceCollectionExtensions). Isso evita 7 downloads idênticos de 170KB
/// só para variar o dia consultado.
/// </summary>
public class GCatholicCalendarScraper(
    IHttpClientFactory httpClientFactory,
    ILogger<GCatholicCalendarScraper> logger) : ILiturgicalCalendarScraper
{
    private const string HttpClientName = "GCatholic";

    // ConcurrentDictionary porque este scraper é registrado como Singleton
    // no container de DI (ver ServiceCollectionExtensions) e pode, em teoria,
    // ser chamado concorrentemente (o DevotionalBuilderService dispara os 4
    // scrapers em paralelo via Task.WhenAll para VÁRIOS dias, se o Worker for
    // ajustado no futuro para processar dias em paralelo também).
    private readonly ConcurrentDictionary<int, Task<HtmlDocument?>> _yearDocumentCache = new();

    public async Task<LiturgicalCalendarScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var document = await GetOrLoadYearDocumentAsync(date.Year, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var rowId = date.ToString("MMdd", CultureInfo.InvariantCulture);
        var row = document.DocumentNode.SelectSingleNode($"//tr[@id='{rowId}']");
        if (row is null)
        {
            logger.LogInformation("Nenhuma linha de calendário encontrada para {Data:yyyy-MM-dd} em gcatholic.org.", date);
            return null;
        }

        // Classes de cor são exatamente uma destas 6 (sem sufixo numérico) —
        // por isso comparamos com igualdade exata em vez de "contains", que
        // também bateria acidentalmente com as classes de tamanho de fonte
        // "feast1", "feast2", "feast3", "feast4" usadas para o NOME da celebração.
        var colorSpan = row.SelectSingleNode(
            ".//span[@class='feastw' or @class='feastg' or @class='feastv' " +
            "or @class='feastr' or @class='feastp' or @class='feastb']");
        var colorClass = colorSpan?.GetAttributeValue("class", string.Empty);
        var color = MapColor(colorClass);

        var nameNode = row.SelectSingleNode(".//p[contains(@class,'indent')]");
        var celebrationName = nameNode is null
            ? $"Feria do dia {date:dd/MM/yyyy}"
            : HtmlTextExtractor.CleanText(nameNode.InnerText);

        // O rank só existe como um <a class="feast" title="..."> quando há
        // uma celebração especial; em dias comuns de Tempo Comum essa célula
        // fica vazia — title é null nesse caso, o que é o comportamento correto.
        var rankNode = row.SelectSingleNode(".//a[@class='feast']");
        var rank = rankNode?.GetAttributeValue("title", string.Empty);

        return new LiturgicalCalendarScrapeResult(color, celebrationName, rank);
    }

    private Task<HtmlDocument?> GetOrLoadYearDocumentAsync(int year, CancellationToken cancellationToken)
    {
        // GetOrAdd com uma factory que retorna Task garante que, mesmo se
        // duas chamadas concorrentes pedirem o mesmo ano ao mesmo tempo,
        // apenas UM download real acontece — a segunda chamada simplesmente
        // aguarda a mesma Task já em andamento (padrão comum de
        // "single-flight" para caches assíncronos em .NET).
        return _yearDocumentCache.GetOrAdd(year, y => LoadYearDocumentAsync(y, cancellationToken));
    }

    private async Task<HtmlDocument?> LoadYearDocumentAsync(int year, CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var cycle = ComputeSundayCycle(year);
        var url = $"calendar/{year}/General-{cycle}-pt";

        try
        {
            var html = await httpClient.GetStringAsync(url, cancellationToken);
            var document = new HtmlDocument();
            document.LoadHtml(html);
            return document;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Falha ao baixar calendário litúrgico de {Ano} (ciclo {Ciclo}) em gcatholic.org.", year, cycle);
            return null;
        }
    }

    private static LiturgicalColor MapColor(string? cssClass) => cssClass switch
    {
        "feastw" => LiturgicalColor.Branco,
        "feastg" => LiturgicalColor.Verde,
        "feastv" => LiturgicalColor.Roxo,
        "feastr" => LiturgicalColor.Vermelho,
        "feastp" => LiturgicalColor.Rosa,
        "feastb" => LiturgicalColor.Preto,
        _ => LiturgicalColor.Verde,
    };

    /// <summary>
    /// Calcula a letra do ciclo dominical (A, B ou C) usado nas leituras de
    /// domingo, que se repete a cada 3 anos.
    ///
    /// LIMITAÇÃO DOCUMENTADA DE PROPÓSITO: o ciclo litúrgico "oficialmente"
    /// muda no 1º Domingo do Advento (final de novembro), não no dia 1º de
    /// janeiro — ou seja, tecnicamente as últimas semanas de dezembro de um
    /// ano civil já pertencem ao ciclo do PRÓXIMO ano litúrgico. O próprio
    /// gcatholic.org, no entanto, publica uma página por ANO CIVIL rotulada
    /// com um único ciclo dominante (confirmamos isso inspecionando a URL
    /// real "General-A-pt" para o ano de 2026). Esta função replica esse
    /// mesmo comportamento do site (uma aproximação por ano civil, não por
    /// ano litúrgico), o que é suficiente para o nosso caso de uso: cruzar/
    /// validar a cor litúrgica do dia, não para uso litúrgico oficial preciso.
    /// </summary>
    private static char ComputeSundayCycle(int year)
    {
        // Ponto de referência verificado manualmente: ano civil de 2026 = Ciclo A.
        // O ciclo se repete a cada 3 anos civis.
        const int referenceYear = 2026;
        const char referenceCycle = 'A';

        var offset = ((year - referenceYear) % 3 + 3) % 3; // módulo sempre positivo
        var cycles = new[] { 'A', 'B', 'C' };
        var referenceIndex = Array.IndexOf(cycles, referenceCycle);
        return cycles[(referenceIndex + offset) % 3];
    }
}
