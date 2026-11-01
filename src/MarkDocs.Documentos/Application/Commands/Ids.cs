using MongoDB.Bson;

namespace MarkDocs.Documentos.Application.Commands;

/// <summary>Um id que não é um <see cref="ObjectId"/> é um recurso que não existe.</summary>
internal static class Ids
{
    public static ObjectId Ler(string id, Func<Exception> naoEncontrado) =>
        ObjectId.TryParse(id, out var objectId) ? objectId : throw naoEncontrado();
}
