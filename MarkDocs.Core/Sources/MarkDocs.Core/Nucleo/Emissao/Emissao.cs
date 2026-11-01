namespace MarkDocs.Core.Nucleo;

/// <summary><b>O único lugar do pacote que escreve Typst.</b></summary>
/// <remarks>
/// Não é refinamento, é o que torna a arquitetura de N geradores sustentável:
/// quando o Typst reformular <c>set</c>/<c>show</c>, a sintaxe nova entra aqui e
/// os geradores não mudam. O custo de subir o motor volta a ser constante, e
/// não linear no número de famílias.
///
/// A revisão de código pergunta uma coisa: <i>há aspas de Typst fora deste
/// diretório?</i>
/// </remarks>
internal static class Emissao
{
    // ─── Primitivas ─────────────────────────────────────────────────────

    public static string Literal(string texto) => LiteralTypst.Texto(texto);

    public static string Medida(Medida m) => m.Descricao;

    public static string Cor(Cor c) => $"rgb({Literal(c.Hexadecimal)})";

    public static string Alinhamento(AlinhamentoHorizontal a) => a switch
    {
        AlinhamentoHorizontal.Centro => "center",
        AlinhamentoHorizontal.Direita => "right",
        _ => "left",
    };

    private static string Booleano(bool valor) => valor ? "true" : "false";

    // ─── Regras de estilo ───────────────────────────────────────────────

    public static string RegraDePagina(Papel papel, Margem margem, Moldura moldura)
    {
        var argumentos = new List<string>
        {
            $"paper: {Literal(papel.Texto())}",
            $"margin: (top: {Medida(margem.Topo)}, bottom: {Medida(margem.Base)}, "
                + $"left: {Medida(margem.Esquerda)}, right: {Medida(margem.Direita)})",
        };

        if (moldura.TemFundo)
        {
            argumentos.Add("background: " + PorVariante(
                primeira: moldura.Primeira.Fundo is { } p ? ImagemDeFundo(p) : "none",
                demais: moldura.Demais.Fundo is { } d ? ImagemDeFundo(d) : "none"));
        }

        // Seção ausente significa parâmetro não emitido. Quem tem um timbre
        // que já traz cabeçalho e rodapé desenhados omite as seções, e não
        // existe um "cabeçalho vazio" a desligar depois.
        if (moldura.TemCabecalho)
        {
            argumentos.Add("header: " + PorVariante(
                primeira: NomeDaFaixa(moldura.Primeira.Cabecalho, "cabecalho-primeira"),
                demais: NomeDaFaixa(moldura.Demais.Cabecalho, "cabecalho-demais")));
            if (moldura.AfastamentoDoCabecalho is { } afastamento)
                argumentos.Add($"header-ascent: {Medida(afastamento)}");
        }

        if (moldura.TemRodape)
        {
            argumentos.Add("footer: " + PorVariante(
                primeira: NomeDaFaixa(moldura.Primeira.Rodape, "rodape-primeira"),
                demais: NomeDaFaixa(moldura.Demais.Rodape, "rodape-demais")));
            if (moldura.AfastamentoDoRodape is { } afastamento)
                argumentos.Add($"footer-descent: {Medida(afastamento)}");
        }

        return "set page(\n    " + string.Join(",\n    ", argumentos) + ",\n  )";
    }

    public static string RegraDeTexto(string familia, Medida tamanho, Cor cor, string idioma) =>
        $"set text(font: {Literal(familia)}, size: {Medida(tamanho)}, "
        + $"fill: {Cor(cor)}, lang: {Literal(idioma)})";

    public static string RegraDeParagrafo(bool justificado, Medida entrelinha, Medida espacoEntre) =>
        $"set par(justify: {Booleano(justificado)}, leading: {Medida(entrelinha)}, "
        + $"spacing: {Medida(espacoEntre)})";

    public static string RegraDeTitulo(
        int nivel,
        string? familia,
        Medida tamanho,
        AlinhamentoHorizontal alinhamento,
        bool caixaAlta,
        bool forte,
        Cor? cor,
        Medida espacoAntes,
        Medida espacoDepois)
    {
        var corpo = caixaAlta ? "upper(it.body)" : "it.body";
        if (forte) corpo = $"strong({corpo})";

        var argumentosDeTexto = new List<string> { $"size: {Medida(tamanho)}" };
        if (familia is not null) argumentosDeTexto.Add($"font: {Literal(familia)}");
        if (cor is { } c) argumentosDeTexto.Add($"fill: {Cor(c)}");
        var texto = $"text({string.Join(", ", argumentosDeTexto)}, {corpo})";

        var caixa = $"block(above: {Medida(espacoAntes)}, below: {Medida(espacoDepois)}, "
                    + $"width: 100%, align({Alinhamento(alinhamento)}, {texto}))";

        return $"show heading.where(level: {nivel}): it => {caixa}";
    }

    public static string RegraDeTabela(
        Cor filete,
        Medida espessura,
        Medida respiro,
        bool cabecalhoForte,
        bool repetirCabecalho)
    {
        var regras = new List<string>
        {
            $"set table(stroke: (x, y) => (bottom: {Medida(espessura)} + {Cor(filete)}), "
                + $"inset: {Medida(respiro)})",
            $"set table.header(repeat: {Booleano(repetirCabecalho)})",
        };
        if (cabecalhoForte)
            regras.Add("show table.cell.where(y: 0): strong");
        return string.Join("\n  ", regras);
    }

    // ─── Declarações ────────────────────────────────────────────────────

