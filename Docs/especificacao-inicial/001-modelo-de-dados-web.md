# 003 — Modelo de dados do app web (.NET + MongoDB)

- Data: 2026-10-31
- Status: Proposto

## Contexto

Um app web vai compor PDFs com os mesmos templates do app Swift. Os documentos
ficam no MongoDB, e os ativos (timbres, logotipos, imagens) num bucket externo.

São dois pacotes .NET:

| Pacote | O que é | Depende de |
|---|---|---|
| `MarkDocs.Core` | a transcrição .NET do `NotiqTypst`, com a mesma API e os mesmos testes | — |
| `MarkDocs.Abstractions` | **o modelo especificado aqui**: `IDocumento`, `Formatacao` e suas famílias, `Template`, os valores e a serialização | `MarkDocs.Core`, `MongoDB.Bson` |

`MarkDocs.Abstractions` depende de `MarkDocs.Core` porque `Formatacao.Aplicar`
recebe um `EixoDeFormatacao` e um `ValorDeEixo`, que são API do `Core`. O
`Core` não conhece o `Abstractions`, como o `NotiqTypst` não conhece o app.

O documento precisa ser **autocontido**: guardar tudo o que a composição
precisa para refazer o PDF, sem depender de um template que mude depois.

**Fora do MVP:** a correspondência 1:1 com o `.mdoc`. Isso é problema de
exportação e importação, e se resolve depois por mapeamento.

## O modelo

```
Template (preset) ──aplicar = copiar──▶ Documento
  ├── Formatacao                          ├── Markdown
  └── Ativos (referências)                ├── Formatacao  (completa, da família)
                                          └── Ativos      (template + imagens)
```

### `IDocumento`

| Atributo | O que é |
|---|---|
| `Id` (`ObjectId`), `Titulo`, `Tipo` | identidade e rótulo |
| `Corpo` | o conteúdo, em Markdown. A fonte de verdade |
| `Formatacao` | a formatação **completa** do documento, numa classe da família |
| `Ativos` | as referências ao bucket que a composição lê |
| `Familia`, `Esquema` | derivados de `Formatacao`, nunca gravados à parte |

```csharp
namespace MarkDocs.Abstractions;

public interface IDocumento
{
    ObjectId Id { get; }
    string Titulo { get; }
    string Tipo { get; }                   // o tipo documental: "Contrato"
    string Corpo { get; }                  // Markdown
    Formatacao Formatacao { get; }
    IReadOnlyList<ReferenciaDeAtivo> Ativos { get; }

    string Familia => Formatacao.Familia;
    int Esquema => Formatacao.Esquema;
}

public sealed record ReferenciaDeAtivo(string Id, string Nome, PapelDoAtivo Papel);
public enum PapelDoAtivo { Template, Imagem }
```

`Nome` é como o `template.json` ou o Markdown cita o ativo (`timbre-p1.svg`).
`Id` é a chave do objeto no bucket.

### `ValorDeEixo` e `EixoDeFormatacao`

São **API pública do `MarkDocs.Core`**, transcritos de
`Publico/EixoDeFormatacao.swift` do `NotiqTypst` com a mesma forma. O
`MarkDocs.Abstractions` não os declara; usa os do `Core`. Também não os
grava: o documento guarda valores tipados na `Formatacao`, e `ValorDeEixo`
só transporta uma edição da tela até `Aplicar`.

```csharp
namespace MarkDocs.Core;

/// O valor de um eixo. Cinco casos fechados.
public abstract record ValorDeEixo
{
    private ValorDeEixo() { }

    public sealed record Medida(double Valor, string Unidade) : ValorDeEixo;   // 10.5, "pt"
    public sealed record Escolha(string Id) : ValorDeEixo;                     // "centro"
    public sealed record Alternancia(bool Ligado) : ValorDeEixo;
    public sealed record Cor(string Hexadecimal) : ValorDeEixo;                // "#1c1917"
    public sealed record FamiliaTipografica(string Familia) : ValorDeEixo;     // "Libertinus Serif"
}

/// Um controle de formatação, como dado. O domínio carrega os limites.
public sealed record EixoDeFormatacao(
    string Id,              // "titulos.2.tamanho" — opaco para a tela
    string Grupo,           // "Papel", "Texto", "Título 2", "Tabela"
    string Rotulo,
    DominioDeEixo Dominio,
    ValorDeEixo Valor);     // o valor atual, já com a formatação aplicada

public abstract record DominioDeEixo
{
    private DominioDeEixo() { }

    public sealed record Medida(double Min, double Max, double Passo, string Unidade) : DominioDeEixo;
    public sealed record Escolha(IReadOnlyList<Opcao> Opcoes) : DominioDeEixo;
    public sealed record Alternancia : DominioDeEixo;
    public sealed record Cor : DominioDeEixo;
    public sealed record FamiliaTipografica(IReadOnlyList<string> Disponiveis) : DominioDeEixo;
}

public sealed record Opcao(string Id, string Rotulo);
```

### `Formatacao`

