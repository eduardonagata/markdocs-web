using AppTemplate.Contatos.Infrastructure.Persistence.Configuration;
using AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;
using AppTemplate.Contatos.Infrastructure.Persistence.Repositories;
using AppTemplate.Contatos.Infrastructure.Persistence.Transactions;
using AppTemplate.Contatos.Infrastructure.Tenancy;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AppTemplate.Contatos.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todos os servicos do slice Contatos no container de DI.
    /// Inclui: MediatR, tenant accessor, MongoDB (client singleton +
    /// database scoped por tenant), repositories e session accessor.
    ///
    /// Multi-tenancy por database: o <see cref="IMongoDatabase"/> e
    /// resolvido por requisicao, lendo o tenantId da claim <c>tid</c> do
    /// JWT via <see cref="ITenantAccessor"/>, e abrindo o database cujo
    /// nome segue o padrao <c>tid-{tenantId}</c> — prefixo fixo por
    /// convencao do template.
    ///
    /// Precondicao: o pipeline HTTP deve registrar
    /// <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> — feito
    /// em <c>Program.cs</c> via <c>AddHttpContextAccessor()</c>.
    /// </summary>
    public static IServiceCollection AddContatosModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ============================================================
        // MEDIATR
        // ============================================================
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(ContatosMarker).Assembly));

        // ============================================================
        // OPTIONS
        // --------------------------------------------------------------
        // Env vars flat UPPER_CASE (convencao UNIX), mapeadas manualmente
        // para o ContatosOptions. Evita o estilo Contatos__ConnectionString
        // do binder automatico do ASP.NET, que mistura case em env vars e
        // quebra a consistencia com as demais (JWT_*, CORS_*, DEV_JWT_*).
        //
        // Cada slice le sua propria <SLICE>_CONNECTION_STRING. Dai a
        // flexibilidade por deployment:
        //   - Apontar todas as <SLICE>_CONNECTION_STRING para a mesma URI
        //     = um unico MongoDB compartilhado (caso comum em MVP/SaaS).
        //   - Apontar cada uma para uma URI distinta = cluster por slice
        //     (util quando um slice tem requisitos de isolamento, carga,
        //     jurisdicao de dados, ou foi extraido para outro repo).
        // Em ambos os casos o isolamento por tenant (tid-{tenantId})
        // permanece — e via database name, nao via cluster.
        // ============================================================
        services.Configure<ContatosOptions>(options =>
        {
            options.ConnectionString = configuration["CONTATOS_CONNECTION_STRING"] ?? string.Empty;
        });

        // ============================================================
        // TENANT ACCESSOR (scoped — resolve por request via JWT claim)
        // ============================================================
        services.AddScoped<ITenantAccessor, HttpContextTenantAccessor>();

        // ============================================================
        // MONGODB
        // --------------------------------------------------------------
        // Client: singleton (thread-safe, connection pool compartilhado).
        // Database: scoped — nome fixo no padrao tid-{tenantId}, tenantId
        //           vindo do ITenantAccessor (claim "tid" do JWT).
        // ============================================================
        services.AddSingleton<IMongoClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<ContatosOptions>>().Value;
            return new MongoClient(opts.ConnectionString);
        });

        services.AddScoped<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var tenant = sp.GetRequiredService<ITenantAccessor>().TenantId;
            // Convencao fixa do template: database por tenant = tid-{tenantId}.
            return client.GetDatabase($"tid-{tenant}");
        });

        services.AddScoped<IMongoSessionAccessor, MongoSessionAccessor>();

        // ============================================================
        // REPOSITORIES
        // ============================================================
        services.AddScoped<IEntidadeExternaRepository, EntidadeExternaRepository>();

        return services;
    }
}
