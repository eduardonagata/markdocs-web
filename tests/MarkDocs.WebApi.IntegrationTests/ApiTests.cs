using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MarkDocs.WebApi.IntegrationTests;

/// <summary>Os endpoints da borda, ponta a ponta, na ordem em que a tela os usa.</summary>
public class ApiTests(AppFactory app) : IClassFixture<AppFactory>
{
    private readonly HttpClient _http = app.CreateClient();

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private async Task<JsonArray> TemplatesAsync() =>
        (await _http.GetFromJsonAsync<JsonArray>("/api/templates"))!;

    private async Task<JsonObject> CriarAsync(string corpo, string template = "Contrato padrão")
    {
        var id = (await TemplatesAsync()).Single(t => t!["nome"]!.GetValue<string>() == template)!["id"]!.GetValue<string>();
        var resposta = await _http.PostAsJsonAsync("/api/documentos", new { templateId = id, corpo });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<JsonObject>())!;
    }

    [Fact]
    public async Task TemplatesDeFabrica()
    {
        var templates = await TemplatesAsync();
        Assert.Equal(3, templates.Count(t => t!["deFabrica"]!.GetValue<bool>()));
        Assert.All(templates, t => Assert.Equal("documento-de-negocio", t!["familia"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AmostraDoTemplateEPdf()
    {
        var id = (await TemplatesAsync())[0]!["id"]!.GetValue<string>();
        var resposta = await _http.GetAsync($"/api/templates/{id}/amostra");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CicloDeVidaDeUmDocumento()
    {
        // Criar: escolher o template e colar o Markdown.
        var criado = await CriarAsync("# Contrato de locação\n\nAs partes acordam.\n\n![Planta](imagens/planta.png)\n");
        var id = criado["id"]!.GetValue<string>();
        Assert.Equal("Contrato de locação", criado["titulo"]!.GetValue<string>());
        Assert.Equal("Contrato", criado["tipo"]!.GetValue<string>());
        Assert.Equal(26, criado["eixos"]!.AsArray().Count);

        var tamanho = criado["eixos"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "texto.tamanho")!;
        Assert.Equal("medida", tamanho["dominio"]!["tipo"]!.GetValue<string>());
        Assert.Equal(18, tamanho["dominio"]!["max"]!.GetValue<double>());
        Assert.Equal("medida", tamanho["valor"]!["tipo"]!.GetValue<string>());

        // O PDF.
        var pdf = await _http.GetAsync($"/api/documentos/{id}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", pdf.Content.Headers.ContentDisposition?.DispositionType);

        // Alterar o Markdown e a formatação: o valor é limitado ao domínio.
        var alterado = await _http.PatchAsJsonAsync($"/api/documentos/{id}", new
        {
            corpo = "# Contrato de locação\n\nTexto revisto.\n\n![Planta](imagens/planta.png)\n",
            ajustes = new object[]
            {
                new { eixo = "texto.tamanho", valor = new { tipo = "medida", valor = 999, unidade = "pt" } },
                new { eixo = "paragrafo.justificado", valor = new { tipo = "alternancia", ligado = false } },
            },
        });
        Assert.Equal(HttpStatusCode.OK, alterado.StatusCode);
        var corpo = (await alterado.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Contains("Texto revisto.", corpo["corpo"]!.GetValue<string>());
        var eixos = corpo["eixos"]!.AsArray();
        Assert.Equal(18, eixos.Single(e => e!["id"]!.GetValue<string>() == "texto.tamanho")!["valor"]!["valor"]!.GetValue<double>());
        Assert.False(eixos.Single(e => e!["id"]!.GetValue<string>() == "paragrafo.justificado")!["valor"]!["ligado"]!.GetValue<bool>());

        // Uma imagem: o Markdown a cita por imagens/planta.png.
        using var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Png);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        formulario.Add(arquivo, "arquivo", "Planta.png");
        var enviada = await _http.PostAsync($"/api/documentos/{id}/imagens", formulario);
        Assert.Equal(HttpStatusCode.Created, enviada.StatusCode);
        var imagem = (await enviada.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("planta.png", imagem["nome"]!.GetValue<string>());
        Assert.Equal("imagens/planta.png", imagem["referencia"]!.GetValue<string>());
        Assert.Equal($"/api/documentos/{id}/imagens/planta.png", imagem["url"]!.GetValue<string>());

        var servida = await _http.GetAsync($"/api/documentos/{id}/imagens/planta.png");
        Assert.Equal(HttpStatusCode.OK, servida.StatusCode);
        Assert.Equal("image/png", servida.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await servida.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/documentos/{id}/imagens/outra.png")).StatusCode);

        // Um template a partir do documento formatado.
        var template = await _http.PostAsJsonAsync("/api/templates", new { documentoId = id, nome = "Contrato da firma" });
        Assert.Equal(HttpStatusCode.Created, template.StatusCode);
        Assert.Contains(await TemplatesAsync(), t => t!["nome"]!.GetValue<string>() == "Contrato da firma");

        // Listar e remover.
        var lista = (await _http.GetFromJsonAsync<JsonArray>("/api/documentos"))!;
        Assert.Contains(lista, d => d!["id"]!.GetValue<string>() == id);
        Assert.Equal(HttpStatusCode.NoContent, (await _http.DeleteAsync($"/api/documentos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync($"/api/documentos/{id}")).StatusCode);
    }

    [Fact]
    public async Task Erros()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/documentos/nao-existe")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync("/api/rota-inexistente")).StatusCode);

        var semTemplate = await _http.PostAsJsonAsync("/api/documentos", new { templateId = "65f0c0ffee0000000000beef", corpo = "x" });
        Assert.Equal(HttpStatusCode.NotFound, semTemplate.StatusCode);
        Assert.Contains("não existe", (await semTemplate.Content.ReadFromJsonAsync<JsonObject>())!["mensagem"]!.GetValue<string>());

        var documento = await CriarAsync("x");
        using var formulario = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "arquivo", "notas.txt" } };
        var recusada = await _http.PostAsync($"/api/documentos/{documento["id"]}/imagens", formulario);
        Assert.Equal(HttpStatusCode.BadRequest, recusada.StatusCode);

        var semNome = await _http.PostAsJsonAsync("/api/templates", new { documentoId = documento["id"]!.GetValue<string>(), nome = "" });
        Assert.Equal(HttpStatusCode.BadRequest, semNome.StatusCode);
    }

    [Fact]
    public async Task AplicarOutroTemplate()
    {
        var documento = await CriarAsync("# Recibo\n\nRecebi.");
        var recibo = (await TemplatesAsync()).Single(t => t!["nome"]!.GetValue<string>() == "Recibo padrão")!["id"]!.GetValue<string>();

        var resposta = await _http.PatchAsJsonAsync($"/api/documentos/{documento["id"]}", new { templateId = recibo });
        var corpo = (await resposta.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Recibo padrão", corpo["templateDeOrigem"]!.GetValue<string>());
        Assert.Equal("a5", corpo["eixos"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == "pagina.papel")!["valor"]!["id"]!.GetValue<string>());
    }
}
