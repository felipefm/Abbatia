using Scriptorium.Application.DTOs;

namespace Scriptorium.Application.Interfaces;

/// <summary>
/// Abstração sobre "algo que traduz texto de um idioma para português".
///
/// PORQUÊ ISOLAR ISSO ATRÁS DE UMA INTERFACE?
/// A implementação concreta (Scriptorium.Infrastructure.Translation.LmStudioTranslationService)
/// fala HTTP com um servidor LM Studio local, usando o formato de API
/// compatível com OpenAI (POST /v1/chat/completions). Essa é uma decisão de
/// INFRAESTRUTURA, não deveria "vazar" para quem consome o serviço
/// (o Worker). Se no futuro você trocar o LM Studio por outra solução
/// (Ollama, um serviço de tradução na nuvem, etc.), o Worker não precisa
/// mudar UMA linha sequer — só a implementação desta interface muda.
/// Esse é exatamente o mesmo raciocínio do padrão Strategy usado nos
/// scrapers (veja IScrapers.cs), aplicado agora à tradução.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Tenta traduzir <paramref name="text"/> (escrito no idioma indicado por
    /// <paramref name="sourceLanguage"/>, ex: "en", "it") para português do Brasil.
    ///
    /// IMPORTANTE: este método NUNCA deve lançar exceção em caso de falha de
    /// rede/timeout/IA indisponível — a máquina do LM Studio pode estar
    /// desligada, e isso é um cenário ESPERADO no ambiente de homelab desse
    /// projeto. Falhas devem ser reportadas via
    /// <see cref="TranslationAttemptResult.Success"/> = false, permitindo que
    /// quem chamou decida o que fazer (tipicamente: marcar o registro como
    /// "Pendente" e tentar de novo no próximo ciclo do Worker).
    /// </summary>
    Task<TranslationAttemptResult> TranslateAsync(
        string text,
        string sourceLanguage,
        CancellationToken cancellationToken);
}
