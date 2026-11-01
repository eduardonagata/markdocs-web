using System.Text;
using MarkDocs.Abstractions;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;
using MediatR;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos;

/// <summary>
/// <c>/api/documentos</c>: criar, listar, ler, alterar (Markdown e
/// formatação), remover, e o PDF.
/// </summary>
public class DocumentosHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/documentos").WithTags("Documentos");

        grupo.MapGet("", ListarAsync)
            .Produces<IReadOnlyList<DocumentoResumoResponse>>();

        grupo.MapPost("", CriarAsync)
            .Produces<DocumentoResponse>(StatusCodes.Status201Created)
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id}", ObterAsync)
            .Produces<DocumentoResponse>()
            .Produces<ErroResponse>(StatusCodes.Status404NotFound)
            .Produces<ErroResponse>(StatusCodes.Status422UnprocessableEntity);

        grupo.MapPatch("/{id}", AtualizarAsync)
            .Produces<DocumentoResponse>()
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id}", RemoverAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id}/pdf", PdfAsync)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErroResponse>(StatusCodes.Status404NotFound)
            .Produces<ErroResponse>(StatusCodes.Status422UnprocessableEntity);
    }

    internal static async Task<IResult> ListarAsync(IMediator mediator, CancellationToken ct)
    {
        var documentos = await mediator.Send(new ListarDocumentosQuery(), ct);
        return Results.Ok(documentos.Select(d => new DocumentoResumoResponse(
            d.Id.ToString(), d.Titulo, d.Tipo, d.TemplateDeOrigem, d.CriadoEm, d.AlteradoEm)).ToList());
    }

    internal static Task<IResult> CriarAsync(IMediator mediator, CriarDocumentoRequest request, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var documento = await mediator.Send(new CriarDocumentoCommand(request.TemplateId, request.Corpo ?? "", request.Titulo), ct);
            var completo = await mediator.Send(new ObterDocumentoPorIdQuery(documento.Id.ToString()), ct);
            return Results.Created($"/api/documentos/{documento.Id}", ToDto(completo));
        });

    internal static Task<IResult> ObterAsync(IMediator mediator, string id, CancellationToken ct) =>
        Erros.Traduzir(async () => Results.Ok(ToDto(await mediator.Send(new ObterDocumentoPorIdQuery(id), ct))));

    internal static Task<IResult> AtualizarAsync(IMediator mediator, string id, AtualizarDocumentoRequest request, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            await mediator.Send(new AtualizarDocumentoCommand(
                id,
                request.Titulo,
                request.Tipo,
                request.Corpo,
                request.Ajustes?.Select(a => new AjusteDeFormatacao(a.Eixo, a.Valor.ParaCore())).ToList(),
                request.TemplateId), ct);
            return Results.Ok(ToDto(await mediator.Send(new ObterDocumentoPorIdQuery(id), ct)));
        });

    internal static Task<IResult> RemoverAsync(IMediator mediator, string id, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            await mediator.Send(new RemoverDocumentoCommand(id), ct);
            return Results.NoContent();
        });

    /// <summary>
    /// O PDF, composto na hora, para exibição (<c>inline</c>). Os avisos do
    /// motor, quando há, seguem no cabeçalho <c>X-MarkDocs-Avisos</c>.
    /// </summary>
    internal static Task<IResult> PdfAsync(IMediator mediator, HttpResponse resposta, string id, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var composicao = await mediator.Send(new ObterPdfDoDocumentoQuery(id), ct);
            return Pdf(resposta, composicao, $"documento-{id}.pdf");
        });

    internal static IResult Pdf(HttpResponse resposta, Composicao composicao, string arquivo)
    {
        resposta.Headers.CacheControl = "no-store";
        resposta.Headers.ContentDisposition = $"inline; filename=\"{arquivo}\"";
        if (composicao.Avisos.Count > 0)
            resposta.Headers["X-MarkDocs-Avisos"] = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(string.Join("\n", composicao.Avisos.Select(a => a.DescricaoLegivel))));
        return Results.Bytes(composicao.Pdf, "application/pdf");
    }

    private static DocumentoResponse ToDto(DocumentoComEixos completo)
    {
        var (documento, eixos) = completo;
        return new DocumentoResponse(
            documento.Id.ToString(),
            documento.Titulo,
            documento.Tipo,
            documento.Corpo,
            ((IDocumento)documento).Familia,
            documento.TemplateDeOrigem,
            documento.CriadoEm,
            documento.AlteradoEm,
            eixos.Select(EixoResponse.De).ToList(),
            documento.Imagens.Select(i => ImagensHandler.ToDto(documento, i)).ToList());
    }
}