Há uma classe base e **uma classe concreta por família** do `MarkDocs.Core`. A
base não conhece família nenhuma: limita o valor ao domínio que o eixo trouxe e
entrega à classe concreta, que sabe onde gravá-lo.

```csharp
public abstract class Formatacao
{
    public abstract string Familia { get; }
    public int Esquema { get; set; }

    /// As famílias tipográficas que esta formatação oferece e exige.
    /// Gravado, não derivado: é também o domínio do eixo "texto.familia".
    public List<string> Fontes { get; set; } = [];

    /// Recebe o eixo inteiro, e não só o id, para que os limites venham do
    /// MarkDocs.Core e não sejam repetidos aqui.
    public void Aplicar(EixoDeFormatacao eixo, ValorDeEixo valor) =>
        AplicarLimitado(eixo.Id, Limitar(valor, eixo.Dominio));

    protected abstract void AplicarLimitado(string eixo, ValorDeEixo valor);

    /// O sentinela de "herdar do corpo", que cada família define.
    protected abstract string Herdado { get; }

    /// Transcrição de `limitado(_:a:)` em Eixos.swift.
    private ValorDeEixo Limitar(ValorDeEixo valor, DominioDeEixo dominio) => (valor, dominio) switch
    {
        (ValorDeEixo.Medida m, DominioDeEixo.Medida d) =>
            m with { Valor = Math.Clamp(m.Valor, d.Min, d.Max) },
        (ValorDeEixo.Escolha e, DominioDeEixo.Escolha d) =>
            d.Opcoes.Any(o => o.Id == e.Id) ? e : new ValorDeEixo.Escolha(d.Opcoes.FirstOrDefault()?.Id ?? ""),
        (ValorDeEixo.FamiliaTipografica f, DominioDeEixo.FamiliaTipografica d) =>
            d.Disponiveis.Contains(f.Familia) ? f : new ValorDeEixo.FamiliaTipografica(Herdado),
        _ => valor
    };

    /// Cópia profunda: aplicar um template não pode compartilhar objetos com ele.
    public Formatacao Copiar() =>
        (Formatacao)BsonSerializer.Deserialize(this.ToBson(GetType()), GetType());

    public abstract byte[] ParaTemplateJson(string nome, string? tipoDocumental);
}
```

### `FormatacaoDocumentoDeNegocio`

A classe concreta transcreve o **esquema inteiro** da família
(`Geradores/DocumentoDeNegocio/Esquema.swift`), e não só os eixos. Moldura,
estilos de texto, identidade e espaçamentos de título não têm eixo, mas sem
eles não se monta o `template.json`. Os eixos são o subconjunto que a tela deixa
editar.

```csharp
[BsonDiscriminator("documento-de-negocio")]
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
    public List<TituloFormatado> Titulos { get; set; } = [];     // nível único por entrada
    public TabelaFormatada Tabela { get; set; } = new();
    public Dictionary<string, EstiloDeTexto> EstilosDeTexto { get; set; } = [];  // "nota" → estilo
    public Identidade? Identidade { get; set; }
    public Moldura? Moldura { get; set; }

    // ─── as partes ───────────────────────────────────────────────

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
        public string? Familia { get; set; }          // null = herda do corpo
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

    /// Os dados da firma. Não são aparência, mas viajam com ela.
    public sealed class Identidade
    {
        public string Nome { get; set; } = "";
        public List<string> Linhas { get; set; } = [];
        public string? Logotipo { get; set; }          // nome de um ativo
    }

    /// Em JSON é `moldura.paginas.{todas, primeira, demais}`. Primeira e demais
    /// caem em `Todas` quando ausentes.
    public sealed class Moldura
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
        public string Arquivo { get; set; } = "";      // nome de um ativo: o timbre
        public string? Formato { get; set; }
    }

    public sealed class Faixa
    {
        public Medida? Afastamento { get; set; }
        public string? Estilo { get; set; }            // chave de EstilosDeTexto
        public List<Peca>? Esquerda { get; set; }
        public List<Peca>? Centro { get; set; }
        public List<Peca>? Direita { get; set; }
    }

    /// Uma peça de cabeçalho ou rodapé: fechada e plana. Em JSON e BSON o
    /// discriminador é o campo "peca".
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(PecaTexto), typeof(PecaCampo), typeof(PecaLiteral),
                    typeof(PecaImagem), typeof(PecaQuebra))]
    public abstract record Peca;
    [BsonDiscriminator("texto")]   public sealed record PecaTexto(string Valor) : Peca;
    [BsonDiscriminator("campo")]   public sealed record PecaCampo(CampoDerivado Campo) : Peca;
    [BsonDiscriminator("literal")] public sealed record PecaLiteral(string Valor) : Peca;
    [BsonDiscriminator("imagem")]  public sealed record PecaImagem(string Arquivo, Medida? Altura) : Peca;
    [BsonDiscriminator("quebra")]  public sealed record PecaQuebra : Peca;

    // ─── a edição: transcrição de `aplicar` e `aplicarEmTitulo` em Eixos.swift ──

    protected override void AplicarLimitado(string eixo, ValorDeEixo valor)
    {
        switch (eixo, valor)
        {
            case ("pagina.papel", ValorDeEixo.Escolha e) when PapelJson.TentarLer(e.Id, out var papel):
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

    /// "titulos.{nivel}.{campo}". Tocar um nível ausente cria a entrada.
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
                EspacoAntes = new(1, UnidadeDeMedida.Em), EspacoDepois = new(0.5, UnidadeDeMedida.Em)
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
            case ("alinhamento", ValorDeEixo.Escolha e) when Enum.TryParse<Alinhamento>(e.Id, true, out var a):
                titulo.Alinhamento = a; break;
            case ("caixa", ValorDeEixo.Escolha e) when Enum.TryParse<Caixa>(e.Id, true, out var c):
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
            moldura = Moldura is null ? null : new { paginas = Moldura }
        }, SerializacaoDaFormatacao.Json);

    /// Para os templates de fábrica: lê um template.json.
    public static FormatacaoDocumentoDeNegocio DeTemplateJson(ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<FormatacaoDocumentoDeNegocio>(json, SerializacaoDaFormatacao.Json)!;
}
```

