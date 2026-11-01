using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDocs.Core.Nucleo;

namespace MarkDocs.Core.Geradores.DocumentoDeNegocio;

/// <summary>
/// O esquema da família <c>documento-de-negocio</c> — a do contrato, do
/// orçamento, do recibo.
/// </summary>
/// <remarks>
/// <b>Nenhum destes tipos é público.</b> O que atravessa para o núcleo é um
/// <see cref="TemplateCarregado"/>, a forma apagada: é isso que permite a
/// próxima família ter um esquema completamente diferente sem que
/// <see cref="RenderizadorPDF"/> mude de assinatura.
///
/// As classes são mutáveis porque <c>Aplicando</c> trabalha sobre uma cópia
/// profunda (<see cref="Copiar"/>), como o <c>var template = original</c> de
/// um <c>struct</c> Swift.
/// </remarks>
internal sealed class TemplateDeNegocio
{
    public required int Esquema { get; set; }
    public required string Id { get; set; }
    public required string Nome { get; set; }

    /// <summary>As famílias tipográficas exigidas.</summary>
    public required List<string> Fontes { get; set; }

    /// <summary>
    /// Constante por template, porque um template é 1:1 com um tipo
    /// documental. É o que permite <c>CampoDerivado.TipoDocumental</c> existir
    /// sem abrir um canal paralelo de metadados.
    /// </summary>
    public string? TipoDocumental { get; set; }

    public required PaginaDeclarada Pagina { get; set; }
    public required TextoDeclarado Texto { get; set; }
    public required ParagrafoDeclarado Paragrafo { get; set; }
    public required List<Titulo> Titulos { get; set; }
    public required TabelaDeclarada Tabela { get; set; }
    public required Dictionary<string, EstiloDeTexto> EstilosDeTexto { get; set; }
    public IdentidadeDeclarada? Identidade { get; set; }
    public MolduraDeclarada? Moldura { get; set; }

    public sealed class PaginaDeclarada
    {
        public required Papel Papel { get; set; }
        public required MargemDeclarada Margem { get; set; }
    }

    public sealed class MargemDeclarada
    {
        public required Medida Topo { get; set; }
        public required Medida Base { get; set; }
        public required Medida Esquerda { get; set; }
        public required Medida Direita { get; set; }

        public Margem ParaNucleo() => new(Topo, Base, Esquerda, Direita);
    }

    public sealed class TextoDeclarado
    {
        public required string Familia { get; set; }
        public required Medida Tamanho { get; set; }
        public required Cor Cor { get; set; }
        public required string Idioma { get; set; }
    }

    public sealed class ParagrafoDeclarado
    {
        public required bool Justificado { get; set; }
        public required Medida Entrelinha { get; set; }
        public required Medida EspacoEntre { get; set; }
    }

    public sealed class Titulo
    {
        public required int Nivel { get; set; }

        /// <summary>
        /// Opcional: ausente, o título herda a família do corpo. "Títulos em
        /// sem-serifa sobre corpo serifado" é a papelaria mais comum de
        /// documento de negócio, e sem o campo o esquema seria estreito demais.
        /// </summary>
        public string? Familia { get; set; }
        public required Medida Tamanho { get; set; }
        public AlinhamentoHorizontal? Alinhamento { get; set; }
        public Caixa? Caixa { get; set; }
        public bool? Forte { get; set; }
        public Cor? Cor { get; set; }
        public required Medida EspacoAntes { get; set; }
        public required Medida EspacoDepois { get; set; }
    }

    public enum Caixa { Normal, Alta }

    public sealed class TabelaDeclarada
    {
        public required Cor Filete { get; set; }
        public required Medida Espessura { get; set; }
        public required Medida Respiro { get; set; }
        public required bool CabecalhoForte { get; set; }
        public required bool RepetirCabecalho { get; set; }
    }

    public sealed class EstiloDeTexto
    {
        public string? Familia { get; set; }
        public Medida? Tamanho { get; set; }
        public Cor? Cor { get; set; }
        public Caixa? Caixa { get; set; }
        public Medida? Espacamento { get; set; }
    }

    /// <summary>
    /// Os dados da firma não são aparência: são um fato sobre a organização.
    /// É <b>seção do <c>template.json</c></b>, e não configuração do app.
    /// </summary>
    public sealed class IdentidadeDeclarada
    {
        public required string Nome { get; set; }
        public required List<string> Linhas { get; set; }
        public string? Logotipo { get; set; }
    }

    public sealed class MolduraDeclarada
    {
        public required Dictionary<string, VariantePagina> Paginas { get; set; }

