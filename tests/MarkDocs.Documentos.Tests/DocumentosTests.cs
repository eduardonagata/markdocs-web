using MarkDocs.Abstractions;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Abstractions.Valores;
using MarkDocs.Core;
using MarkDocs.Documentos.Application.Commands;
using MarkDocs.Documentos.Application.Services;
using MarkDocs.Documentos.Domain.Entities;
using MarkDocs.Documentos.Domain.Exceptions;
using MarkDocs.Documentos.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using UglyToad.PdfPig;

namespace MarkDocs.Documentos.Tests;

[Collection("Mongo")]
public class DocumentosTests(MongoFixture mongo)
{
    private const string Contrato = """
        # Contrato de locação

        Pelo presente instrumento, as partes acordam a locação do imóvel.

        ## Cláusula primeira

        O prazo é de doze meses.
        """;

    private static string Texto(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        return string.Join("\n", documento.GetPages().Select(p => string.Concat(p.Letters.Select(l => l.Value))));
    }

    // ─── Templates de fábrica ───────────────────────────────────────────

    [Fact]
    public async Task OsTemplatesDeFabricaSaoSemeadosUmaVez()
    {
        var ambiente = new Ambiente(mongo);
        var templates = await ambiente.Templates.ListarAsync();
        Assert.Equal(["Contrato padrão", "Orçamento padrão", "Recibo padrão"], templates.Select(t => t.Nome).Order());
        Assert.All(templates, t => Assert.True(t.DeFabrica));
        Assert.Equal("Contrato", templates.Single(t => t.Nome == "Contrato padrão").Tipo);

        // Semear de novo não duplica: a checagem é pela contagem.
        await new Infrastructure.Workers.SemeadorDeTemplates(
                ambiente.Templates, ambiente.Relogio,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Infrastructure.Workers.SemeadorDeTemplates>.Instance)
            .StartAsync(CancellationToken.None);
        Assert.Equal(3, (await ambiente.Templates.ListarAsync()).Count);
    }