### Os valores, compartilhados pelas famílias

Transcrevem `Nucleo/Emissao/Valores.swift`. São fechados: nenhum aceita
sintaxe.

Em disco, no `template.json` e no MongoDB, uma medida é a **string** `"11pt"`.
Na memória, é o record `Medida(11, Pt)`. Cada tipo de valor **declara no
próprio tipo** como vira string e volta, com dois atributos:

```csharp
[JsonConverter(typeof(MedidaJson))]    // System.Text.Json
[BsonSerializer(typeof(MedidaBson))]   // driver do MongoDB
public readonly record struct Medida(…)
```

Ao encontrar uma propriedade do tipo `Medida`, os dois serializadores leem o
atributo e chamam o conversor indicado, que chama `Medida.TentarLer` (string →
record) ou `ToString` (record → string). Não há registro a fazer na
inicialização para esses tipos. A Parte III explica o mecanismo.

```
"11pt" ──▶ MedidaJson.Read / MedidaBson.Deserialize ──▶ Medida.TentarLer("11pt") ──▶ Medida(11, Pt)
Medida(11, Pt) ──▶ MedidaJson.Write / MedidaBson.Serialize ──▶ medida.ToString() ──▶ "11pt"
```

#### `Medida`

```csharp
public enum UnidadeDeMedida { Mm, Cm, Pt, Em, Percentual }

/// "20mm", "11pt", "0.65em", "30%". Não há campo em que se escreva uma expressão.
[JsonConverter(typeof(MedidaJson))]
[BsonSerializer(typeof(MedidaBson))]
public readonly record struct Medida(double Valor, UnidadeDeMedida Unidade)
{
    // O texto de cada unidade, da mais longa para a mais curta.
    private static readonly (string Texto, UnidadeDeMedida Unidade)[] Sufixos =
    [
        ("mm", UnidadeDeMedida.Mm), ("cm", UnidadeDeMedida.Cm),
        ("pt", UnidadeDeMedida.Pt), ("em", UnidadeDeMedida.Em),
        ("%",  UnidadeDeMedida.Percentual),
    ];

    /// "11pt" → Medida(11, Pt). Devolve false para qualquer coisa fora do formato.
    public static bool TentarLer(string? texto, out Medida medida)
    {
        medida = default;
        if (texto is null) return false;
        var limpo = texto.Trim();

        foreach (var (sufixo, unidade) in Sufixos)
        {
            if (!limpo.EndsWith(sufixo, StringComparison.Ordinal)) continue;

            // InvariantCulture: "0.65" é sempre ponto, qualquer que seja o
            // idioma do servidor. Com a cultura pt-BR, "0.65" não leria.
            var numero = limpo[..^sufixo.Length];
            if (!double.TryParse(numero, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
                return false;

            medida = new Medida(valor, unidade);
            return true;
        }
        return false;
    }

    /// A unidade de um eixo vem como texto ("pt"). Mesma tabela de sufixos.
    public static bool TentarDe(ValorDeEixo.Medida valor, out Medida medida)
    {
        medida = default;
        var achado = Array.Find(Sufixos, s => s.Texto == valor.Unidade);
        if (achado.Texto is null) return false;
        medida = new Medida(valor.Valor, achado.Unidade);
        return true;
    }

    /// Medida(11, Pt) → "11pt"; Medida(0.65, Em) → "0.65em". Inteiro sai sem
    /// casas decimais, como no Swift.
    public override string ToString()
    {
        var numero = Valor == Math.Round(Valor)
            ? ((long)Valor).ToString(CultureInfo.InvariantCulture)
            : Valor.ToString("G6", CultureInfo.InvariantCulture);
        return numero + Array.Find(Sufixos, s => s.Unidade == Unidade).Texto;
    }
}

public sealed class MedidaJson : JsonConverter<Medida>
{
    public override Medida Read(ref Utf8JsonReader leitor, Type tipo, JsonSerializerOptions opcoes) =>
        Medida.TentarLer(leitor.GetString(), out var medida)
            ? medida
            : throw new JsonException($"“{leitor.GetString()}” não é uma medida");

    public override void Write(Utf8JsonWriter escritor, Medida medida, JsonSerializerOptions opcoes) =>
        escritor.WriteStringValue(medida.ToString());
}

public sealed class MedidaBson : SerializerBase<Medida>
{
    public override Medida Deserialize(BsonDeserializationContext contexto, BsonDeserializationArgs args)
    {
        var texto = contexto.Reader.ReadString();
        return Medida.TentarLer(texto, out var medida)
            ? medida
            : throw new FormatException($"“{texto}” não é uma medida");
    }

    public override void Serialize(BsonSerializationContext contexto, BsonSerializationArgs args, Medida medida) =>
        contexto.Writer.WriteString(medida.ToString());
}
```

