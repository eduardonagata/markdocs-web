using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.ValueObjects;
using MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;
using MediatR;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos;

/// <summary>
/// <c>/api/documentos/{id}/timbre/{paginas}</c>: o papel timbrado — a imagem
/// de fundo das páginas —, em <c>todas</c>, na <c>primeira</c> ou nas
/// <c>demais</c>.
/// </summary>
/// <remarks>
/// Gravar e remover devolvem o documento inteiro: o timbre é formatação, e a
/// tela troca o documento do cache pela resposta, como num <c>PATCH</c>.
/// </remarks>
public class TimbreHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/documentos/{id}/timbre").WithTags("Papel timbrado");

        grupo.MapPut("/{paginas}", GravarAsync)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentoResponse>()
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapGet("/{paginas}", ObterAsync)
            .Produces(StatusCodes.Status200OK, null, "image/png", "image/jpeg", "image/gif", "image/svg+xml", "image/webp")
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{paginas}", RemoverAsync)
            .Produces<DocumentoResponse>()
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);
    }

    internal static Task<IResult> GravarAsync(IMediator mediator, string id, string paginas, IFormFile arquivo, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            using var memoria = new MemoryStream();
            await arquivo.CopyToAsync(memoria, ct);
            await mediator.Send(new GravarTimbreCommand(id, paginas, arquivo.FileName, memoria.ToArray()), ct);
            return Results.Ok(DocumentosHandler.ToDto(await mediator.Send(new ObterDocumentoPorIdQuery(id), ct)));
        });

    internal static Task<IResult> ObterAsync(IMediator mediator, HttpResponse resposta, string id, string paginas, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var timbre = await mediator.Send(new ObterTimbreDoDocumentoQuery(id, paginas), ct);
            resposta.Headers.CacheControl = "no-cache";
            return Results.Bytes(timbre.Conteudo, timbre.TipoDeConteudo);
        });

    internal static Task<IResult> RemoverAsync(IMediator mediator, string id, string paginas, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            await mediator.Send(new RemoverTimbreCommand(id, paginas), ct);
            return Results.Ok(DocumentosHandler.ToDto(await mediator.Send(new ObterDocumentoPorIdQuery(id), ct)));
        });

    internal static TimbresResponse ToDto(Documento documento)
    {
        TimbreResponse? Timbre(PaginasDoTimbre paginas) => documento.Timbre(paginas) is { } nome
            ? new TimbreResponse(nome, $"/api/documentos/{documento.Id}/timbre/{paginas.Texto()}")
            : null;
        return new TimbresResponse(Timbre(PaginasDoTimbre.Primeira), Timbre(PaginasDoTimbre.Demais));
    }
}
