using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>
/// O documento e os controles de formatação dele. A tela desenha o inspetor a
/// partir dos eixos — domínio, limites e valor atual —, sem saber de família
/// nenhuma.
/// </summary>
public record DocumentoComEixos(Documento Documento, IReadOnlyList<EixoDeFormatacao> Eixos);

public record ObterDocumentoPorIdQuery(string Id) : IRequest<DocumentoComEixos>;

public class ObterDocumentoPorIdHandler(IDocumentoRepository documentos, CompositorDePdf compositor)
    : IRequestHandler<ObterDocumentoPorIdQuery, DocumentoComEixos>
{
    public async Task<DocumentoComEixos> Handle(ObterDocumentoPorIdQuery request, CancellationToken ct)
    {
        var id = Ids.Ler(request.Id, () => new DocumentoNaoEncontradoException(request.Id));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.Id);
        return new DocumentoComEixos(documento, compositor.Eixos(documento));
    }
}
