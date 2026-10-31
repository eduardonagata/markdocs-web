using AppTemplate.Contatos.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AppTemplate.Contatos.Domain.Entities;

public class EntidadeExterna
{
    [BsonId]
    public required ObjectId Id { get; set; }

    public required string Nome { get; set; }

    public required IReadOnlyList<PapelEntidadeExterna> Papeis { get; set; }

    public required ObjectId CriadoPor { get; set; }

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    public static EntidadeExterna Criar(
        string nome,
        IReadOnlyList<PapelEntidadeExterna> papeis,
        ObjectId criadoPor)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("Nome e obrigatorio.", nameof(nome));

        if (papeis is null || papeis.Count == 0)
            throw new ArgumentException("A entidade deve ter ao menos um papel.", nameof(papeis));

        if (papeis.Distinct().Count() != papeis.Count)
            throw new ArgumentException(
                "Os papeis da entidade externa nao podem conter duplicatas.",
                nameof(papeis));

        return new EntidadeExterna
        {
            Id = ObjectId.GenerateNewId(),
            Nome = nome.Trim(),
            Papeis = papeis,
            CriadoPor = criadoPor,
        };
    }
}