#### `Cor`

```csharp
/// "#1c1917": 3, 4, 6 ou 8 dígitos hexadecimais. Nada de nomes, nada de funções.
[JsonConverter(typeof(CorJson))]
[BsonSerializer(typeof(CorBson))]
public readonly record struct Cor(string Hexadecimal)
{
    public static bool TentarLer(string? texto, out Cor cor)
    {
        cor = default;
        var limpo = texto?.Trim();
        if (limpo is null || !limpo.StartsWith('#')) return false;

        var digitos = limpo[1..];
        if (digitos.Length is not (3 or 4 or 6 or 8) || !digitos.All(char.IsAsciiHexDigit))
            return false;

        cor = new Cor(limpo);
        return true;
    }

    public override string ToString() => Hexadecimal;
}

// CorJson e CorBson: iguais a MedidaJson e MedidaBson, trocando Medida por Cor.
```

`Medida?` e `Cor?` não precisam de nada a mais: os dois serializadores usam o
conversor do tipo de base, e gravam `null` como `null`.

#### Os enums

Em disco, cada caso é o identificador em *camelCase* (`Centro` → `"centro"`,
`TituloDoDocumento` → `"tituloDoDocumento"`). `Papel` é a exceção: os nomes são
os do Typst, e dois não derivam do identificador.

```csharp
[JsonConverter(typeof(EnumComoTextoJson<Papel>))]
public enum Papel
{
    [JsonStringEnumMemberName("a4")]        A4,
    [JsonStringEnumMemberName("a5")]        A5,
    [JsonStringEnumMemberName("us-letter")] Carta,
    [JsonStringEnumMemberName("us-legal")]  Oficio,
}

[JsonConverter(typeof(EnumComoTextoJson<Alinhamento>))]
public enum Alinhamento { Esquerda, Centro, Direita, Justificado }

[JsonConverter(typeof(EnumComoTextoJson<Caixa>))]
public enum Caixa { Normal, Alta }

[JsonConverter(typeof(EnumComoTextoJson<CampoDerivado>))]
public enum CampoDerivado { TituloDoDocumento, TipoDocumental, NumeroDaPagina, TotalDePaginas }

/// JSON: o conversor de enum do .NET, com camelCase. Existe só porque um
/// atributo não aceita argumentos de construtor.
public sealed class EnumComoTextoJson<T>() : JsonStringEnumConverter<T>(JsonNamingPolicy.CamelCase)
    where T : struct, Enum;

/// BSON: a mesma regra — [JsonStringEnumMemberName] quando existe, senão
/// camelCase —, para o banco gravar exatamente o que o JSON grava.
public sealed class EnumComoTextoBson<T> : SerializerBase<T> where T : struct, Enum
{
    private static readonly Dictionary<T, string> ParaTexto = Enum.GetValues<T>().ToDictionary(
        v => v,
        v => typeof(T).GetField(v.ToString())!
                 .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
             ?? JsonNamingPolicy.CamelCase.ConvertName(v.ToString()));

    private static readonly Dictionary<string, T> DeTexto =
        ParaTexto.ToDictionary(par => par.Value, par => par.Key);

    public override T Deserialize(BsonDeserializationContext contexto, BsonDeserializationArgs args)
    {
        var texto = contexto.Reader.ReadString();
        return DeTexto.TryGetValue(texto, out var valor)
            ? valor
            : throw new FormatException($"“{texto}” não é um {typeof(T).Name}");
    }

    public override void Serialize(BsonSerializationContext contexto, BsonSerializationArgs args, T valor) =>
        contexto.Writer.WriteString(ParaTexto[valor]);
}
```

**Os enums são a exceção ao atributo no BSON.** `[BsonSerializer]` aceita
classe, struct, interface, propriedade e campo, mas **não enum**. Por isso
`EnumComoTextoBson<T>` é registrado na inicialização (abaixo).

#### As peças da moldura

