using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>
/// O PDF do documento, composto na hora. O PDF é exportado, não guardado: a
/// fonte de verdade é o Markdown, e compor é leitura.
/// </summary>
public record ObterPdfDoDocumentoQuery(string Id) : IRequest<Composicao>;

public class ObterPdfDoDocumentoHandler(IDocumentoRepository documentos, CompositorDePdf compositor)
    : IRequestHandler<ObterPdfDoDocumentoQuery, Composicao>
{
    public async Task<Composicao> Handle(ObterPdfDoDocumentoQuery request, CancellationToken ct)
    {
        var id = Ids.Ler(request.Id, () => new DocumentoNaoEncontradoException(request.Id));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.Id);
        return await compositor.ComporAsync(documento, ct);
    }
}
