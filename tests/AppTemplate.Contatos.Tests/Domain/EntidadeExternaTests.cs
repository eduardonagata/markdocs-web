using AppTemplate.Contatos.Domain.Entities;
using AppTemplate.Contatos.Domain.Enums;
using MongoDB.Bson;

namespace AppTemplate.Contatos.Tests.Domain;

public class EntidadeExternaTests
{
    [Fact]
    public void Criar_com_dados_validos_retorna_entidade()
    {
        var criadoPor = ObjectId.GenerateNewId();

        var entidade = EntidadeExterna.Criar(
            nome: "ACME LTDA",
            papeis: new[] { PapelEntidadeExterna.Cliente, PapelEntidadeExterna.Fornecedor },
            criadoPor: criadoPor);

        Assert.NotEqual(ObjectId.Empty, entidade.Id);
        Assert.Equal("ACME LTDA", entidade.Nome);
        Assert.Equal(2, entidade.Papeis.Count);
        Assert.Equal(criadoPor, entidade.CriadoPor);
    }

    [Fact]
    public void Criar_com_nome_vazio_lanca_ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => EntidadeExterna.Criar(
            nome: "  ",
            papeis: new[] { PapelEntidadeExterna.Cliente },
            criadoPor: ObjectId.GenerateNewId()));

        Assert.Equal("nome", ex.ParamName);
    }

    [Fact]
    public void Criar_sem_papeis_lanca_ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => EntidadeExterna.Criar(
            nome: "ACME",
            papeis: Array.Empty<PapelEntidadeExterna>(),
            criadoPor: ObjectId.GenerateNewId()));

        Assert.Equal("papeis", ex.ParamName);
    }

    [Fact]
    public void Criar_com_papeis_duplicados_lanca_ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => EntidadeExterna.Criar(
            nome: "ACME",
            papeis: new[] { PapelEntidadeExterna.Cliente, PapelEntidadeExterna.Cliente },
            criadoPor: ObjectId.GenerateNewId()));

        Assert.Equal("papeis", ex.ParamName);
        Assert.Contains("duplicatas", ex.Message);
    }
}
