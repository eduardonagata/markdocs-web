using System.Text;
using AGUI.Abstractions;
using AGUI.Server;
using Microsoft.Extensions.AI;

namespace MarkDocs.Documentos.Infrastructure.Agente;

/// <summary>
/// Leva ao modelo o <c>context</c> do AG-UI — o que a tela diz que está aberto
/// (o documento, o Markdown dele, o trecho selecionado) — como parte das
/// instruções de <b>cada</b> chamada.
/// </summary>
/// <remarks>
/// O contexto vai nas instruções, e não como mensagem, de propósito: ele
/// descreve a tela <b>agora</b>, e é reenviado a cada execução. Como
/// instrução, não entra no histórico da conversa — que é o que permite, mais
/// tarde, persistir o histórico pelo <c>AgentSessionStore</c> sem guardar
/// retratos velhos do documento (EI-001, "Histórico").
/// </remarks>
internal sealed class ContextoDaTelaChatClient(IChatClient interno) : DelegatingChatClient(interno)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetResponseAsync(messages, ComContexto(options), cancellationToken);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        base.GetStreamingResponseAsync(messages, ComContexto(options), cancellationToken);

    internal static ChatOptions? ComContexto(ChatOptions? options)
    {
        if (options is null
            || !options.TryGetRunAgentInput(out var entrada)
            || entrada.Context is not { Count: > 0 } contexto)
            return options;

        var copia = options.Clone();
        copia.Instructions = string.Join("\n\n", new[] { options.Instructions, Formatar(contexto) }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        return copia;
    }

    internal static string Formatar(IEnumerable<AGUIContext> contexto)
    {
        var texto = new StringBuilder("# O que está na tela do usuário agora\n");
        foreach (var item in contexto)
        {
            texto.Append("\n## ").Append(item.Description.Trim()).Append('\n');
            texto.Append(item.Value.Trim()).Append('\n');
        }
        return texto.ToString().TrimEnd();
    }
}
