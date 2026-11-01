using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace MarkDocs.Documentos.Hosting;

/// <summary>
/// Os eventos AG-UI saem sem os campos nulos. O SDK .NET (AGUI.Server 1.0)
/// escreve os opcionais ausentes como <c>null</c> — <c>"usage": null</c>,
/// <c>"pendingToolCallIds": null</c> —, e o cliente TypeScript
/// (<c>@ag-ui/client</c> 1.0) valida o stream e recusa: para ele, opcional é
/// ausente, não nulo.
/// </summary>
/// <remarks>
/// Vale só para os tipos do protocolo (<c>AGUI.Abstractions</c>): o JSON do
/// resto da API — em que <c>null</c> é um valor — não muda. Quando os dois
/// SDKs concordarem, esta classe sai.
/// </remarks>
internal sealed class EventosAguiSemNulos : IPostConfigureOptions<JsonOptions>
{
    private const string Protocolo = "AGUI.Abstractions";

    public void PostConfigure(string? name, JsonOptions options)
    {
        var resolvedor = options.SerializerOptions.TypeInfoResolver;
        if (resolvedor is null) return;
        options.SerializerOptions.TypeInfoResolver = resolvedor.WithAddedModifier(SemNulos);
    }

    private static void SemNulos(JsonTypeInfo tipo)
    {
        if (tipo.Kind != JsonTypeInfoKind.Object || tipo.Type.Namespace != Protocolo) return;
        foreach (var propriedade in tipo.Properties)
        {
            var original = propriedade.ShouldSerialize;
            propriedade.ShouldSerialize = original is null
                ? (_, valor) => valor is not null
                : (dono, valor) => valor is not null && original(dono, valor);
        }
    }
}
