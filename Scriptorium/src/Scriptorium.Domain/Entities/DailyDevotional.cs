using Scriptorium.Domain.Enums;

namespace Scriptorium.Domain.Entities;

/// <summary>
/// A ENTIDADE RAIZ (Aggregate Root, no vocabulário de Domain-Driven Design)
/// do domínio da Abbatia: representa "o devocional de um dia específico",
/// amarrando tudo o que o app Oratorium precisa para montar a tela de
/// oração diária: leituras, santo do dia, cor litúrgica e (quando disponível)
/// a homilia do Papa.
///
/// PORQUÊ "AGGREGATE ROOT"? Em vez de o app cliente (Oratorium) precisar
/// fazer 4 chamadas separadas (uma para leituras, uma para o santo, uma
/// para a cor litúrgica, uma para a homilia), a API expõe UM único endpoint
/// que devolve este objeto já com tudo carregado (Readings, Saint, Homily).
/// Isso simplifica MUITO o consumo pelo frontend e é uma prática comum em
/// APIs REST bem desenhadas: modelar em torno de "casos de uso" (a tela do
/// devocional) e não apenas espelhar 1-para-1 as tabelas do banco.
/// </summary>
public class DailyDevotional
{
    public int Id { get; set; }

    /// <summary>
    /// A data à qual este devocional se refere. Usamos <see cref="DateOnly"/>
    /// (introduzido no .NET 6) em vez de <see cref="DateTime"/> porque um
    /// devocional é sempre sobre um DIA inteiro, nunca um instante
    /// específico — usar DateOnly deixa essa intenção explícita no tipo e
    /// evita bugs sutis de fuso horário/hora que DateTime carregaria sem
    /// necessidade nenhuma.
    /// </summary>
    public required DateOnly Date { get; set; }

    /// <summary>
    /// Título do dia litúrgico, ex: "Assunção da Bem-aventurada Virgem Maria
    /// | Solenidade | Domingo", extraído da página de liturgia diária.
    /// </summary>
    public required string LiturgicalTitle { get; set; }

    /// <summary>
    /// Cor litúrgica do dia (Verde, Branco, Vermelho, Roxo, Rosa). Ver
    /// <see cref="Enums.LiturgicalColor"/> para o porquê de ser um enum.
    /// </summary>
    public required LiturgicalColor Color { get; set; }

    /// <summary>
    /// Coleção de leituras do dia (1ª Leitura, Salmo, [2ª Leitura], Evangelho).
    /// Inicializada como lista vazia (nunca null) para que o código
    /// consumidor (API, testes) não precise ficar checando null antes de
    /// iterar — um pequeno detalhe que evita uma classe inteira de bugs de
    /// NullReferenceException. Esse é o padrão "Null Object" aplicado a
    /// coleções.
    /// </summary>
    public List<Reading> Readings { get; set; } = [];

    /// <summary>
    /// O santo celebrado neste dia (relação 1-para-1). Nullable porque, em
    /// teoria, o scraper pode falhar em capturar o santo mesmo que as
    /// leituras tenham sido salvas com sucesso — cada scraper roda
    /// independentemente (padrão Strategy) e falhas são isoladas.
    /// </summary>
    public SaintOfTheDay? Saint { get; set; }

    /// <summary>
    /// Homilia do Papa associada a este dia, se houver sido publicada e
    /// vinculada pelo Worker. A maioria dos dias NÃO terá homilia (o Papa
    /// não celebra missa pública todo dia), então null é o valor esperado
    /// na maior parte do tempo — não é um erro.
    /// </summary>
    public Homily? Homily { get; set; }

    /// <summary>
    /// Outros santos comemorados no mesmo dia, além de <see cref="Saint"/>
    /// (raspados do Vatican News). Normalmente vazio ou com 1-2 itens.
    /// </summary>
    public List<OtherSaintOfDay> OtherSaints { get; set; } = [];

    /// <summary>
    /// Timestamp (UTC) de quando este registro foi gravado/atualizado pela
    /// última vez pelo Worker. Útil para debug ("por que esse dado está
    /// desatualizado?") e para decidir se vale a pena re-raspar um dia que
    /// já está no banco há muito tempo.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
