using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.TestStubs;

public static class TestStubsServiceExtensions
{
    /// <summary>
    /// Registra no container o necessário para os stubs de desenvolvimento:
    /// <see cref="DevJwtOptions"/> (singleton) e <see cref="DevTokenIssuer"/>
    /// (singleton). Uso esperado apenas em <c>builder.Environment.IsDevelopment()</c>.
    /// </summary>
    public static IServiceCollection AddTestStubs(
        this IServiceCollection services,
        DevJwtOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<DevTokenIssuer>();
        return services;
    }
}
