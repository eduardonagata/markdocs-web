using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MarkDocs.Documentos.Application.Agente;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace MarkDocs.WebApi.IntegrationTests;

/// <summary>O app com o assistente configurado, e o modelo trocado pelo <see cref="ModeloRoteirizado"/>.</summary>
public sealed class AppComAgente : AppFactory
{
    public ModeloRoteirizado Modelo { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("AGENTE_PROVEDOR", "openai");
        builder.UseSetting("AGENTE_MODELO", "modelo-de-teste");
        builder.UseSetting("AGENTE_CHAVE_API", "chave-de-teste");
        builder.ConfigureTestServices(s => s.AddKeyedSingleton<IChatClient>(Assistente.Nome, Modelo));
    }
}

/// <summary>
/// O assistente pela borda AG-UI: o endpoint, as ferramentas do backend, as
/// ferramentas da tela (que o servidor devolve sem executar) e o contexto.
/// </summary>
public class AgenteTests(AppComAgente app) : IClassFixture<AppComAgente>
{
    private readonly HttpClient _http = app.CreateClient();

    private static readonly JsonObject FerramentaDaTela = new()
    {
        ["name"] = "substituirTrecho",
        ["description"] = "Substitui um trecho exato do Markdown do documento aberto.",
        ["parameters"] = JsonNode.Parse("""
            {"type":"object","properties":{"trechoAtual":{"type":"string"},"novoTexto":{"type":"string"}},"required":["trechoAtual","novoTexto"]}
            """),
    };

