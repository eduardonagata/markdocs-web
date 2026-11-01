using System.Text;
using MarkDocs.Documentos.Infrastructure.Agente;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace MarkDocs.Documentos.Application.Agente;

/// <summary>
/// O agente do assistente: um <see cref="ChatClientAgent"/> com as instruções,
/// as ferramentas de leitura do backend e o contexto da tela. As ferramentas
/// de edição não estão aqui — chegam do frontend a cada execução (AG-UI).
/// </summary>
public static class Assistente
{
    /// <summary>O nome do agente no DI do Agent Framework e no <c>MapAGUIServer</c>.</summary>
    public const string Nome = "markdocs-assistente";

    /// <summary>As instruções, em <c>Instrucoes.md</c> (recurso do assembly).</summary>
    public static string Instrucoes => TextoDasInstrucoes.Value;

    private static readonly Lazy<string> TextoDasInstrucoes = new(() =>
    {
        using var fluxo = typeof(Assistente).Assembly.GetManifestResourceStream("MarkDocs.Documentos.Agente.Instrucoes.md")
            ?? throw new InvalidOperationException("O recurso Instrucoes.md não está no assembly.");
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    });

    internal static AIAgent Criar(IChatClient modelo, FerramentasDoAgente ferramentas, ILoggerFactory logs, IServiceProvider servicos) =>
        new ChatClientAgent(
            new ContextoDaTelaChatClient(modelo),
            new ChatClientAgentOptions
            {
                Name = Nome,
                Description = "O assistente do MarkDocs: lê e edita os documentos do usuário, na tela dele.",
                ChatOptions = new ChatOptions
                {
                    Instructions = Instrucoes,
                    Tools = ferramentas.Todas(),
                    // Uma chamada de ferramenta por resposta. Sem histórico no
                    // servidor (EI-001), uma resposta que misturasse uma
                    // ferramenta da tela com uma do backend deixaria a do
                    // backend órfã na continuação.
                    AllowMultipleToolCalls = false,
                },
            },
            logs,
            servicos);
}
