using System.Text.Json;
using MarkDocs.Core.Nucleo;
using static MarkDocs.Core.Geradores.DocumentoDeNegocio.TemplateDeNegocio;
using static MarkDocs.Core.Geradores.DocumentoDeNegocio.TemplateDeNegocio.MolduraDeclarada;

namespace MarkDocs.Core.Geradores.DocumentoDeNegocio;

/// <summary>
/// A primeira família: o esquema de documento de negócio e seu gerador.
/// </summary>
/// <remarks>
/// Por dentro é <c>TemplateDeNegocio → string</c>, puro, sem I/O — exatamente
/// o perfil de <c>ArvoreDeMarkdown</c> e <c>ValorTypst</c>.
///
/// E <b>ele não escreve sintaxe Typst diretamente</b>: traduz o esquema em
/// chamadas a <see cref="Emissao"/>, que é o único lugar do pacote autorizado a
/// isso.
/// </remarks>
internal sealed partial class DocumentoDeNegocio : IGeradorDeTemplate
{
    public static string Familia => "documento-de-negocio";
    public static int EsquemaAtual => 1;

    public static TemplateCarregado Carregar(byte[] json, IReadOnlyDictionary<string, byte[]> ativos)
    {
        var declarado = MigracaoDeNegocio.EsquemaDeclarado(json);
        var migrado = MigracaoDeNegocio.Migrar(json, declarado, EsquemaAtual);

        TemplateDeNegocio template;
        try
        {
            template = JsonSerializer.Deserialize<TemplateDeNegocio>(migrado, TemplateDeNegocio.Json)
                       ?? throw new JsonException("o template é nulo");
        }
        catch (ErroTypst)
        {
            throw;
        }
        catch (Exception erro) when (erro is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new ErroTypst.TemplateInvalido("template.json", erro.Message);
        }

        Validar(template, ativos);
        return Carregado(template, ativos);
    }

    /// <summary>
    /// Monta a forma apagada a partir de um <see cref="TemplateDeNegocio"/> já
    /// válido. Está separado de <see cref="Carregar"/> porque
    /// <c>ComAjustes</c> precisa do mesmo caminho: aplicar um ajuste produz
    /// <b>outro</b> <see cref="TemplateCarregado"/>, com eixos recalculados e
    /// <c>EmitirTyp</c> fechando sobre o valor novo.
    /// </summary>
    private static TemplateCarregado Carregado(TemplateDeNegocio template, IReadOnlyDictionary<string, byte[]> ativos) =>
        new(
            familia: Familia,
            esquema: EsquemaAtual,
            nome: template.Nome,
            fontesExigidas: template.Fontes.ToList(),
            ativos: ativos,
            eixos: Eixos(template, template.Fontes),
            emitirTyp: () => Emitir(template),
            aplicarAjustes: ajustes => Carregado(Aplicando(ajustes, template), ativos));

    // ─── Validação ──────────────────────────────────────────────────────

    /// <summary>
    /// O gerador valida antes de emitir, e o erro é de template — não de motor.
    /// </summary>
    /// <remarks>
    /// Uma validação que <b>deliberadamente não existe</b>: o gerador não
    /// verifica se o cabeçalho cobre um elemento do timbre. Cobrir o timbre é
    /// muitas vezes o que se quer, e o PDF é o preview.
    /// </remarks>
    private static void Validar(TemplateDeNegocio template, IReadOnlyDictionary<string, byte[]> ativos)
    {
        // Toda família citada no esquema tem de estar em `fontes`: é essa
        // lista que o renderizador confere contra o que o app embarca.
        var declaradas = template.Fontes.ToHashSet();
        var citadas = new[] { template.Texto.Familia }
            .Concat(template.Titulos.Select(t => t.Familia).OfType<string>())
            .Concat(template.EstilosDeTexto.Values.Select(e => e.Familia).OfType<string>())
            .ToHashSet();
        foreach (var familia in citadas.Except(declaradas).Order(StringComparer.Ordinal))
            throw new ErroTypst.TemplateInvalido("fontes", $"a família “{familia}” é usada mas não está declarada");

        var niveis = new HashSet<int>();
        foreach (var titulo in template.Titulos)
        {
            if (!niveis.Add(titulo.Nivel))
                throw new ErroTypst.TemplateInvalido("titulos", $"o nível {titulo.Nivel} aparece duas vezes");
        }

        if (template.Moldura is not { } moldura) return;

        foreach (var (chave, variante) in moldura.Paginas)
        {
            if (chave is not ("todas" or "primeira" or "demais"))
                throw new ErroTypst.TemplateInvalido("moldura.paginas", $"“{chave}” não é uma variante de página");

            if (variante.Fundo?.Arquivo is { } arquivo && !ativos.ContainsKey(arquivo))
                throw new ErroTypst.TemplateInvalido(
                    $"moldura.paginas.{chave}.fundo", $"o ativo “{arquivo}” não acompanha o template");

            foreach (var faixa in new[] { variante.Cabecalho, variante.Rodape }.OfType<FaixaDeclarada>())
            {
                if (faixa.Estilo is { } estilo && !template.EstilosDeTexto.ContainsKey(estilo))
                    throw new ErroTypst.TemplateInvalido(
                        $"moldura.paginas.{chave}", $"o estilo “{estilo}” não existe em estilosDeTexto");

                foreach (var peca in (faixa.Esquerda ?? []).Concat(faixa.Centro ?? []).Concat(faixa.Direita ?? []))
                    Validar(peca, template, ativos, chave);
            }
        }

        // `header-ascent` e `footer-descent` são propriedades da página: um
        // afastamento diferente por variante não é expressável. Recusar é a
        // escolha — afastamento é um por documento.
        ConferirAfastamentoUnico(moldura, v => v.Cabecalho, "cabecalho");
        ConferirAfastamentoUnico(moldura, v => v.Rodape, "rodape");
    }

