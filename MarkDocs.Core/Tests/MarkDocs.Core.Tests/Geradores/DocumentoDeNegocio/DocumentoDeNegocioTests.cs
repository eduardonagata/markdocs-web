using System.Globalization;
using System.Text;
using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core.Tests.Geradores.DocumentoDeNegocio;

/// <summary>
/// Suíte 2 (§4 de <c>Docs/Testes.md</c>), organizada <b>como o inspetor</b>: um
/// grupo por seção da tela, um teste por controle. Cada teste parte do
/// template mínimo, ajusta por <c>ComAjustes</c> — o caminho do app — e
/// afirma o valor inicial e a regra do elemento.
/// </summary>
[Trait("Tag", Tags.Gerador)]
public class DocumentoDeNegocioTests
{
    // ─── Apoio da suíte ─────────────────────────────────────────────────

    /// <summary>O rótulo de "sem família própria" que o inspetor mostra.</summary>
    public const string Herdado = "— herdar do corpo —";

    /// <summary>O mínimo com uma segunda família declarada, para os controles de fonte.</summary>
    public static TemplateCarregado ComDuasFamilias() =>
        Apoio.TemplateMinimo(("fontes", new[] { "Libertinus Serif", "Libertinus Sans" }));

    public static EixoDeFormatacao Eixo(string id, TemplateCarregado template)
    {
        var eixo = template.Eixos.FirstOrDefault(e => e.Id == id);
        Assert.True(eixo is not null, $"não há eixo {id}");
        return eixo;
    }

    public static string Regra(string prefixo, TemplateCarregado template)
    {
        var regra = Apoio.Regra(prefixo, template.EmitirTyp());
        Assert.True(regra is not null, $"não há regra {prefixo}");
        return regra;
    }

    public static string Titulo(int nivel) => $"show heading.where(level: {nivel})";

    public static Dictionary<string, ValorDeEixo> Ajuste(string id, ValorDeEixo valor) => new() { [id] = valor };

    /// <summary>
    /// Os passos 1 e 2 da §4: o eixo nasce com o valor do <c>template.json</c>,
    /// e o ajuste chega à regra do elemento.
    /// </summary>
    public static void Confere(
        string id,
        ValorDeEixo inicial,
        ValorDeEixo ajuste,
        string regra,
        string contem,
        TemplateCarregado? partindoDe = null)
    {
        var papelaria = partindoDe ?? Apoio.TemplateMinimo();
        Assert.Equal(inicial, Eixo(id, papelaria).Valor);

        var ajustada = Regra(regra, papelaria.ComAjustes(Ajuste(id, ajuste)));
        Assert.True(ajustada.Contains(contem), $"{contem} não está em {ajustada}");
    }

    private static ValorDeEixo.Medida M(double valor, string unidade) => new(valor, unidade);
    private static ValorDeEixo.Escolha E(string id) => new(id);

