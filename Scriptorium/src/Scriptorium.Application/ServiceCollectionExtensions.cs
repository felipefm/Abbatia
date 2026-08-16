using Microsoft.Extensions.DependencyInjection;
using Scriptorium.Application.Services;

namespace Scriptorium.Application;

/// <summary>
/// Registro de DI dos serviços da camada Application — hoje, apenas o
/// <see cref="DevotionalBuilderService"/>. Mantemos esse método de extensão
/// separado do <c>AddScriptoriumInfrastructure</c> (na camada Infrastructure)
/// para respeitar a separação de responsabilidades entre as camadas: cada
/// camada é dona do registro de DI dos seus próprios serviços, e quem monta
/// a aplicação final (Scriptorium.API e Scriptorium.Worker) simplesmente
/// chama os dois métodos de extensão em sequência no Program.cs.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScriptoriumApplication(this IServiceCollection services)
    {
        // Scoped: o DevotionalBuilderService depende (indiretamente, via
        // scrapers) de HttpClients e do DbContext, então deve acompanhar o
        // mesmo ciclo de vida "por escopo" dessas dependências.
        services.AddScoped<DevotionalBuilderService>();
        return services;
    }
}
