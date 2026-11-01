using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Infrastructure.Fontes;
using MarkDocs.Documentos.Infrastructure.Persistence.Repositories;
using MarkDocs.Documentos.Infrastructure.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;

namespace MarkDocs.Documentos.Tests;

/// <summary>
/// Os handlers montados à mão (DI manual), sobre um database próprio e o
/// armazenamento em memória. Os templates de fábrica já semeados.
/// </summary>
public sealed class Ambiente
{
    private static readonly RenderizadorPDF Renderizador = new(FontesEmbarcadas.Carregar());

    public Ambiente(MongoFixture mongo)
    {
        Database = mongo.NovoDatabase();
        Documentos = new DocumentoRepository(Database);
        Templates = new TemplateRepository(Database);
        Compositor = new CompositorDePdf(Renderizador, Armazenamento);
        new SemeadorDeTemplates(Templates, Relogio, NullLogger<SemeadorDeTemplates>.Instance)
            .StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public IMongoDatabase Database { get; }
    public DocumentoRepository Documentos { get; }
    public TemplateRepository Templates { get; }
    public ArmazenamentoEmMemoria Armazenamento { get; } = new();
    public FakeTimeProvider Relogio { get; } = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
    public CompositorDePdf Compositor { get; }

    public async Task<TemplateDeDocumento> TemplateAsync(string nome) =>
        (await Templates.ListarAsync()).Single(t => t.Nome == nome);

    public Task<Documento> CriarAsync(CriarDocumentoCommand comando) =>
        new CriarDocumentoHandler(Templates, Documentos, Relogio, NullLogger<CriarDocumentoHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    public Task<Documento> AtualizarAsync(AtualizarDocumentoCommand comando) =>
        new AtualizarDocumentoHandler(Documentos, Templates, Compositor, Relogio, NullLogger<AtualizarDocumentoHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    public Task RemoverAsync(string id) =>
        new RemoverDocumentoHandler(Documentos, Armazenamento, NullLogger<RemoverDocumentoHandler>.Instance)
            .Handle(new RemoverDocumentoCommand(id), CancellationToken.None);

    public Task<TemplateDeDocumento> CriarTemplateAsync(string documentoId, string nome) =>
        new CriarTemplateHandler(Documentos, Templates, Relogio, NullLogger<CriarTemplateHandler>.Instance)
            .Handle(new CriarTemplateCommand(documentoId, nome), CancellationToken.None);

    public Task<MarkDocs.Abstractions.ReferenciaDeAtivo> GravarImagemAsync(string documentoId, string arquivo, byte[] conteudo) =>
        new GravarImagemHandler(Documentos, Armazenamento, Relogio, NullLogger<GravarImagemHandler>.Instance)
            .Handle(new GravarImagemCommand(documentoId, arquivo, conteudo), CancellationToken.None);

    public Task RemoverImagemAsync(string documentoId, string nome) =>
        new RemoverImagemHandler(Documentos, Armazenamento, Relogio)
            .Handle(new RemoverImagemCommand(documentoId, nome), CancellationToken.None);

    public Task<DocumentoComEixos> ObterAsync(string id) =>
        new ObterDocumentoPorIdHandler(Documentos, Compositor).Handle(new ObterDocumentoPorIdQuery(id), CancellationToken.None);

    public Task<Composicao> PdfAsync(string id) =>
        new ObterPdfDoDocumentoHandler(Documentos, Compositor).Handle(new ObterPdfDoDocumentoQuery(id), CancellationToken.None);

    public Task<ImagemDoDocumento> ImagemAsync(string id, string nome) =>
        new ObterImagemDoDocumentoHandler(Documentos, Armazenamento).Handle(new ObterImagemDoDocumentoQuery(id, nome), CancellationToken.None);

    /// <summary>Um PNG de 1 × 1 pixel.</summary>
    public static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
}
