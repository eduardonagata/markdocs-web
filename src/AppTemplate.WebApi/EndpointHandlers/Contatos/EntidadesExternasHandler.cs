using System.Security.Claims;
using AppTemplate.Contatos.Application.EntidadesExternas.Commands;
using AppTemplate.Contatos.Application.EntidadesExternas.Queries;
using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Domain.Exceptions;
using AppTemplate.WebApi.EndpointHandlers.Contatos.Dto;
using MediatR;
using MongoDB.Bson;

namespace AppTemplate.WebApi.EndpointHandlers.Contatos;

public class EntidadesExternasHandler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/entidades-externas", CadastrarAsync)
            .RequireAuthorization()
            .Produces<EntidadeExternaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/entidades-externas", ListarAsync)
            .RequireAuthorization()
            .Produces<List<EntidadeExternaResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/entidades-externas/{id}", ObterPorIdAsync)
            .RequireAuthorization()
            .Produces<EntidadeExternaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> CadastrarAsync(
        IMediator mediator,
        HttpContext httpContext,
        CadastrarEntidadeExternaRequest request)
    {
        if (!TryGetCriadoPor(httpContext, out var criadoPor))
            return Results.Unauthorized();

        try
        {
            var entidade = await mediator.Send(
                new CadastrarEntidadeExternaCommand(request.Nome, request.Papeis, criadoPor));

            return Results.Created(
                $"/api/entidades-externas/{entidade.Id}",
                ToDto(entidade));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    internal static async Task<IResult> ListarAsync(IMediator mediator)
    {
        var entidades = await mediator.Send(new ObterEntidadesExternasQuery());
        return Results.Ok(entidades.Select(ToDto).ToList());
    }

    internal static async Task<IResult> ObterPorIdAsync(IMediator mediator, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return Results.BadRequest("id invalido.");

        try
        {
            var entidade = await mediator.Send(new ObterEntidadeExternaPorIdQuery(objectId));
            return Results.Ok(ToDto(entidade));
        }
        catch (EntidadeExternaNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static bool TryGetCriadoPor(HttpContext ctx, out ObjectId criadoPor)
    {
        var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? ctx.User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(sub) && ObjectId.TryParse(sub, out criadoPor))
            return true;
        criadoPor = default;
        return false;
    }

    private static EntidadeExternaResponse ToDto(EntidadeExterna e) => new()
    {
        Id = e.Id.ToString(),
        Nome = e.Nome,
        Papeis = e.Papeis,
        CriadoEm = e.CriadoEm,
    };
}