        public sealed class VariantePagina
        {
            public Fundo? Fundo { get; set; }
            public FaixaDeclarada? Cabecalho { get; set; }
            public FaixaDeclarada? Rodape { get; set; }
        }

        public sealed class Fundo
        {
            public required string Arquivo { get; set; }
            public string? Formato { get; set; }
        }

        public sealed class FaixaDeclarada
        {
            public Medida? Afastamento { get; set; }
            public string? Estilo { get; set; }
            public List<PecaDeclarada>? Esquerda { get; set; }
            public List<PecaDeclarada>? Centro { get; set; }
            public List<PecaDeclarada>? Direita { get; set; }
        }

        /// <summary>
        /// Uma peça é fechada e <b>plana</b> — sem filhos, sem aninhamento.
        /// <c>quebra</c> é o que torna a lista plana suficiente.
        /// </summary>
        public sealed class PecaDeclarada
        {
            public required string Peca { get; set; }
            public string? Valor { get; set; }
            public string? Campo { get; set; }
            public string? Arquivo { get; set; }
            public Medida? Altura { get; set; }
        }
    }

    // ─── Leitura ─────────────────────────────────────────────────────────

    /// <summary>
    /// As regras de leitura do <c>JSONDecoder</c> do Swift: campo obrigatório
    /// ausente ou nulo é erro, chave desconhecida é ignorada, enum só aceita o
    /// texto exato, e uma medida ou cor malformada é erro <b>daquele</b> campo.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new MedidaJson(),
            new CorJson(),
            new TextoExato<Papel>(p => p.Texto(), Papeis.Ler),
            new TextoExato<AlinhamentoHorizontal>(),
            new TextoExato<Caixa>(),
        },
    };

    /// <summary>Uma cópia profunda — o equivalente ao valor de um <c>struct</c> Swift.</summary>
    public TemplateDeNegocio Copiar() =>
        JsonSerializer.Deserialize<TemplateDeNegocio>(JsonSerializer.SerializeToUtf8Bytes(this, Json), Json)!;
}

/// <summary>Uma medida em JSON é a string <c>"11pt"</c>.</summary>
internal sealed class MedidaJson : JsonConverter<Medida>
{
    public override Medida Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
    {
        if (leitor.TokenType != JsonTokenType.String)
            throw new JsonException("uma medida é uma string");
        var bruto = leitor.GetString()!;
        return Medida.Ler(bruto)
               ?? throw new ErroTypst.TemplateInvalido("medida", $"“{bruto}” não é uma medida");
    }

    public override void Write(Utf8JsonWriter escritor, Medida medida, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(medida.Descricao);
}

/// <summary>Uma cor em JSON é a string <c>"#1c1917"</c>.</summary>
internal sealed class CorJson : JsonConverter<Cor>
{
    public override Cor Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
    {
        if (leitor.TokenType != JsonTokenType.String)
            throw new JsonException("uma cor é uma string");
        var bruto = leitor.GetString()!;
        return Cor.Ler(bruto)
               ?? throw new ErroTypst.TemplateInvalido("cor", $"“{bruto}” não é uma cor");
    }

    public override void Write(Utf8JsonWriter escritor, Cor cor, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(cor.Hexadecimal);
}

/// <summary>
/// Um enum como texto exato: <c>camelCase</c> do identificador, ou o nome
/// dado. Ao contrário do conversor do .NET, não aceita outra caixa nem número
/// — como o <c>Codable</c> de um enum <c>String</c> do Swift.
/// </summary>
internal sealed class TextoExato<T> : JsonConverter<T> where T : struct, Enum
{
    private readonly Func<T, string> _paraTexto;
    private readonly Func<string, T?> _deTexto;

    public TextoExato()
    {
        var nomes = Enum.GetValues<T>().ToDictionary(v => v, v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()));
        _paraTexto = v => nomes[v];
        _deTexto = t => nomes.FirstOrDefault(par => par.Value == t) is { Value: not null } achado ? achado.Key : null;
    }

    public TextoExato(Func<T, string> paraTexto, Func<string, T?> deTexto)
    {
        _paraTexto = paraTexto;
        _deTexto = deTexto;
    }

    public override T Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
    {
        if (leitor.TokenType != JsonTokenType.String)
            throw new JsonException($"{typeof(T).Name} é uma string");
        var texto = leitor.GetString()!;
        return _deTexto(texto) ?? throw new JsonException($"“{texto}” não é um {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter escritor, T valor, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(_paraTexto(valor));
}
