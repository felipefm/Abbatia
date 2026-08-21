using Microsoft.EntityFrameworkCore;
using Scriptorium.Application.Interfaces;
using Scriptorium.Domain.Entities;
using Scriptorium.Infrastructure.Data;

namespace Scriptorium.Infrastructure.Repositories;

/// <summary>Implementação concreta de <see cref="IDiaryRepository"/> sobre EF Core/SQLite.</summary>
public class DiaryRepository(ScriptoriumDbContext dbContext) : IDiaryRepository
{
    public Task<DiaryEntry?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        dbContext.DiaryEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Date == date, cancellationToken);

    public async Task<DiaryEntry> UpsertAsync(DateOnly date, string text, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DiaryEntries.FirstOrDefaultAsync(e => e.Date == date, cancellationToken);

        if (existing is null)
        {
            existing = new DiaryEntry { Date = date, Text = text };
            await dbContext.DiaryEntries.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Text = text;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DiaryEntries.FirstOrDefaultAsync(e => e.Date == date, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        dbContext.DiaryEntries.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
