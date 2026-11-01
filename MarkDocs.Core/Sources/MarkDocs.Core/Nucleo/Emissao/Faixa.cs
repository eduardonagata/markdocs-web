namespace MarkDocs.Core.Nucleo;

/// <summary>
/// Uma faixa de cabeçalho ou rodapé: três fatias e um estilo opcional.
/// </summary>
/// <remarks>
/// São os parâmetros <c>header:</c> e <c>footer:</c> <b>nativos</b> do Typst, e
/// não posicionamento absoluto: a semântica no PDF é a certa para tagging, o
/// espaço é reservado automaticamente, e omitir é não emitir o parâmetro.
/// </remarks>
internal sealed record Faixa(
    IReadOnlyList<PecaDeConteudo> Esquerda,
    IReadOnlyList<PecaDeConteudo> Centro,
    IReadOnlyList<PecaDeConteudo> Direita,
    // O nome de um `estilosDeTexto`. Resolve-se na emissão; o núcleo só sabe
    // que é o nome de uma função já declarada.
    string? Estilo)
{
    public bool Vazia => Esquerda.Count == 0 && Centro.Count == 0 && Direita.Count == 0;
}

/// <summary>O que varia entre a primeira página e as demais.</summary>
internal sealed record VarianteDePagina(
    // O caminho do ativo no Acervo, sob `/template/`.
    string? Fundo = null,
    Faixa? Cabecalho = null,
    Faixa? Rodape = null);

/// <summary>
/// A moldura resolvida: a primeira página e as demais, já com <c>todas</c>
/// aplicada como base.
/// </summary>
internal sealed record Moldura
{
    public VarianteDePagina Primeira { get; init; } = new();
    public VarianteDePagina Demais { get; init; } = new();

    // `header-ascent` e `footer-descent` são propriedades da página, não do
    // conteúdo da faixa — então um afastamento diferente por variante não é
    // expressável. A validação recusa a divergência.
    public Medida? AfastamentoDoCabecalho { get; init; }
    public Medida? AfastamentoDoRodape { get; init; }

    public bool TemFundo => Primeira.Fundo is not null || Demais.Fundo is not null;
    public bool TemCabecalho => !(Primeira.Cabecalho?.Vazia ?? true) || !(Demais.Cabecalho?.Vazia ?? true);
    public bool TemRodape => !(Primeira.Rodape?.Vazia ?? true) || !(Demais.Rodape?.Vazia ?? true);
}
