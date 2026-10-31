using MediatR;
using MongoDB.Bson;

namespace AppTemplate.Contatos.Domain.Events;

public record EntidadeExternaCadastradaEvent(ObjectId EntidadeExternaId, string Nome) : INotification;
