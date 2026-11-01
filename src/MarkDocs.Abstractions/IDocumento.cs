using MongoDB.Bson;

namespace MarkDocs.Abstractions;

/// <summary>
/// O contrato de um documento do app: o que a composição precisa para refazer
/// o PDF, guardado no próprio documento — sem depender de um template que
/// mude depois.
/// </summary>
/// <remarks>
/// O <c>MarkDocs.Core</c> não recebe um <see cref="IDocumento"/>: ele continua
/// recebendo só a família, o JSON do template, os ativos e o Markdown.
/// <see cref="IDocumento"/> é o contrato do lado do app, consumido pela ponte
/// que monta essas entradas. Qualquer classe que o implemente é composta do
/// mesmo jeito, venha de qualquer coleção.
/// </remarks>
public interface IDocumento
{
    ObjectId Id { get; }
    string Titulo { get; }

    /// <summary>O tipo documental: <c>"Contrato"</c>. A moldura pode imprimi-lo.</summary>
    string Tipo { get; }

    /// <summary>O conteúdo, em Markdown. A fonte de verdade.</summary>
    string Corpo { get; }

    /// <summary>A formatação <b>completa</b> do documento, numa classe da família.</summary>
    Formatacao Formatacao { get; }

    /// <summary>As referências ao bucket que a composição lê.</summary>
    IReadOnlyList<ReferenciaDeAtivo> Ativos { get; }

    string Familia => Formatacao.Familia;
    int Esquema => Formatacao.Esquema;
}

/// <summary>
/// Uma referência a um objeto do bucket. <c>Nome</c> é como o
/// <c>template.json</c> ou o Markdown cita o ativo (<c>timbre-p1.svg</c>,
/// <c>imagens/planta.png</c>); <c>Id</c> é a chave do objeto no bucket.
/// </summary>
/// <remarks>
/// <b>Objeto no bucket é imutável.</b> Timbre novo é objeto novo, com id novo.
/// É o que faz copiar uma referência equivaler a copiar o arquivo.
/// </remarks>
public sealed record ReferenciaDeAtivo(string Id, string Nome, PapelDoAtivo Papel);

public enum PapelDoAtivo { Template, Imagem }
