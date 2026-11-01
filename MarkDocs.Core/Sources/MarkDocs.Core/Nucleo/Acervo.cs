using System.Text;
using MarkDocs.Core.Conteudo;

namespace MarkDocs.Core.Nucleo;

/// <summary>O "World" do Typst.</summary>
/// <remarks>
/// O motor não presume nada sobre o ambiente: não abre arquivos, não lista
/// fontes instaladas, não acessa rede. Ele <b>pergunta</b> — e o Acervo
/// responde de um dicionário em memória com quatro ou cinco entradas que nós
/// escolhemos.
/// </remarks>
internal sealed class Acervo
{
    /// <summary>
    /// O ponto de entrada montado pelo núcleo, igual para toda família. Quatro
    /// linhas de conteúdo, e nenhum canal de dados além dos blocos:
    /// <c>estilo</c> veste, <c>renderizar</c> produz o corpo.
    /// </summary>
    public const string CaminhoDeEntrada = "/documento.typ";
    public const string CaminhoDoTemplate = "/template.typ";

    /// <summary>
    /// O mesmo caminho do NotiqTypst, de propósito: o <c>base.typ</c> é o
    /// contrato do vocabulário, e os testes dos dois pacotes o fixam.
    /// </summary>
    public const string CaminhoDaBase = "/notiq/base.typ";

    /// <summary>Espaço de nomes por template: sem isso duas famílias colidiriam.</summary>
    public const string DiretorioDeAtivos = "/template/";

    /// <summary>
    /// As imagens do documento (extensão do MarkDocs.Core). A raiz do Acervo
    /// espelha a pasta exportada do documento: <c>/documento.typ</c> ao lado
    /// de <c>/imagens/&lt;nome&gt;</c>, que é o que o Markdown cita.
    /// </summary>
    public const string DiretorioDeImagens = "/" + ImagensDoDocumento.Diretorio + "/";

    private readonly Dictionary<string, byte[]> _arquivos = new(StringComparer.Ordinal);

    public Acervo(
        string documento,
        string template,
        string @base,
        IReadOnlyDictionary<string, byte[]> ativos,
        IReadOnlyDictionary<string, byte[]>? imagens = null)
    {
        _arquivos[CaminhoDeEntrada] = Encoding.UTF8.GetBytes(documento);
        _arquivos[CaminhoDoTemplate] = Encoding.UTF8.GetBytes(template);
        _arquivos[CaminhoDaBase] = Encoding.UTF8.GetBytes(@base);
        foreach (var (nome, bytes) in ativos)
            _arquivos[CaminhoDoAtivo(nome)] = bytes;
        foreach (var (nome, bytes) in imagens ?? new Dictionary<string, byte[]>())
            _arquivos[CaminhoDaImagem(nome)] = bytes;
    }

    public IReadOnlyDictionary<string, byte[]> Arquivos => _arquivos;

    public static string CaminhoDoAtivo(string nome) => DiretorioDeAtivos + nome;

    public static string CaminhoDaImagem(string nome) => DiretorioDeImagens + nome;

    /// <summary>O esqueleto de seis linhas. Não muda entre famílias.</summary>
    public static string DocumentoTyp(IReadOnlyList<BlocoDoc> blocos) =>
        $"#import \"{CaminhoDaBase}\": renderizar\n"
        + $"#import \"{CaminhoDoTemplate}\": estilo\n"
        + "\n"
        + $"#let documento = {ValorTypst.Literal(blocos)}\n"
        + "\n"
        + "#show: estilo\n"
        + "#renderizar(documento)";

    private static readonly Lazy<string> TextoDaBase = new(() =>
    {
        using var fluxo = typeof(Acervo).Assembly.GetManifestResourceStream("MarkDocs.Core.base.typ")
            ?? throw new ErroTypst.EntradaInvalida("o recurso base.typ não está no pacote");
        using var leitor = new StreamReader(fluxo, Encoding.UTF8);
        return leitor.ReadToEnd();
    });

    /// <summary>
    /// <c>base.typ</c> é recurso do pacote, e o único arquivo Typst de que
    /// <b>todo</b> template depende.
    /// </summary>
    public static string Base() => TextoDaBase.Value;
}
