using AGUI.Abstractions;
using MarkDocs.Documentos.Application.Agente;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Infrastructure.Agente;
using Microsoft.Extensions.AI;

namespace MarkDocs.Documentos.Tests;

/// <summary>
/// O assistente sem a borda HTTP: a configuração, o endereço do Foundry, o
/// contexto da tela e as ferramentas de leitura contra um MongoDB real.
/// </summary>
[Collection("Mongo")]
public class AgenteTests(MongoFixture mongo)
{
    [Theory]
    [InlineData("openai", "gpt-4.1", "sk-x", null, true)]
    [InlineData("openai", "gpt-4.1", null, null, false)]
    [InlineData("openai", "", "sk-x", null, false)]
    [InlineData("foundry", "minha-implantacao", null, "https://recurso.openai.azure.com", true)]
    [InlineData("foundry", "minha-implantacao", "chave", null, false)]
    [InlineData("FOUNDRY ", "minha-implantacao", "chave", "https://recurso.services.ai.azure.com", true)]
    [InlineData("anthropic", "x", "y", "z", false)]
    public void OMinimoParaOAssistenteExistir(string provedor, string modelo, string? chave, string? endpoint, bool configurado)
    {
        var opcoes = new AgenteOptions { Provedor = provedor, Modelo = modelo, ChaveApi = chave, Endpoint = endpoint };
        Assert.Equal(configurado, opcoes.Configurado);
        Assert.Equal(configurado, opcoes.OQueFalta is null);
    }

    [Fact]
    public void OQueFaltaNomeiaAsVariaveis()
    {
        Assert.Contains("AGENTE_CHAVE_API", new AgenteOptions { Modelo = "gpt-4.1" }.OQueFalta);
        Assert.Contains("AGENTE_ENDPOINT", new AgenteOptions { Provedor = "foundry", Modelo = "x" }.OQueFalta);
        Assert.Contains("openai ou foundry", new AgenteOptions { Provedor = "outro", Modelo = "x" }.OQueFalta);
    }

    [Theory]
    [InlineData("https://recurso.openai.azure.com", "https://recurso.openai.azure.com/openai/v1/")]
    [InlineData("https://recurso.openai.azure.com/", "https://recurso.openai.azure.com/openai/v1/")]
    [InlineData("https://recurso.services.ai.azure.com/openai/v1", "https://recurso.services.ai.azure.com/openai/v1/")]
    [InlineData(" https://recurso.cognitiveservices.azure.com/openai/v1/ ", "https://recurso.cognitiveservices.azure.com/openai/v1/")]
    public void OFoundryEChamadoPelaApiV1(string endpoint, string esperado) =>
        Assert.Equal(esperado, ClienteDoModelo.EnderecoV1(endpoint).ToString());

    [Fact]
    public void OsDoisProvedoresCriamOClienteSemRede()
    {
        Assert.NotNull(ClienteDoModelo.Criar(new AgenteOptions { Modelo = "gpt-4.1", ChaveApi = "sk-x" }));
        Assert.NotNull(ClienteDoModelo.Criar(new AgenteOptions { Provedor = "foundry", Modelo = "x", ChaveApi = "k", Endpoint = "https://r.openai.azure.com" }));
        Assert.NotNull(ClienteDoModelo.Criar(new AgenteOptions { Provedor = "foundry", Modelo = "x", Endpoint = "https://r.openai.azure.com" }));
    }

    [Fact]
    public void OContextoDaTelaEntraNasInstrucoesESoNelas()
    {
        var entrada = new RunAgentInput
        {
            Context = [new AGUIContext { Description = "Documento aberto", Value = "Contrato de locação" }],
        };
        var opcoes = new ChatOptions { Instructions = "Base.", AdditionalProperties = new() { ["agui_input"] = entrada } };

        var comContexto = ContextoDaTelaChatClient.ComContexto(opcoes)!;

        Assert.Equal("Base.\n\n# O que está na tela do usuário agora\n\n## Documento aberto\nContrato de locação", comContexto.Instructions);
        // As opções originais não são tocadas: o contexto vale para esta chamada.
        Assert.Equal("Base.", opcoes.Instructions);
    }

    [Fact]
    public void SemContextoAsInstrucoesNaoMudam()
    {
        var opcoes = new ChatOptions { Instructions = "Base." };
        Assert.Same(opcoes, ContextoDaTelaChatClient.ComContexto(opcoes));
        Assert.Null(ContextoDaTelaChatClient.ComContexto(null));
    }

    [Fact]
    public void AsInstrucoesSaoORecursoDoAssembly() =>
        Assert.StartsWith("Você é o assistente do MarkDocs", Assistente.Instrucoes);

    [Fact]
    public async Task AsFerramentasDoBackendLeemOsDocumentos()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var criado = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "# Locação do galpão\n\nTexto."));
        await ambiente.GravarImagemAsync(criado.Id.ToString(), "planta.png", Ambiente.Png);
        var ferramentas = new FerramentasDoAgente(ambiente.Documentos, ambiente.Templates);

        Assert.Equal(
            [FerramentasDoAgente.ListarDocumentos, FerramentasDoAgente.ConsultarDocumento, FerramentasDoAgente.ListarTemplates],
            ferramentas.Todas().Select(f => f.Name));

        var todos = await ferramentas.ListarDocumentosAsync();
        Assert.Contains(todos, d => d.Titulo == "Locação do galpão");
        // O filtro ignora caixa e acento.
        Assert.Single(await ferramentas.ListarDocumentosAsync("LOCACAO"));
        Assert.Empty(await ferramentas.ListarDocumentosAsync("orçamento de obra"));

        var lido = Assert.IsType<FerramentasDoAgente.DocumentoParaOAgente>(await ferramentas.ConsultarDocumentoAsync(criado.Id.ToString()));
        Assert.Equal("# Locação do galpão\n\nTexto.", lido.Markdown);
        Assert.Equal(["imagens/planta.png"], lido.Imagens);

        // Documento que não existe é texto para o modelo corrigir, não exceção.
        var naoExiste = Assert.IsType<string>(await ferramentas.ConsultarDocumentoAsync("nao-e-um-id"));
        Assert.Contains("Liste os documentos", naoExiste);

        var templates = await ferramentas.ListarTemplatesAsync();
        Assert.Equal(3, templates.Count(t => t.DeFabrica));
    }
}