No `template.json`, uma peça é um objeto plano com o campo `"peca"` dizendo qual
é: `{ "peca": "campo", "campo": "numeroDaPagina" }`. No C#, é uma hierarquia de
records, e os dois serializadores só precisam saber que o discriminador se chama
`"peca"`. No JSON, isso é atributo:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "peca")]
[JsonDerivedType(typeof(PecaTexto), "texto")]
[JsonDerivedType(typeof(PecaCampo), "campo")]
[JsonDerivedType(typeof(PecaLiteral), "literal")]
[JsonDerivedType(typeof(PecaImagem), "imagem")]
[JsonDerivedType(typeof(PecaQuebra), "quebra")]
public abstract record Peca;
```

No BSON, os nomes vêm dos `[BsonDiscriminator]` das subclasses, e o nome do
campo é uma convenção registrada na inicialização.

#### O que sobra para a inicialização

Só o que não tem forma de atributo:

```csharp
public static class SerializacaoDaFormatacao
{
    /// As opções de JSON de ParaTemplateJson e DeTemplateJson. Os conversores
    /// de valor não estão aqui: vêm dos atributos dos tipos.
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,          // Pagina → "pagina"
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,                   // "peca" não precisa vir primeiro
    };

    /// Chamado uma vez, no Program.cs, antes de qualquer acesso ao MongoDB.
    public static void RegistrarBson()
    {
        // Enums: [BsonSerializer] não se aplica a enum.
        BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Papel>());
        BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Alinhamento>());
        BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Caixa>());
        BsonSerializer.RegisterSerializer(new EnumComoTextoBson<CampoDerivado>());

        // Convenções: o nome dos campos discriminadores e o camelCase.
        BsonSerializer.RegisterDiscriminatorConvention(
            typeof(Formatacao), new ScalarDiscriminatorConvention("familia"));
        BsonSerializer.RegisterDiscriminatorConvention(
            typeof(FormatacaoDocumentoDeNegocio.Peca), new ScalarDiscriminatorConvention("peca"));
        ConventionRegistry.Register(
            "MarkDocs: camelCase",
            new ConventionPack { new CamelCaseElementNameConvention() },
            tipo => tipo.Namespace?.StartsWith("MarkDocs.") == true);
    }
}
```

Com isso, a mesma formatação tem a mesma forma nos três lugares — no
`template.json` de fábrica, no JSON entregue ao `MarkDocs.Core` e no MongoDB:

```js
texto: { familia: "Libertinus Serif", tamanho: "11pt", cor: "#1c1917", idioma: "pt" }
```

Um teste guarda essa propriedade: para cada template de fábrica,
`DeTemplateJson → ToBson → Deserialize → ParaTemplateJson` devolve um JSON
semanticamente igual ao original.

Dois campos do `template.json` ficam fora da `Formatacao`, porque não são
formatação:

| Campo | Origem em `ParaTemplateJson` |
|---|---|
| `id`, `nome` | o documento |
| `tipoDocumental` | `Tipo` do documento (a moldura pode imprimi-lo) |

`Fontes` fica **dentro**, e gravada. Não pode ser derivada das famílias em uso:
é também o domínio do eixo `texto.familia`, e derivá-la reduziria a escolha de
fonte à fonte já escolhida. O `MarkDocs.Core` recusa, ao carregar, uma formatação
que use uma família fora dela.

### `Template`

Um preset: valores de formatação e os ativos que eles citam. Não é referenciado
pelo documento.

```csharp
public sealed class Template
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    public Formatacao Formatacao { get; set; } = default!;
    public List<ReferenciaDeAtivo> Ativos { get; set; } = [];
}
```

Os templates de fábrica nascem de `contrato-padrao.json`, `orcamento-padrao.json`
e `recibo-padrao.json`, lidos com `FormatacaoDocumentoDeNegocio.DeTemplateJson`.

## As operações

**Aplicar um template** é copiar a formatação e trocar os ativos do template,
mantendo as imagens do Markdown:

```csharp
documento.Formatacao = template.Formatacao.Copiar();
documento.Ativos = [.. documento.Ativos.Where(a => a.Papel == PapelDoAtivo.Imagem), .. template.Ativos];
```

**Compor:**

```csharp
var carregado = RegistroDeGeradores.Carregar(
    doc.Familia,
    doc.Formatacao.ParaTemplateJson(doc.Titulo, doc.Tipo),
    await bucket.BaixarAsync(doc.Ativos));          // nome → bytes

