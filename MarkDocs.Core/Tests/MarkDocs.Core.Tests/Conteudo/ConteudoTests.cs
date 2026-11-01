using System.Text.RegularExpressions;
using MarkDocs.Core.Conteudo;
using MarkDocs.Core.Nucleo;
using static MarkDocs.Core.Tests.Sonda;

namespace MarkDocs.Core.Tests.Conteudo;

/// <summary>
/// Suíte 1 (§3 de <c>Docs/Testes.md</c>). <b>Geral</b>: não conhece família
/// nenhuma. Cada elemento de Markdown vira um elemento Typst, sempre o mesmo.
/// </summary>
[Trait("Tag", Tags.Conteudo)]
public class ConteudoTests
{
    // ─── §3.1 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Conteudo)]
    public class Blocos
    {
        [Theory]
        [MemberData(nameof(CasosDeConteudo.NomesDosBlocos), MemberType = typeof(CasosDeConteudo))]
        public void BlocoViraArvore(string nome)
        {
            var caso = CasosDeConteudo.BlocoChamado(nome);
            Assert.Equal(caso.Arvore, ArvoreDeMarkdown.Converter(caso.Markdown));
        }

        [Theory]
        [MemberData(nameof(CasosDeConteudo.NomesDosBlocos), MemberType = typeof(CasosDeConteudo))]
        public void BlocoViraLiteral(string nome)
        {
            var caso = CasosDeConteudo.BlocoChamado(nome);
            var literal = ValorTypst.Literal(ArvoreDeMarkdown.Converter(caso.Markdown));
            Assert.Contains($"tipo: {LiteralTypst.Texto(caso.Tipo)}", literal);
            foreach (var trecho in caso.NoLiteral ?? [])
                Assert.True(literal.Contains(trecho), $"falta {trecho} em {literal}");
        }

        [Theory, Trait("Tag", Tags.Motor)]
        [MemberData(nameof(CasosDeConteudo.NomesDosBlocos), MemberType = typeof(CasosDeConteudo))]
        public void BlocoViraElemento(string nome)
        {
            var caso = CasosDeConteudo.BlocoChamado(nome);
            if (caso.ProblemaConhecido is { } problema)
                ProblemaConhecido.Registrar(problema, () => Verificar(caso.Markdown, caso.Elemento));
            else
                Verificar(caso.Markdown, caso.Elemento);
        }

        [Fact, Trait("Tag", Tags.Motor)]
        public void AlinhamentosDaTabela()
        {
            const string markdown = "| a | b | c | d |\n|:--|:-:|--:|---|\n| 1 | 2 | 3 | 4 |";
            var tabela = Assert.IsType<BlocoDoc.Tabela>(ArvoreDeMarkdown.Converter(markdown).FirstOrDefault());
            Assert.Equal(new[] { Alinhamento.Esquerda, Alinhamento.Centro, Alinhamento.Direita, Alinhamento.Esquerda }, tabela.Alinhamentos);

            Verificar(markdown,
                Igual("(c.align)(0, 0)", "left"),
                Igual("(c.align)(1, 0)", "center"),
                Igual("(c.align)(2, 0)", "right"),
                Igual("(c.align)(3, 0)", "left"));
        }

        [Fact, Trait("Tag", Tags.Motor)]
        public void TabelaColunasIguais() =>
            Verificar("| a | b | c |\n|---|---|---|\n| 1 | 2 | 3 |",
                Igual("c.columns", "(1fr, 1fr, 1fr)"));

        /// <summary>
        /// Cabeçalho de duas colunas, linha de uma: a linha ganha uma célula
        /// vazia. O cabeçalho é um filho da tabela; as células, os outros.
        /// </summary>
        [Fact, Trait("Tag", Tags.Motor)]
        public void TabelaLinhaCurtaECompletada()
        {
            const string markdown = "| A | B |\n|---|---|\n| 1 |";
            var tabela = Assert.IsType<BlocoDoc.Tabela>(ArvoreDeMarkdown.Converter(markdown).FirstOrDefault());
            Assert.Equal(2, tabela.Linhas.FirstOrDefault()?.Count);
            Verificar(markdown,
                Igual("c.columns.len()", "2"),
                Igual("c.children.len()", "3"));
        }

        [Fact, Trait("Tag", Tags.Motor)]
        public void TabelaLinhaLongaECortada()
        {
            const string markdown = "| A | B |\n|---|---|\n| 1 | 2 | 3 |";
            var tabela = Assert.IsType<BlocoDoc.Tabela>(ArvoreDeMarkdown.Converter(markdown).FirstOrDefault());
            Assert.Equal(2, tabela.Linhas.FirstOrDefault()?.Count);
            Verificar(markdown,
                Igual("c.columns.len()", "2"),
                Igual("c.children.len()", "3"));
        }
    }

