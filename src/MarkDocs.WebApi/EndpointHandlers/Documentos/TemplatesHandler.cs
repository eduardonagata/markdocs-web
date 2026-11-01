using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Queries;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;
using MediatR;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos;

/// <summary><c>/api/templates</c>: listar, criar a partir de um documento formatado, e a amostra em PDF.</summary>
public class TemplatesHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/templates").WithTags("Templates");

        grupo.MapGet("", ListarAsync)
            .Produces<IReadOnlyList<TemplateResponse>>();

        grupo.MapPost("", CriarAsync)
            .Produces<TemplateResponse>(StatusCodes.Status201Created)
            .Produces<ErroResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id}/amostra", AmostraAsync)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErroResponse>(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> ListarAsync(IMediator mediator, CancellationToken ct)
    {
        var templates = await mediator.Send(new ListarTemplatesQuery(), ct);
        return Results.Ok(templates.Select(ToDto).ToList());
    }

    /// <summary>Cria um template a partir de um documento que foi formatado.</summary>
    internal static Task<IResult> CriarAsync(IMediator mediator, CriarTemplateRequest request, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var template = await mediator.Send(new CriarTemplateCommand(request.DocumentoId, request.Nome ?? ""), ct);
            return Results.Created($"/api/templates/{template.Id}", ToDto(template));
        });

    internal static Task<IResult> AmostraAsync(IMediator mediator, HttpResponse resposta, string id, CancellationToken ct) =>
        Erros.Traduzir(async () =>
        {
            var composicao = await mediator.Send(new ObterAmostraDoTemplateQuery(id), ct);
            return DocumentosHandler.Pdf(resposta, composicao, $"template-{id}.pdf");
        });

    private static TemplateResponse ToDto(TemplateDeDocumento t) => new(
        t.Id.ToString(), t.Nome, t.Tipo, t.Formatacao.Familia, t.DeFabrica, t.DocumentoDeOrigem?.ToString(), t.CriadoEm);
}
