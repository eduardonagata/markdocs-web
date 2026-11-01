using MarkDocs.Documentos.Infrastructure.Agente;
using MediatR;
using Microsoft.Extensions.Options;

namespace MarkDocs.Documentos.Application.Queries;

/// <summary>Se o assistente está configurado, com que provedor e modelo — ou o que falta.</summary>
public record StatusDoAgente(bool Disponivel, string Provedor, string? Modelo, string? OQueFalta);

/// <summary>
/// O que a tela pergunta antes de abrir a conversa: barato, sem chamar o
/// modelo. Sem configuração, o painel explica o que falta em vez de falhar.
/// </summary>
public record ObterStatusDoAgenteQuery : IRequest<StatusDoAgente>;

public class ObterStatusDoAgenteHandler(IOptions<AgenteOptions> opcoes) : IRequestHandler<ObterStatusDoAgenteQuery, StatusDoAgente>
{
    public Task<StatusDoAgente> Handle(ObterStatusDoAgenteQuery request, CancellationToken ct)
    {
        var o = opcoes.Value;
        return Task.FromResult(new StatusDoAgente(o.Configurado, o.ProvedorNormalizado, o.Configurado ? o.Modelo : null, o.OQueFalta));
    }
}
