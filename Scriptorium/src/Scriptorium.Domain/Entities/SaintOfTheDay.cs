namespace Scriptorium.Domain.Entities;

/// <summary>
/// Representa o(s) Santo(s) celebrado(s) num determinado dia do calendário
/// litúrgico, com uma pequena biografia/hagiografia.
///
/// RELACIONAMENTO: Cada <see cref="SaintOfTheDay"/> pertence a exatamente um
/// <see cref="DailyDevotional"/> (relação 1-para-1, já que a fonte
/// santo.cancaonova.com normalmente destaca um único santo principal por dia,
/// mesmo que o calendário litúrgico oficial às vezes liste vários).
/// </summary>
public class SaintOfTheDay
{
    /// <summary>
    /// Chave primária. Usamos <c>int</c> autoincremento (identity) em vez de
    /// Guid propositalmente: como o volume de dados é baixo (1 registro por
    /// dia) e o banco é SQLite local (não distribuído), não há necessidade
    /// da unicidade global que um Guid ofereceria — e int é mais leve e
    /// mais fácil de debugar (dá pra olhar "Id = 42" e entender na hora).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Chave estrangeira para o devocional do dia ao qual este santo pertence.
    /// Propriedade de navegação "sombra" explícita — facilita consultas
    /// diretas (ex: filtrar SaintOfTheDay por DevotionalId) sem precisar
    /// sempre carregar o objeto pai inteiro.
    /// </summary>
    public int DailyDevotionalId { get; set; }

    /// <summary>
    /// Propriedade de navegação do EF Core para o devocional "pai".
    /// É opcional (nullable) porque nem sempre precisamos carregá-la — o
    /// EF só a popula quando pedimos explicitamente via .Include(), evitando
    /// consultas pesadas desnecessárias (lazy vs. eager loading).
    /// </summary>
    public DailyDevotional? DailyDevotional { get; set; }

    /// <summary>Nome/título do santo, ex: "Santo Estêvão da Hungria: rei, diplomata e caridoso".</summary>
    public required string Name { get; set; }

    /// <summary>
    /// Biografia/hagiografia em texto corrido (HTML já foi removido pelo
    /// scraper, sobrando apenas o texto puro dos parágrafos).
    /// </summary>
    public required string Biography { get; set; }

    /// <summary>URL da imagem do santo, quando disponível na fonte original.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>URL da página de origem, útil para auditoria e para o usuário
    /// final que queira ler a matéria completa na fonte.</summary>
    public string? SourceUrl { get; set; }
}