    /// <summary>Um <c>estilosDeTexto</c> vira uma função. É a única indireção do esquema, e é por nome.</summary>
    public static string EstiloDeTexto(
        string nome,
        Medida? tamanho,
        Cor? cor,
        bool caixaAlta,
        Medida? espacamento,
        string? familia)
    {
        var argumentos = new List<string>();
        if (familia is not null) argumentos.Add($"font: {Literal(familia)}");
        if (tamanho is { } t) argumentos.Add($"size: {Medida(t)}");
        if (cor is { } c) argumentos.Add($"fill: {Cor(c)}");
        if (espacamento is { } e) argumentos.Add($"tracking: {Medida(e)}");

        var corpo = caixaAlta ? "upper(c)" : "c";
        var lista = string.Join(", ", argumentos.Append(corpo));
        return $"#let {IdentificadorDeEstilo(nome)}(c) = text({lista})";
    }

    public static string IdentificadorDeEstilo(string nome) => "est-" + Sanear(nome);

    /// <summary>
    /// A grade é <c>(1fr, auto, 1fr)</c>, <b>não</b> três colunas iguais: com
    /// colunas iguais, uma fatia esquerda longa empurra a central e ela deixa
    /// de estar centralizada na página.
    /// </summary>
    public static string DeclaracaoDaFaixa() =>
        "#let faixa(esq, meio, dir) = grid(\n"
        + "  columns: (1fr, auto, 1fr),\n"
        + "  align(left, esq), align(center, meio), align(right, dir),\n"
        + ")";

    public static string DeclaracaoDeFaixa(string nome, Faixa faixa)
    {
        var fatias = new[] { faixa.Esquerda, faixa.Centro, faixa.Direita }
            .Select(fatia => Vestir(Conteudo(fatia), faixa.Estilo));
        return $"#let {nome} = faixa({string.Join(", ", fatias)})";
    }

    public static string DeclaracoesDerivadas() =>
        "#let campo-titulo-do-documento = context {\n"
        + "  let t = query(heading.where(level: 1))\n"
        + "  if t.len() > 0 { t.first().body }\n"
        + "}\n"
        + "#let campo-numero-da-pagina = context counter(page).display()\n"
        + "#let campo-total-de-paginas = context counter(page).final().first()";

    /// <summary>
    /// <c>estilo(body)</c> — o contrato de saída de todo gerador. <c>body</c> é o
    /// ponto onde o conteúdo entra; sem ele o documento sai vazio.
    /// </summary>
    public static string FuncaoEstilo(IEnumerable<string> regras) =>
        "#let estilo(body) = {\n"
        + "  " + string.Join("\n  ", regras) + "\n"
        + "\n"
        + "  body\n"
        + "}";

    // ─── Peças ──────────────────────────────────────────────────────────

    public static string Conteudo(IReadOnlyList<PecaDeConteudo> pecas)
    {
        if (pecas.Count == 0) return "none";
        var partes = pecas.Select(Peca).OfType<string>().ToList();
        return partes.Count == 0 ? "none" : string.Join(" + ", partes);
    }

    /// <summary>
    /// Peça desconhecida é <b>omitida do papel</b> e relatada na interface — e
    /// não pintada de vermelho como um bloco de conteúdo. Um aviso vermelho no
    /// rodapé apareceria em <b>todas</b> as páginas de um contrato que alguém
    /// vai assinar. Aqui a omissão é estrutural: a hierarquia é fechada.
    /// </summary>
    private static string? Peca(PecaDeConteudo peca) => peca switch
    {
        PecaDeConteudo.Texto t => t.Valor.Length == 0 ? null : Literal(t.Valor),
        PecaDeConteudo.Campo c => c.Qual switch
        {
            CampoDerivado.TituloDoDocumento => "campo-titulo-do-documento",
            CampoDerivado.NumeroDaPagina => "campo-numero-da-pagina",
            CampoDerivado.TotalDePaginas => "campo-total-de-paginas",
            // Constante por template, resolvido pelo gerador em texto. Se
            // chegar aqui é porque não foi resolvido: omite.
            _ => null,
        },
        PecaDeConteudo.Literal l => l.Nome,
        PecaDeConteudo.Imagem i => i.Altura is { } altura
            ? $"image({Literal(i.Caminho)}, height: {Medida(altura)})"
            : $"image({Literal(i.Caminho)})",
        PecaDeConteudo.Quebra => "linebreak()",
        _ => null,
    };

    private static string Vestir(string conteudo, string? estilo) =>
        conteudo == "none" || estilo is null ? conteudo : $"{IdentificadorDeEstilo(estilo)}({conteudo})";

    // ─── Internos ───────────────────────────────────────────────────────

    /// <summary>O timbre entra como fundo da página, esticado ao papel. A moldura não é desenhada: é <b>trazida</b>.</summary>
    private static string ImagemDeFundo(string caminho) =>
        $"image({Literal(caminho)}, width: 100%, height: 100%)";

    /// <summary>
    /// Variante de página é uma condição sobre o contador, dentro de um único
    /// parâmetro — e o gerador a emite sozinho.
    /// </summary>
    private static string PorVariante(string primeira, string demais)
    {
        // Iguais, não há variante: emitir o `context` custaria uma consulta
        // ao contador em toda página para decidir sempre a mesma coisa.
        if (primeira == demais) return primeira;
        return "context {\n"
               + $"      if counter(page).get().first() == 1 {{ {primeira} }} else {{ {demais} }}\n"
               + "    }";
    }

    private static string NomeDaFaixa(Faixa? faixa, string nome) =>
        faixa is null || faixa.Vazia ? "none" : nome;

    private static string Sanear(string nome)
    {
        var limpo = new string(nome.ToLowerInvariant()
            .Select(c => char.IsLetter(c) || char.IsNumber(c) ? c : '-')
            .ToArray());
        // Um identificador Typst não começa com dígito.
        return limpo.Length > 0 && char.IsNumber(limpo[0]) ? "e" + limpo : limpo;
    }
}
