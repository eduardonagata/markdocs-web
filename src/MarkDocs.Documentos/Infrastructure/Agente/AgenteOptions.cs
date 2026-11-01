namespace MarkDocs.Documentos.Infrastructure.Agente;

/// <summary>
/// O modelo de linguagem do assistente, lido das variáveis <c>AGENTE_*</c> em
/// <c>AddDocumentosModule</c> (README, "Variáveis de ambiente";
/// Docs/decisoes-arquitetura/EI-001).
/// </summary>
/// <remarks>
/// Sem configuração mínima o app sobe igual, só que sem o assistente: o
/// endpoint responde 503 e a tela explica o que falta.
/// </remarks>
public sealed class AgenteOptions
{
    public const string OpenAI = "openai";
    public const string Foundry = "foundry";

    /// <summary>
    /// <c>AGENTE_PROVEDOR</c>: <c>openai</c> (padrão) — a API da OpenAI, ou
    /// qualquer serviço compatível em <see cref="Endpoint"/> — ou
    /// <c>foundry</c> — um modelo implantado no Microsoft Foundry (Azure
    /// OpenAI), pela API v1.
    /// </summary>
    public string Provedor { get; set; } = OpenAI;

    /// <summary>
    /// <c>AGENTE_MODELO</c>: na OpenAI, o id do modelo (<c>gpt-4.1</c>); no
    /// Foundry, o nome da implantação.
    /// </summary>
    public string Modelo { get; set; } = "";

    /// <summary>
    /// <c>AGENTE_CHAVE_API</c>. Obrigatória na OpenAI. No Foundry é opcional:
    /// sem ela, a autenticação é por Entra ID (<c>DefaultAzureCredential</c>).
    /// </summary>
    public string? ChaveApi { get; set; }

    /// <summary>
    /// <c>AGENTE_ENDPOINT</c>. No Foundry, o endereço do recurso
    /// (<c>https://&lt;recurso&gt;.openai.azure.com</c> ou
    /// <c>https://&lt;recurso&gt;.services.ai.azure.com</c>), obrigatório. Na
    /// OpenAI, opcional: um endereço compatível (um gateway, um proxy).
    /// </summary>
    public string? Endpoint { get; set; }

    public string ProvedorNormalizado => Provedor.Trim().ToLowerInvariant();

    /// <summary>O mínimo para o assistente existir.</summary>
    public bool Configurado => ProvedorNormalizado switch
    {
        OpenAI => !string.IsNullOrWhiteSpace(Modelo) && !string.IsNullOrWhiteSpace(ChaveApi),
        Foundry => !string.IsNullOrWhiteSpace(Modelo) && !string.IsNullOrWhiteSpace(Endpoint),
        _ => false,
    };

    /// <summary>O que falta, em uma frase, para quem configura o ambiente.</summary>
    public string? OQueFalta => ProvedorNormalizado switch
    {
        _ when Configurado => null,
        OpenAI => "Defina AGENTE_MODELO e AGENTE_CHAVE_API (ver .env.example).",
        Foundry => "Defina AGENTE_MODELO (a implantação) e AGENTE_ENDPOINT (o recurso do Foundry); AGENTE_CHAVE_API é opcional (ver .env.example).",
        var outro => $"AGENTE_PROVEDOR “{outro}” não é conhecido: use openai ou foundry.",
    };
}
