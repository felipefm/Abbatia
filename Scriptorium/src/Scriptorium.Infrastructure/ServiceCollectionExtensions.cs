using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scriptorium.Application.Interfaces;
using Scriptorium.Infrastructure.Data;
using Scriptorium.Infrastructure.Options;
using Scriptorium.Infrastructure.Repositories;
using Scriptorium.Infrastructure.Scrapers;
using Scriptorium.Infrastructure.Translation;

namespace Scriptorium.Infrastructure;

/// <summary>
/// Ponto ÚNICO de configuração da Injeção de Dependência (DI) para tudo que
/// vive na camada Infrastructure. Tanto a API quanto o Worker chamam este
/// mesmo método de extensão no respectivo Program.cs — evitando duplicar a
/// lista de registros de serviço em dois lugares diferentes (e correr o
/// risco de um dia os dois "Program.cs" ficarem dessincronizados).
///
/// O QUE É "INJEÇÃO DE DEPENDÊNCIA" (para quem está aprendendo)?
/// Em vez de cada classe criar (com `new`) as dependências de que precisa,
/// ela as RECEBE prontas via construtor (veja, por exemplo,
/// CancaoNovaSaintScraper, que recebe IHttpClientFactory e ILogger no
/// construtor em vez de criar um HttpClient com `new HttpClient()` dentro
/// dela mesma). Quem monta essas dependências e "encaixa as peças" é o
/// CONTAINER DE DI do ASP.NET Core (a coleção `IServiceCollection`
/// configurada aqui). Isso é o que permite, por exemplo, trocar
/// ISaintOfTheDayScraper por uma implementação diferente sem tocar em
/// nenhuma classe que o consome — só muda o registro abaixo.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScriptoriumInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---------------------------------------------------------------
        // BANCO DE DADOS (SQLite via EF Core)
        // ---------------------------------------------------------------
        // A connection string vem do appsettings.json (seção
        // ConnectionStrings:Default) e pode ser sobrescrita por variável de
        // ambiente (ConnectionStrings__Default) — útil para apontar o
        // caminho do arquivo .db para o volume Docker montado em produção
        // (veja docker-compose.yml na raiz do monorepo).
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=/data/scriptorium.db";

        services.AddDbContext<ScriptoriumDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IDevotionalRepository, DevotionalRepository>();

        // ---------------------------------------------------------------
        // OPTIONS PATTERN: configuração do LM Studio
        // ---------------------------------------------------------------
        // Vincula a seção "LmStudio" do appsettings.json (ou das variáveis
        // de ambiente LmStudio__BaseUrl, LmStudio__ModelName, etc, que o
        // ASP.NET Core mapeia automaticamente) à classe fortemente tipada
        // LmStudioOptions. Ver comentários detalhados em LmStudioOptions.cs.
        services.Configure<LmStudioOptions>(configuration.GetSection(LmStudioOptions.SectionName));

        // ---------------------------------------------------------------
        // HTTP CLIENTS NOMEADOS (IHttpClientFactory)
        // ---------------------------------------------------------------
        // PORQUÊ IHttpClientFactory EM VEZ DE `new HttpClient()`?
        // Instanciar HttpClient diretamente com `new` e não descartá-lo
        // corretamente é uma causa clássica de esgotamento de sockets em
        // aplicações .NET de longa duração (cada `new HttpClient()` abre uma
        // conexão TCP que pode ficar presa em TIME_WAIT). O
        // IHttpClientFactory resolve isso reciclando o "message handler"
        // (a conexão TCP de baixo nível) por trás dos panos, enquanto ainda
        // nos entrega uma instância "fresca" de HttpClient a cada
        // CreateClient(). Registrar clientes NOMEADOS (com AddHttpClient
        // "NomeDoCliente")  também nos permite configurar um BaseAddress e
        // headers padrão DIFERENTES para cada site de scraping, sem precisar
        // repetir essa configuração em cada scraper individualmente.
        services.AddHttpClient("SantoCancaoNova", client =>
        {
            client.BaseAddress = new Uri("https://santo.cancaonova.com/");
            ConfigureDefaultHeaders(client);
        });

        services.AddHttpClient("LiturgiaCancaoNova", client =>
        {
            client.BaseAddress = new Uri("https://liturgia.cancaonova.com/pb/");
            ConfigureDefaultHeaders(client);
        });

        services.AddHttpClient("VaticanVa", client =>
        {
            client.BaseAddress = new Uri("https://www.vatican.va/");
            ConfigureDefaultHeaders(client);
        });

        services.AddHttpClient("GCatholic", client =>
        {
            client.BaseAddress = new Uri("https://gcatholic.org/");
            ConfigureDefaultHeaders(client);
        });

        // Cliente do LM Studio: BaseAddress é lido DINAMICAMENTE das
        // Options (e não fixado aqui como os demais) exatamente porque este
        // é o endereço que PRECISA ser configurável via variável de
        // ambiente para apontar para a máquina certa da homelab. Usamos o
        // overload de AddHttpClient que recebe um IServiceProvider para
        // conseguir resolver IOptions<LmStudioOptions> na hora de montar o
        // cliente.
        services.AddHttpClient<ITranslationService, LmStudioTranslationService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<LmStudioOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // ---------------------------------------------------------------
        // SCRAPERS (padrão Strategy) — registro das implementações concretas
        // contra as interfaces definidas na camada Application.
        // ---------------------------------------------------------------
        // Santo e Liturgia são "Scoped": criam uma instância nova por
        // escopo de injeção de dependência, o que é o padrão seguro e
        // recomendado quando não há necessidade explícita de estado
        // compartilhado entre chamadas.
        services.AddScoped<ISaintOfTheDayScraper, CancaoNovaSaintScraper>();
        services.AddScoped<ILiturgyScraper, CancaoNovaLiturgyScraper>();
        services.AddScoped<IHomilyScraper, VaticanHomilyScraper>();

        // O scraper do GCatholic é Singleton DE PROPÓSITO: ele mantém um
        // cache em memória do calendário anual (~170KB de HTML por ano) para
        // evitar baixar a mesma página do ano repetidamente quando o Worker
        // processa vários dias em sequência (ver comentário detalhado em
        // GCatholicCalendarScraper). Isso é seguro porque a classe usa
        // ConcurrentDictionary internamente e não guarda nenhum estado
        // específico de uma única requisição.
        services.AddSingleton<ILiturgicalCalendarScraper, GCatholicCalendarScraper>();

        return services;
    }

    /// <summary>
    /// Define um User-Agent "de navegador comum" nos HttpClients de
    /// scraping. Alguns servidores web bloqueiam ou tratam de forma
    /// diferente requisições sem um User-Agent (ou com o User-Agent padrão
    /// do .NET, que entrega o segredo de que é uma chamada automatizada).
    /// Isso NÃO é usado para burlar nenhuma proteção anti-bot — apenas para
    /// nos identificarmos como um cliente HTTP "normal", já que estamos
    /// consumindo conteúdo público, do mesmo jeito que um navegador faria.
    /// </summary>
    private static void ConfigureDefaultHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; AbbatiaScriptorium/1.0; +https://github.com/)");
        client.Timeout = TimeSpan.FromSeconds(30);
    }
}
