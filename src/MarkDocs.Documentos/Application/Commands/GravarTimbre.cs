using MarkDocs.Abstractions;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Domain.ValueObjects;
using MarkDocs.Documentos.Infrastructure.Armazenamento;
using MarkDocs.Documentos.Infrastructure.Persistence.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>
/// Põe uma imagem como papel timbrado — o fundo das páginas — em todas as
/// páginas, só na primeira, ou nas demais. A imagem vai como está: o
/// <c>MarkDocs.Core</c> a estica ao tamanho do papel, e quem a envia a
/// prepara na proporção dele.
/// </summary>
public record GravarTimbreCommand(string DocumentoId, string Paginas, string NomeDoArquivo, byte[] Conteudo) : IRequest<Documento>;

public class GravarTimbreHandler(
    IDocumentoRepository documentos,
    IArmazenamentoDeObjetos armazenamento,
    TimeProvider relogio,
    ILogger<GravarTimbreHandler> logger) : IRequestHandler<GravarTimbreCommand, Documento>
{
    public async Task<Documento> Handle(GravarTimbreCommand request, CancellationToken ct)
    {
        var paginas = PaginasDoTimbreTexto.Ler(request.Paginas);
        var id = Ids.Ler(request.DocumentoId, () => new DocumentoNaoEncontradoException(request.DocumentoId));
        var documento = await documentos.ObterAsync(id, ct) ?? throw new DocumentoNaoEncontradoException(request.DocumentoId);

        // Um nome novo a cada envio: o documento pode ter vindo de um template
        // que cita o timbre antigo pelo nome, e objeto no bucket é imutável.
        var extensao = Path.GetExtension(request.NomeDoArquivo).ToLowerInvariant();
        var nome = $"{Documento.PrefixoDoTimbre}{paginas.Texto()}-{Guid.NewGuid().ToString("N")[..8]}{extensao}";
        ImagensRecebidas.Validar(nome, request.Conteudo);

        var timbre = new ReferenciaDeAtivo(IArmazenamentoDeObjetos.NovaChave(nome), nome, PapelDoAtivo.Template);
        await armazenamento.GravarAsync(timbre.Id, request.Conteudo, ImagensRecebidas.TipoDeConteudo(nome), ct);

        documento.DefinirTimbre(paginas, timbre);
        documento.AlteradoEm = relogio.GetUtcNow();
        await documentos.SubstituirAsync(documento, ct);

        logger.LogInformation("Timbre {Nome} posto em {Paginas} no documento {DocumentoId}", nome, paginas, documento.Id);
        return documento;
    }
}