var pdf = renderizador.Pdf(doc.Corpo, carregado);
```

**Editar:** a tela desenha os controles a partir de `carregado.Eixos`, que traz
domínio, limites e valor atual. Uma edição vira
`doc.Formatacao.Aplicar(eixo, valor)`, que limita o valor ao domínio do eixo e
o grava no campo certo, e o documento é gravado. `ComAjustes` não é usado.

## Regras

1. **Objeto no bucket é imutável.** Timbre novo é objeto novo, com id novo. É o
   que faz copiar uma referência equivaler a copiar o arquivo, e o que impede
   que mudar um template altere documentos já criados.
2. **Os limites vêm do `MarkDocs.Core`** (`TemplateCarregado.Eixos`), não da
   classe C#. `Aplicar` recebe o eixo inteiro e limita ao domínio dele, como
   o gerador faz; nenhum número de limite é escrito no app.
3. **`Esquema` é o esquema da família**, e não `ValorTypst.versaoDoVocabulario`,
   que é interno ao pacote e não se guarda.
4. **Esquema novo que quebre compatibilidade** pede migração em lote dos
   documentos e dos templates. O `Esquema` gravado identifica quem ainda não
   migrou.

## O que esta escolha custa

- **`Aplicar` duplica o mapeamento de `Eixos.swift`.** Dois testes vigiam a
  duplicação:
  - para cada template de fábrica, `json → FormatacaoDocumentoDeNegocio →
    ParaTemplateJson → Carregar` emite o mesmo `.typ` que `json → Carregar`;
  - para cada eixo de `carregado.Eixos`, inclusive com valor fora do domínio,
    `Aplicar(eixo, v)` emite o mesmo `.typ` que `ComAjustes({eixo.Id: v})`.
    Esse teste também falha quando o `MarkDocs.Core` ganha um eixo que a classe
    ainda não conhece.
- **"Voltar ao template" é aplicar o template de novo, inteiro.** Sem camada de
  ajustes, não se desfaz um eixo isolado.

## Pendência

**Imagem no Markdown ainda não existe no `NotiqTypst`, nem, portanto, no
`MarkDocs.Core`.** `ArvoreDeMarkdown`
reduz `![alt](…)` ao texto alternativo. Os ativos com papel `Imagem` podem ser
guardados desde já, mas só aparecem no PDF depois de o vocabulário ganhar o
elemento: `BlocoDoc`/`InlineDoc`, `ValorTypst` e `base.typ`, com
`versaoDoVocabulario` subindo. A mudança acontece **primeiro no Swift**, para os
dois pacotes continuarem passando os mesmos testes. A convenção proposta é o
Markdown citar a imagem pelo `Nome` do ativo (`![Planta](planta.png)`).

---

# Parte II — Usando `IDocumento`

## Quem recebe `IDocumento`

O `MarkDocs.Core` **não** recebe um `IDocumento`. O pacote continua recebendo só a
família, o JSON do template, os ativos e o Markdown. `IDocumento` é o contrato
do **lado do app**, consumido pela ponte que monta essas entradas:

```csharp
public sealed class Compositor(RenderizadorPDF renderizador, IBucketDeAtivos bucket)
{
    public async Task<byte[]> PdfAsync(IDocumento doc, CancellationToken ct = default)
    {
        var carregado = RegistroDeGeradores.Carregar(
            doc.Familia,
            doc.Formatacao.ParaTemplateJson(doc.Titulo, doc.Tipo),
            await bucket.BaixarAsync(doc.Ativos, ct));
        return renderizador.Pdf(doc.Corpo, carregado);
    }
}
```

Qualquer classe que implemente `IDocumento` é composta assim, venha de qualquer
coleção. Vários tipos de documento podem morar numa mesma coleção ou em coleções
separadas, e o `Compositor` não muda.

## Tipos de documento: quando há classe nova

Variar entre contrato, orçamento e recibo **não** pede classe nova: é o `Tipo` e
um template diferente.

Classe nova se justifica quando a entidade tem **dados e regras próprias** — um
`ContratoDeLocacao` com `Locador`, `Locatario` e `Valor`, por exemplo. Ela
implementa `IDocumento` e acrescenta o que é dela; a interface não vê esses
campos.

## O que não está no `Corpo` não vai ao papel

Esse é o comportamento desejado, e não uma limitação. Não há canal paralelo de
metadados entre a entidade e o PDF (doc 1, §2.2): o que deve aparecer no papel
está escrito no Markdown.

Campos estruturados, quando existirem, servem à **lógica que os escreve no
`Corpo`** de forma estruturada — uma tabela de partes, uma cláusula de valor.
A entidade gera ou atualiza o Markdown a partir deles; o `Compositor` continua
lendo só o `Corpo`.

A única exceção é o `Tipo`, que a moldura pode imprimir por
`CampoDerivado.TipoDocumental`.

## O acoplamento com o MongoDB

Com `Id` do tipo `ObjectId`, a interface depende de `MongoDB.Bson`, e
`Formatacao` também (`[BsonDiscriminator]`, `Copiar` via BSON). Isso não
restringe a coleção, mas restringe o banco. Para o MVP é aceitável.

Se a biblioteca um dia precisar valer fora do MongoDB, o `Id` vira `string` e os
atributos BSON passam para um `BsonClassMap` registrado pelo app.

O mapeamento polimórfico de `Formatacao` — discriminador por família e as
subclasses conhecidas — é registrado **uma vez**, no início da aplicação, e vale
para qualquer coleção onde a formatação apareça, inclusive a de `Template`.

---

# Parte III — Como a serialização funciona no .NET

Esta parte explica o mecanismo por trás da seção *Os valores, compartilhados
pelas famílias*: como um serializador do .NET descobre que `Medida` é
`"11pt"`, por que quase tudo é declarado no próprio tipo, e o que ainda
precisa ser registrado na inicialização.

## 1. Dois serializadores

O app usa **duas bibliotecas de serialização independentes**:

| | `System.Text.Json` | Driver do MongoDB (`MongoDB.Bson`) |
|---|---|---|
| Formato | JSON (texto) | BSON (binário do MongoDB) |
| Usado em | `ParaTemplateJson`, `DeTemplateJson`, respostas da API | ler e gravar coleções |
| Peça que converte um tipo | `JsonConverter<T>` | `IBsonSerializer<T>` (`SerializerBase<T>`) |
| Atributo que a liga ao tipo | `[JsonConverter(typeof(…))]` | `[BsonSerializer(typeof(…))]` |
| Registro alternativo | a lista `Converters` de um `JsonSerializerOptions` | `BsonSerializer.RegisterSerializer`, **global** ao processo |

Uma não enxerga o que foi declarado para a outra. Por isso `Medida` tem dois
conversores e dois atributos. Os dois conversores chamam o mesmo
`Medida.TentarLer`, e a regra do formato fica num lugar só.

## 2. O que um serializador faz por padrão

Sem nenhuma indicação, os dois serializadores percorrem o objeto por
**reflexão**, propriedade por propriedade, e para cada uma perguntam *"como eu
converto um valor deste tipo?"*:

- para os tipos que já conhecem (`string`, `int`, `double`, `bool`, listas,
  dicionários), a resposta vem pronta;
- para um tipo que não conhecem, a resposta padrão é **tratá-lo como objeto** e
  repetir o processo nas propriedades dele.

Esse padrão produz o resultado errado para `Medida`. Ela é um record com duas
propriedades, então viraria:

```json
"tamanho": { "Valor": 11, "Unidade": 2 }
```

e não `"tamanho": "11pt"`, que é o que o `template.json` diz e o que o
`MarkDocs.Core` espera. E ao ler `"11pt"` de volta, o serializador esperaria um
objeto, encontraria uma string e falharia.

O que muda isso é responder a pergunta antes que o padrão responda: *"quando
encontrar um `Medida`, não desça nas propriedades — chame este código"*. Há
duas formas de dar essa resposta.

## 3. Primeira forma: o atributo no tipo (a adotada)

```csharp
[JsonConverter(typeof(MedidaJson))]
[BsonSerializer(typeof(MedidaBson))]
public readonly record struct Medida(double Valor, UnidadeDeMedida Unidade) { … }
```

Um atributo é um metadado gravado no próprio tipo, legível por reflexão. Antes
de cair no padrão, cada serializador olha o tipo da propriedade e procura o seu
atributo. Achando, instancia o conversor indicado (uma vez, e o guarda) e o usa
para aquele tipo em todo lugar.

O que acontece ao gravar `Texto.Tamanho` em JSON:

```
o serializador vê uma propriedade do tipo Medida
  → o tipo Medida tem [JsonConverter(typeof(MedidaJson))]
  → chama MedidaJson.Write(escritor, medida, opcoes)
      → escritor.WriteStringValue(medida.ToString())   // "11pt"
