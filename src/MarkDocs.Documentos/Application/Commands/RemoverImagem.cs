using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Remove uma imagem dos ativos. O Markdown que ainda a cite passa a mostrar o
/// texto alternativo — nada quebra.
/// </summary>
public record RemoverImagemCommand(string DocumentoId, string Nome) : IRequest;

public class RemoverImagemHandler(
    IDocumentoRepository documentos,
    IArmazenamentoDeObjetos armazenamento,
    TimeProvider relogio) : IRequestHandler<RemoverImagemCommand>
{
    public async Task Handle(RemoverImagemCommand request, CancellationToken ct)
    {
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        var removida = documento.RemoverImagem(request.Nome) ?? throw new ImagemNaoEncontradaException(request.Nome);
        documento.AlteradoEm = relogio.GetUtcNow();
        await documentos.SubstituirAsync(documento, ct);
        await armazenamento.RemoverAsync(removida.Id, ct);
    }
}
