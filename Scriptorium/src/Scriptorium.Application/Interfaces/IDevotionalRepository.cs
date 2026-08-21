using Scriptorium.Domain.Entities;

namespace Scriptorium.Application.Interfaces;

/// <summary>
/// Abstração de persistência para <see cref="DailyDevotional"/> (o padrão
/// Repository).
///
/// PORQUÊ NÃO USAR O DbContext DO EF CORE DIRETO NA API/WORKER?
/// Poderíamos injetar o ScriptoriumDbContext diretamente nos endpoints e no
/// Worker — o EF Core já implementa boa parte do padrão Repository/Unit of
/// Work internamente (DbSet&lt;T&gt; = Repository, SaveChangesAsync = Unit
/// of Work). MAS, ao introduzir esta interface na camada Application:
///
/// 1) A camada Application (onde vive a lógica de "o que fazer com os
///    dados") NÃO PRECISA REFERENCIAR o pacote Entity Framework Core.
///    Só a camada Infrastructure (que implementa esta interface) conhece
///    EF Core, SQLite, LINQ-to-Entities etc. Isso é a "Regra da Dependência"
///    da Clean Architecture: as camadas internas (Domain, Application) nunca
///    dependem de detalhes técnicos externos (banco, HTTP, etc) — é sempre
///    o contrário.
/// 2) Fica trivial trocar SQLite por outro banco no futuro (PostgreSQL, por
///    exemplo) sem tocar em nenhuma linha da Application ou da API: basta
///    reimplementar esta interface.
/// 3) Testes automatizados da lógica do Worker podem usar uma implementação
///    "fake" em memória desta interface, sem precisar de um banco SQLite
///    real rodando.
/// </summary>
public interface IDevotionalRepository
{
    /// <summary>
    /// Busca o devocional de uma data específica, já com Readings, Saint e
    /// Homily carregados (eager loading) — é exatamente o que o endpoint
    /// GET /api/devotional/{date} precisa devolver.
    /// </summary>
    Task<DailyDevotional?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>
    /// Insere um novo devocional OU atualiza um já existente para a mesma
    /// data ("upsert" = update + insert). Usado pelo Worker, que roda
    /// diariamente e pode re-processar um dia que já tinha sido salvo antes
    /// (ex: para tentar de novo uma tradução pendente).
    /// </summary>
    Task UpsertAsync(DailyDevotional devotional, CancellationToken cancellationToken);

    /// <summary>
    /// Busca todos os devocionais já persistidos cuja Date caia entre
    /// <paramref name="startInclusive"/> (incluso) e <paramref name="endExclusive"/>
    /// (excluso) — usado pelo calendário mensal. Não carrega Readings/Saint/
    /// Homily/OtherSaints (não são necessários para colorir o calendário),
    /// pra manter a consulta leve.
    /// </summary>
    Task<List<DailyDevotional>> GetByMonthRangeAsync(DateOnly startInclusive, DateOnly endExclusive, CancellationToken cancellationToken);

    /// <summary>
    /// Lista todas as homilias cujo texto ainda não foi traduzido com
    /// sucesso (Status == Pendente ou FalhouTentativa). O Worker usa isso
    /// para tentar traduzir de novo em cada execução, sem depender de ter
    /// sido ele mesmo quem inseriu o registro na mesma rodada.
    /// </summary>
    Task<List<Homily>> GetHomiliesPendingTranslationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Persiste alterações feitas diretamente em entidades já rastreadas
    /// pelo contexto (ex: depois de atualizar o Status de uma Homily
    /// retornada por <see cref="GetHomiliesPendingTranslationAsync"/>).
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