```

E ao ler a mesma propriedade do MongoDB:

```
o driver vê uma propriedade do tipo Medida
  → o tipo Medida tem [BsonSerializer(typeof(MedidaBson))]
  → chama MedidaBson.Deserialize, que lê a string "11pt"
      → Medida.TentarLer("11pt") → Medida(11, Pt)
```

O ganho é que **a declaração viaja com o tipo**. Não importa quem serializa —
`ParaTemplateJson`, o driver em qualquer coleção, o ASP.NET Core devolvendo uma
resposta da API, um teste —: todos acham o atributo. Não há registro para
esquecer nem ordem de inicialização para errar.

Foi adotado para `Medida` e `Cor` nos dois serializadores, e para os enums no
JSON. O custo é o tipo conhecer as duas bibliotecas de serialização; aqui ele
não é novo, porque a biblioteca já depende de `MongoDB.Bson` pelo `ObjectId`.

## 4. Segunda forma: o registro na inicialização

Onde não há atributo possível, a resposta é dada por registro.

**No JSON**, o registro é um objeto `JsonSerializerOptions`, e só vale onde
for passado. O `System.Text.Json` **não tem configuração global mutável**, por
decisão de projeto: `JsonSerializerOptions.Default` é somente leitura, e o
motivo declarado é que uma biblioteca não deve mudar, sem aviso, o JSON de outra
no mesmo processo.

Isso não obriga quem usa a formatação a informar opções. Neste app, as opções
não carregam conversores — eles vêm dos atributos —, só três ajustes que não
têm forma de atributo no nível do tipo: a política de nomes em camelCase,
omitir `null` e aceitar o campo `"peca"` fora da primeira posição. E elas são
usadas **em dois lugares só**, ambos dentro da própria formatação:

```csharp
// dentro de ParaTemplateJson e DeTemplateJson — e em nenhum outro lugar
JsonSerializer.SerializeToUtf8Bytes(…, SerializacaoDaFormatacao.Json);
JsonSerializer.Deserialize<FormatacaoDocumentoDeNegocio>(json, SerializacaoDaFormatacao.Json);
```

O resto do app chama `formatacao.ParaTemplateJson(…)` e nunca chama
`JsonSerializer` sobre uma formatação. O efeito é o de uma configuração global:
ninguém fora desses dois métodos sabe que as opções existem.

O mais perto de "global" que o .NET oferece para JSON é a configuração do
**ASP.NET Core**: as opções que ele usa para ler requisições e escrever
respostas são definidas uma vez para a aplicação inteira, com
`ConfigureHttpJsonOptions`. Elas já usam camelCase por padrão, e por isso a API
não precisa de nada.

**No BSON**, o registro é **global**: o driver não recebe opções a cada
chamada, e consulta sempre o mesmo registro estático do processo.

```csharp
BsonSerializer.RegisterSerializer(new EnumComoTextoBson<Papel>());
```

Ele é necessário para duas coisas que não têm forma de atributo:

| O quê | Por que não é atributo |
|---|---|
| os serializadores dos enums | `[BsonSerializer]` aceita classe, struct, interface, propriedade e campo, mas não enum |
| as convenções: discriminador `"familia"` em `Formatacao`, `"peca"` em `Peca`, nomes em camelCase | são regras de mapeamento, não conversores de um tipo |

Três regras decorrem de o registro BSON ser global:

1. **Uma vez só.** Registrar o mesmo tipo duas vezes lança
   `BsonSerializationException: There is already a serializer registered for
   type Papel`. Em testes que sobem o app várias vezes, isso aparece logo.
2. **Antes do primeiro uso.** Na primeira vez que o driver lê ou grava uma
   classe, ele monta e **congela** o mapa dela (o `BsonClassMap`), com o
   serializador de cada propriedade já resolvido. Registrar depois não muda o
   mapa congelado.
3. **Vale para o processo inteiro**, em qualquer coleção. Para este app, é o
   desejado.

## 5. Onde a inicialização fica num app ASP.NET Core

No `Program.cs`, **antes** de qualquer coisa que toque o MongoDB:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Enums e convenções do MarkDocs no driver. Medida e Cor não precisam: têm atributo.
SerializacaoDaFormatacao.RegistrarBson();

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

var app = builder.Build();
// …
app.Run();
```

