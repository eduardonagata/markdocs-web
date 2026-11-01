using MarkDocs.Abstractions;
using MarkDocs.Abstractions.Formatacoes;
using MarkDocs.Documentos.Domain.ValueObjects;
using static MarkDocs.Abstractions.Formatacoes.FormatacaoDocumentoDeNegocio;

namespace MarkDocs.Documentos.Domain.Entities;

/// <summary>
/// O papel timbrado: a imagem de fundo das páginas, que o <c>MarkDocs.Core</c>
/// lê de <c>moldura.paginas.{todas, primeira, demais}.fundo</c> e estica ao
/// tamanho do papel. O app não ajusta a imagem: quem a envia a prepara na
/// proporção do papel.
/// </summary>
/// <remarks>
/// <para>
/// No esquema, uma variante declarada (<c>primeira</c>, <c>demais</c>)
/// substitui <c>todas</c> <b>inteira</b>. Daí as duas regras daqui:
/// </para>
/// <list type="bullet">
///   <item>o timbre de <b>todas</b> as páginas vai para <c>todas</c> e para
///         cada variante declarada — senão o Contrato padrão, que declara
///         <c>demais</c>, o perderia da segunda página em diante;</item>
///   <item>declarar <c>primeira</c> ou <c>demais</c> para receber um timbre
///         próprio <b>copia</b> o cabeçalho e o rodapé de <c>todas</c> — senão a
///         página perderia a moldura que tinha.</item>
/// </list>
/// <para>
/// O timbre é ativo de template (<see cref="PapelDoAtivo.Template"/>): vai
/// junto quando o documento vira template, e sai quando outro template é
/// aplicado. Uma referência que nada mais cita sai dos ativos; o objeto fica
/// no bucket, porque pode ser compartilhado com um template.
/// </para>
/// </remarks>
public sealed partial class Documento
{
    /// <summary>O nome do ativo do timbre que vale para a primeira página, ou para as demais.</summary>
    public string? Timbre(PaginasDoTimbre paginas)
    {
        if (Formatacao is not FormatacaoDocumentoDeNegocio { Moldura: { } moldura }) return null;
        var variante = paginas switch
        {
            PaginasDoTimbre.Primeira => moldura.Primeira ?? moldura.Todas,
            PaginasDoTimbre.Demais => moldura.Demais ?? moldura.Todas,
            _ => moldura.Todas,
        };
        return variante?.Fundo?.Arquivo is { Length: > 0 } arquivo ? arquivo : null;
    }

    /// <summary>
    /// Põe o timbre nas páginas pedidas e o acrescenta aos ativos de template.
    /// Devolve as referências que deixaram de ser citadas (e saíram dos ativos).
    /// </summary>
    public IReadOnlyList<ReferenciaDeAtivo> DefinirTimbre(PaginasDoTimbre paginas, ReferenciaDeAtivo timbre)
    {
        if (timbre.Papel != PapelDoAtivo.Template)
            throw new ArgumentException("o timbre é um ativo de template", nameof(timbre));

        var formatacao = FormatacaoComMoldura();
        var moldura = formatacao.Moldura ??= new MolduraFormatada();
        foreach (var variante in Variantes(moldura, paginas, criar: true))
            variante.Fundo = new Fundo { Arquivo = timbre.Nome };

        Ativos.RemoveAll(a => a.Papel == PapelDoAtivo.Template && a.Nome == timbre.Nome);
        Ativos.Add(timbre);
        return SoltarAtivosSemCitacao(formatacao);
    }

    /// <summary>Tira o timbre das páginas pedidas. Devolve as referências que deixaram de ser citadas.</summary>
    public IReadOnlyList<ReferenciaDeAtivo> RemoverTimbre(PaginasDoTimbre paginas)
    {
        if (Formatacao is not FormatacaoDocumentoDeNegocio { Moldura: { } moldura } formatacao || Timbre(paginas) is null && paginas != PaginasDoTimbre.Todas)
            return [];

        // Tirar o timbre de uma variante que herda de `todas` é declará-la sem ele.
        foreach (var variante in Variantes(moldura, paginas, criar: paginas != PaginasDoTimbre.Todas))
            variante.Fundo = null;
        return SoltarAtivosSemCitacao(formatacao);
    }