    // ─── §3.2 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Conteudo)]
    public class Inlines
    {
        [Theory]
        [MemberData(nameof(CasosDeConteudo.NomesDosInlines), MemberType = typeof(CasosDeConteudo))]
        public void InlineViraArvore(string nome)
        {
            var caso = CasosDeConteudo.InlineChamado(nome);
            Assert.Equal(new BlocoDoc[] { new BlocoDoc.Paragrafo(caso.Arvore) }, ArvoreDeMarkdown.Converter(caso.Markdown));
        }

        [Theory]
        [MemberData(nameof(CasosDeConteudo.NomesDosInlines), MemberType = typeof(CasosDeConteudo))]
        public void InlineViraLiteral(string nome)
        {
            var caso = CasosDeConteudo.InlineChamado(nome);
            var literal = ValorTypst.Literal(ArvoreDeMarkdown.Converter(caso.Markdown));
            Assert.Contains($"tipo: {LiteralTypst.Texto(caso.Tipo)}", literal);
        }

        [Theory, Trait("Tag", Tags.Motor)]
        [MemberData(nameof(CasosDeConteudo.NomesDosInlines), MemberType = typeof(CasosDeConteudo))]
        public void InlineViraElemento(string nome)
        {
            var caso = CasosDeConteudo.InlineChamado(nome);
            Verificar(caso.Markdown, caso.Elemento);
        }

        [Fact]
        public void InlinesAninham() =>
            Assert.Equal(
                new BlocoDoc[] { new BlocoDoc.Paragrafo([
                    new InlineDoc.Forte([new InlineDoc.Texto("texto "), new InlineDoc.Enfase([new InlineDoc.Texto("misto")])]),
                ]) },
                ArvoreDeMarkdown.Converter("**texto _misto_**"));

        /// <summary>O Acervo não tem os arquivos do autor, e o motor não tem I/O.</summary>
        [Fact, Trait("Tag", Tags.Motor)]
        public void ImagemNaoTemArquivo()
        {
            const string markdown = "![alt](f.png)";
            var literal = ValorTypst.Literal(ArvoreDeMarkdown.Converter(markdown));
            Assert.DoesNotContain("f.png", literal);
            Verificar(markdown, Igual("descendentes(c).any(d => d.func() == image)", "false"));
        }
    }

    // ─── §3.3 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Conteudo)]
    public class Literal
    {
        /// <summary>
        /// Duas verificações: no C#, o texto não sai das aspas; no Typst, o valor
        /// lido é o digitado, ponto de código por ponto de código — e a
        /// comparação não passa pelo escape que está sendo testado.
        /// </summary>
        [Theory, Trait("Tag", Tags.Motor)]
        [MemberData(nameof(CasosDeConteudo.PerigososComoDados), MemberType = typeof(CasosDeConteudo))]
        public void NadaEhInjetado(string perigoso)
        {
            foreach (var literal in new[]
                     {
                         ValorTypst.Literal(ArvoreDeMarkdown.Converter(perigoso)),
                         ValorTypst.Literal([new BlocoDoc.Paragrafo([new InlineDoc.Texto(perigoso)])]),
                     })
            {
                var fora = ForaDasAspas(literal);
                Assert.True(fora is not null, $"aspas desbalanceadas em {literal}");
                Assert.True(
                    fora.All(c => char.IsLower(c) || char.IsNumber(c) || char.IsWhiteSpace(c) || "():,-".Contains(c)),
                    $"sobrou fora das aspas: {fora}");
            }

            Verificar([new BlocoDoc.Paragrafo([new InlineDoc.Texto(perigoso)])],
                Igual(
                    "documento.blocos.at(0).filhos.at(0).valor.codepoints().map(str.to-unicode)",
                    PontosDeCodigo(perigoso)),
                Igual("c.body.text.codepoints().map(str.to-unicode)", PontosDeCodigo(perigoso)));
        }

        [Fact]
        public void EscapeDeDoisCaracteres()
        {
            Assert.Equal("""
                         "a\"b\\c"
                         """, LiteralTypst.Texto("""a"b\c"""));
            Assert.Equal("\"Silva & Cia #2\"", LiteralTypst.Texto("Silva & Cia #2"));

            // Todo o ASCII imprimível fora os dois atravessa intacto.
            for (var valor = 0x20; valor <= 0x7E; valor++)
            {
                var caractere = ((char)valor).ToString();
                if (caractere is "\\" or "\"") continue;
                Assert.Equal($"\"{caractere}\"", LiteralTypst.Texto(caractere));
            }
        }

