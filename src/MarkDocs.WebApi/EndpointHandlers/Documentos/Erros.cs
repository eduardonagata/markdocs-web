using MarkDocs.Core;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.WebApi.EndpointHandlers.Documentos.Dto;

namespace MarkDocs.WebApi.EndpointHandlers.Documentos;

/// <summary>A tradução das exceções de domínio para HTTP, num lugar só.</summary>
internal static class Erros
{
    public static async Task<IResult> Traduzir(Func<Task<IResult>> acao)
    {
        try
        {
            return await acao();
        }
        catch (NaoEncontradoException erro)
        {
            return Results.NotFound(new ErroResponse(erro.Message));
        }
        catch (ArgumentException erro)
        {
            return Results.BadRequest(new ErroResponse(erro.Message));
        }
        catch (ErroTypst erro)
        {
            // Template inválido, fonte que falta, recusa do motor: o documento
            // existe, mas não compõe. A mensagem é a que a tela mostra.
            return Results.UnprocessableEntity(new ErroResponse(erro.DescricaoLegivel));
        }
    }
}
