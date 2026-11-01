using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

public record ImagemDoDocumento(string Nome, byte[] Conteudo, string TipoDeConteudo);

/// <summary>
/// Uma imagem do documento, pelo nome que o Markdown cita. Só as imagens dos
/// ativos do documento são servidas: um nome fora da lista é 404, e o bucket
/// nunca é consultado por um caminho que o documento não declare.
/// </summary>
public record ObterImagemDoDocumentoQuery(string DocumentoId, string Nome) : IRequest<ImagemDoDocumento>;

public class ObterImagemDoDocumentoHandler(IDocumentoRepository documentos, IArmazenamentoDeObjetos armazenamento)
    : IRequestHandler<ObterImagemDoDocumentoQuery, ImagemDoDocumento>
{
    public async Task<ImagemDoDocumento> Handle(ObterImagemDoDocumentoQuery request, CancellationToken ct)
    {
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        var imagem = documento.Imagens.FirstOrDefault(i => i.Nome == request.Nome)
                     ?? throw new ImagemNaoEncontradaException(request.Nome);
        var conteudo = await armazenamento.LerAsync(imagem.Id, ct);
        return new ImagemDoDocumento(imagem.Nome, conteudo, ImagensRecebidas.TipoDeConteudo(imagem.Nome));
    }
}
