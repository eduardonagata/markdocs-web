namespace MarkDocs.Core;

/// <summary>
/// A convenção de imagem no Markdown. <b>Extensão do MarkDocs.Core</b>: o
/// NotiqTypst ainda reduz toda imagem ao texto alternativo (ver
/// <c>Docs/Imagens.md</c>).
/// </summary>
/// <remarks>
/// O Markdown cita a imagem por um caminho <b>relativo e fixo</b>:
/// <c>![Planta](imagens/planta.png)</c>. O mesmo texto vale nos três lugares
/// em que o documento existe, sem nunca ser reescrito:
/// <list type="bullet">
///   <item><b>No PDF</b>, o Acervo do motor guarda a imagem em
///   <c>/imagens/planta.png</c>, ao lado de <c>/documento.typ</c>.</item>
///   <item><b>Na web</b>, o caminho é resolvido contra o endereço do
///   documento: <c>/api/documentos/{id}/</c> + <c>imagens/planta.png</c>.</item>
///   <item><b>Numa exportação</b>, a pasta tem <c>documento.md</c> e
///   <c>imagens/planta.png</c>, e qualquer visualizador de Markdown a acha.</item>
/// </list>
/// Só os nomes entregues ao renderizador viram imagem: os demais — inclusive
/// URLs externas e caminhos fora de <c>imagens/</c> — continuam sendo o texto
/// alternativo, porque o motor não tem I/O.
/// </remarks>
public static class ImagensDoDocumento
{
    /// <summary>O diretório relativo que o Markdown cita.</summary>
    public const string Diretorio = "imagens";

    /// <summary>
    /// Um nome é uma única componente de caminho, sem espaço e sem nada que
    /// precise de escape numa URL ou num destino de link Markdown: letras,
    /// dígitos, <c>.</c>, <c>-</c> e <c>_</c>, sem começar por ponto.
    /// </summary>
    public static bool NomeValido(string? nome) =>
        !string.IsNullOrEmpty(nome)
        && nome.Length <= 200
        && nome[0] != '.'
        && nome.All(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_');

    /// <summary>
    /// O nome citado por um destino de imagem, ou <c>null</c> se o destino não
    /// está em <c>imagens/</c>. Aceita <c>imagens/x.png</c> e
    /// <c>./imagens/x.png</c>.
    /// </summary>
    public static string? NomeCitado(string? destino)
    {
        if (string.IsNullOrEmpty(destino)) return null;
        var caminho = destino.StartsWith("./", StringComparison.Ordinal) ? destino[2..] : destino;
        if (!caminho.StartsWith(Diretorio + "/", StringComparison.Ordinal)) return null;
        var nome = Uri.UnescapeDataString(caminho[(Diretorio.Length + 1)..]);
        return NomeValido(nome) ? nome : null;
    }

    /// <summary>A referência que o Markdown deve escrever para <paramref name="nome"/>.</summary>
    public static string Referencia(string nome) => $"{Diretorio}/{nome}";
}
