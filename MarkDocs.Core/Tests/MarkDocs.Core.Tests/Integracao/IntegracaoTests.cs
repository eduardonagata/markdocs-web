using System.Text;
using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core.Tests.Integracao;

/// <summary>
/// Suíte 3 (§5 de <c>Docs/Testes.md</c>). A entrada é sempre o
/// <see cref="RenderizadorPDF"/>: <c>Acervo</c> para ver o que o motor recebe,
/// <c>Compor</c> para ver o que ele devolve. Nada é remontado pelo teste.
/// </summary>
[Trait("Tag", Tags.Integracao), Trait("Tag", Tags.Motor)]
public class IntegracaoTests
{
    // ─── Apoio da suíte ─────────────────────────────────────────────────

    public static DocumentoPdf Pdf(string markdown, TemplateCarregado template) =>
        new(Apoio.Renderizador().Pdf(markdown, template));

    public static double Milimetros(double pontos) => pontos / 72 * 25.4;

    public static string Texto(byte[]? arquivo) => arquivo is null ? "" : Encoding.UTF8.GetString(arquivo);

    private static Dictionary<string, ValorDeEixo> Ajuste(string id, ValorDeEixo valor) => new() { [id] = valor };

    // ─── §5.1 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Integracao), Trait("Tag", Tags.Motor)]
    public class AcervoTests
    {
        [Fact]
        public void QuatroCaminhos()
        {
            var template = Apoio.Carregar(
                Apoio.Json(("moldura.paginas.todas.fundo", new { arquivo = "timbre.png" })),
                new Dictionary<string, byte[]> { ["timbre.png"] = Apoio.Png });
            var acervo = Apoio.Renderizador().Acervo("# Oi", template);
            Assert.Equal(
                new HashSet<string> { "/documento.typ", "/template.typ", "/notiq/base.typ", "/template/timbre.png" },
                acervo.Arquivos.Keys.ToHashSet());
        }

        [Fact]
        public void EsqueletoNaoMudaEntreTemplates()
        {
            var renderizador = Apoio.Renderizador();
            const string markdown = "# Contrato\n\nCorpo.";
            var minimo = renderizador.Acervo(markdown, Apoio.TemplateMinimo());
            var captura = renderizador.Acervo(markdown, Apoio.TemplateDaCaptura());

            var documento = Texto(minimo.Arquivos.GetValueOrDefault("/documento.typ"));
            Assert.Equal(documento, Texto(captura.Arquivos.GetValueOrDefault("/documento.typ")));

            var linhas = documento.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("#import \"/notiq/base.typ\": renderizar", linhas.First());
            Assert.Equal("#import \"/template.typ\": estilo", linhas.Skip(1).First());
            Assert.Contains(linhas, l => l.StartsWith("#let documento = ("));
            Assert.Equal(new[] { "#show: estilo", "#renderizar(documento)" }, linhas.TakeLast(2));
        }

        [Fact]
        public void TemplateEOQueOGeradorEmite()
        {
            var template = Apoio.TemplateDaCaptura();
            var acervo = Apoio.Renderizador().Acervo("Texto.", template);
            Assert.Equal(template.EmitirTyp(), Texto(acervo.Arquivos.GetValueOrDefault("/template.typ")));
        }

        [Fact]
        public void BaseEORecurso()
        {
            var acervo = Apoio.Renderizador().Acervo("Texto.", Apoio.TemplateMinimo());
            Assert.Equal(Encoding.UTF8.GetBytes(Acervo.Base()), acervo.Arquivos.GetValueOrDefault("/notiq/base.typ"));
        }
    }

    // ─── §5.2 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Integracao), Trait("Tag", Tags.Motor)]
    public class Elementos
    {
        [Theory]
        [MemberData(nameof(CasosDeConteudo.NomesDosBlocos), MemberType = typeof(CasosDeConteudo))]
        public void ElementoChegaAoMotor(string nome)
        {
            var caso = CasosDeConteudo.BlocoChamado(nome);
            var template = Apoio.TemplateMinimo();
            var renderizador = Apoio.Renderizador();
            var acervo = renderizador.Acervo(caso.Markdown, template);
            Assert.Contains($"tipo: \"{caso.Tipo}\"", Texto(acervo.Arquivos.GetValueOrDefault("/documento.typ")));
            _ = renderizador.Pdf(caso.Markdown, template);
        }

        [Theory]
        [InlineData("show heading.where(level: 1)"), InlineData("show heading.where(level: 2)"),
         InlineData("show heading.where(level: 3)"), InlineData("set par("), InlineData("set table(")]
        public void ElementoTemRegra(string regra)
        {
            var acervo = Apoio.Renderizador().Acervo("", Apoio.TemplateDaCaptura());
            Assert.Contains(regra, Texto(acervo.Arquivos.GetValueOrDefault("/template.typ")));
        }

