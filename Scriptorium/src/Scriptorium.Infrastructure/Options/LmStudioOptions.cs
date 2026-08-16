namespace Scriptorium.Infrastructure.Options;

/// <summary>
/// Representa as configurações necessárias para conectar ao servidor LM
/// Studio rodando na rede local (homelab).
///
/// PORQUÊ UMA CLASSE DE OPTIONS (E NÃO SÓ LER VARIÁVEIS DE AMBIENTE DIRETO)?
/// O .NET tem um padrão chamado "Options Pattern": em vez de espalhar
/// chamadas a <c>Environment.GetEnvironmentVariable("LM_STUDIO_BASE_URL")</c>
/// por todo o código (o que é frágil — erro de digitação na string só
/// aparece em runtime), centralizamos a configuração numa classe fortemente
/// tipada. O ASP.NET Core então "liga" essa classe à seção correspondente
/// do appsettings.json / variáveis de ambiente automaticamente via
/// <c>builder.Services.Configure&lt;LmStudioOptions&gt;(...)</c>, e qualquer
/// classe pode receber essa configuração já validada e tipada via
/// injeção de dependência (<c>IOptions&lt;LmStudioOptions&gt;</c>).
///
/// ISSO ATENDE DIRETAMENTE UM REQUISITO CRÍTICO DO PROJETO: "o IP do LM
/// Studio deve ser configurável via variáveis de ambiente", porque no
/// ASP.NET Core, variáveis de ambiente automaticamente sobrescrevem valores
/// do appsettings.json quando seguem a convenção de nomes com "__" (dois
/// underscores) representando a hierarquia JSON. Por exemplo, a variável de
/// ambiente:
///   <c>LmStudio__BaseUrl=http://192.168.1.50:1234</c>
/// sobrescreve automaticamente o valor de "LmStudio:BaseUrl" no
/// appsettings.json — sem precisarmos escrever NENHUM código extra para
/// isso. É exatamente essa a variável que você vai configurar no
/// docker-compose.yml para apontar para o computador do LM Studio na sua
/// rede doméstica.
/// </summary>
public class LmStudioOptions
{
    /// <summary>
    /// Nome da seção correspondente no appsettings.json, usado na chamada
    /// de <c>builder.Services.Configure&lt;LmStudioOptions&gt;(configuration.GetSection(SectionName))</c>.
    /// </summary>
    public const string SectionName = "LmStudio";

    /// <summary>
    /// URL base do servidor LM Studio, ex: "http://192.168.1.50:1234".
    /// O LM Studio expõe uma API LOCAL compatível com o formato da OpenAI
    /// (endpoint POST {BaseUrl}/v1/chat/completions), o que nos permite usar
    /// HttpClient puro sem depender de nenhum SDK proprietário.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:1234";

    /// <summary>
    /// Nome/identificador do modelo carregado no LM Studio (ex:
    /// "llama-3.1-8b-instruct"). Alguns servidores LM Studio aceitam
    /// qualquer valor aqui (usam o modelo que já está carregado
    /// independente do que for pedido), mas é uma boa prática enviar sempre.
    /// </summary>
    public string ModelName { get; set; } = "local-model";

    /// <summary>
    /// Tempo máximo (em segundos) que esperamos por uma resposta antes de
    /// desistir e tratar como falha. Modelos locais rodando em CPU podem
    /// ser lentos — um timeout curto demais geraria falsos negativos
    /// (marcaria como "falhou" uma tradução que só estava demorando).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;
}