    private static void Validar(PecaDeclarada peca, TemplateDeNegocio template, IReadOnlyDictionary<string, byte[]> ativos, string variante)
    {
        switch (peca.Peca)
        {
            case "texto" or "quebra":
                break;
            case "campo":
                if (peca.Campo is null || CampoDe(peca.Campo) is null)
                    throw new ErroTypst.TemplateInvalido(
                        $"moldura.paginas.{variante}", $"“{peca.Campo ?? ""}” não é um campo derivado");
                break;
            case "identidade":
                if (template.Identidade is null)
                    throw new ErroTypst.TemplateInvalido(
                        $"moldura.paginas.{variante}", "uma fatia cita `identidade`, mas o template não a declara");
                break;
            case "imagem":
                if (peca.Arquivo is null || !ativos.ContainsKey(peca.Arquivo))
                    throw new ErroTypst.TemplateInvalido(
                        $"moldura.paginas.{variante}", $"a imagem “{peca.Arquivo ?? ""}” não acompanha o template");
                break;
            default:
                // Peça desconhecida é omitida do papel, não é erro: é o caso de
                // um `template.json` de esquema mais novo aberto por uma versão
                // mais antiga.
                break;
        }
    }

    private static void ConferirAfastamentoUnico(MolduraDeclarada moldura, Func<VariantePagina, FaixaDeclarada?> faixa, string campo)
    {
        var afastamentos = moldura.Paginas.Values
            .Select(v => faixa(v)?.Afastamento?.Descricao)
            .OfType<string>()
            .ToHashSet();
        if (afastamentos.Count > 1)
            throw new ErroTypst.TemplateInvalido(
                $"moldura.{campo}.afastamento",
                "o afastamento é um por documento, e as variantes declaram "
                + string.Join(", ", afastamentos.Order(StringComparer.Ordinal)));
    }

    private static CampoDerivado? CampoDe(string texto) => texto switch
    {
        "tituloDoDocumento" => CampoDerivado.TituloDoDocumento,
        "tipoDocumental" => CampoDerivado.TipoDocumental,
        "numeroDaPagina" => CampoDerivado.NumeroDaPagina,
        "totalDePaginas" => CampoDerivado.TotalDePaginas,
        _ => null,
    };

    // ─── Emissão ────────────────────────────────────────────────────────

    private static string Emitir(TemplateDeNegocio template)
    {
        var partes = new List<string> { "// /template.typ — gerado por MarkDocs.Core. Não editar." };

        foreach (var nome in template.EstilosDeTexto.Keys.Order(StringComparer.Ordinal))
        {
            var estilo = template.EstilosDeTexto[nome];
            partes.Add(Emissao.EstiloDeTexto(
                nome: nome,
                tamanho: estilo.Tamanho,
                cor: estilo.Cor,
                caixaAlta: estilo.Caixa == Caixa.Alta,
                espacamento: estilo.Espacamento,
                familia: estilo.Familia));
        }

        var moldura = Resolver(template);

        if (moldura.TemCabecalho || moldura.TemRodape)
        {
            partes.Add(Emissao.DeclaracoesDerivadas());
            partes.Add(Emissao.DeclaracaoDaFaixa());

            var faixas = new (string Nome, Faixa? Faixa)[]
            {
                ("cabecalho-primeira", moldura.Primeira.Cabecalho),
                ("cabecalho-demais", moldura.Demais.Cabecalho),
                ("rodape-primeira", moldura.Primeira.Rodape),
                ("rodape-demais", moldura.Demais.Rodape),
            };
            foreach (var (nome, faixa) in faixas)
            {
                if (faixa is null || faixa.Vazia) continue;
                partes.Add(Emissao.DeclaracaoDeFaixa(nome, faixa));
            }
        }

        var regras = new List<string>
        {
            Emissao.RegraDePagina(template.Pagina.Papel, template.Pagina.Margem.ParaNucleo(), moldura),
            Emissao.RegraDeTexto(template.Texto.Familia, template.Texto.Tamanho, template.Texto.Cor, template.Texto.Idioma),
            Emissao.RegraDeParagrafo(template.Paragrafo.Justificado, template.Paragrafo.Entrelinha, template.Paragrafo.EspacoEntre),
        };

        foreach (var titulo in template.Titulos.OrderBy(t => t.Nivel))
        {
            regras.Add(Emissao.RegraDeTitulo(
                nivel: titulo.Nivel,
                familia: titulo.Familia,
                tamanho: titulo.Tamanho,
                alinhamento: titulo.Alinhamento ?? AlinhamentoHorizontal.Esquerda,
                caixaAlta: titulo.Caixa == Caixa.Alta,
                forte: titulo.Forte ?? true,
                cor: titulo.Cor,
                espacoAntes: titulo.EspacoAntes,
                espacoDepois: titulo.EspacoDepois));
        }

        regras.Add(Emissao.RegraDeTabela(
            template.Tabela.Filete,
            template.Tabela.Espessura,
            template.Tabela.Respiro,
            template.Tabela.CabecalhoForte,
            template.Tabela.RepetirCabecalho));

        partes.Add(Emissao.FuncaoEstilo(regras));
        return string.Join("\n\n", partes) + "\n";
    }