        /// <summary>Uma afirmação do estado atual: quando um destes ganhar eixo, o teste muda.</summary>
        [Theory]
        [InlineData("list"), InlineData("enum"), InlineData("quote"), InlineData("raw"), InlineData("line")]
        public void ElementosSemRegraUsamOPadrao(string elemento)
        {
            var acervo = Apoio.Renderizador().Acervo("", Apoio.TemplateDaCaptura());
            var template = Texto(acervo.Arquivos.GetValueOrDefault("/template.typ"));
            Assert.DoesNotContain($"set {elemento}(", template);
            Assert.DoesNotContain($"show {elemento}", template);
        }

        [Fact]
        public void TextoChegaAoPapel()
        {
            const string markdown = """
                # Alfa

                Bravo.

                - Charlie

                1. Delta

                > Echo

                ```
                foxtrot
                ```

                | Golf |
                |---|
                | 1 |

                ---

                Hotel.

                <p>India</p>
                """;
            using var pdf = Pdf(markdown, Apoio.TemplateMinimo());
            var papel = pdf.Texto.ToLowerInvariant();
            var posicoes = new[] { "alfa", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india" }
                .Select(marca => (Marca: marca, Posicao: papel.IndexOf(marca, StringComparison.Ordinal)))
                .ToList();
            foreach (var (marca, posicao) in posicoes)
                Assert.True(posicao >= 0, $"{marca} não chegou ao papel");
            var encontradas = posicoes.Select(p => p.Posicao).Where(p => p >= 0).ToList();
            Assert.Equal(encontradas.Order(), encontradas);
        }

        [Theory]
        [MemberData(nameof(CasosDeConteudo.PerigososComoDados), MemberType = typeof(CasosDeConteudo))]
        public void NadaEhInjetadoAtePdf(string perigoso)
        {
            using var pdf = Pdf($"antes {perigoso} depois", Apoio.TemplateMinimo());
            var papel = pdf.Texto;
            void Confere() => Assert.True(papel.Contains($"antes {perigoso} depois"), $"no papel: {papel}");

            // §6.6 do NotiqTypst (a aspa reta chegava ao papel tipográfica) não
            // ocorre aqui: o Markdig não aplica aspas tipográficas, e o Typst
            // não as aplica a texto que vem de string. A aspa chega reta, igual
            // ao digitado — e por isso o caso `"` não é problema conhecido.
            switch (perigoso)
            {
                case "🙂" or "مرحبا":
                    ProblemaConhecido.Registrar("§6.7: sem glifo nas fontes embarcadas, o texto some do papel sem aviso", Confere);
                    break;
                default:
                    Confere();
                    break;
            }
        }
    }

    // ─── §5.3 ───────────────────────────────────────────────────────────

    /// <summary>
    /// O que a captura promete, conferido no PDF, com tolerância de 0,5 mm ou
    /// 0,5 pt.
    /// </summary>
    [Trait("Tag", Tags.Integracao), Trait("Tag", Tags.Motor)]
    public class Formatacao
    {
        [Theory]
        [InlineData("a4", 210.0, 297.0), InlineData("a5", 148.0, 210.0),
         InlineData("us-letter", 215.9, 279.4), InlineData("us-legal", 215.9, 355.6)]
        public void PapelNoPdf(string papel, double largura, double altura)
        {
            var template = Apoio.TemplateMinimo().ComAjustes(Ajuste("pagina.papel", new ValorDeEixo.Escolha(papel)));
            using var pdf = Pdf("Texto.", template);
            var folha = pdf.Pagina(0);
            Assert.True(Math.Abs(Milimetros(folha.Largura) - largura) < 0.5);
            Assert.True(Math.Abs(Milimetros(folha.Altura) - altura) < 0.5);
        }

        [Fact]
        public void MargemEsquerdaNoPdf()
        {
            using var pdf = Pdf("Delta começa o parágrafo.", Apoio.TemplateDaCaptura());
            Assert.True(Math.Abs(Milimetros(pdf.Pagina(0).Caixa("Delta").MinX) - 34) < 0.5);
        }

        /// <summary>
        /// O Typst põe a altura das versais da primeira linha na margem, e a
        /// caixa do glifo passa um pouco acima dela. Por isso a medida é
        /// diferencial: com margem 0, a mesma linha sobe exatamente 30 mm, e o
        /// que sobra acima da versal se cancela.
        /// </summary>
        [Fact]
        public void MargemSuperiorNoPdf()
        {
            double TopoDaPrimeiraLinha(double margem)
            {
                var ajustes = Apoio.AjustesDaCaptura;
                ajustes["pagina.margem.topo"] = new ValorDeEixo.Medida(margem, "mm");
                var template = Apoio.TemplateMinimo().ComAjustes(ajustes);
                using var pdf = Pdf("Delta começa o parágrafo.", template);
                var pagina = pdf.Pagina(0);
                return Milimetros(pagina.Altura - pagina.Caixa("Delta").MaxY);
            }
            var deslocamento = TopoDaPrimeiraLinha(30) - TopoDaPrimeiraLinha(0);
            Assert.True(Math.Abs(deslocamento - 30) < 0.5, $"a margem deslocou a primeira linha {deslocamento} mm");
        }

