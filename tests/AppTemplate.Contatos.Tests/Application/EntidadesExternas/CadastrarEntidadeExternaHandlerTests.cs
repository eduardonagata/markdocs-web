using AppTemplate.Contatos.Application.EntidadesExternas.Commands;
using AppTemplate.Contatos.Application.EntidadesExternas.Queries;
using AppTemplate.Contatos.Domain.Enums;
using AppTemplate.Contatos.Infrastructure.Persistence.Repositories;
using AppTemplate.Contatos.Infrastructure.Persistence.Transactions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using NSubstitute;

namespace AppTemplate.Contatos.Tests.Application.EntidadesExternas;

[Collection("Mongo")]
public class CadastrarEntidadeExternaHandlerTests
{
    private readonly MongoFixture _mongo;

    public CadastrarEntidadeExternaHandlerTests(MongoFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task Cadastrar_persiste_entidade_e_lista_retorna_ela()
    {
        if (!_mongo.IsAvailable)
            return; // Mongo nao disponivel no ambiente de CI; teste pulado implicitamente.

        var sessionAccessor = new MongoSessionAccessor();
        var repo = new EntidadeExternaRepository(_mongo.Database, sessionAccessor);
        var publisher = Substitute.For<IPublisher>();

        var handler = new CadastrarEntidadeExternaCommandHandler(
            repo,
            _mongo.Client,
            sessionAccessor,
            publisher,
            NullLogger<CadastrarEntidadeExternaCommandHandler>.Instance);

        var criadoPor = ObjectId.GenerateNewId();
        var entidade = await handler.Handle(
            new CadastrarEntidadeExternaCommand(
                "ACME LTDA",
                new[] { PapelEntidadeExterna.Cliente, PapelEntidadeExterna.Fornecedor },
                criadoPor),
            CancellationToken.None);

        Assert.NotEqual(ObjectId.Empty, entidade.Id);

        var queryHandler = new ObterEntidadesExternasQueryHandler(repo);
        var todas = await queryHandler.Handle(new ObterEntidadesExternasQuery(), CancellationToken.None);

        Assert.Single(todas);
        Assert.Equal(entidade.Id, todas[0].Id);
        Assert.Equal("ACME LTDA", todas[0].Nome);
        Assert.Equal(criadoPor, todas[0].CriadoPor);

        await publisher.Received(1).Publish(
            Arg.Any<AppTemplate.Contatos.Domain.Events.EntidadeExternaCadastradaEvent>(),
            Arg.Any<CancellationToken>());
    }
}
