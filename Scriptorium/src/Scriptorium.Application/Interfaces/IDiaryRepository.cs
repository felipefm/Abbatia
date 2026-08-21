using Scriptorium.Domain.Entities;

namespace Scriptorium.Application.Interfaces;

/// <summary>
/// Abstração de persistência para o diário espiritual pessoal — uma entrada
/// de texto por data. App de usuário único, sem autenticação: não há
/// distinção "de quem" é a entrada.
/// </summary>
public interface IDiaryRepository
{
    Task<DiaryEntry?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Cria a entrada da data se não existir, ou substitui o texto se já existir.</summary>
    Task<DiaryEntry> UpsertAsync(DateOnly date, string text, CancellationToken cancellationToken);

    /// <summary>Remove a entrada da data, se existir. Devolve false se não havia nada pra remover.</summary>
    Task<bool> DeleteAsync(DateOnly date, CancellationToken cancellationToken);
}