A API que devolve uma `Formatacao` ao front-end não precisa de configuração: o
ASP.NET Core já usa camelCase por padrão, e `Medida`, `Cor` e os enums trazem
os próprios conversores.

Os testes que tocam o MongoDB precisam da mesma chamada a `RegistrarBson()`,
uma vez por execução — normalmente num *fixture* compartilhado. Os testes que só
usam JSON não precisam de nada.

## 6. Um documento lido do MongoDB, passo a passo

```
colecao.Find(…).FirstAsync()
  → o BSON do documento chega do servidor
  → mapa de DocumentoDoApp: propriedade Formatacao, do tipo Formatacao (abstrata)
      → convenção registrada: o discriminador é o campo "familia"
      → "documento-de-negocio" → mapa de FormatacaoDocumentoDeNegocio
  → propriedade Pagina → mapa de PaginaFormatada
      → propriedade Papel, do tipo Papel (enum)
          → registro global: EnumComoTextoBson<Papel> lê "us-letter" → Papel.Carta
      → propriedade Margem → propriedade Topo, do tipo Medida
          → atributo: MedidaBson lê "32mm" → Medida.TentarLer → Medida(32, Mm)
```

Na volta, para compor, a mesma formatação vira JSON com
`SerializacaoDaFormatacao.Json`, passando por `MedidaJson.Write` e
`EnumComoTextoJson<Papel>`, e chega ao `MarkDocs.Core` como `"32mm"` e
`"us-letter"` outra vez.

## 7. Como saber que funcionou

| Sintoma | Causa |
|---|---|
| No MongoDB aparece `papel: "Carta"` ou `papel: 2` | `RegistrarBson()` não foi chamado, ou foi chamado depois do primeiro uso |
| `FormatException` ao ler um enum do MongoDB | o banco tem `"us-letter"` e o processo atual não registrou `EnumComoTextoBson<Papel>` |
| `BsonSerializationException: There is already a serializer registered` | `RegistrarBson()` chamado duas vezes |
| No MongoDB aparece `_t` em vez de `familia` | a convenção de discriminador não foi registrada |
| No MongoDB aparece `tamanho: { Valor: 11, Unidade: 2 }` | o atributo `[BsonSerializer]` foi removido de `Medida` |

O teste de ida e volta da Parte I
(`DeTemplateJson → ToBson → Deserialize → ParaTemplateJson`) passa por todos
esses pontos, e falha em qualquer um dos casos acima.