        [Theory]
        [InlineData("ALFA", 24.5), InlineData("Bravo", 21.0), InlineData("Charlie", 11.0), InlineData("Delta", 9.5)]
        public void TamanhoNoPdf(string trecho, double tamanho)
        {
            const string markdown = "# Alfa\n\n## Bravo\n\n### Charlie\n\nDelta no corpo.";
            using var pdf = Pdf(markdown, Apoio.TemplateDaCaptura());
            Assert.True(Math.Abs(pdf.Pagina(0).Corpo(trecho) - tamanho) < 0.5);
        }

        [Fact]
        public void VersaisNoPdf()
        {
            using var pdf = Pdf("# Contrato de locação", Apoio.TemplateDaCaptura());
            var papel = pdf.Texto;
            Assert.Contains("CONTRATO DE LOCAÇÃO", papel);
            Assert.DoesNotContain("Contrato de locação", papel);
        }

        [Theory]
        [InlineData(true), InlineData(false)]
        public void CabecalhoRepetidoNoPdf(bool repetir)
        {
            var ajustes = Apoio.AjustesDaCaptura;
            ajustes["tabela.repetirCabecalho"] = new ValorDeEixo.Alternancia(repetir);
            var template = Apoio.TemplateMinimo().ComAjustes(ajustes);
            using var documento = Pdf("## Cronograma\n\n" + Apoio.TabelaLonga(80), template);
            Assert.True(documento.Paginas >= 2);
            for (var indice = 1; indice < documento.Paginas; indice++)
            {
                var continuacao = documento.Pagina(indice).Texto;
                Assert.True(continuacao.Contains("Descrição") == repetir, $"página {indice + 1}");
            }
        }

        [Fact]
        public void RodapeNoPdf()
        {
            using var documento = Pdf(Apoio.DocumentoDeTresPaginas, Apoio.TemplateMinimo());
            var total = documento.Paginas;
            Assert.True(total >= 3);
            for (var indice = 0; indice < total; indice++)
            {
                var pagina = documento.Pagina(indice).Texto;
                Assert.True(pagina.Contains($"Página {indice + 1} de {total}"), $"página {indice + 1}");
            }
        }

        [Fact]
        public void ACapturaDoInspetor()
        {
            using var documento = Pdf(ContratoDaCaptura, Apoio.TemplateDaCaptura());
            var primeira = documento.Pagina(0);

            Assert.True(Math.Abs(Milimetros(primeira.Largura) - 210) < 0.5);
            Assert.True(Math.Abs(Milimetros(primeira.Altura) - 297) < 0.5);
            Assert.Equal(2, documento.Paginas);
            Assert.Contains("CONTRATO DE LOCAÇÃO", primeira.Texto);
            Assert.Contains("Página 1 de 2", primeira.Texto);
        }

        /// <summary>Um contrato de duas páginas com o template da captura.</summary>
        public static string ContratoDaCaptura
        {
            get
            {
                var texto = new StringBuilder("""
                    # Contrato de locação

                    Pelo presente instrumento particular, as partes abaixo qualificadas têm entre si
                    justo e contratado a locação do imóvel descrito na cláusula primeira, que se regerá
                    pelas cláusulas e condições seguintes.

                    """);
                for (var numero = 1; numero <= 7; numero++)
                {
                    texto.Append($"""

                        ## Cláusula {numero}

                        O locatário se obriga a utilizar o imóvel exclusivamente para fins residenciais,
                        mantendo-o em perfeito estado de conservação e limpeza, e a restituí-lo, findo o
                        prazo da locação, nas mesmas condições em que o recebeu, ressalvado o desgaste
                        natural decorrente do uso regular. Qualquer benfeitoria depende de autorização
                        prévia e por escrito do locador.

                        """);
                }
                return texto.ToString();
            }
        }
    }