    // ─── Criar ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CriarCopiaATemplateEDerivaOTitulo()
    {
        var ambiente = new Ambiente(mongo);
        var template = await ambiente.TemplateAsync("Contrato padrão");

        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), Contrato));

        Assert.Equal("Contrato de locação", documento.Titulo);
        Assert.Equal("Contrato", documento.Tipo);
        Assert.Equal("Contrato padrão", documento.TemplateDeOrigem);
        Assert.Equal(ambiente.Relogio.GetUtcNow(), documento.CriadoEm);
        Assert.NotSame(template.Formatacao, documento.Formatacao);

        var lido = await ambiente.Documentos.ObterAsync(documento.Id);
        Assert.NotNull(lido);
        Assert.Equal(Contrato, lido.Corpo);
        Assert.IsType<FormatacaoDocumentoDeNegocio>(lido.Formatacao);
    }

    [Fact]
    public async Task TituloInformadoPrevalece()
    {
        var ambiente = new Ambiente(mongo);
        var template = await ambiente.TemplateAsync("Recibo padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), "Sem título aqui.", "Recibo nº 12"));
        Assert.Equal("Recibo nº 12", documento.Titulo);
    }

    [Theory]
    [InlineData("# Título **forte**\n\nCorpo.", "Título forte")]
    [InlineData("Primeira linha com texto.\n\nOutra.", "Primeira linha com texto.")]
    [InlineData("", Documento.TituloPadrao)]
    [InlineData("\n\n## Seção\n", "Seção")]
    public void TituloDoMarkdown(string markdown, string titulo) =>
        Assert.Equal(titulo, Documento.TituloDoMarkdown(markdown));

    [Fact]
    public async Task TemplateInexistenteENaoEncontrado()
    {
        var ambiente = new Ambiente(mongo);
        await Assert.ThrowsAsync<TemplateNaoEncontradoException>(() =>
            ambiente.CriarAsync(new CriarDocumentoCommand(ObjectId.GenerateNewId().ToString(), "x")));
        await Assert.ThrowsAsync<TemplateNaoEncontradoException>(() =>
            ambiente.CriarAsync(new CriarDocumentoCommand("nao-e-um-id", "x")));
    }

    // ─── A forma no banco ───────────────────────────────────────────────

    [Fact]
    public async Task NoMongoAFormatacaoTemAFormaDoTemplateJson()
    {
        var ambiente = new Ambiente(mongo);
        var template = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), Contrato));

        var bruto = await ambiente.Database.GetCollection<BsonDocument>(DocumentoRepository.Colecao)
            .Find(new BsonDocument("_id", documento.Id)).SingleAsync();

        Assert.Equal("documento-de-negocio", bruto["formatacao"]["familia"].AsString);
        Assert.Equal("11pt", bruto["formatacao"]["texto"]["tamanho"].AsString);
        Assert.Equal("a4", bruto["formatacao"]["pagina"]["papel"].AsString);
        Assert.Equal(BsonType.DateTime, bruto["criadoEm"].BsonType);
        Assert.True(bruto.Contains("corpo"));
    }

    // ─── Atualizar ──────────────────────────────────────────────────────

    [Fact]
    public async Task AtualizarCorpoETitulo()
    {
        var ambiente = new Ambiente(mongo);
        var template = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), Contrato));
        ambiente.Relogio.Advance(TimeSpan.FromMinutes(5));

        var alterado = await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(
            documento.Id.ToString(), Titulo: "Contrato revisado", Corpo: "# Novo\n\nTexto novo."));

        Assert.Equal("Contrato revisado", alterado.Titulo);
        Assert.Equal("# Novo\n\nTexto novo.", alterado.Corpo);
        Assert.Equal(documento.CriadoEm, alterado.CriadoEm);
        Assert.Equal(ambiente.Relogio.GetUtcNow(), alterado.AlteradoEm);
    }

    /// <summary>Os limites vêm do Core: um valor fora do domínio é aparado, um eixo desconhecido é ignorado.</summary>
    [Fact]
    public async Task AjustesDeFormatacaoSaoLimitadosPeloCore()
    {
        var ambiente = new Ambiente(mongo);
        var template = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(template.Id.ToString(), Contrato));

        await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(documento.Id.ToString(), Ajustes:
        [
            new AjusteDeFormatacao("texto.tamanho", new ValorDeEixo.Medida(999, "pt")),
            new AjusteDeFormatacao("titulos.1.alinhamento", new ValorDeEixo.Escolha("direita")),
            new AjusteDeFormatacao("pagina.papel", new ValorDeEixo.Escolha("us-letter")),
            new AjusteDeFormatacao("eixo.que.nao.existe", new ValorDeEixo.Alternancia(true)),
        ]));

        var (lido, eixos) = await ambiente.ObterAsync(documento.Id.ToString());
        var formatacao = Assert.IsType<FormatacaoDocumentoDeNegocio>(lido.Formatacao);
        Assert.Equal(new Medida(18, UnidadeDeMedida.Pt), formatacao.Texto.Tamanho);
        Assert.Equal(Alinhamento.Direita, formatacao.Titulos.Single(t => t.Nivel == 1).Alinhamento);
        Assert.Equal(Papel.Carta, formatacao.Pagina.Papel);

        Assert.Equal(new ValorDeEixo.Medida(18, "pt"), eixos.Single(e => e.Id == "texto.tamanho").Valor);
        Assert.Equal(26, eixos.Count);
    }

    /// <summary>Aplicar um template troca a formatação inteira e mantém as imagens do Markdown.</summary>
    [Fact]
    public async Task AplicarOutroTemplateMantemAsImagens()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var recibo = await ambiente.TemplateAsync("Recibo padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), Contrato));
        await ambiente.GravarImagemAsync(documento.Id.ToString(), "planta.png", Ambiente.Png);

        var alterado = await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(documento.Id.ToString(), TemplateId: recibo.Id.ToString()));

        var formatacao = Assert.IsType<FormatacaoDocumentoDeNegocio>(alterado.Formatacao);
        Assert.Equal(Papel.A5, formatacao.Pagina.Papel);
        Assert.Equal("Recibo padrão", alterado.TemplateDeOrigem);
        Assert.Single(alterado.Imagens);
    }

    // ─── Templates a partir de documentos ───────────────────────────────

    [Fact]
    public async Task CriarTemplateDeUmDocumentoFormatado()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), Contrato));
        await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(documento.Id.ToString(), Ajustes:
            [new AjusteDeFormatacao("texto.tamanho", new ValorDeEixo.Medida(13, "pt"))]));
        await ambiente.GravarImagemAsync(documento.Id.ToString(), "planta.png", Ambiente.Png);

        var novo = await ambiente.CriarTemplateAsync(documento.Id.ToString(), "Contrato da firma");

        Assert.False(novo.DeFabrica);
        Assert.Equal("Contrato", novo.Tipo);
        Assert.Equal(documento.Id, novo.DocumentoDeOrigem);
        Assert.Empty(novo.Ativos);   // as imagens são do documento, não do template
        Assert.Equal(new Medida(13, UnidadeDeMedida.Pt), ((FormatacaoDocumentoDeNegocio)novo.Formatacao).Texto.Tamanho);

        // E ele serve para criar documentos.
        var outro = await ambiente.CriarAsync(new CriarDocumentoCommand(novo.Id.ToString(), "# Outro contrato"));
        Assert.Equal(new Medida(13, UnidadeDeMedida.Pt), ((FormatacaoDocumentoDeNegocio)outro.Formatacao).Texto.Tamanho);
        Assert.Equal(4, (await ambiente.Templates.ListarAsync()).Count);
    }

    [Fact]
    public async Task TemplateSemNomeERecusado()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), Contrato));
        await Assert.ThrowsAsync<ArgumentException>(() => ambiente.CriarTemplateAsync(documento.Id.ToString(), " "));
    }

    // ─── O PDF ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OPdfSaiDoDocumento()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), Contrato));

        var composicao = await ambiente.PdfAsync(documento.Id.ToString());

        Assert.Equal("%PDF-"u8.ToArray(), composicao.Pdf.Take(5));
        var texto = Texto(composicao.Pdf);
        Assert.Contains("CONTRATO DE LOCAÇÃO".Replace(" ", ""), texto.Replace(" ", ""));
        Assert.Contains("Página1de1", texto.Replace(" ", ""));
    }

    [Fact]
    public async Task OrcamentoImprimeOTipoDoDocumento()
    {
        var ambiente = new Ambiente(mongo);
        var orcamento = await ambiente.TemplateAsync("Orçamento padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(orcamento.Id.ToString(), "# Reforma\n\nItens."));
        await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(documento.Id.ToString(), Tipo: "Proposta"));

        var texto = Texto((await ambiente.PdfAsync(documento.Id.ToString())).Pdf);
        Assert.Contains("Proposta", texto);
    }

    // ─── Imagens ────────────────────────────────────────────────────────

    [Fact]
    public async Task ImagemGravadaChegaAoPdfEAWeb()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(),
            "# Laudo\n\n![Planta baixa](imagens/planta-baixa.png)\n"));

        var imagem = await ambiente.GravarImagemAsync(documento.Id.ToString(), "Planta Baixa.PNG", Ambiente.Png);

        Assert.Equal("planta-baixa.png", imagem.Nome);
        Assert.Equal(PapelDoAtivo.Imagem, imagem.Papel);
        Assert.True(ambiente.Armazenamento.Objetos.ContainsKey(imagem.Id));

        var servida = await ambiente.ImagemAsync(documento.Id.ToString(), "planta-baixa.png");
        Assert.Equal(Ambiente.Png, servida.Conteudo);
        Assert.Equal("image/png", servida.TipoDeConteudo);

        using var pdf = PdfDocument.Open((await ambiente.PdfAsync(documento.Id.ToString())).Pdf);
        Assert.Single(pdf.GetPage(1).GetImages());
    }

    [Fact]
    public async Task SoAsImagensDosAtivosSaoServidas()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "x"));
        await Assert.ThrowsAsync<ImagemNaoEncontradaException>(() => ambiente.ImagemAsync(documento.Id.ToString(), "outra.png"));
        await Assert.ThrowsAsync<ImagemNaoEncontradaException>(() => ambiente.ImagemAsync(documento.Id.ToString(), "../segredo.png"));
    }

    [Fact]
    public async Task SubstituirImagemApagaOObjetoAntigo()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "x"));

        var primeira = await ambiente.GravarImagemAsync(documento.Id.ToString(), "logo.png", Ambiente.Png);
        var segunda = await ambiente.GravarImagemAsync(documento.Id.ToString(), "logo.png", Ambiente.Png);

        Assert.NotEqual(primeira.Id, segunda.Id);
        Assert.False(ambiente.Armazenamento.Objetos.ContainsKey(primeira.Id));
        Assert.Single((await ambiente.Documentos.ObterAsync(documento.Id))!.Imagens);

        await ambiente.RemoverImagemAsync(documento.Id.ToString(), "logo.png");
        Assert.Empty(ambiente.Armazenamento.Objetos);
        Assert.Empty((await ambiente.Documentos.ObterAsync(documento.Id))!.Imagens);
    }

    [Theory]
    [InlineData("notas.txt")]
    [InlineData("script.exe")]
    public async Task FormatoNaoAceitoERecusado(string arquivo)
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => ambiente.GravarImagemAsync(documento.Id.ToString(), arquivo, [1, 2, 3]));
    }

    [Theory]
    [InlineData("Planta Baixa (1).PNG", "planta-baixa-1.png")]
    [InlineData("situação atual.jpeg", "situacao-atual.jpeg")]
    [InlineData("../../etc/passwd.png", "passwd.png")]
    [InlineData("C:\\fotos\\Foto.JPG", "foto.jpg")]
    [InlineData("###.png", "imagem.png")]
    public void NomeSeguro(string original, string seguro) =>
        Assert.Equal(seguro, ImagensRecebidas.NomeSeguro(original));

    // ─── Remover ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoverApagaODocumentoESuasImagens()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var documento = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), Contrato));
        await ambiente.GravarImagemAsync(documento.Id.ToString(), "planta.png", Ambiente.Png);

        await ambiente.RemoverAsync(documento.Id.ToString());

        Assert.Null(await ambiente.Documentos.ObterAsync(documento.Id));
        Assert.Empty(ambiente.Armazenamento.Objetos);
        await Assert.ThrowsAsync<DocumentoNaoEncontradoException>(() => ambiente.RemoverAsync(documento.Id.ToString()));
    }

    [Fact]
    public async Task ListarDoMaisRecenteAoMaisAntigo()
    {
        var ambiente = new Ambiente(mongo);
        var contrato = await ambiente.TemplateAsync("Contrato padrão");
        var primeiro = await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "# Primeiro"));
        ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        await ambiente.CriarAsync(new CriarDocumentoCommand(contrato.Id.ToString(), "# Segundo"));
        ambiente.Relogio.Advance(TimeSpan.FromMinutes(1));
        await ambiente.AtualizarAsync(new AtualizarDocumentoCommand(primeiro.Id.ToString(), Corpo: "# Primeiro, alterado"));

        var lista = await ambiente.Documentos.ListarAsync();
        Assert.Equal(["Primeiro", "Segundo"], lista.Select(d => d.Titulo));
    }
}
