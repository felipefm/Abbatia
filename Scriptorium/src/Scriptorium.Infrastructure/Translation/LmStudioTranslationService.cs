using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Scriptorium.Application.DTOs;
using Scriptorium.Application.Interfaces;

namespace Scriptorium.Infrastructure.Translation;

/// <summary>
/// Implementação de <see cref="ITranslationService"/> que fala com um
/// servidor LM Studio rodando localmente na rede da homelab.
///
/// COMO FUNCIONA O LM STUDIO POR BAIXO DOS PANOS:
/// O LM Studio expõe um servidor HTTP local que imita a API REST da OpenAI
/// (o mesmo formato usado por ChatGPT via API). Isso significa que, para
/// "conversar" com o modelo de IA rodando localmente, fazemos exatamente a
/// mesma requisição HTTP que faríamos para a OpenAI de verdade — só
/// mudando a URL base (do endereço da OpenAI para o IP da máquina local).
/// Essa compatibilidade é o que nos permite escrever este cliente com
/// HttpClient puro, sem depender de nenhum SDK proprietário.
///
/// PORQUÊ NÃO USAR UM SDK OFICIAL DA OPENAI?
/// Poderíamos, mas para um único endpoint simples (chat/completions) isso
/// adicionaria uma dependência externa pesada só para economizar ~40 linhas
/// de código. Escrever o HttpClient manualmente também é OTIMO para fins
/// pedagógicos: você vê exatamente o que está sendo enviado/recebido pela
/// rede, sem "mágica" escondida por trás de um SDK.
/// </summary>
public class LmStudioTranslationService(
    HttpClient httpClient,
    ILogger<LmStudioTranslationService> logger) : ITranslationService
{
    // Opções de serialização JSON compartilhadas: usamos
    // camelCase (padrão JSON/JavaScript) porque é o que a API estilo OpenAI
    // espera nos nomes dos campos (ex: "max_tokens" já vem com esse nome
    // exato via [JsonPropertyName], mas os demais seguem convenção camelCase).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<TranslationAttemptResult> TranslateAsync(
        string text,
        string sourceLanguage,
        CancellationToken cancellationToken)
    {
        // Prompt de sistema instruindo o modelo a se comportar como um
        // tradutor puro — sem comentários extras, sem "Claro, aqui está a
        // tradução:", apenas o texto traduzido. Isso é crucial porque
        // vamos usar a resposta DIRETO como o texto final salvo no banco.
        var systemPrompt =
            "Você é um tradutor profissional especializado em textos católicos e religiosos. " +
            $"Traduza o texto do usuário do idioma '{sourceLanguage}' para português do Brasil (PT-BR), " +
            "mantendo um tom solene e respeitoso, preservando nomes próprios, citações bíblicas e a " +
            "formatação de parágrafos original. Responda APENAS com o texto traduzido, sem introduções, " +
            "explicações ou comentários adicionais.";

        var requestBody = new LmStudioChatRequest
        {
            Model = "local-model", // A maioria dos servidores LM Studio ignora este campo e usa o modelo já carregado na UI.
            Messages =
            [
                new LmStudioChatMessage { Role = "system", Content = systemPrompt },
                new LmStudioChatMessage { Role = "user", Content = text },
            ],
            Temperature = 0.2, // Baixa temperatura = tradução mais literal/consistente, menos "criativa".
        };

        try
        {
            // O endereço base (http://IP_DO_LM_STUDIO:1234) já foi
            // configurado no HttpClient via DI (veja
            // ServiceCollectionExtensions.cs) — aqui só completamos o path
            // do endpoint específico da API compatível com OpenAI.
            using var response = await httpClient.PostAsJsonAsync(
                "/v1/chat/completions",
                requestBody,
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "LM Studio respondeu com status {StatusCode}: {Body}",
                    response.StatusCode,
                    errorBody);
                return TranslationAttemptResult.Fail($"LM Studio retornou status HTTP {(int)response.StatusCode}.");
            }

            var parsed = await response.Content.ReadFromJsonAsync<LmStudioChatResponse>(JsonOptions, cancellationToken);
            var translatedText = parsed?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                return TranslationAttemptResult.Fail("LM Studio retornou uma resposta vazia.");
            }

            return TranslationAttemptResult.Ok(translatedText.Trim());
        }
        catch (HttpRequestException ex)
        {
            // ESTE É O CENÁRIO CENTRAL DE RESILIÊNCIA DO PROJETO: a máquina
            // do LM Studio pode estar DESLIGADA. Quando isso acontece, a
            // tentativa de conexão TCP falha e o HttpClient lança
            // HttpRequestException (ex: "Connection refused" ou "No route
            // to host"). Em vez de deixar essa exceção estourar e derrubar
            // o Worker inteiro, nós a capturamos aqui, registramos um aviso
            // (não um erro fatal — é um evento ESPERADO neste ambiente) e
            // devolvemos um resultado de falha estruturado.
            logger.LogWarning(
                ex,
                "Não foi possível conectar ao LM Studio em '{BaseAddress}'. A máquina pode estar desligada.",
                httpClient.BaseAddress);
            return TranslationAttemptResult.Fail("Não foi possível conectar ao servidor LM Studio (máquina pode estar desligada).");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // TaskCanceledException sem o CancellationToken do CHAMADOR ter
            // sido cancelado significa que foi o PRÓPRIO HttpClient quem
            // cancelou por timeout (configurado em LmStudioOptions.TimeoutSeconds).
            logger.LogWarning(ex, "Timeout ao aguardar resposta do LM Studio.");
            return TranslationAttemptResult.Fail("Tempo limite excedido ao aguardar resposta do LM Studio.");
        }
        catch (Exception ex)
        {
            // Rede final de segurança: qualquer outra falha inesperada
            // (JSON malformado, etc.) também vira um resultado de falha
            // estruturado, nunca uma exceção não tratada subindo até o
            // BackgroundService e derrubando o processo inteiro do Worker.
            logger.LogError(ex, "Erro inesperado ao traduzir texto via LM Studio.");
            return TranslationAttemptResult.Fail($"Erro inesperado: {ex.Message}");
        }
    }

    // -----------------------------------------------------------------
    // Modelos privados que espelham o formato JSON da API estilo OpenAI
    // usada pelo LM Studio. Ficam privados/internos a este arquivo porque
    // são um DETALHE DE IMPLEMENTAÇÃO deste cliente HTTP específico — nada
    // fora desta classe precisa conhecer o formato exato do payload JSON.
    // -----------------------------------------------------------------

    private sealed class LmStudioChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("messages")]
        public required List<LmStudioChatMessage> Messages { get; init; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }
    }

    private sealed class LmStudioChatMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; init; }

        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    private sealed class LmStudioChatResponse
    {
        [JsonPropertyName("choices")]
        public List<LmStudioChatChoice>? Choices { get; init; }
    }

    private sealed class LmStudioChatChoice
    {
        [JsonPropertyName("message")]
        public LmStudioChatMessage? Message { get; init; }
    }
}
