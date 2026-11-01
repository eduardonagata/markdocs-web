using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Fontes;
using MarkDocs.Documentos.Infrastructure.Persistence.Configuration;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MarkDocs.Documentos.Infrastructure.Persistence.Repositories;
using MarkDocs.Documentos.Infrastructure.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MarkDocs.Documentos.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o slice: MongoDB (single-tenant, um database só), o adaptador
    /// de armazenamento de objetos escolhido por <c>ARMAZENAMENTO_PROVEDOR</c>,
    /// o renderizador do MarkDocs.Core com as fontes do app, e a semeadura dos
    /// templates de fábrica.
    /// </summary>
    public static IServiceCollection AddDocumentosModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Enums e convenções do MarkDocs no driver, antes de qualquer acesso
        // ao MongoDB. Idempotente.
        SerializacaoDaFormatacao.RegistrarBson();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DocumentosMarker).Assembly));

        services.Configure<DocumentosOptions>(o =>
        {
            o.ConnectionString = configuration["DOCUMENTOS_CONNECTION_STRING"] ?? o.ConnectionString;
            o.DatabaseName = configuration["DOCUMENTOS_DATABASE_NAME"] ?? o.DatabaseName;
            o.Armazenamento.Provedor = configuration["ARMAZENAMENTO_PROVEDOR"] ?? o.Armazenamento.Provedor;
            o.Armazenamento.Diretorio = configuration["ARMAZENAMENTO_DIRETORIO"] ?? o.Armazenamento.Diretorio;
            o.Armazenamento.Minio.Endpoint = configuration["MINIO_ENDPOINT"] ?? o.Armazenamento.Minio.Endpoint;
            o.Armazenamento.Minio.AccessKey = configuration["MINIO_ACCESS_KEY"] ?? o.Armazenamento.Minio.AccessKey;
            o.Armazenamento.Minio.SecretKey = configuration["MINIO_SECRET_KEY"] ?? o.Armazenamento.Minio.SecretKey;
            o.Armazenamento.Minio.Bucket = configuration["MINIO_BUCKET"] ?? o.Armazenamento.Minio.Bucket;
            o.Armazenamento.Minio.Regiao = configuration["MINIO_REGIAO"] ?? o.Armazenamento.Minio.Regiao;
            if (bool.TryParse(configuration["MINIO_USAR_SSL"], out var ssl)) o.Armazenamento.Minio.UsarSsl = ssl;
        });

        // MongoDB: o cliente é thread-safe e mantém o pool; os repositórios
        // são invólucros sem estado sobre as coleções.
        services.AddSingleton<IMongoClient>(sp => new MongoClient(sp.GetRequiredService<IOptions<DocumentosOptions>>().Value.ConnectionString));
        services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>()
            .GetDatabase(sp.GetRequiredService<IOptions<DocumentosOptions>>().Value.DatabaseName));
        services.AddSingleton<IDocumentoRepository, DocumentoRepository>();
        services.AddSingleton<ITemplateRepository, TemplateRepository>();

        // O adaptador do armazenamento de objetos.
        services.AddSingleton<IArmazenamentoDeObjetos>(sp =>
            sp.GetRequiredService<IOptions<DocumentosOptions>>().Value.Armazenamento.Provedor.Trim().ToLowerInvariant() switch
            {
                "minio" or "s3" => ActivatorUtilities.CreateInstance<MinioArmazenamentoDeObjetos>(sp),
                "disco" => ActivatorUtilities.CreateInstance<DiscoArmazenamentoDeObjetos>(sp),
                var outro => throw new InvalidOperationException(
                    $"ARMAZENAMENTO_PROVEDOR “{outro}” não é conhecido (minio ou disco)."),
            });

        // O MarkDocs.Core: sem estado mutável, um para o processo.
        services.AddSingleton(_ => new RenderizadorPDF(FontesEmbarcadas.Carregar()));
        services.AddScoped<CompositorDePdf>();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<SemeadorDeTemplates>();

        return services;
    }
}
