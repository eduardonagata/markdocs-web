using System.Text.Json.Nodes;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Abstractions.Valores;
using MarkDocs.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace MarkDocs.Abstractions.Tests;

/// <summary>
/// Os testes que a especificação 001 pede: a mesma formatação tem a mesma
/// forma nos três lugares (template.json, JSON entregue ao Core, MongoDB), e
/// <c>Aplicar</c> não diverge do mapeamento do Core.
/// </summary>
public class FormatacaoDocumentoDeNegocioTests
{
    public FormatacaoDocumentoDeNegocioTests() => SerializacaoDaFormatacao.RegistrarBson();

    // ─── A serialização ─────────────────────────────────────────────────

    /// <summary>
    /// Para cada template de fábrica, <c>DeTemplateJson → ToBson → Deserialize →
    /// ParaTemplateJson</c> devolve um JSON semanticamente igual ao original.
    /// </summary>
    [Theory, MemberData(nameof(Apoio.TemplatesDeFabrica), MemberType = typeof(Apoio))]
    public void IdaEVoltaPeloMongo(string arquivo)
    {
        var original = Apoio.Json(arquivo);
        var formatacao = Apoio.Formatacao(arquivo);

        var bson = formatacao.ToBson<Formatacao>();
        var lida = BsonSerializer.Deserialize<Formatacao>(bson);
        var devolvido = Apoio.No(lida.ParaTemplateJson(Apoio.Nome(original), Apoio.TipoDocumental(original)));

        var esperado = Apoio.No(original);
        // `id` não é formatação: ParaTemplateJson o preenche com o nome.
        esperado["id"] = Apoio.Nome(original);
        Assert.True(JsonNode.DeepEquals(esperado, devolvido),
            $"esperado:\n{esperado.ToJsonString()}\n\ndevolvido:\n{devolvido.ToJsonString()}");
    }

    /// <summary>A forma no banco é a do template.json: strings, camelCase, discriminador "familia".</summary>
    [Fact]
    public void FormaNoMongo()
    {
        var bson = Apoio.Formatacao("contrato-padrao.json").ToBsonDocument<Formatacao>();

        Assert.Equal("documento-de-negocio", bson["familia"].AsString);
        Assert.False(bson.Contains("_t"));
        Assert.Equal(1, bson["esquema"].AsInt32);
        Assert.Equal(
            new BsonDocument { ["familia"] = "Libertinus Serif", ["tamanho"] = "11pt", ["cor"] = "#1c1917", ["idioma"] = "pt" },
            bson["texto"].AsBsonDocument);
        Assert.Equal("a4", bson["pagina"]["papel"].AsString);
        Assert.Equal("32mm", bson["pagina"]["margem"]["topo"].AsString);
        Assert.Equal("centro", bson["titulos"][0]["alinhamento"].AsString);
        Assert.Equal("campo", bson["moldura"]["todas"]["rodape"]["direita"][1]["peca"].AsString);
        Assert.Equal("numeroDaPagina", bson["moldura"]["todas"]["rodape"]["direita"][1]["campo"].AsString);
    }

    [Fact]
    public void PapelUsaONomeDoTypst()
    {
        var formatacao = Apoio.Formatacao("contrato-padrao.json");
        formatacao.Pagina.Papel = Papel.Carta;
        Assert.Equal("us-letter", formatacao.ToBsonDocument<Formatacao>()["pagina"]["papel"].AsString);
        Assert.Equal("us-letter", Apoio.No(formatacao.ParaTemplateJson("x", null))["pagina"]!["papel"]!.GetValue<string>());
    }

    [Fact]
    public void CopiarNaoCompartilhaObjetos()
    {
        var original = Apoio.Formatacao("contrato-padrao.json");
        var copia = (FormatacaoDocumentoDeNegocio)original.Copiar();
        copia.Texto.Tamanho = new Medida(14, UnidadeDeMedida.Pt);
        copia.Titulos.Clear();
        Assert.Equal(new Medida(11, UnidadeDeMedida.Pt), original.Texto.Tamanho);
        Assert.Equal(3, original.Titulos.Count);
    }

    [Theory]
    [InlineData("11pt", 11, UnidadeDeMedida.Pt)]
    [InlineData("0.65em", 0.65, UnidadeDeMedida.Em)]
    [InlineData(" 20mm ", 20, UnidadeDeMedida.Mm)]
    [InlineData("30%", 30, UnidadeDeMedida.Percentual)]
    public void MedidaLe(string texto, double valor, UnidadeDeMedida unidade)
    {
        Assert.True(Medida.TentarLer(texto, out var medida));
        Assert.Equal(new Medida(valor, unidade), medida);
    }

    [Theory]
    [InlineData("20"), InlineData("calc(1pt+2pt)"), InlineData("pt"), InlineData("1,5pt"), InlineData("infpt")]
    public void MedidaRecusa(string texto) => Assert.False(Medida.TentarLer(texto, out _));

    [Theory]
    [InlineData(11, UnidadeDeMedida.Pt, "11pt"), InlineData(0.65, UnidadeDeMedida.Em, "0.65em"), InlineData(9.5, UnidadeDeMedida.Pt, "9.5pt")]
    public void MedidaEscreve(double valor, UnidadeDeMedida unidade, string texto) =>
        Assert.Equal(texto, new Medida(valor, unidade).ToString());

