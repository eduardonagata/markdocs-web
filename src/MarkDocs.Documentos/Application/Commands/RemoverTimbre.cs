using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Domain.ValueObjects;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Tira o papel timbrado de todas as páginas, da primeira ou das demais. O
/// objeto fica no bucket: pode ser compartilhado com um template.
/// </summary>
public record RemoverTimbreCommand(string DocumentoId, string Paginas) : IRequest<Documento>;

public class RemoverTimbreHandler(IDocumentoRepository documentos, TimeProvider relogio)
    : IRequestHandler<RemoverTimbreCommand, Documento>
{
    public async Task<Documento> Handle(RemoverTimbreCommand request, CancellationToken ct)
    {
        var paginas = PaginasDoTimbreTexto.Ler(request.Paginas);
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        documento.RemoverTimbre(paginas);
        documento.AlteradoEm = relogio.GetUtcNow();
        await documentos.SubstituirAsync(documento, ct);
        return documento;
    }
}