    // ─── §5.4 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Integracao), Trait("Tag", Tags.Motor)]
    public class Robustez
    {
        [Theory]
        [InlineData("")]
        [InlineData("#")]
        [InlineData("$x^2$")]
        [InlineData("\"aspas\" e 'apóstrofos'")]
        [InlineData("contra\\barra")]
        [InlineData("<script>alert(1)</script>")]
        [InlineData("@referencia")]
        [InlineData("#let estilo(x) = 1")]
        [InlineData("مرحبا بالعالم")]
        [InlineData("emoji 🙂 no meio")]
        [InlineData("- a\n  - b\n    - c\n      - d\n        - e\n          - f")]
        [InlineData("| vazia |\n|---|\n")]
        [InlineData("```swift\nlet x = 1\n```")]
        [InlineData("> citação\n>\n> com dois parágrafos")]
        [InlineData("***")]
        [InlineData("[link](https://exemplo.com) e ![imagem](foto.png)")]
        public void MarkdownArbitrarioCompila(string entrada)
        {
            var pdf = Apoio.Renderizador().Pdf(entrada, Apoio.TemplateMinimo());
            Assert.Equal("%PDF-"u8.ToArray(), pdf.Take(5));
        }

        [Fact]
        public void LinhaEnorme()
        {
            var linha = string.Concat(Enumerable.Repeat("palavra ", 1_250));
            Assert.Equal(10_000, linha.Length);
            var pdf = Apoio.Renderizador().Pdf(linha, Apoio.TemplateMinimo());
            Assert.NotEmpty(pdf);
        }

        /// <summary>Os limites de cada medida e cada opção de cada escolha: (id, índice do valor).</summary>
        public static IReadOnlyList<(string Id, ValorDeEixo Valor)> ValoresDoDominio =>
            Apoio.TemplateMinimo().Eixos.SelectMany(eixo => eixo.Dominio switch
            {
                DominioDeEixo.Medida m => new ValorDeEixo[] { new ValorDeEixo.Medida(m.Min, m.Unidade), new ValorDeEixo.Medida(m.Max, m.Unidade) }
                    .Select(v => (eixo.Id, v)),
                DominioDeEixo.Escolha e => e.Opcoes.Select(o => (eixo.Id, (ValorDeEixo)new ValorDeEixo.Escolha(o.Id))),
                _ => [],
            }).ToList();

        public static TheoryData<string> NomesDosValores => new(ValoresDoDominio.Select(v => $"{v.Id} = {v.Valor}"));

        [Theory, MemberData(nameof(NomesDosValores))]
        public void TodoValorDoDominioCompila(string nome)
        {
            var caso = ValoresDoDominio.Single(v => $"{v.Id} = {v.Valor}" == nome);
            var template = Apoio.TemplateMinimo().ComAjustes(Ajuste(caso.Id, caso.Valor));
            var markdown = "# Um\n\n## Dois\n\n### Três\n\nCorpo do texto.\n\n" + Apoio.TabelaLonga(3);
            _ = Apoio.Renderizador().Pdf(markdown, template);
        }

        /// <summary>O <c>today()</c> é fixo.</summary>
        [Fact]
        public void Determinismo()
        {
            var renderizador = Apoio.Renderizador();
            var template = Apoio.TemplateMinimo();
            var primeiro = renderizador.Pdf(Apoio.DocumentoDeTresPaginas, template);
            var segundo = renderizador.Pdf(Apoio.DocumentoDeTresPaginas, template);
            Assert.Equal(primeiro, segundo);
        }

        [Fact]
        public void ComposicaoNaoMudaOTemplate()
        {
            var template = Apoio.TemplateDaCaptura();
            var typ = template.EmitirTyp();
            var eixos = template.Eixos.ToList();
            _ = Apoio.Renderizador().Compor(Apoio.DocumentoDeTresPaginas, template);
            Assert.Equal(typ, template.EmitirTyp());
            Assert.Equal(eixos, template.Eixos);
        }

        [Fact]
        public void FonteQueFaltaBarradaAntes()
        {
            var semAFamilia = new RenderizadorPDF([new FonteEmbarcada("Outra Família", Apoio.Fontes[0].Dados)]);
            var erro = Apoio.Erro(() => semAFamilia.Pdf("# Oi", Apoio.TemplateMinimo()));
            var falta = Assert.IsType<ErroTypst.FonteNaoEmbarcada>(erro);
            Assert.Equal("Libertinus Serif", falta.Familia);
        }

        /// <summary>
        /// O nome declarado passa pela conferência do renderizador, mas o motor
        /// não conhece família com esse nome — e avisa, sem recusar.
        /// </summary>
        [Fact]
        public void AvisosNaoBloqueiam()
        {
            const string fantasma = "Fonte Fantasma";
            var renderizador = new RenderizadorPDF(Apoio.Fontes.Select(f => new FonteEmbarcada(fantasma, f.Dados)));
            var template = Apoio.TemplateMinimo(("fontes", new[] { fantasma }), ("texto.familia", fantasma));

            var composicao = renderizador.Compor("Texto.", template);
            Assert.Equal("%PDF-"u8.ToArray(), composicao.Pdf.Take(5));
            Assert.NotEmpty(composicao.Avisos);
        }
    }
}
