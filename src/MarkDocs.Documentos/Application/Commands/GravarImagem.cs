using MarkDocs.Abstractions;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Grava uma imagem nos ativos do documento. O Markdown a cita por
/// <c>imagens/&lt;nome&gt;</c>, e só as imagens dos ativos chegam ao papel e à
/// tela. Um nome que já existe é substituído.
/// </summary>
public record GravarImagemCommand(string DocumentoId, string NomeDoArquivo, byte[] Conteudo) : IRequest<ReferenciaDeAtivo>;

public class GravarImagemHandler(
    IDocumentoRepository documentos,
    IArmazenamentoDeObjetos armazenamento,
    TimeProvider relogio,
    ILogger<GravarImagemHandler> logger) : IRequestHandler<GravarImagemCommand, ReferenciaDeAtivo>
{
    public async Task<ReferenciaDeAtivo> Handle(GravarImagemCommand request, CancellationToken ct)
    {
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        var nome = ImagensRecebidas.NomeSeguro(request.NomeDoArquivo);
        ImagensRecebidas.Validar(nome, request.Conteudo);

        // Objeto no bucket é imutável: imagem nova é objeto novo.
        var imagem = new ReferenciaDeAtivo(IArmazenamentoDeObjetos.NovaChave(nome), nome, PapelDoAtivo.Imagem);
        await armazenamento.GravarAsync(imagem.Id, request.Conteudo, ImagensRecebidas.TipoDeConteudo(nome), ct);

        var anterior = documento.GravarImagem(imagem);
        documento.AlteradoEm = relogio.GetUtcNow();
        await documentos.SubstituirAsync(documento, ct);
        if (anterior is not null) await armazenamento.RemoverAsync(anterior.Id, ct);

        logger.LogInformation("Imagem {Nome} gravada no documento {DocumentoId}", nome, documento.Id);
        return imagem;
    }
}
