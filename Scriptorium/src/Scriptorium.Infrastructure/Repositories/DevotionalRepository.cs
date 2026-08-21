using Microsoft.EntityFrameworkCore;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain.Entities;
using Scriptorium.Domain.Enums;
using Scriptorium.Infrastructure.Data;

namespace Scriptorium.Infrastructure.Repositories;

/// <summary>
/// Implementação concreta de <see cref="IDevotionalRepository"/> usando o
/// Entity Framework Core sobre SQLite. Esta é a ÚNICA classe do projeto que
/// deveria "saber" como o EF Core funciona por dentro (LINQ-to-Entities,
/// Include/ThenInclude, rastreamento de mudanças) — todo o resto do sistema
/// (Worker, API) só conhece a interface <see cref="IDevotionalRepository"/>.
/// </summary>
public class DevotionalRepository(ScriptoriumDbContext dbContext) : IDevotionalRepository
{
    public async Task<DailyDevotional?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        // .Include() e .ThenInclude() instruem o EF Core a fazer o JOIN
        // necessário para trazer as entidades relacionadas junto na mesma
        // consulta (eager loading). Sem isso, Readings/Saint/Homily
        // viriam como coleções/referências vazias mesmo que existam dados
        // no banco — o EF Core NÃO carrega relacionamentos automaticamente
        // por padrão, para evitar consultas pesadas desnecessárias
        // (você precisa pedir explicitamente o que quer carregar).
        return await dbContext.DailyDevotionals
            .Include(d => d.Readings)
            .Include(d => d.Saint)
            .Include(d => d.Homily)
            .Include(d => d.OtherSaints)
            .AsNoTracking() // Leitura pura: não precisamos rastrear mudanças, o que melhora performance.
            .FirstOrDefaultAsync(d => d.Date == date, cancellationToken);
    }

    public async Task<List<DailyDevotional>> GetByMonthRangeAsync(DateOnly startInclusive, DateOnly endExclusive, CancellationToken cancellationToken)
    {
        // Sem nenhum .Include(): o calendário mensal só precisa de Date/
        // LiturgicalTitle/Color (campos escalares da tabela raiz) — carregar
        // Readings/Saint/Homily/OtherSaints aqui seria trabalho desperdiçado.
        return await dbContext.DailyDevotionals
            .Where(d => d.Date >= startInclusive && d.Date < endExclusive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(DailyDevotional devotional, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DailyDevotionals
            .Include(d => d.Readings)
            .Include(d => d.Saint)
            .Include(d => d.Homily)
            .Include(d => d.OtherSaints)
            .FirstOrDefaultAsync(d => d.Date == devotional.Date, cancellationToken);

        if (existing is null)
        {
            // Caminho "Insert": simplesmente adiciona o grafo de objetos
            // inteiro (DailyDevotional + Readings + Saint + Homily) — o EF
            // Core detecta automaticamente que são entidades novas (Id == 0)
            // e vai inserir todas elas, já resolvendo as chaves estrangeiras.
            await dbContext.DailyDevotionals.AddAsync(devotional, cancellationToken);
        }
        else
        {
            // Caminho "Update": o Worker roda diariamente e pode reprocessar
            // um dia que já existe no banco (por exemplo, para tentar de
            // novo uma tradução que falhou). Em vez de deixar duplicar
            // registros, substituímos os dados "filhos" (Readings, Saint)
            // e atualizamos os campos escalares do próprio devocional.
            existing.LiturgicalTitle = devotional.LiturgicalTitle;
            existing.Color = devotional.Color;
            existing.UpdatedAtUtc = DateTime.UtcNow;

            // Remove as leituras antigas e insere as novas — mais simples e
            // seguro do que tentar "casar" item por item (leituras não têm
            // uma chave de negócio estável para comparação, ex: o mesmo
            // ReadingType pode raramente mudar de referência entre raspagens).
            dbContext.Readings.RemoveRange(existing.Readings);
            existing.Readings = devotional.Readings;

            if (devotional.Saint is not null)
            {
                if (existing.Saint is not null)
                {
                    existing.Saint.Name = devotional.Saint.Name;
                    existing.Saint.Biography = devotional.Saint.Biography;
                    existing.Saint.ImageUrl = devotional.Saint.ImageUrl;
                    existing.Saint.SourceUrl = devotional.Saint.SourceUrl;
                }
                else
                {
                    existing.Saint = devotional.Saint;
                }
            }

            if (devotional.Homily is not null)
            {
                // Homilias são vinculadas por URL única (ver índice único em
                // OnModelCreating). Verificamos se já existe uma homilia com
                // essa URL para não violar a constraint e não duplicar o
                // trabalho de tradução já feito anteriormente.
                var homiliaExistente = await dbContext.Homilies
                    .FirstOrDefaultAsync(h => h.SourceUrl == devotional.Homily.SourceUrl, cancellationToken);

                if (homiliaExistente is not null)
                {
                    homiliaExistente.DailyDevotionalId = existing.Id;
                }
                else
                {
                    devotional.Homily.DailyDevotionalId = existing.Id;
                    await dbContext.Homilies.AddAsync(devotional.Homily, cancellationToken);
                }
            }

            // Só sobrescreve "outros santos" quando a nova raspagem trouxe
            // algo (Count > 0). DevotionalBuilderService só popula essa
            // lista num scrape bem-sucedido — deixá-la vazia por default
            // numa falha pontual do Vatican News nunca deveria apagar dados
            // já salvos de uma raspagem anterior que tinha funcionado.
            if (devotional.OtherSaints.Count > 0)
            {
                dbContext.OtherSaints.RemoveRange(existing.OtherSaints);
                existing.OtherSaints = devotional.OtherSaints;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Homily>> GetHomiliesPendingTranslationAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Homilies
            .Where(h => h.Status == TranslationStatus.Pendente || h.Status == TranslationStatus.FalhouTentativa)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
