using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Domain.Enums;
using AppTemplate.Contatos.Domain.Events;
using AppTemplate.Contatos.Infrastructure.Persistence.Interfaces;
using AppTemplate.Contatos.Infrastructure.Persistence.Transactions;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AppTemplate.Contatos.Application.EntidadesExternas.Commands;

public record CadastrarEntidadeExternaCommand(
    string Nome,
    IReadOnlyList<PapelEntidadeExterna> Papeis,
    ObjectId CriadoPor
) : IRequest<EntidadeExterna>;

public class CadastrarEntidadeExternaCommandHandler
    : IRequestHandler<CadastrarEntidadeExternaCommand, EntidadeExterna>
{
    private readonly IEntidadeExternaRepository _repository;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoSessionAccessor _sessionAccessor;
    private readonly IPublisher _publisher;
    private readonly ILogger<CadastrarEntidadeExternaCommandHandler> _logger;

    public CadastrarEntidadeExternaCommandHandler(
        IEntidadeExternaRepository repository,
        IMongoClient mongoClient,
        IMongoSessionAccessor sessionAccessor,
        IPublisher publisher,
        ILogger<CadastrarEntidadeExternaCommandHandler> logger)
    {
        _repository = repository;
        _mongoClient = mongoClient;
        _sessionAccessor = sessionAccessor;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<EntidadeExterna> Handle(
        CadastrarEntidadeExternaCommand request,
        CancellationToken ct)
    {
        var entidade = EntidadeExterna.Criar(request.Nome, request.Papeis, request.CriadoPor);

        using var session = await _mongoClient.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        _sessionAccessor.Set(session);

        try
        {
            await _repository.CriarAsync(entidade, ct);
            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
        finally
        {
            _sessionAccessor.Clear();
        }

        _logger.LogInformation(
            "EntidadeExterna cadastrada {Id} nome={Nome}.", entidade.Id, entidade.Nome);

        await _publisher.Publish(
            new EntidadeExternaCadastradaEvent(entidade.Id, entidade.Nome), ct);

        return entidade;
    }
}
