using System.Text.Json;
using System.Text.Json.Serialization;
using MarkDocs.Abstractions.Valores;
using MarkDocs.Core;
using MongoDB.Bson.Serialization.Attributes;

namespace MarkDocs.Abstractions.Formatacoes;

/// <summary>
/// A formatação da família <c>documento-de-negocio</c> — contrato, orçamento,
/// recibo.
/// </summary>
/// <remarks>
/// Transcreve o <b>esquema inteiro</b> da família, e não só os eixos. Moldura,
/// estilos de texto, identidade e espaçamentos de título não têm eixo, mas
/// sem eles não se monta o <c>template.json</c>. Os eixos são o subconjunto que
/// a tela deixa editar.
///
/// Duas diferenças de forma em relação à especificação 001, ambas porque o
/// texto dela não compilaria ou não leria o JSON: as classes aninhadas que
/// teriam o nome da propriedade (<c>Identidade</c>, <c>Moldura</c>) ganham o
/// sufixo <c>…Formatada</c>; e a moldura tem um conversor próprio de JSON, que
/// põe e tira o nível <c>paginas</c> do <c>template.json</c>.
/// </remarks>
[BsonDiscriminator(NomeDaFamilia)]
public sealed class FormatacaoDocumentoDeNegocio : Formatacao
{
    public const string NomeDaFamilia = "documento-de-negocio";
    public const int EsquemaAtual = 1;
    public static readonly IReadOnlyList<int> NiveisDeTitulo = [1, 2, 3];
    public const string SentinelaHerdado = "— herdar do corpo —";

    public override string Familia => NomeDaFamilia;
    protected override string Herdado => SentinelaHerdado;

    public PaginaFormatada Pagina { get; set; } = new();
    public TextoFormatado Texto { get; set; } = new();
    public ParagrafoFormatado Paragrafo { get; set; } = new();

    /// <summary>Nível único por entrada.</summary>
    public List<TituloFormatado> Titulos { get; set; } = [];

    public TabelaFormatada Tabela { get; set; } = new();

    /// <summary><c>"nota"</c> → estilo.</summary>
    public Dictionary<string, EstiloDeTexto> EstilosDeTexto { get; set; } = [];

    public IdentidadeFormatada? Identidade { get; set; }
    public MolduraFormatada? Moldura { get; set; }

    // ─── as partes ───────────────────────────────────────────────────

    public sealed class PaginaFormatada
    {
        public Papel Papel { get; set; } = Papel.A4;
        public Margem Margem { get; set; } = new();
    }

    public sealed class Margem
    {
        public Medida Topo { get; set; }
        public Medida Base { get; set; }
        public Medida Esquerda { get; set; }
        public Medida Direita { get; set; }
    }

    public sealed class TextoFormatado
    {
        public string Familia { get; set; } = "";
        public Medida Tamanho { get; set; }
        public Cor Cor { get; set; }
        public string Idioma { get; set; } = "pt";
    }

    public sealed class ParagrafoFormatado
    {
        public bool Justificado { get; set; }
        public Medida Entrelinha { get; set; }
        public Medida EspacoEntre { get; set; }
    }

    public sealed class TituloFormatado
    {
        public int Nivel { get; set; }

        /// <summary><c>null</c> = herda do corpo.</summary>
        public string? Familia { get; set; }

        public Medida Tamanho { get; set; }
        public Alinhamento? Alinhamento { get; set; }
        public Caixa? Caixa { get; set; }
        public bool? Forte { get; set; }
        public Cor? Cor { get; set; }
        public Medida EspacoAntes { get; set; }
        public Medida EspacoDepois { get; set; }
    }

    public sealed class TabelaFormatada
    {
        public Cor Filete { get; set; }
        public Medida Espessura { get; set; }
        public Medida Respiro { get; set; }
        public bool CabecalhoForte { get; set; }
        public bool RepetirCabecalho { get; set; }
    }

    public sealed class EstiloDeTexto
    {
        public string? Familia { get; set; }
        public Medida? Tamanho { get; set; }
        public Cor? Cor { get; set; }
        public Caixa? Caixa { get; set; }
        public Medida? Espacamento { get; set; }
    }

    /// <summary>Os dados da firma. Não são aparência, mas viajam com ela.</summary>
    public sealed class IdentidadeFormatada
    {
        public string Nome { get; set; } = "";
        public List<string> Linhas { get; set; } = [];

        /// <summary>O nome de um ativo.</summary>
        public string? Logotipo { get; set; }
    }