    // ─── §4.1 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Catalogo
    {
        public sealed record Linha(string Grupo, string Rotulo, string Id, DominioDeEixo Dominio);

        public static IReadOnlyList<Linha> Inventario
        {
            get
            {
                var mm = new DominioDeEixo.Medida(0, 60, 1, "mm");
                var linhas = new List<Linha>
                {
                    new("Papel", "Tamanho", "pagina.papel", new DominioDeEixo.Escolha([
                        new Opcao("a4", "A4"), new Opcao("a5", "A5"),
                        new Opcao("us-letter", "Carta"), new Opcao("us-legal", "Ofício"),
                    ])),
                    new("Papel", "Margem superior", "pagina.margem.topo", mm),
                    new("Papel", "Margem inferior", "pagina.margem.base", mm),
                    new("Papel", "Margem esquerda", "pagina.margem.esquerda", mm),
                    new("Papel", "Margem direita", "pagina.margem.direita", mm),
                    new("Texto", "Fonte", "texto.familia", new DominioDeEixo.FamiliaTipografica(["Libertinus Serif"])),
                    new("Texto", "Corpo", "texto.tamanho", new DominioDeEixo.Medida(7, 18, 0.5, "pt")),
                    new("Texto", "Cor do texto", "texto.cor", new DominioDeEixo.Cor()),
                    new("Texto", "Justificar", "paragrafo.justificado", new DominioDeEixo.Alternancia()),
                    new("Texto", "Entrelinha", "paragrafo.entrelinha", new DominioDeEixo.Medida(0.4, 1.6, 0.05, "em")),
                    new("Texto", "Espaço entre parágrafos", "paragrafo.espacoEntre", new DominioDeEixo.Medida(0, 2, 0.05, "em")),
                };
                for (var nivel = 1; nivel <= 3; nivel++)
                {
                    var grupo = $"Título {nivel}";
                    linhas.AddRange([
                        new(grupo, "Fonte", $"titulos.{nivel}.familia",
                            new DominioDeEixo.FamiliaTipografica([Herdado, "Libertinus Serif"])),
                        new(grupo, "Tamanho", $"titulos.{nivel}.tamanho", new DominioDeEixo.Medida(8, 32, 0.5, "pt")),
                        new(grupo, "Alinhamento", $"titulos.{nivel}.alinhamento", new DominioDeEixo.Escolha([
                            new Opcao("esquerda", "À esquerda"),
                            new Opcao("centro", "Centralizado"),
                            new Opcao("direita", "À direita"),
                        ])),
                        new(grupo, "Caixa", $"titulos.{nivel}.caixa", new DominioDeEixo.Escolha([
                            new Opcao("normal", "Normal"), new Opcao("alta", "Versais"),
                        ])),
                    ]);
                }
                linhas.AddRange([
                    new("Tabela", "Espessura do filete", "tabela.espessura", new DominioDeEixo.Medida(0, 3, 0.1, "pt")),
                    new("Tabela", "Respiro das células", "tabela.respiro", new DominioDeEixo.Medida(0, 12, 0.5, "pt")),
                    new("Tabela", "Repetir cabeçalho nas páginas", "tabela.repetirCabecalho", new DominioDeEixo.Alternancia()),
                ]);
                return linhas;
            }
        }

        /// <summary>É o teste que muda quando um controle entra ou sai da tela.</summary>
        [Fact]
        public void InventarioDoInspetor()
        {
            var eixos = Apoio.TemplateMinimo().Eixos;
            Assert.Equal(26, eixos.Count);
            Assert.Equal(Inventario, eixos.Select(e => new Linha(e.Grupo, e.Rotulo, e.Id, e.Dominio)));
        }

        [Fact]
        public void ValoresIniciaisVemDoTemplate()
        {
            var esperados = new Dictionary<string, ValorDeEixo>
            {
                ["pagina.papel"] = E("a4"),
                ["pagina.margem.topo"] = M(28, "mm"),
                ["pagina.margem.base"] = M(24, "mm"),
                ["pagina.margem.esquerda"] = M(22, "mm"),
                ["pagina.margem.direita"] = M(22, "mm"),
                ["texto.familia"] = new ValorDeEixo.FamiliaTipografica("Libertinus Serif"),
                ["texto.tamanho"] = M(11, "pt"),
                ["texto.cor"] = new ValorDeEixo.Cor("#1c1917"),
                ["paragrafo.justificado"] = new ValorDeEixo.Alternancia(true),
                ["paragrafo.entrelinha"] = M(0.65, "em"),
                ["paragrafo.espacoEntre"] = M(8, "pt"),
                ["titulos.1.familia"] = new ValorDeEixo.FamiliaTipografica(Herdado),
                ["titulos.1.tamanho"] = M(20, "pt"),
                ["titulos.1.alinhamento"] = E("centro"),
                ["titulos.1.caixa"] = E("alta"),
                ["titulos.2.familia"] = new ValorDeEixo.FamiliaTipografica(Herdado),
                ["titulos.2.tamanho"] = M(13, "pt"),
                ["titulos.2.alinhamento"] = E("esquerda"),
                ["titulos.2.caixa"] = E("normal"),
                ["titulos.3.familia"] = new ValorDeEixo.FamiliaTipografica(Herdado),
                ["titulos.3.tamanho"] = M(11, "pt"),
                ["titulos.3.alinhamento"] = E("esquerda"),
                ["titulos.3.caixa"] = E("normal"),
                ["tabela.espessura"] = M(0.5, "pt"),
                ["tabela.respiro"] = M(4, "pt"),
                ["tabela.repetirCabecalho"] = new ValorDeEixo.Alternancia(true),
            };
            var eixos = Apoio.TemplateMinimo().Eixos;
            Assert.Equal(esperados, eixos.ToDictionary(e => e.Id, e => e.Valor));

            // Outro JSON, outro valor inicial: não há padrão compilado.
            var outro = Apoio.TemplateMinimo(("texto.tamanho", "13pt"));
            Assert.Equal(M(13, "pt"), Eixo("texto.tamanho", outro).Valor);
        }

        [Fact]
        public void FaixaDeTitulosEDaFamilia()
        {
            var soUm = Apoio.TemplateMinimo(
                ("titulos", new object[] { new { nivel = 1, tamanho = "20pt", espacoAntes = "0pt", espacoDepois = "24pt" } }));
            for (var nivel = 1; nivel <= 3; nivel++)
                Assert.Contains(soUm.Eixos, e => e.Id == $"titulos.{nivel}.tamanho");
            Assert.DoesNotContain(soUm.Eixos, e => e.Id.StartsWith("titulos.4."));
        }

        [Fact]
        public void NivelAusenteMostraHerdado()
        {
            var template = Apoio.TemplateMinimo();
            Assert.Equal(new ValorDeEixo.FamiliaTipografica(Herdado), Eixo("titulos.3.familia", template).Valor);
            Assert.Equal(M(11, "pt"), Eixo("titulos.3.tamanho", template).Valor);
            Assert.Equal(E("esquerda"), Eixo("titulos.3.alinhamento", template).Valor);
            Assert.Equal(E("normal"), Eixo("titulos.3.caixa", template).Valor);
        }
    }