    /// <summary>Os eventos AG-UI de uma execução, lidos do SSE.</summary>
    private async Task<List<JsonObject>> RodarAsync(JsonArray mensagens, JsonArray? ferramentas = null, JsonArray? contexto = null)
    {
        var entrada = new JsonObject
        {
            ["threadId"] = "conversa-1",
            ["runId"] = Guid.NewGuid().ToString("N"),
            ["messages"] = mensagens,
            ["tools"] = ferramentas ?? [],
            ["context"] = contexto ?? [],
        };
        var resposta = await _http.PostAsync("/api/agente", new StringContent(entrada.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/event-stream", resposta.Content.Headers.ContentType?.MediaType);

        var eventos = new List<JsonObject>();
        foreach (var linha in (await resposta.Content.ReadAsStringAsync()).Split('\n'))
        {
            if (linha.StartsWith("data:", StringComparison.Ordinal))
                eventos.Add(JsonNode.Parse(linha["data:".Length..].Trim())!.AsObject());
        }
        return eventos;
    }

    private static JsonObject Usuario(string texto) => new() { ["id"] = Guid.NewGuid().ToString("N"), ["role"] = "user", ["content"] = texto };

    private static string Tipo(JsonObject evento) => evento["type"]!.GetValue<string>();

    [Fact]
    public async Task StatusComOAgenteConfigurado()
    {
        var status = (await _http.GetFromJsonAsync<JsonObject>("/api/agente/status"))!;
        Assert.True(status["disponivel"]!.GetValue<bool>());
        Assert.Equal("openai", status["provedor"]!.GetValue<string>());
        Assert.Equal("modelo-de-teste", status["modelo"]!.GetValue<string>());
    }

    [Fact]
    public async Task FerramentaDoBackendEExecutadaNoServidor()
    {
        var templates = (await _http.GetFromJsonAsync<JsonArray>("/api/templates"))!;
        var criado = await _http.PostAsJsonAsync("/api/documentos",
            new { templateId = templates[0]!["id"]!.GetValue<string>(), corpo = "# Contrato do agente\n\nTexto." });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);

        var eventos = await RodarAsync([Usuario("liste os documentos")]);

        Assert.Equal("RUN_STARTED", Tipo(eventos[0]));
        Assert.Equal("RUN_FINISHED", Tipo(eventos[^1]));
        // O cliente AG-UI 1.0 recusa opcional nulo: os eventos saem sem eles.
        Assert.All(eventos, e => Assert.DoesNotContain(e, campo => campo.Value is null));
        var inicio = eventos.Single(e => Tipo(e) == "TOOL_CALL_START");
        Assert.Equal(FerramentasDoAgente.ListarDocumentos, inicio["toolCallName"]!.GetValue<string>());
        // Executada no servidor: o resultado volta no próprio stream.
        var resultado = eventos.Single(e => Tipo(e) == "TOOL_CALL_RESULT");
        Assert.Contains("Contrato do agente", resultado["content"]!.GetValue<string>());
        var texto = string.Concat(eventos.Where(e => Tipo(e) == "TEXT_MESSAGE_CONTENT").Select(e => e["delta"]!.GetValue<string>()));
        Assert.Contains("Contrato do agente", texto);
    }

    [Fact]
    public async Task FerramentaDaTelaVoltaParaOClienteSemSerExecutada()
    {
        var eventos = await RodarAsync([Usuario("troque o trecho")], [FerramentaDaTela.DeepClone()]);

        var inicio = eventos.Single(e => Tipo(e) == "TOOL_CALL_START");
        Assert.Equal("substituirTrecho", inicio["toolCallName"]!.GetValue<string>());
        var argumentos = string.Concat(eventos.Where(e => Tipo(e) == "TOOL_CALL_ARGS").Select(e => e["delta"]!.GetValue<string>()));
        Assert.Equal("As partes acordam e assinam.", JsonNode.Parse(argumentos)!["novoTexto"]!.GetValue<string>());
        Assert.Contains(eventos, e => Tipo(e) == "TOOL_CALL_END");
        // A execução termina esperando a tela: nenhum resultado vem do servidor.
        Assert.DoesNotContain(eventos, e => Tipo(e) == "TOOL_CALL_RESULT");
        Assert.Equal("RUN_FINISHED", Tipo(eventos[^1]));

        // A continuação: a tela executou a ferramenta e devolve o resultado.
        var idDaChamada = inicio["toolCallId"]!.GetValue<string>();
        var continuacao = await RodarAsync(
        [
            Usuario("troque o trecho"),
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["role"] = "assistant",
                ["toolCalls"] = new JsonArray(new JsonObject
                {
                    ["id"] = idDaChamada,
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = "substituirTrecho", ["arguments"] = argumentos },
                }),
            },
            new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["role"] = "tool",
                ["toolCallId"] = idDaChamada,
                ["content"] = "Trecho substituído na tela.",
            },
        ], [FerramentaDaTela.DeepClone()]);

        var texto = string.Concat(continuacao.Where(e => Tipo(e) == "TEXT_MESSAGE_CONTENT").Select(e => e["delta"]!.GetValue<string>()));
        Assert.Contains("Trecho substituído na tela.", texto);
    }

    [Fact]
    public async Task OContextoDaTelaVaiNasInstrucoes()
    {
        await RodarAsync([Usuario("olá")], contexto:
        [
            new JsonObject { ["description"] = "Documento aberto", ["value"] = "Contrato de locação (id 123)" },
            new JsonObject { ["description"] = "Trecho selecionado pelo usuário", ["value"] = "cláusula terceira" },
        ]);

        var (mensagens, opcoes) = app.Modelo.Chamadas.Last(c => c.Mensagens.Last().Text == "olá");
        Assert.NotNull(opcoes?.Instructions);
        Assert.Contains("assistente do MarkDocs", opcoes.Instructions);
        Assert.Contains("## Documento aberto\nContrato de locação (id 123)", opcoes.Instructions);
        Assert.Contains("## Trecho selecionado pelo usuário\ncláusula terceira", opcoes.Instructions);
        // O contexto não entra na conversa: não vira mensagem.
        Assert.DoesNotContain(mensagens, m => m.Text.Contains("cláusula terceira"));
        // As ferramentas do backend estão sempre lá.
        Assert.Contains(opcoes.Tools!, t => t.Name == FerramentasDoAgente.ConsultarDocumento);
    }
}

/// <summary>Sem <c>AGENTE_*</c>, o app sobe igual e o assistente diz o que falta.</summary>
public class AgenteSemConfiguracaoTests(AppFactory app) : IClassFixture<AppFactory>
{
    private readonly HttpClient _http = app.CreateClient();

    [Fact]
    public async Task StatusEEndpointExplicamOQueFalta()
    {
        var status = (await _http.GetFromJsonAsync<JsonObject>("/api/agente/status"))!;
        Assert.False(status["disponivel"]!.GetValue<bool>());
        Assert.Contains("AGENTE_MODELO", status["oQueFalta"]!.GetValue<string>());

        var resposta = await _http.PostAsync("/api/agente", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
    }
}
