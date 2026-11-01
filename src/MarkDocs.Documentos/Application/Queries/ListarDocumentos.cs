using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>Os documentos, do alterado mais recentemente ao mais antigo.</summary>
public record ListarDocumentosQuery : IRequest<IReadOnlyList<ResumoDeDocumento>>;

public class ListarDocumentosHandler(IDocumentoRepository documentos)
    : IRequestHandler<ListarDocumentosQuery, IReadOnlyList<ResumoDeDocumento>>
{
    public Task<IReadOnlyList<ResumoDeDocumento>> Handle(ListarDocumentosQuery request, CancellationToken ct) =>
        documentos.ListarAsync(ct);
}