    // ─── §4.2 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Papel
    {
        /// <summary>
        /// O <c>id</c> da opção, o valor no <c>template.json</c> e o nome no Typst
        /// são o mesmo texto: não há tradução.
        /// </summary>
        [Theory]
        [InlineData("a4"), InlineData("a5"), InlineData("us-letter"), InlineData("us-legal")]
        public void TamanhoDoPapel(string papel)
        {
            Confere("pagina.papel", E("a4"), E(papel), "set page(", $"paper: \"{papel}\"");

            var declarado = Apoio.TemplateMinimo(("pagina.papel", papel));
            Assert.Equal(E(papel), Eixo("pagina.papel", declarado).Valor);
            Assert.Contains($"paper: \"{papel}\"", Regra("set page(", declarado));
        }

        [Theory]
        [InlineData("topo", "top"), InlineData("base", "bottom"), InlineData("esquerda", "left"), InlineData("direita", "right")]
        public void Margem(string lado, string chave)
        {
            var originais = new Dictionary<string, string> { ["top"] = "28mm", ["bottom"] = "24mm", ["left"] = "22mm", ["right"] = "22mm" };
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste($"pagina.margem.{lado}", M(30, "mm")));
            var pagina = Regra("set page(", ajustado);

            Assert.Contains($"{chave}: 30mm", pagina);
            foreach (var (outra, valor) in originais)
            {
                if (outra == chave) continue;
                Assert.True(pagina.Contains($"{outra}: {valor}"), $"{outra} mudou em {pagina}");
            }
        }
    }

    // ─── §4.3 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Texto
    {
        [Fact]
        public void FonteDoCorpo() =>
            Confere("texto.familia", new ValorDeEixo.FamiliaTipografica("Libertinus Serif"),
                new ValorDeEixo.FamiliaTipografica("Libertinus Sans"),
                "set text(", "font: \"Libertinus Sans\"",
                partindoDe: ComDuasFamilias());

        [Fact]
        public void TamanhoDoCorpo() =>
            Confere("texto.tamanho", M(11, "pt"), M(9.5, "pt"), "set text(", "size: 9.5pt");

        [Fact]
        public void CorDoTexto() =>
            Confere("texto.cor", new ValorDeEixo.Cor("#1c1917"), new ValorDeEixo.Cor("#000000"),
                "set text(", "fill: rgb(\"#000000\")");

        [Theory]
        [InlineData(true), InlineData(false)]
        public void Justificar(bool ligado) =>
            Confere("paragrafo.justificado", new ValorDeEixo.Alternancia(true), new ValorDeEixo.Alternancia(ligado),
                "set par(", $"justify: {(ligado ? "true" : "false")}");

        [Fact]
        public void Entrelinha() =>
            Confere("paragrafo.entrelinha", M(0.65, "em"), M(0.9, "em"), "set par(", "leading: 0.9em");

        [Fact]
        public void EspacoEntreParagrafos() =>
            Confere("paragrafo.espacoEntre", M(8, "pt"), M(2, "em"), "set par(", "spacing: 2em");

        /// <summary>Na carga a cor inválida é erro (§4.9); no ajuste, é ignorada.</summary>
        [Theory]
        [InlineData("red"), InlineData("#zzz")]
        public void CorInvalidaEIgnorada(string cor)
        {
            var regraDeTexto = Regra("set text(", Apoio.TemplateMinimo().ComAjustes(Ajuste("texto.cor", new ValorDeEixo.Cor(cor))));
            Assert.Contains("fill: rgb(\"#1c1917\")", regraDeTexto);
        }

        [Fact]
        public void MedidaComDecimalNaoArredonda()
        {
            var @base = Apoio.TemplateMinimo();
            Assert.Contains("size: 9.5pt", Regra("set text(", @base.ComAjustes(Ajuste("texto.tamanho", M(9.5, "pt")))));
            Assert.Contains("size: 11pt,", Regra("set text(", @base.ComAjustes(Ajuste("texto.tamanho", M(11, "pt")))));
        }
    }

    // ─── §4.4 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Titulos
    {
        public static TheoryData<int> Niveis => new(1, 2, 3);

        public static TheoryData<int, string> NiveisEAlinhamentos => Produto(["esquerda", "centro", "direita"]);

        public static TheoryData<int, string> NiveisECaixas => Produto(["normal", "alta"]);

        private static TheoryData<int, string> Produto(string[] opcoes)
        {
            var dados = new TheoryData<int, string>();
            foreach (var nivel in new[] { 1, 2, 3 })
                foreach (var opcao in opcoes)
                    dados.Add(nivel, opcao);
            return dados;
        }

        [Theory, MemberData(nameof(Niveis))]
        public void FonteDoTitulo(int nivel) =>
            Confere($"titulos.{nivel}.familia", new ValorDeEixo.FamiliaTipografica(Herdado),
                new ValorDeEixo.FamiliaTipografica("Libertinus Sans"),
                Titulo(nivel), "font: \"Libertinus Sans\"",
                partindoDe: ComDuasFamilias());

        [Theory, MemberData(nameof(Niveis))]
        public void FonteHerdada(int nivel)
        {
            var @base = Apoio.TemplateMinimo(
                ("fontes", new[] { "Libertinus Serif", "Libertinus Sans" }),
                ("titulos.0.familia", "Libertinus Sans"),
                ("titulos.1.familia", "Libertinus Sans"));
            var ajustado = @base.ComAjustes(Ajuste($"titulos.{nivel}.familia", new ValorDeEixo.FamiliaTipografica(Herdado)));
            var regraDoNivel = Regra(Titulo(nivel), ajustado);
            Assert.Contains("text(", regraDoNivel);
            Assert.DoesNotContain("font:", regraDoNivel);
        }

        [Theory, MemberData(nameof(Niveis))]
        public void TamanhoDoTitulo(int nivel)
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste($"titulos.{nivel}.tamanho", M(24.5, "pt")));
            Assert.Contains("text(size: 24.5pt", Regra(Titulo(nivel), ajustado));
        }

        [Theory, MemberData(nameof(NiveisEAlinhamentos))]
        public void AlinhamentoDoTitulo(int nivel, string opcao)
        {
            var typst = new Dictionary<string, string> { ["esquerda"] = "left", ["centro"] = "center", ["direita"] = "right" }[opcao];
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste($"titulos.{nivel}.alinhamento", E(opcao)));
            Assert.Contains($"align({typst},", Regra(Titulo(nivel), ajustado));
        }

        [Theory, MemberData(nameof(NiveisECaixas))]
        public void CaixaDoTitulo(int nivel, string caixa)
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste($"titulos.{nivel}.caixa", E(caixa)));
            var regraDoNivel = Regra(Titulo(nivel), ajustado);
            Assert.Equal(caixa == "alta", regraDoNivel.Contains("upper(it.body)"));
            Assert.Contains("it.body", regraDoNivel);
        }

        [Theory, MemberData(nameof(Niveis))]
        public void AjusteNaoVazaParaOutroNivel(int nivel)
        {
            var @base = Apoio.TemplateMinimo();
            var ajustado = @base.ComAjustes(Ajuste($"titulos.{nivel}.tamanho", M(30, "pt")));
            for (var outro = 1; outro <= 3; outro++)
            {
                if (outro == nivel) continue;
                Assert.True(
                    Apoio.Regra(Titulo(outro), ajustado.EmitirTyp()) == Apoio.Regra(Titulo(outro), @base.EmitirTyp()),
                    $"o título {outro} mudou");
            }
        }

        [Fact]
        public void NivelAusenteNaoEmiteRegra() =>
            Assert.DoesNotContain(Titulo(3), Apoio.TemplateMinimo().EmitirTyp());

        [Fact]
        public void TocarNivelAusenteMaterializa()
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste("titulos.3.alinhamento", E("centro")));
            Assert.Equal(
                Titulo(3)
                + ": it => block(above: 1em, below: 0.5em, width: 100%, "
                + "align(center, text(size: 11pt, strong(it.body))))",
                Regra(Titulo(3), ajustado));
        }

        [Fact]
        public void NiveisAcimaDeTresSaoIgnorados()
        {
            var @base = Apoio.TemplateMinimo();
            var ajustado = @base.ComAjustes(Ajuste("titulos.4.tamanho", M(20, "pt")));
            Assert.Equal(@base.EmitirTyp(), ajustado.EmitirTyp());
            Assert.DoesNotContain("level: 4", ajustado.EmitirTyp());
        }
    }

    // ─── §4.5 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Tabela
    {
        [Fact]
        public void EspessuraDoFilete() =>
            Confere("tabela.espessura", M(0.5, "pt"), M(1.5, "pt"), "set table(", "stroke: (x, y) => (bottom: 1.5pt + rgb(");

        [Fact]
        public void FiletesSoNaBase()
        {
            var tabela = Regra("set table(", Apoio.TemplateMinimo());
            Assert.Contains("bottom:", tabela);
            foreach (var lado in new[] { "top:", "left:", "right:" })
                Assert.DoesNotContain(lado, tabela);
        }

        [Fact]
        public void RespiroDasCelulas() =>
            Confere("tabela.respiro", M(4, "pt"), M(5, "pt"), "set table(", "inset: 5pt");

        [Theory]
        [InlineData(true), InlineData(false)]
        public void RepetirCabecalho(bool ligado) =>
            Confere("tabela.repetirCabecalho", new ValorDeEixo.Alternancia(true), new ValorDeEixo.Alternancia(ligado),
                "set table.header(", $"repeat: {(ligado ? "true" : "false")}");
    }

    // ─── §4.6 ───────────────────────────────────────────────────────────

    /// <summary>O caminho de quem monta um template: altera-se o JSON, não um ajuste.</summary>
    [Trait("Tag", Tags.Gerador)]
    public class CamposSemControle
    {
        [Fact]
        public void IdiomaDoTexto()
        {
            var template = Apoio.TemplateMinimo(("texto.idioma", "en"));
            Assert.Contains("lang: \"en\"", Regra("set text(", template));
        }

        [Fact]
        public void CorDoFilete()
        {
            var template = Apoio.TemplateMinimo(("tabela.filete", "#cccccc"));
            Assert.Contains("+ rgb(\"#cccccc\")", Regra("set table(", template));
        }

        [Theory]
        [InlineData(true), InlineData(false)]
        public void CabecalhoDaTabelaForte(bool forte)
        {
            var template = Apoio.TemplateMinimo(("tabela.cabecalhoForte", forte));
            Assert.Equal(forte, template.EmitirTyp().Contains("show table.cell.where(y: 0): strong"));
        }

        /// <summary>Ausente vale <c>true</c>.</summary>
        [Theory]
        [InlineData(true), InlineData(false), InlineData(null)]
        public void TituloForte(bool? forte)
        {
            var template = Apoio.TemplateMinimo(("titulos.1.forte", forte));
            Assert.Equal(forte ?? true, Regra(Titulo(2), template).Contains("strong("));
        }

        [Fact]
        public void CorDoTitulo()
        {
            var comCor = Apoio.TemplateMinimo(("titulos.1.cor", "#57534e"));
            Assert.Contains("fill: rgb(\"#57534e\")", Regra(Titulo(2), comCor));
            Assert.DoesNotContain("fill:", Regra(Titulo(2), Apoio.TemplateMinimo()));
        }

        [Fact]
        public void EspacoDoTitulo()
        {
            var template = Apoio.TemplateMinimo(("titulos.1.espacoAntes", "22pt"), ("titulos.1.espacoDepois", "8pt"));
            Assert.Contains("block(above: 22pt, below: 8pt, width: 100%,", Regra(Titulo(2), template));
        }

        [Fact]
        public void EstiloDeTexto()
        {
            var template = Apoio.TemplateMinimo(
                ("estilosDeTexto.nota", new { tamanho = "9pt", cor = "#57534e", caixa = "alta", espacamento = "0.5pt" }));
            Assert.Contains(
                "#let est-nota(c) = text(size: 9pt, fill: rgb(\"#57534e\"), tracking: 0.5pt, upper(c))",
                template.EmitirTyp());
        }
    }

    // ─── §4.7 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Moldura
    {
        public const string RodapeDoMinimo =
            "faixa(none, none, est-nota(\"Página \" + campo-numero-da-pagina + \" de \" + campo-total-de-paginas))";

        public static Dictionary<string, object> CabecalhoComTitulo() => new()
        {
            ["direita"] = new object[] { new { peca = "campo", campo = "tituloDoDocumento" } },
        };

        [Fact]
        public void SemMolduraSemParametros()
        {
            var template = Apoio.TemplateMinimo(("moldura", null));
            var typ = template.EmitirTyp();
            var pagina = Regra("set page(", template);
            foreach (var parametro in new[] { "header:", "footer:", "background:" })
                Assert.DoesNotContain(parametro, pagina);
            Assert.DoesNotContain("#let faixa(", typ);
            Assert.DoesNotContain("campo-numero-da-pagina", typ);
        }

        [Fact]
        public void RodapeComCampos() =>
            Assert.Contains(RodapeDoMinimo, Apoio.TemplateMinimo().EmitirTyp());

        /// <summary>
        /// A resolução é rasa: uma variante declarada substitui <c>todas</c>
        /// inteira, inclusive nas partes que ela não declara.
        /// </summary>
        [Fact]
        public void TodasEABase()
        {
            // Só `todas`: primeira e demais saem iguais a ela.
            var soTodas = Apoio.TemplateMinimo().EmitirTyp();
            Assert.Contains($"#let rodape-primeira = {RodapeDoMinimo}", soTodas);
            Assert.Contains($"#let rodape-demais = {RodapeDoMinimo}", soTodas);

            // `demais` só com cabeçalho: nas demais, o rodapé de `todas` some.
            var comDemais = Apoio.TemplateMinimo(
                ("moldura.paginas.demais", new { cabecalho = CabecalhoComTitulo() })).EmitirTyp();
            Assert.Contains($"#let rodape-primeira = {RodapeDoMinimo}", comDemais);
            Assert.DoesNotContain("#let rodape-demais", comDemais);
            Assert.Contains("#let cabecalho-demais", comDemais);
            Assert.DoesNotContain("#let cabecalho-primeira", comDemais);

            // `primeira` declarada: só ela muda; as demais continuam em `todas`.
            var comPrimeira = Apoio.TemplateMinimo(
                ("moldura.paginas.primeira", new { rodape = new { direita = new object[] { new { peca = "texto", valor = "Primeira" } } } })).EmitirTyp();
            Assert.Contains("#let rodape-primeira = faixa(none, none, \"Primeira\")", comPrimeira);
            Assert.Contains($"#let rodape-demais = {RodapeDoMinimo}", comPrimeira);
        }

        [Fact]
        public void VariantesIguaisSemContext()
        {
            // Primeira e demais diferentes: uma condição sobre o contador.
            var diferentes = Regra("set page(", Apoio.TemplateMinimo(
                ("moldura.paginas.demais", new { cabecalho = CabecalhoComTitulo() })));
            Assert.Contains("context {", diferentes);
            Assert.Contains("counter(page).get().first() == 1", diferentes);

            // Iguais: o nome da faixa direto.
            var iguais = Regra("set page(", Apoio.TemplateMinimo());
            ProblemaConhecido.Registrar("§6.5: as variantes são comparadas pelo nome da faixa, que sempre difere",
                () => Assert.DoesNotContain("context {", iguais));
        }

        [Fact]
        public void Afastamento()
        {
            Assert.Contains("footer-descent: 14mm", Regra("set page(", Apoio.TemplateMinimo()));

            var cabecalho = CabecalhoComTitulo();
            cabecalho["afastamento"] = "8mm";
            var comCabecalho = Apoio.TemplateMinimo(("moldura.paginas.todas.cabecalho", cabecalho));
            Assert.Contains("header-ascent: 8mm", Regra("set page(", comCabecalho));
        }

        [Fact]
        public void FundoDoTimbre()
        {
            var template = Apoio.Carregar(
                Apoio.Json(("moldura.paginas.todas.fundo", new { arquivo = "timbre.png" })),
                new Dictionary<string, byte[]> { ["timbre.png"] = Apoio.Png });
            Assert.Contains(
                "background: image(\"/template/timbre.png\", width: 100%, height: 100%)",
                Regra("set page(", template));
        }

        [Theory]
        [InlineData("nome"), InlineData("linhas"), InlineData("logotipo")]
        public void Identidade(string campo)
        {
            var template = Apoio.Carregar(
                Apoio.Json(
                    ("identidade", new { nome = "Silva & Cia", linhas = new[] { "Rua A, 1", "São Paulo" }, logotipo = "logo.png" }),
                    ("moldura.paginas.todas.cabecalho", new
                    {
                        esquerda = new object[] { new { peca = "identidade", campo, altura = "12mm" } },
                    })),
                new Dictionary<string, byte[]> { ["logo.png"] = Apoio.Png });
            var esperado = new Dictionary<string, string>
            {
                ["nome"] = "faixa(\"Silva & Cia\", none, none)",
                ["linhas"] = "faixa(\"Rua A, 1\" + linebreak() + \"São Paulo\", none, none)",
                ["logotipo"] = "faixa(image(\"/template/logo.png\", height: 12mm), none, none)",
            }[campo];
            Assert.Contains($"#let cabecalho-primeira = {esperado}", template.EmitirTyp());
        }

        [Fact]
        public void TipoDocumentalResolvidoNoGerador()
        {
            (string, object?) peca = ("moldura.paginas.todas.rodape.direita", new object[] { new { peca = "campo", campo = "tipoDocumental" } });
            var declarado = Apoio.TemplateMinimo(peca, ("tipoDocumental", "Contrato")).EmitirTyp();
            Assert.Contains("est-nota(\"Contrato\")", declarado);
            Assert.DoesNotContain("campo-tipo", declarado);

            var ausente = Apoio.TemplateMinimo(peca).EmitirTyp();
            Assert.Contains("est-nota(\"Template de teste\")", ausente);
        }

        /// <summary>É o <c>template.json</c> mais novo aberto por uma versão mais antiga.</summary>
        [Fact]
        public void PecaDesconhecidaOmitida()
        {
            var template = Apoio.TemplateMinimo(("moldura.paginas.todas.rodape.direita", new object[]
            {
                new { peca = "texto", valor = "A" }, new { peca = "foguete" }, new { peca = "texto", valor = "B" },
            }));
            Assert.Contains("est-nota(\"A\" + \"B\")", template.EmitirTyp());
        }

        [Fact]
        public void TextoDaFaixaEscapado()
        {
            var template = Apoio.TemplateMinimo(("moldura.paginas.todas.rodape.direita", new object[]
            {
                new { peca = "texto", valor = "Silva & Cia #2 \"x\"" },
            }));
            Assert.Contains("""est-nota("Silva & Cia #2 \"x\"")""", template.EmitirTyp());
        }
    }

    // ─── §4.8 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Ajustes
    {
        /// <summary>
        /// Um ajuste válido e diferente do mínimo para cada um dos 26 eixos, a
        /// regra que ele pode mudar e o que ela passa a conter.
        /// </summary>
        public sealed record Caso(string Id, ValorDeEixo Ajuste, string Regra, string Esperado);

        public static IReadOnlyList<Caso> Casos
        {
            get
            {
                var sans = new ValorDeEixo.FamiliaTipografica("Libertinus Sans");
                var casos = new List<Caso>
                {
                    new("pagina.papel", E("us-letter"), "set page(", "paper: \"us-letter\""),
                    new("pagina.margem.topo", M(40, "mm"), "set page(", "top: 40mm"),
                    new("pagina.margem.base", M(40, "mm"), "set page(", "bottom: 40mm"),
                    new("pagina.margem.esquerda", M(40, "mm"), "set page(", "left: 40mm"),
                    new("pagina.margem.direita", M(40, "mm"), "set page(", "right: 40mm"),
                    new("texto.familia", sans, "set text(", "font: \"Libertinus Sans\""),
                    new("texto.tamanho", M(14, "pt"), "set text(", "size: 14pt"),
                    new("texto.cor", new ValorDeEixo.Cor("#000000"), "set text(", "fill: rgb(\"#000000\")"),
                    new("paragrafo.justificado", new ValorDeEixo.Alternancia(false), "set par(", "justify: false"),
                    new("paragrafo.entrelinha", M(1, "em"), "set par(", "leading: 1em"),
                    new("paragrafo.espacoEntre", M(1, "em"), "set par(", "spacing: 1em"),
                };
                for (var nivel = 1; nivel <= 3; nivel++)
                {
                    var regra = Titulo(nivel);
                    casos.AddRange([
                        new($"titulos.{nivel}.familia", sans, regra, "font: \"Libertinus Sans\""),
                        new($"titulos.{nivel}.tamanho", M(16, "pt"), regra, "text(size: 16pt"),
                        new($"titulos.{nivel}.alinhamento", E("direita"), regra, "align(right,"),
                        nivel == 1
                            ? new("titulos.1.caixa", E("normal"), regra, "strong(it.body)")
                            : new($"titulos.{nivel}.caixa", E("alta"), regra, "upper(it.body)"),
                    ]);
                }
                casos.AddRange([
                    new("tabela.espessura", M(1, "pt"), "set table(", "bottom: 1pt +"),
                    new("tabela.respiro", M(6, "pt"), "set table(", "inset: 6pt"),
                    new("tabela.repetirCabecalho", new ValorDeEixo.Alternancia(false), "set table.header(", "repeat: false"),
                ]);
                return casos;
            }
        }

        public static TheoryData<string> IdsDosCasos => new(Casos.Select(c => c.Id));

        private static Caso CasoDe(string id) => Casos.Single(c => c.Id == id);

        /// <summary>
        /// A asserção diferencial: o eixo muda a regra dele e nenhuma outra
        /// linha do <c>/template.typ</c>.
        /// </summary>
        [Theory, MemberData(nameof(IdsDosCasos))]
        public void UmEixoUmaRegra(string id)
        {
            var caso = CasoDe(id);
            var @base = ComDuasFamilias();
            var ajustado = @base.ComAjustes(Ajuste(caso.Id, caso.Ajuste));
            var antes = @base.EmitirTyp();
            var depois = ajustado.EmitirTyp();

            Assert.True(Apoio.Regra(caso.Regra, antes) != Apoio.Regra(caso.Regra, depois), "a regra não mudou");
            Assert.Equal(Apoio.Linhas(antes, caso.Regra), Apoio.Linhas(depois, caso.Regra));
        }

        /// <summary>O inspetor nunca mostra o que o papel não tem.</summary>
        [Theory, MemberData(nameof(IdsDosCasos))]
        public void EixoEEmissaoConcordam(string id)
        {
            var caso = CasoDe(id);
            var @base = ComDuasFamilias();
            var ajustado = @base.ComAjustes(Ajuste(caso.Id, caso.Ajuste));
            Assert.Equal(caso.Ajuste, Eixo(caso.Id, ajustado).Valor);
            Assert.Contains(caso.Esperado, Regra(caso.Regra, ajustado));

            // Antes de qualquer ajuste, uma medida está na unidade e na faixa
            // do domínio — e o número no papel é o mesmo do eixo.
            var inicial = Eixo(caso.Id, @base);
            if (inicial.Valor is not ValorDeEixo.Medida(var numero, var unidade)
                || inicial.Dominio is not DominioDeEixo.Medida(var minimo, var maximo, _, var unidadeDoDominio))
                return;

            void ConfereFaixa()
            {
                Assert.True(unidade == unidadeDoDominio, $"o eixo exibe {unidade}, o domínio é em {unidadeDoDominio}");
                Assert.True(numero >= minimo && numero <= maximo, $"{numero} está fora de {minimo}…{maximo}");
            }

            if (caso.Id == "paragrafo.espacoEntre")
                ProblemaConhecido.Registrar("§6.1: o limite do domínio ignora a unidade", ConfereFaixa);
            else
                ConfereFaixa();
        }

        public static readonly (string Id, double Minimo, double Maximo, string Unidade)[] Medidas =
        [
            ("pagina.margem.topo", 0, 60, "mm"), ("pagina.margem.base", 0, 60, "mm"),
            ("pagina.margem.esquerda", 0, 60, "mm"), ("pagina.margem.direita", 0, 60, "mm"),
            ("texto.tamanho", 7, 18, "pt"), ("paragrafo.entrelinha", 0.4, 1.6, "em"),
            ("paragrafo.espacoEntre", 0, 2, "em"),
            ("titulos.1.tamanho", 8, 32, "pt"), ("titulos.2.tamanho", 8, 32, "pt"), ("titulos.3.tamanho", 8, 32, "pt"),
            ("tabela.espessura", 0, 3, "pt"), ("tabela.respiro", 0, 12, "pt"),
        ];

        /// <summary>Abaixo do mínimo e acima do máximo de cada medida: (id, pedido, limite, unidade).</summary>
        public static TheoryData<string, double, double, string> Limites
        {
            get
            {
                var dados = new TheoryData<string, double, double, string>();
                foreach (var m in Medidas)
                {
                    dados.Add(m.Id, m.Minimo - 50, m.Minimo, m.Unidade);
                    dados.Add(m.Id, m.Maximo + 999, m.Maximo, m.Unidade);
                }
                return dados;
            }
        }

        [Theory, MemberData(nameof(Limites))]
        public void ForaDaFaixaEAparado(string id, double pedido, double limite, string unidade)
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste(id, M(pedido, unidade)));
            Assert.Equal(M(limite, unidade), Eixo(id, ajustado).Valor);
            var descricao = Medida.Ler(string.Create(CultureInfo.InvariantCulture, $"{limite}{unidade}"));
            Assert.NotNull(descricao);
            Assert.Contains(descricao.Value.Descricao, ajustado.EmitirTyp());
        }

        /// <summary>
        /// §6.4: a escolha inválida deveria manter o valor do template, como a
        /// família inválida já faz. Hoje vira a primeira opção.
        /// </summary>
        [Fact]
        public void EscolhaForaDoConjunto()
        {
            var carta = Apoio.TemplateMinimo(("pagina.papel", "us-letter"));
            var papel = Eixo("pagina.papel", carta.ComAjustes(Ajuste("pagina.papel", E("tabloide"))));
            var alinhamento = Eixo(
                "titulos.1.alinhamento",
                Apoio.TemplateMinimo().ComAjustes(Ajuste("titulos.1.alinhamento", E("justificado"))));

            ProblemaConhecido.Registrar("§6.4: escolha fora do conjunto vira a primeira opção, e não o valor do template", () =>
            {
                Assert.Equal(E("us-letter"), papel.Valor);
                Assert.Equal(E("centro"), alinhamento.Valor);
            });
        }

        [Theory]
        [InlineData("texto.familia"), InlineData("titulos.1.familia")]
        public void FonteNaoEmbarcadaNaoEntra(string id)
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(Ajuste(id, new ValorDeEixo.FamiliaTipografica("Comic Sans MS")));
            var typ = ajustado.EmitirTyp();
            Assert.DoesNotContain("Comic Sans", typ);

            if (id == "texto.familia")
            {
                Assert.Equal(new ValorDeEixo.FamiliaTipografica("Libertinus Serif"), Eixo(id, ajustado).Valor);
            }
            else
            {
                Assert.Equal(new ValorDeEixo.FamiliaTipografica(Herdado), Eixo(id, ajustado).Valor);
                Assert.DoesNotContain("font:", Regra(Titulo(1), ajustado));
            }
        }

        [Fact]
        public void ValorDeOutroTipoEIgnorado()
        {
            var @base = Apoio.TemplateMinimo();
            Assert.Equal(@base.EmitirTyp(), @base.ComAjustes(Ajuste("texto.tamanho", new ValorDeEixo.Alternancia(true))).EmitirTyp());
        }

        [Fact]
        public void IdDesconhecidoEIgnorado()
        {
            var ajustado = Apoio.TemplateMinimo().ComAjustes(new Dictionary<string, ValorDeEixo>
            {
                ["texto.tamanho"] = M(13, "pt"),
                ["eixo.que.nao.existe"] = new ValorDeEixo.Alternancia(true),
            });
            Assert.Equal(M(13, "pt"), Eixo("texto.tamanho", ajustado).Valor);
        }

        [Fact]
        public void OriginalIntacto()
        {
            var original = Apoio.TemplateMinimo();
            var antes = original.EmitirTyp();
            _ = original.ComAjustes(Ajuste("texto.tamanho", M(14, "pt")));
            Assert.Equal(M(11, "pt"), Eixo("texto.tamanho", original).Valor);
            Assert.Equal(antes, original.EmitirTyp());
        }

        [Fact]
        public void AjusteVazioDevolveAPapelaria()
        {
            var papelaria = Apoio.TemplateMinimo();
            Assert.Equal(papelaria.EmitirTyp(), papelaria.ComAjustes(new Dictionary<string, ValorDeEixo>()).EmitirTyp());
        }

        /// <summary>
        /// Foi um defeito real: "restaurar não restaura". Quem consome guarda o
        /// template do pacote e aplica sempre sobre ele.
        /// </summary>
        [Fact]
        public void EncadearAssaNaBase()
        {
            var papelaria = Apoio.TemplateMinimo();
            var ajustado = papelaria.ComAjustes(Ajuste("texto.tamanho", M(18, "pt")));
            var vazio = new Dictionary<string, ValorDeEixo>();
            Assert.Equal(M(18, "pt"), Eixo("texto.tamanho", ajustado.ComAjustes(vazio)).Valor);
            Assert.Equal(M(11, "pt"), Eixo("texto.tamanho", papelaria.ComAjustes(vazio)).Valor);
        }
    }

    // ─── §4.9 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Gerador)]
    public class Carga
    {
        private static string? Campo(params (string, object?)[] alteracoes) =>
            Apoio.CampoInvalido(() => Apoio.TemplateMinimo(alteracoes));

        [Fact]
        public void EsquemaAusente() => Assert.Equal("esquema", Campo(("esquema", null)));

        [Fact]
        public void EsquemaFuturo()
        {
            var erro = Apoio.Erro(() => Apoio.TemplateMinimo(("esquema", 2)));
            var futuro = Assert.IsType<ErroTypst.EsquemaFuturo>(erro);
            Assert.Equal("documento-de-negocio", futuro.Familia);
            Assert.Equal(2, futuro.Esquema);
            Assert.Equal(1, futuro.Atual);
        }

        [Fact]
        public void JsonMalformado() => Assert.Equal("template.json", Campo(("pagina", null)));

        [Theory]
        [InlineData("20"), InlineData("calc(1pt+2pt)")]
        public void MedidaInvalida(string medida) => Assert.Equal("medida", Campo(("texto.tamanho", medida)));

        /// <summary>O papel é guardado com o nome do Typst; o nome curto não existe.</summary>
        [Theory]
        [InlineData("letter"), InlineData("legal"), InlineData("carta")]
        public void PapelForaDoVocabulario(string papel) => Assert.Equal("template.json", Campo(("pagina.papel", papel)));

        [Theory]
        [InlineData("red"), InlineData("#zzz")]
        public void CorInvalida(string cor) => Assert.Equal("cor", Campo(("texto.cor", cor)));

        [Fact]
        public void FamiliaCitadaNaoDeclarada() => Assert.Equal("fontes", Campo(("titulos.0.familia", "Outra Família")));

        [Fact]
        public void NivelRepetido() => Assert.Equal("titulos", Campo(("titulos.1.nivel", 1)));

        [Fact]
        public void VarianteDePaginaInvalida() =>
            Assert.Equal("moldura.paginas", Campo(("moldura.paginas.ultima", new { rodape = new { direita = Array.Empty<object>() } })));

        [Theory]
        [InlineData("fundo"), InlineData("imagem")]
        public void AtivoAusente(string ativo)
        {
            if (ativo == "fundo")
                Assert.Equal("moldura.paginas.todas.fundo",
                    Campo(("moldura.paginas.todas.fundo", new { arquivo = "timbre.png" })));
            else
                Assert.Equal("moldura.paginas.todas",
                    Campo(("moldura.paginas.todas.rodape.direita", new object[] { new { peca = "imagem", arquivo = "logo.png" } })));
        }

        [Fact]
        public void EstiloInexistente() =>
            Assert.NotNull(Campo(("moldura.paginas.todas.rodape.estilo", "inexistente")));

        [Fact]
        public void CampoDerivadoInvalido() =>
            Assert.NotNull(Campo(("moldura.paginas.todas.rodape.direita", new object[] { new { peca = "campo", campo = "data" } })));

        [Fact]
        public void IdentidadeNaoDeclarada() =>
            Assert.NotNull(Campo(("moldura.paginas.todas.rodape.direita", new object[] { new { peca = "identidade", campo = "nome" } })));

        [Fact]
        public void AfastamentoDivergente() =>
            Assert.Equal("moldura.rodape.afastamento", Campo(("moldura.paginas", new
            {
                primeira = new { rodape = new { afastamento = "10mm", direita = Array.Empty<object>() } },
                demais = new { rodape = new { afastamento = "20mm", direita = Array.Empty<object>() } },
            })));

        [Fact]
        public void FamiliaDesconhecida()
        {
            var erro = Apoio.Erro(() => RegistroDeGeradores.Carregar("abnt", Encoding.UTF8.GetBytes(Apoio.TemplateMinimoJson)));
            var desconhecida = Assert.IsType<ErroTypst.FamiliaDesconhecida>(erro);
            Assert.Equal("abnt", desconhecida.Familia);
        }

        /// <summary>
        /// §6.3: o esquema admite <c>justificado</c> num título, e a emissão o
        /// converte em <c>left</c> sem avisar. Recusar na carga também resolveria.
        /// </summary>
        [Fact]
        public void TituloJustificado() =>
            ProblemaConhecido.Registrar("§6.3: alinhamento justificado num título sai à esquerda, em silêncio", () =>
            {
                TemplateCarregado template;
                try
                {
                    template = Apoio.TemplateMinimo(("titulos.0.alinhamento", "justificado"));
                }
                catch (ErroTypst.TemplateInvalido)
                {
                    // Recusado na carga: é uma das correções possíveis.
                    return;
                }
                Assert.DoesNotContain("align(left,", Regra(Titulo(1), template));
            });

        [Fact]
        public void CargaEhPura()
        {
            const string nota = """
                                "nota": { "tamanho": "9pt", "cor": "#57534e" }
                                """;
            string ComEstilos(params string[] ordem)
            {
                var estilos = ordem.Select(e => $"\"{e}\": {{ \"tamanho\": \"8pt\" }}").Append(nota);
                var json = Apoio.TemplateMinimoJson.Replace(nota, string.Join(", ", estilos));
                return Apoio.Carregar(Encoding.UTF8.GetBytes(json)).EmitirTyp();
            }

            Assert.Equal(Apoio.TemplateMinimo().EmitirTyp(), Apoio.TemplateMinimo().EmitirTyp());

            var umaOrdem = ComEstilos("zeta", "alfa");
            Assert.Equal(umaOrdem, ComEstilos("alfa", "zeta"));

            var posicoes = new[] { "est-alfa", "est-nota", "est-zeta" }
                .Select(e => umaOrdem.IndexOf($"#let {e}", StringComparison.Ordinal))
                .ToList();
            Assert.DoesNotContain(-1, posicoes);
            Assert.Equal(posicoes.Order(), posicoes);
        }
    }
}
