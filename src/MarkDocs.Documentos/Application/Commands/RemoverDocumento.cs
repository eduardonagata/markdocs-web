using MarkDocs.Abstractions;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Remove um documento e as imagens dele. Os ativos de template ficam no
/// bucket: podem ser compartilhados com o template de onde vieram.
/// </summary>
public record RemoverDocumentoCommand(string Id) : IRequest;

public class RemoverDocumentoHandler(
    IDocumentoRepository documentos,
    IArmazenamentoDeObjetos armazenamento,
    ILogger<RemoverDocumentoHandler> logger) : IRequestHandler<RemoverDocumentoCommand>
{
    public async Task Handle(RemoverDocumentoCommand request, CancellationToken ct)
    {
        var id = Ids.Ler(request.Id, () => new DocumentoNaoEncontradoException(request.Id));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.Id);

        await documentos.RemoverAsync(id, ct);
        foreach (var imagem in documento.Ativos.Where(a => a.Papel == PapelDoAtivo.Imagem))
            await armazenamento.RemoverAsync(imagem.Id, ct);

        logger.LogInformation("Documento {DocumentoId} removido", id);
    }
}
