using System.Text.RegularExpressions;
using MarkDocs.Abstractions;
using MarkDocs.Core;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MarkDocs.Documentos.Domain.Entities;

/// <summary>
/// O documento de negócio do app: Markdown, a formatação completa e as
/// referências aos ativos. <b>Autocontido</b>: guarda tudo o que a composição
/// precisa para refazer o PDF, sem depender de um template que mude depois.
/// </summary>
/// <remarks>
/// Variar entre contrato, orçamento e recibo não pede classe nova: é o
/// <see cref="Tipo"/> e um template diferente. Hoje há um tipo de documento só.
/// </remarks>
public sealed partial class Documento : IDocumento
{
    public const string TituloPadrao = "Documento sem título";

    public ObjectId Id { get; init; }
    public required string Titulo { get; set; }
    public required string Tipo { get; set; }
    public required string Corpo { get; set; }
    public required Formatacao Formatacao { get; set; }
    public List<ReferenciaDeAtivo> Ativos { get; set; } = [];

    /// <summary>O nome do último template aplicado. Informativo: o documento não o referencia.</summary>
    public string? TemplateDeOrigem { get; set; }

    [BsonRepresentation(BsonType.DateTime)]
    public required DateTimeOffset CriadoEm { get; init; }

    [BsonRepresentation(BsonType.DateTime)]
    public required DateTimeOffset AlteradoEm { get; set; }

    IReadOnlyList<ReferenciaDeAtivo> IDocumento.Ativos => Ativos;

    public IEnumerable<ReferenciaDeAtivo> Imagens => Ativos.Where(a => a.Papel == PapelDoAtivo.Imagem);

    /// <summary>
    /// Cria um documento a partir de um template: o título, se não vier, é o
    /// primeiro título do Markdown.
    /// </summary>
    public static Documento Criar(string corpo, string? titulo, string tipo, Template template, DateTimeOffset agora)
    {
        var documento = new Documento
        {
            Id = ObjectId.GenerateNewId(),
            Titulo = string.IsNullOrWhiteSpace(titulo) ? TituloDoMarkdown(corpo) : titulo.Trim(),
            Tipo = tipo,
            Corpo = corpo,
            Formatacao = template.Formatacao,
            CriadoEm = agora,
            AlteradoEm = agora,
        };
        documento.AplicarTemplate(template);
        return documento;
    }

    /// <summary>
    /// Aplicar um template é <b>copiar</b> a formatação e trocar os ativos do
    /// template, mantendo as imagens do Markdown. "Voltar ao template" é
    /// aplicá-lo de novo, inteiro.
    /// </summary>
    public void AplicarTemplate(Template template)
    {
        Formatacao = template.Formatacao.Copiar();
        Ativos = [.. Ativos.Where(a => a.Papel == PapelDoAtivo.Imagem), .. template.Ativos];
        TemplateDeOrigem = template.Nome;
    }

    /// <summary>
    /// Grava uma imagem. Um nome que já existe é substituído — e a referência
    /// antiga volta, para que o objeto dela saia do bucket.
    /// </summary>
    public ReferenciaDeAtivo? GravarImagem(ReferenciaDeAtivo imagem)
    {
        if (imagem.Papel != PapelDoAtivo.Imagem)
            throw new ArgumentException("só imagens são gravadas no documento", nameof(imagem));
        if (!ImagensDoDocumento.NomeValido(imagem.Nome))
            throw new ArgumentException($"“{imagem.Nome}” não é um nome de imagem válido", nameof(imagem));

        var anterior = RemoverImagem(imagem.Nome);
        Ativos.Add(imagem);
        return anterior;
    }

    public ReferenciaDeAtivo? RemoverImagem(string nome)
    {
        var anterior = Imagens.FirstOrDefault(i => i.Nome == nome);
        if (anterior is not null) Ativos.Remove(anterior);
        return anterior;
    }

    /// <summary>
    /// O primeiro título do Markdown, sem a marcação; senão a primeira linha
    /// com texto; senão <see cref="TituloPadrao"/>.
    /// </summary>
    public static string TituloDoMarkdown(string markdown)
    {
        var titulo = PrimeiroTitulo().Match(markdown);
        var texto = titulo.Success
            ? titulo.Groups[1].Value
            : markdown.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('|') && !l.StartsWith("---")) ?? "";

        texto = Marcacao().Replace(texto, "").Trim().TrimEnd('#').Trim();
        if (texto.Length > 120) texto = texto[..120].TrimEnd() + "…";
        return texto.Length == 0 ? TituloPadrao : texto;
    }

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex PrimeiroTitulo();

    [GeneratedRegex(@"^[#>\-*+\s]+|[*_`~]|!?\[|\]\([^)]*\)")]
    private static partial Regex Marcacao();
}
