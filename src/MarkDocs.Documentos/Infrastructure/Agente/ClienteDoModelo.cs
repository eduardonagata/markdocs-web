using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MarkDocs.Documentos.Infrastructure.Agente;

/// <summary>
/// O <see cref="IChatClient"/> do provedor configurado. É o único lugar que
/// sabe de onde o modelo vem; o agente, as ferramentas e o endpoint não mudam
/// entre a OpenAI e o Foundry.
/// </summary>
internal static class ClienteDoModelo
{
    /// <summary>O escopo do Entra ID para a API v1 do Foundry.</summary>
    private const string EscopoDoFoundry = "https://ai.azure.com/.default";

    public static IChatClient Criar(AgenteOptions opcoes) => opcoes.ProvedorNormalizado switch
    {
        AgenteOptions.OpenAI => DaOpenAI(opcoes),
        AgenteOptions.Foundry => DoFoundry(opcoes),
        var outro => throw new InvalidOperationException($"AGENTE_PROVEDOR “{outro}” não é conhecido: use openai ou foundry."),
    };

    private static IChatClient DaOpenAI(AgenteOptions opcoes)
    {
        var configuracao = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(opcoes.Endpoint)) configuracao.Endpoint = new Uri(opcoes.Endpoint);
        return new OpenAIClient(new ApiKeyCredential(opcoes.ChaveApi!), configuracao)
            .GetChatClient(opcoes.Modelo)
            .AsIChatClient();
    }

    /// <summary>
    /// O Foundry expõe os modelos OpenAI pela API v1, compatível com o SDK da
    /// OpenAI: muda o endereço e, sem chave, a credencial.
    /// </summary>
    private static IChatClient DoFoundry(AgenteOptions opcoes)
    {
        var configuracao = new OpenAIClientOptions { Endpoint = EnderecoV1(opcoes.Endpoint!) };
        // O construtor com política de autenticação (Entra ID) ainda é
        // marcado como experimental no SDK da OpenAI; é o caminho documentado
        // para o Foundry sem chave.
#pragma warning disable OPENAI001
        var cliente = string.IsNullOrWhiteSpace(opcoes.ChaveApi)
            ? new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), EscopoDoFoundry), configuracao)
            : new OpenAIClient(new ApiKeyCredential(opcoes.ChaveApi), configuracao);
#pragma warning restore OPENAI001
        return cliente.GetChatClient(opcoes.Modelo).AsIChatClient();
    }

    /// <summary>
    /// <c>https://recurso.openai.azure.com</c> → <c>https://recurso.openai.azure.com/openai/v1/</c>.
    /// Um endereço que já termine em <c>/openai/v1</c> fica como está.
    /// </summary>
    internal static Uri EnderecoV1(string endpoint)
    {
        var base_ = endpoint.Trim().TrimEnd('/');
        if (!base_.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            base_ += "/openai/v1";
        return new Uri(base_ + "/");
    }
}