    private FormatacaoDocumentoDeNegocio FormatacaoComMoldura() =>
        Formatacao as FormatacaoDocumentoDeNegocio
        ?? throw new ArgumentException($"a família “{Formatacao.Familia}” não tem papel timbrado");

    /// <summary>As variantes que valem para as páginas pedidas — declarando-as, quando <paramref name="criar"/>.</summary>
    private static IEnumerable<VarianteDePagina> Variantes(MolduraFormatada moldura, PaginasDoTimbre paginas, bool criar)
    {
        switch (paginas)
        {
            case PaginasDoTimbre.Todas:
                moldura.Todas ??= new VarianteDePagina();
                yield return moldura.Todas;
                if (moldura.Primeira is { } primeira) yield return primeira;
                if (moldura.Demais is { } demais) yield return demais;
                break;
            case PaginasDoTimbre.Primeira:
                if (moldura.Primeira is null && criar) moldura.Primeira = CopiaDaMolduraDeTodas(moldura);
                if (moldura.Primeira is { } p) yield return p;
                break;
            case PaginasDoTimbre.Demais:
                if (moldura.Demais is null && criar) moldura.Demais = CopiaDaMolduraDeTodas(moldura);
                if (moldura.Demais is { } d) yield return d;
                break;
        }
    }

    /// <summary>Uma variante nova com o cabeçalho, o rodapé e o fundo de <c>todas</c>.</summary>
    private static VarianteDePagina CopiaDaMolduraDeTodas(MolduraFormatada moldura) => moldura.Todas is { } todas
        ? new VarianteDePagina
        {
            Fundo = todas.Fundo is { } f ? new Fundo { Arquivo = f.Arquivo, Formato = f.Formato } : null,
            Cabecalho = Copiar(todas.Cabecalho),
            Rodape = Copiar(todas.Rodape),
        }
        : new VarianteDePagina();

    private static Faixa? Copiar(Faixa? faixa) => faixa is null
        ? null
        : new Faixa
        {
            Afastamento = faixa.Afastamento,
            Estilo = faixa.Estilo,
            Esquerda = faixa.Esquerda?.ToList(),
            Centro = faixa.Centro?.ToList(),
            Direita = faixa.Direita?.ToList(),
        };

    /// <summary>Os nomes de ativo que a formatação cita: o logotipo, os fundos e as imagens das faixas.</summary>
    internal static HashSet<string> AtivosCitados(FormatacaoDocumentoDeNegocio formatacao)
    {
        var citados = new HashSet<string>(StringComparer.Ordinal);
        if (formatacao.Identidade?.Logotipo is { Length: > 0 } logotipo) citados.Add(logotipo);
        var moldura = formatacao.Moldura;
        foreach (var variante in new[] { moldura?.Todas, moldura?.Primeira, moldura?.Demais }.OfType<VarianteDePagina>())
        {
            if (variante.Fundo?.Arquivo is { Length: > 0 } fundo) citados.Add(fundo);
            foreach (var faixa in new[] { variante.Cabecalho, variante.Rodape }.OfType<Faixa>())
            {
                foreach (var peca in (faixa.Esquerda ?? []).Concat(faixa.Centro ?? []).Concat(faixa.Direita ?? []))
                    if (peca is PecaImagem imagem) citados.Add(imagem.Arquivo);
            }
        }
        return citados;
    }

    /// <summary>Tira dos ativos os timbres que nada mais cita. Os demais ativos de template ficam.</summary>
    private List<ReferenciaDeAtivo> SoltarAtivosSemCitacao(FormatacaoDocumentoDeNegocio formatacao)
    {
        var citados = AtivosCitados(formatacao);
        var soltos = Ativos
            .Where(a => a.Papel == PapelDoAtivo.Template && a.Nome.StartsWith(PrefixoDoTimbre, StringComparison.Ordinal) && !citados.Contains(a.Nome))
            .ToList();
        foreach (var solto in soltos) Ativos.Remove(solto);
        return soltos;
    }

    /// <summary>Os timbres enviados pelo app se chamam <c>timbre-&lt;páginas&gt;-&lt;id&gt;.&lt;ext&gt;</c>.</summary>
    public const string PrefixoDoTimbre = "timbre-";
}
