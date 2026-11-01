using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Agente;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Hosting;
using MarkDocs.Documentos.Infrastructure.Agente;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Fontes;
using MarkDocs.Documentos.Infrastructure.Persistence.Configuration;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MarkDocs.Documentos.Infrastructure.Persistence.Repositories;
using MarkDocs.Documentos.Infrastructure.Workers;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

        services.AddAgente(new AgenteOptions
        {
            Provedor = configuration["AGENTE_PROVEDOR"] is { Length: > 0 } provedor ? provedor : AgenteOptions.OpenAI,
            Modelo = configuration["AGENTE_MODELO"] ?? "",
            ChaveApi = configuration["AGENTE_CHAVE_API"],
            Endpoint = configuration["AGENTE_ENDPOINT"],
        });

        return services;
    }

    /// <summary>
    /// O assistente (Docs/decisoes-arquitetura/EI-001): o Microsoft Agent
    /// Framework com o agente exposto em AG-UI por <c>MapAgente</c>. Sem
    /// configuração mínima, nada de agente é registrado e o endpoint responde
    /// 503 com o que falta.
    /// </summary>
    private static void AddAgente(this IServiceCollection services, AgenteOptions agente)
    {
        services.Configure<AgenteOptions>(o =>
        {
            o.Provedor = agente.Provedor;
            o.Modelo = agente.Modelo;
            o.ChaveApi = agente.ChaveApi;
            o.Endpoint = agente.Endpoint;
        });
        if (!agente.Configurado) return;

        // O cliente do modelo, por nome: os testes trocam só ele.
        services.AddKeyedSingleton<IChatClient>(Assistente.Nome, (sp, _) =>
            ClienteDoModelo.Criar(sp.GetRequiredService<IOptions<AgenteOptions>>().Value));
        services.AddSingleton<FerramentasDoAgente>();
        services.AddAGUIServer();
        services.AddSingleton<IPostConfigureOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>, EventosAguiSemNulos>();

        services.AddAIAgent(
            Assistente.Nome,
            (sp, nome) => Assistente.Criar(
                sp.GetRequiredKeyedService<IChatClient>(nome),
                sp.GetRequiredService<FerramentasDoAgente>(),
                sp.GetRequiredService<ILoggerFactory>(),
                sp),
            ServiceLifetime.Singleton);

        // Histórico da conversa no servidor: decisão em aberto (EI-001,
        // "Histórico"). Hoje a conversa vive no cliente, que a reenvia a cada
        // execução, e nenhum AgentSessionStore é registrado. Persistir é
        // encadear .WithSessionStore((sp, nome) => new <Store>(...)) no
        // retorno de AddAIAgent — o MapAGUIServer já procura o store pelo
        // nome do agente e usa o threadId do AG-UI como chave.
    }
}
