using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace MarkDocs.WebApi.IntegrationTests;

/// <summary>
/// Um modelo de linguagem de mentira, com um roteiro fixo: responde pela
/// última mensagem, sem estado. É o que permite testar a borda AG-UI, as
/// ferramentas e o contexto sem rede e sem custo.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>“liste …” → chama a ferramenta de backend <c>listarDocumentos</c>;</item>
///   <item>“troque …” → chama a ferramenta da tela <c>substituirTrecho</c>;</item>
///   <item>o resultado de uma ferramenta → responde com um texto que o cita.</item>
/// </list>
/// </remarks>
public sealed class ModeloRoteirizado : IChatClient
{
    public ConcurrentQueue<(List<ChatMessage> Mensagens, ChatOptions? Opcoes)> Chamadas { get; } = new();

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lista = messages.ToList();
        Chamadas.Enqueue((lista, options));
        return Task.FromResult(new ChatResponse(Responder(lista)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resposta = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var atualizacao in resposta.ToChatResponseUpdates())
            yield return atualizacao;
    }

    private static ChatMessage Responder(List<ChatMessage> mensagens)
    {
        var ultima = mensagens.Last();
        if (ultima.Contents.OfType<FunctionResultContent>().FirstOrDefault() is { } resultado)
            return new ChatMessage(ChatRole.Assistant, $"Resultado: {resultado.Result}");

        var texto = ultima.Text;
        if (texto.StartsWith("liste", StringComparison.OrdinalIgnoreCase))
            return new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("chamada-backend", "listarDocumentos", new Dictionary<string, object?> { ["filtro"] = "" })]);

        if (texto.StartsWith("troque", StringComparison.OrdinalIgnoreCase))
            return new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("chamada-tela", "substituirTrecho", new Dictionary<string, object?>
                {
                    ["trechoAtual"] = "As partes acordam.",
                    ["novoTexto"] = "As partes acordam e assinam.",
                })]);

        return new ChatMessage(ChatRole.Assistant, "Não entendi.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