    /// <summary>
    /// Traduz a moldura declarada na forma do núcleo, com <c>todas</c> aplicada
    /// como base. A regra de resolução é rasa de propósito: uma variante
    /// declarada substitui <c>todas</c> inteira.
    /// </summary>
    private static Moldura Resolver(TemplateDeNegocio template)
    {
        if (template.Moldura is not { } declarada) return new Moldura();

        declarada.Paginas.TryGetValue("todas", out var todas);
        var primeira = declarada.Paginas.GetValueOrDefault("primeira") ?? todas;
        var demais = declarada.Paginas.GetValueOrDefault("demais") ?? todas;

        VarianteDePagina ConverterVariante(VariantePagina? variante) =>
            variante is null
                ? new VarianteDePagina()
                : new VarianteDePagina(
                    variante.Fundo is { } fundo ? Acervo.CaminhoDoAtivo(fundo.Arquivo) : null,
                    Converter(variante.Cabecalho, template),
                    Converter(variante.Rodape, template));

        var afastamentos = declarada.Paginas.Values;
        return new Moldura
        {
            Primeira = ConverterVariante(primeira),
            Demais = ConverterVariante(demais),
            AfastamentoDoCabecalho = afastamentos.Select(v => v.Cabecalho?.Afastamento).FirstOrDefault(a => a is not null),
            AfastamentoDoRodape = afastamentos.Select(v => v.Rodape?.Afastamento).FirstOrDefault(a => a is not null),
        };
    }

    private static Faixa? Converter(FaixaDeclarada? faixa, TemplateDeNegocio template) =>
        faixa is null
            ? null
            : new Faixa(
                (faixa.Esquerda ?? []).SelectMany(p => Converter(p, template)).ToList(),
                (faixa.Centro ?? []).SelectMany(p => Converter(p, template)).ToList(),
                (faixa.Direita ?? []).SelectMany(p => Converter(p, template)).ToList(),
                faixa.Estilo);

    /// <summary>
    /// Uma peça declarada pode render mais de uma peça do núcleo: as linhas da
    /// identidade viram texto e quebra, alternados.
    /// </summary>
    private static IEnumerable<PecaDeConteudo> Converter(PecaDeclarada peca, TemplateDeNegocio template)
    {
        switch (peca.Peca)
        {
            case "texto":
                return peca.Valor is { } valor ? [new PecaDeConteudo.Texto(valor)] : [];

            case "quebra":
                return [new PecaDeConteudo.Quebra()];

            case "campo":
                if (peca.Campo is null || CampoDe(peca.Campo) is not { } campo) return [];
                // `tipoDocumental` é constante por template, então resolve-se
                // aqui como texto. Nenhum dado novo atravessa a fronteira.
                if (campo == CampoDerivado.TipoDocumental)
                {
                    var tipo = template.TipoDocumental ?? template.Nome;
                    return tipo.Length == 0 ? [] : [new PecaDeConteudo.Texto(tipo)];
                }
                return [new PecaDeConteudo.Campo(campo)];

            case "identidade":
                if (template.Identidade is not { } identidade) return [];
                return peca.Campo switch
                {
                    "nome" => [new PecaDeConteudo.Texto(identidade.Nome)],
                    "linhas" => identidade.Linhas
                        .SelectMany((linha, i) => i == 0
                            ? new PecaDeConteudo[] { new PecaDeConteudo.Texto(linha) }
                            : [new PecaDeConteudo.Quebra(), new PecaDeConteudo.Texto(linha)])
                        .ToList(),
                    "logotipo" => identidade.Logotipo is { } logotipo
                        ? [new PecaDeConteudo.Imagem(Acervo.CaminhoDoAtivo(logotipo), peca.Altura)]
                        : [],
                    _ => [],
                };

            case "imagem":
                return peca.Arquivo is { } arquivo
                    ? [new PecaDeConteudo.Imagem(Acervo.CaminhoDoAtivo(arquivo), peca.Altura)]
                    : [];

            default:
                // Omitida do papel, não pintada de vermelho.
                return [];
        }
    }
}