        [Fact, Trait("Tag", Tags.Motor)]
        public void ControlesSaoEscapados()
        {
            const string bruto = "a\nb\rc\td\u0001e\u001Ff\u007Fg";
            var literal = LiteralTypst.Texto(bruto);

            Assert.DoesNotContain(literal.EnumerateRunes(), r => r.Value < 0x20 || r.Value == 0x7F);
            foreach (var sequencia in new[] { @"\n", @"\r", @"\t", @"\u{1}", @"\u{1F}", @"\u{7F}" })
                Assert.Contains(sequencia, literal);

            Verificar([new BlocoDoc.Paragrafo([new InlineDoc.Texto(bruto)])],
                Igual(
                    "documento.blocos.at(0).filhos.at(0).valor.codepoints().map(str.to-unicode)",
                    PontosDeCodigo(bruto)));
        }

        /// <summary>No Typst, <c>(x)</c> é <c>x</c> entre parênteses; <c>(x,)</c> é um arranjo.</summary>
        [Fact]
        public void ArranjoDeUmElemento()
        {
            Assert.Contains("blocos: ((tipo: \"regra\"),)", ValorTypst.Literal([new BlocoDoc.Regra()]));
            Assert.Contains("blocos: ()", ValorTypst.Literal([]));
        }

        [Theory]
        [InlineData("")]
        [InlineData("# Título")]
        [InlineData("Texto.")]
        [InlineData("| a |\n|---|\n| 1 |")]
        public void LiteralDeclaraVersao(string markdown)
        {
            var literal = ValorTypst.Literal(ArvoreDeMarkdown.Converter(markdown));
            var campos = literal.TrimStart('(', ' ', '\n', '\t', '\r');
            Assert.StartsWith($"versao: {ValorTypst.VersaoDoVocabulario},", campos);
        }

        /// <summary>Os caracteres fora de strings, ou <c>null</c> se uma string não fecha.</summary>
        private static string? ForaDasAspas(string literal)
        {
            var fora = new System.Text.StringBuilder();
            var emTexto = false;
            var escapado = false;
            foreach (var caractere in literal)
            {
                if (emTexto)
                {
                    if (escapado) escapado = false;
                    else if (caractere == '\\') escapado = true;
                    else if (caractere == '"') emTexto = false;
                }
                else if (caractere == '"')
                {
                    emTexto = true;
                }
                else
                {
                    fora.Append(caractere);
                }
            }
            return emTexto ? null : fora.ToString();
        }
    }

    // ─── §3.4 ───────────────────────────────────────────────────────────

    [Trait("Tag", Tags.Conteudo)]
    public class Base
    {
        [Fact, Trait("Tag", Tags.Motor)]
        public void VersaoDivergenteFalhaLegivel()
        {
            var literal = ValorTypst.Literal([new BlocoDoc.Regra()]).Replace(
                $"versao: {ValorTypst.VersaoDoVocabulario},",
                "versao: 99,");
            var erro = Apoio.Erro(() => Compor(literal));
            var compilacao = Assert.IsType<ErroTypst.Compilacao>(erro);
            Assert.Contains(compilacao.Diagnosticos, d => d.Mensagem.Contains("Vocabulário incompatível"));
        }

        [Fact]
        public void VersoesConcordam()
        {
            var linha = Acervo.Base().Split('\n').FirstOrDefault(l => l.StartsWith("#let versao-do-vocabulario"));
            Assert.NotNull(linha);
            var numero = linha.Split('=').Last().Trim();
            Assert.Equal(ValorTypst.VersaoDoVocabulario.ToString(), numero);
        }

        /// <summary>Feio, mas visível: em vermelho, e nunca silêncio.</summary>
        [Fact, Trait("Tag", Tags.Motor)]
        public void NoDesconhecidoEVisivel()
        {
            var literal = ValorTypst.Literal([new BlocoDoc.Regra()]).Replace("\"regra\"", "\"inexistente\"");
            // `text(fill: red)[…]` vira um conteúdo estilizado em volta da
            // mensagem: é o que o Typst deixa ver do vermelho.
            VerificarLiteral(literal,
                Igual("c.func()", "(text(fill: red)[x]).func()"),
                Igual("repr(c).contains(\"bloco desconhecido\")", "true"),
                Igual("repr(c).contains(\"inexistente\")", "true"));

            using var pdf = new DocumentoPdf(Compor(literal, Mostrar()));
            Assert.Contains("bloco desconhecido: inexistente", pdf.Texto);
        }

        [Fact]
        public void BaseNaoDecideAparencia()
        {
            var codigo = string.Join("\n", Acervo.Base()
                .Split('\n')
                .Select(l => l.Split("//", 2)[0]));
            Assert.DoesNotMatch(new Regex(@"(^|[^\w.-])(set|show)\s"), codigo);
        }
    }
}