    [Theory]
    [InlineData("#1c1917", true), InlineData("#fff", true), InlineData("#ffff", true), InlineData("#11223344", true),
     InlineData("red", false), InlineData("#zzz", false), InlineData("#12345", false)]
    public void CorLe(string texto, bool valida) => Assert.Equal(valida, Cor.TentarLer(texto, out _));

    // ─── A ponte com o Core ─────────────────────────────────────────────

    private static TemplateCarregado Carregar(byte[] json) =>
        RegistroDeGeradores.Carregar(FormatacaoDocumentoDeNegocio.NomeDaFamilia, json);

    private static TemplateCarregado Carregar(Formatacao formatacao, byte[] original) =>
        Carregar(formatacao.ParaTemplateJson(Apoio.Nome(original), Apoio.TipoDocumental(original)));

    /// <summary>
    /// Para cada template de fábrica, <c>json → FormatacaoDocumentoDeNegocio →
    /// ParaTemplateJson → Carregar</c> produz o mesmo template que
    /// <c>json → Carregar</c>: os mesmos eixos e o mesmo PDF, byte a byte.
    /// </summary>
    /// <remarks>
    /// O <c>.typ</c> emitido é interno ao Core. O PDF é a prova equivalente: o
    /// motor é determinístico, e um <c>.typ</c> diferente muda o PDF.
    /// </remarks>
    [Theory, MemberData(nameof(Apoio.TemplatesDeFabrica), MemberType = typeof(Apoio))]
    public void FormatacaoCarregaComoOJson(string arquivo)
    {
        var json = Apoio.Json(arquivo);
        var direto = Carregar(json);
        var pelaFormatacao = Carregar(Apoio.Formatacao(arquivo), json);

        Assert.Equal(direto.Eixos, pelaFormatacao.Eixos);
        Assert.Equal(direto.FontesExigidas, pelaFormatacao.FontesExigidas);
        Assert.Equal(Apoio.Renderizador.Pdf(Apoio.Markdown, direto), Apoio.Renderizador.Pdf(Apoio.Markdown, pelaFormatacao));
    }

    /// <summary>
    /// Para cada eixo de <c>carregado.Eixos</c>, com um valor dentro e outro
    /// fora do domínio, <c>Aplicar(eixo, v)</c> produz o mesmo template que
    /// <c>ComAjustes({eixo.Id: v})</c>. Também falha quando o Core ganha um eixo
    /// que a classe ainda não conhece.
    /// </summary>
    [Theory, MemberData(nameof(Casos))]
    public void AplicarConcordaComOCore(string arquivo, string eixoId, string rotulo)
    {
        var json = Apoio.Json(arquivo);
        var carregado = Carregar(json);
        var eixo = carregado.Eixos.Single(e => e.Id == eixoId);
        var valor = ValoresDeTeste(eixo).Single(v => v.Rotulo == rotulo).Valor;

        var peloCore = carregado.ComAjustes(new Dictionary<string, ValorDeEixo> { [eixo.Id] = valor });

        var formatacao = Apoio.Formatacao(arquivo);
        formatacao.Aplicar(eixo, valor);
        var pelaFormatacao = Carregar(formatacao, json);

        Assert.Equal(peloCore.Eixos, pelaFormatacao.Eixos);
        Assert.Equal(Apoio.Renderizador.Pdf(Apoio.Markdown, peloCore), Apoio.Renderizador.Pdf(Apoio.Markdown, pelaFormatacao));
    }

    public static TheoryData<string, string, string> Casos
    {
        get
        {
            var dados = new TheoryData<string, string, string>();
            foreach (var arquivo in Apoio.ArquivosDeFabrica)
                foreach (var eixo in Carregar(Apoio.Json(arquivo)).Eixos)
                    foreach (var (rotulo, _) in ValoresDeTeste(eixo))
                        dados.Add(arquivo, eixo.Id, rotulo);
            return dados;
        }
    }

    /// <summary>Um valor válido e diferente do atual, e um fora do domínio.</summary>
    private static IEnumerable<(string Rotulo, ValorDeEixo Valor)> ValoresDeTeste(EixoDeFormatacao eixo) => eixo.Dominio switch
    {
        DominioDeEixo.Medida d =>
        [
            ("dentro", new ValorDeEixo.Medida((d.Min + d.Max) / 2, d.Unidade)),
            ("acima", new ValorDeEixo.Medida(d.Max + 100, d.Unidade)),
            ("abaixo", new ValorDeEixo.Medida(d.Min - 100, d.Unidade)),
        ],
        DominioDeEixo.Escolha d =>
        [
            ("dentro", new ValorDeEixo.Escolha(d.Opcoes[^1].Id)),
            ("fora", new ValorDeEixo.Escolha("tabloide")),
        ],
        DominioDeEixo.FamiliaTipografica d =>
        [
            ("dentro", new ValorDeEixo.FamiliaTipografica(d.Disponiveis[^1])),
            ("primeira", new ValorDeEixo.FamiliaTipografica(d.Disponiveis[0])),
            ("fora", new ValorDeEixo.FamiliaTipografica("Comic Sans MS")),
        ],
        DominioDeEixo.Cor =>
        [
            ("dentro", new ValorDeEixo.Cor("#000000")),
            ("fora", new ValorDeEixo.Cor("red")),
        ],
        DominioDeEixo.Alternancia =>
        [
            ("ligado", new ValorDeEixo.Alternancia(true)),
            ("desligado", new ValorDeEixo.Alternancia(false)),
        ],
        _ => [],
    };
}
