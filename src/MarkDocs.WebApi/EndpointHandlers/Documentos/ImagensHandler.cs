using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;
using MediatR;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos;

/// <summary>
/// <c>/api/documentos/{id}/imagens/{nome}</c>: as imagens que o Markdown cita
/// por <c>imagens/{nome}</c>.
/// </summary>
/// <remarks>
/// O caminho é uma subseção do endereço do documento, de propósito: o
/// Markdown guarda só o relativo <c>imagens/planta.png</c>, e é a tela que o
/// resolve contra <c>/api/documentos/{id}/</c>. Exportado, o mesmo Markdown
/// acha <c>imagens/planta.png</c> ao lado de si. Só as imagens dos ativos do
/// documento são servidas.
/// </remarks>
public class ImagensHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/documentos/{id}/imagens").WithTags("Imagens");

        grupo.MapPost("", GravarAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImagemResponse>(StatusCodes.Status201Created)
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapGet("/{nome}", ObterAsync)
            .Produces(StatusCodes.Status200OK, null, "image/png", "image/jpeg", "image/gif", "image/svg+xml", "image/webp")
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{nome}", RemoverAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);
    }

    internal static Task<IResult> GravarAsync(IMediator mediator, string id, IFormFile arquivo, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            using var memoria = new MemoryStream();
            await arquivo.CopyToAsync(memoria, ct);
            var imagem = await mediator.Send(new GravarImagemCommand(id, arquivo.FileName, memoria.ToArray()), ct);
            return Results.Created(Url(id, imagem.Nome), ToDto(id, imagem.Nome));
        });

    internal static Task<IResult> ObterAsync(IMediator mediator, HttpResponse resposta, string id, string nome, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var imagem = await mediator.Send(new ObterImagemDoDocumentoQuery(id, nome), ct);
            // O objeto é imutável, mas o nome pode passar a apontar para outro
            // (uma imagem substituída): o navegador revalida.
            resposta.Headers.CacheControl = "no-cache";
            return Results.Bytes(imagem.Conteudo, imagem.TipoDeConteudo);
        });

    internal static Task<IResult> RemoverAsync(IMediator mediator, string id, string nome, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            await mediator.Send(new RemoverImagemCommand(id, nome), ct);
            return Results.NoContent();
        });

    internal static ImagemResponse ToDto(Documento documento, ReferenciaDeAtivo imagem) =>
        ToDto(documento.Id.ToString(), imagem.Nome);

    private static ImagemResponse ToDto(string documentoId, string nome) =>
        new(nome, ImagensDoDocumento.Referencia(nome), Url(documentoId, nome));

    private static string Url(string documentoId, string nome) => $"/api/documentos/{documentoId}/{ImagensDoDocumento.Referencia(nome)}";
}
