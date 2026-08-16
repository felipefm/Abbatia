using Scriptorium.Application.DTOs;

namespace Scriptorium.Application.Interfaces;

/// <summary>
/// AQUI VIVE O PADRÃO STRATEGY do projeto.
///
/// O QUE É O PADRÃO STRATEGY, E POR QUE USÁ-LO AQUI?
/// Temos 4 fontes de dados completamente diferentes (santo.cancaonova.com,
/// liturgia.cancaonova.com, vatican.va, gcatholic.org), cada uma com sua
/// própria estrutura de HTML e sua própria forma de "navegar por data".
/// O padrão Strategy diz: defina uma INTERFACE comum para "o que" precisa
/// ser feito (ex: "buscar o santo de uma data") e deixe cada implementação
/// concreta decidir "como" fazer isso (ex: "fazer POST no admin-ajax.php
/// do WordPress, parsear a tabela de calendário, seguir o link, extrair
/// o H1...").
///
/// BENEFÍCIOS PRÁTICOS PARA VOCÊ (que está aprendendo C#):
/// 1) Se amanhã o site santo.cancaonova.com sair do ar e você quiser trocar
///    de fonte, você cria uma NOVA classe que implementa
///    <see cref="ISaintOfTheDayScraper"/> e troca só a linha de Injeção de
///    Dependência no Program.cs — nenhum outro código muda.
/// 2) Fica fácil escrever testes: você pode criar uma implementação "fake"
///    da interface que devolve dados fixos, sem precisar acessar a internet
///    de verdade durante os testes.
/// 3) O Worker (quem ORQUESTRA os scrapers) não precisa saber NADA sobre
///    HtmlAgilityPack, admin-ajax.php ou XPath — ele só conhece estas
///    interfaces. Isso é "Inversão de Dependência" (o "D" do princípio
///    SOLID): módulos de alto nível (Worker) não dependem de módulos de
///    baixo nível (scrapers concretos), ambos dependem de abstrações
///    (estas interfaces).
/// </summary>

/// <summary>Estratégia para raspar o Santo do Dia de uma data específica.</summary>
public interface ISaintOfTheDayScraper
{
    /// <summary>
    /// Busca o santo celebrado na <paramref name="date"/> informada.
    /// Retorna <c>null</c> (em vez de lançar exceção) quando o dado
    /// simplesmente não pôde ser encontrado/raspado — falha de scraping
    /// de UMA fonte não deve derrubar o processo inteiro do Worker.
    /// </summary>
    Task<SaintScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken);
}

/// <summary>Estratégia para raspar a liturgia diária (leituras + cor) de uma data.</summary>
public interface ILiturgyScraper
{
    Task<LiturgyScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken);
}

/// <summary>
/// Estratégia para raspar o calendário litúrgico anual do gcatholic.org,
/// usado como fonte de checagem/fallback para a cor litúrgica e para obter
/// o "rank" da celebração do dia (Solenidade, Festa, etc).
/// </summary>
public interface ILiturgicalCalendarScraper
{
    Task<LiturgicalCalendarScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken);
}

/// <summary>Estratégia para raspar homilias do Papa publicadas no site do Vaticano.</summary>
public interface IHomilyScraper
{
    /// <summary>
    /// Busca a homilia do Papa referente à <paramref name="date"/>, caso
    /// exista uma publicada para aquele dia exato (o Papa não celebra missa
    /// pública com homilia publicada todos os dias — retornar <c>null</c>
    /// é o caso mais comum, não um erro).
    /// </summary>
    Task<HomilyScrapeResult?> GetForDateAsync(DateOnly date, CancellationToken cancellationToken);
}