    /// <summary>
    /// Em JSON é <c>moldura.paginas.{todas, primeira, demais}</c>; no MongoDB,
    /// <c>moldura.{todas, primeira, demais}</c>. Primeira e demais caem em
    /// <c>Todas</c> quando ausentes.
    /// </summary>
    [JsonConverter(typeof(MolduraJson))]
    public sealed class MolduraFormatada
    {
        public VarianteDePagina? Todas { get; set; }
        public VarianteDePagina? Primeira { get; set; }
        public VarianteDePagina? Demais { get; set; }
    }

    public sealed class VarianteDePagina
    {
        public Fundo? Fundo { get; set; }
        public Faixa? Cabecalho { get; set; }
        public Faixa? Rodape { get; set; }
    }

    public sealed class Fundo
    {
        /// <summary>O nome de um ativo: o timbre.</summary>
        public string Arquivo { get; set; } = "";
        public string? Formato { get; set; }
    }

    public sealed class Faixa
    {
        public Medida? Afastamento { get; set; }

        /// <summary>Chave de <see cref="EstilosDeTexto"/>.</summary>
        public string? Estilo { get; set; }

        public List<Peca>? Esquerda { get; set; }
        public List<Peca>? Centro { get; set; }
        public List<Peca>? Direita { get; set; }
    }

