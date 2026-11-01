using MarkDocs.Abstractions;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Domain.ValueObjects;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>
/// A imagem do papel timbrado que vale para a primeira página, ou para as
/// demais — para a tela mostrar o que vai ao fundo do papel.
/// </summary>
public record ObterTimbreDoDocumentoQuery(string DocumentoId, string Paginas) : IRequest<ImagemDoDocumento>;

public class ObterTimbreDoDocumentoHandler(IDocumentoRepository documentos, IArmazenamentoDeObjetos armazenamento)
    : IRequestHandler<ObterTimbreDoDocumentoQuery, ImagemDoDocumento>
{
    public async Task<ImagemDoDocumento> Handle(ObterTimbreDoDocumentoQuery request, CancellationToken ct)
    {
        var paginas = PaginasDoTimbreTexto.Ler(request.Paginas);
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        var nome = documento.Timbre(paginas) ?? throw new TimbreNaoEncontradoException(request.Paginas);
        var ativo = documento.Ativos.FirstOrDefault(a => a.Papel == PapelDoAtivo.Template && a.Nome == nome)
                    ?? throw new TimbreNaoEncontradoException(request.Paginas);
        return new ImagemDoDocumento(nome, await armazenamento.LerAsync(ativo.Id, ct), ImagensRecebidas.TipoDeConteudo(nome));
    }
}