    /// <summary>
    /// Uma peça de cabeçalho ou rodapé: fechada e plana. Em JSON e BSON o
    /// discriminador é o campo <c>"peca"</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PecaIdentidade"/> não está na lista da especificação 001, mas
    /// está no esquema da família (<c>{ "peca": "identidade", "campo": "nome" }</c>):
    /// sem ela, um template com a identidade da firma não seria lido.
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "peca")]
    [JsonDerivedType(typeof(PecaTexto), "texto")]
    [JsonDerivedType(typeof(PecaCampo), "campo")]
    [JsonDerivedType(typeof(PecaLiteral), "literal")]
    [JsonDerivedType(typeof(PecaImagem), "imagem")]
    [JsonDerivedType(typeof(PecaQuebra), "quebra")]
    [JsonDerivedType(typeof(PecaIdentidade), "identidade")]
    [BsonKnownTypes(typeof(PecaTexto), typeof(PecaCampo), typeof(PecaLiteral),
                    typeof(PecaImagem), typeof(PecaQuebra), typeof(PecaIdentidade))]
    public abstract record Peca;

    [BsonDiscriminator("texto")] public sealed record PecaTexto(string Valor) : Peca;
    [BsonDiscriminator("campo")] public sealed record PecaCampo(CampoDerivado Campo) : Peca;
    [BsonDiscriminator("literal")] public sealed record PecaLiteral(string Valor) : Peca;
    [BsonDiscriminator("imagem")] public sealed record PecaImagem(string Arquivo, Medida? Altura) : Peca;
    [BsonDiscriminator("quebra")] public sealed record PecaQuebra : Peca;
    [BsonDiscriminator("identidade")] public sealed record PecaIdentidade(string Campo, Medida? Altura) : Peca;

    // ─── a edição: transcrição de `aplicar` e `aplicarEmTitulo` em Eixos.swift ──

    protected override void AplicarLimitado(string eixo, ValorDeEixo valor)
    {
        switch (eixo, valor)
        {
            case ("pagina.papel", ValorDeEixo.Escolha e) when TextoDeEnum<Papel>.TentarLer(e.Id, out var papel):
                Pagina.Papel = papel; break;
            case ("pagina.margem.topo", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Pagina.Margem.Topo = x; break;
            case ("pagina.margem.base", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Pagina.Margem.Base = x; break;
            case ("pagina.margem.esquerda", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Pagina.Margem.Esquerda = x; break;
            case ("pagina.margem.direita", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Pagina.Margem.Direita = x; break;

            case ("texto.familia", ValorDeEixo.FamiliaTipografica f) when f.Familia != Herdado:
                Texto.Familia = f.Familia; break;
            case ("texto.tamanho", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Texto.Tamanho = x; break;
            case ("texto.cor", ValorDeEixo.Cor c) when Cor.TentarLer(c.Hexadecimal, out var cor):
                Texto.Cor = cor; break;

            case ("paragrafo.justificado", ValorDeEixo.Alternancia a):
                Paragrafo.Justificado = a.Ligado; break;
            case ("paragrafo.entrelinha", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Paragrafo.Entrelinha = x; break;
            case ("paragrafo.espacoEntre", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Paragrafo.EspacoEntre = x; break;

            case ("tabela.espessura", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Tabela.Espessura = x; break;
            case ("tabela.respiro", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                Tabela.Respiro = x; break;
            case ("tabela.repetirCabecalho", ValorDeEixo.Alternancia a):
                Tabela.RepetirCabecalho = a.Ligado; break;

            default:
                AplicarEmTitulo(eixo, valor); break;   // id desconhecido: ignorado
        }
    }

    /// <summary><c>"titulos.{nivel}.{campo}"</c>. Tocar um nível ausente cria a entrada.</summary>
    private void AplicarEmTitulo(string eixo, ValorDeEixo valor)
    {
        var partes = eixo.Split('.');
        if (partes is not ["titulos", var n, var campo]
            || !int.TryParse(n, out var nivel) || !NiveisDeTitulo.Contains(nivel))
            return;

        var titulo = Titulos.FirstOrDefault(t => t.Nivel == nivel);
        if (titulo is null)
        {
            titulo = new TituloFormatado
            {
                Nivel = nivel, Tamanho = Texto.Tamanho, Forte = true,
                EspacoAntes = new(1, UnidadeDeMedida.Em), EspacoDepois = new(0.5, UnidadeDeMedida.Em),
            };
            Titulos.Add(titulo);
            Titulos.Sort((a, b) => a.Nivel.CompareTo(b.Nivel));
        }

        switch (campo, valor)
        {
            case ("familia", ValorDeEixo.FamiliaTipografica f):
                titulo.Familia = f.Familia == Herdado ? null : f.Familia; break;
            case ("tamanho", ValorDeEixo.Medida m) when Medida.TentarDe(m, out var x):
                titulo.Tamanho = x; break;
            case ("alinhamento", ValorDeEixo.Escolha e) when TextoDeEnum<Alinhamento>.TentarLer(e.Id, out var a):
                titulo.Alinhamento = a; break;
            case ("caixa", ValorDeEixo.Escolha e) when TextoDeEnum<Caixa>.TentarLer(e.Id, out var c):
                titulo.Caixa = c; break;
        }
    }

    // ─── a ponte com o MarkDocs.Core ────────────────────────────────

    public override byte[] ParaTemplateJson(string nome, string? tipoDocumental) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            familia = Familia, esquema = Esquema, id = nome, nome, tipoDocumental,
            fontes = Fontes,
            pagina = Pagina, texto = Texto, paragrafo = Paragrafo, titulos = Titulos,
            tabela = Tabela, estilosDeTexto = EstilosDeTexto, identidade = Identidade,
            moldura = Moldura,
        }, SerializacaoDaFormatacao.Json);

    /// <summary>Para os templates de fábrica: lê um <c>template.json</c>.</summary>
    public static FormatacaoDocumentoDeNegocio DeTemplateJson(ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<FormatacaoDocumentoDeNegocio>(json, SerializacaoDaFormatacao.Json)!;

    /// <summary>Põe e tira o nível <c>paginas</c>, que existe no <c>template.json</c> e não no C#.</summary>
    private sealed class MolduraJson : JsonConverter<MolduraFormatada>
    {
        private sealed class Envelope
        {
            public Dictionary<string, VarianteDePagina> Paginas { get; set; } = [];
        }

        public override MolduraFormatada Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes)
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(ref leitor, opcoes) ?? new Envelope();
            var desconhecida = envelope.Paginas.Keys.FirstOrDefault(k => k is not ("todas" or "primeira" or "demais"));
            if (desconhecida is not null)
                throw new JsonException($"“{desconhecida}” não é uma variante de página");
            return new MolduraFormatada
            {
                Todas = envelope.Paginas.GetValueOrDefault("todas"),
                Primeira = envelope.Paginas.GetValueOrDefault("primeira"),
                Demais = envelope.Paginas.GetValueOrDefault("demais"),
            };
        }

        public override void Write(Utf8JsonWriter escritor, MolduraFormatada moldura, JsonSerializerOptions opcoes)
        {
            var paginas = new Dictionary<string, VarianteDePagina>();
            if (moldura.Todas is not null) paginas["todas"] = moldura.Todas;
            if (moldura.Primeira is not null) paginas["primeira"] = moldura.Primeira;
            if (moldura.Demais is not null) paginas["demais"] = moldura.Demais;
            JsonSerializer.Serialize(escritor, new Envelope { Paginas = paginas }, opcoes);
        }
    }
}
