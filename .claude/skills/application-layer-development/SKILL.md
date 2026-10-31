---
name: application-layer-development
description: "Implementa a camada de aplicação de um slice .NET: commands/queries MediatR, handlers, repositories, método de extensão Add<Slice>Module para DI, e testes xUnit. Use quando o usuário fornecer documento de especificação de operações ou texto descrevendo transações a implementar."
---

# Application Layer Skill (.NET)

## Objetivo

Implementar a camada de aplicação do slice a partir da especificação de operações:
- Commands e Queries MediatR (`record : IRequest<T>`) com handlers (`IRequestHandler<TRequest,TResponse>`).
- Repositories (interface em `Infrastructure/Persistence/Interfaces/`, implementação em `Repositories/`).
- Método de extensão `Add<Slice>Module(IServiceCollection, IConfiguration)` em `DependencyInjection/ServiceCollectionExtensions.cs` com todos os registros do slice colocalizados.
- Chamada `builder.Services.Add<Slice>Module(builder.Configuration)` no `Program.cs` do Host (`src/<Produto>/`).
- Testes xUnit para regras críticas em `tests/<Produto>.<Slice>.Tests/`.

## Quando Usar

Quando explicitamente instruído pelo usuário E quando houver `<SLICE>_APPLICATION_OPERATIONS.md` em `skill-outputs/domain-modeling/` E um documento com requisitos adicionais em `skill-inputs/application-layer-development/`.

## Modelo Mental: Slice vs Layer

- **Slice = projeto .NET** (uma `.csproj` em `src/`). Ex: `AppTemplate.Contatos`, `<Produto>.<Slice>`.
- **Layer = camada arquitetural** que vive **dentro** de um slice: `Domain/`, `Application/`, `Infrastructure/`, `DependencyInjection/`.
- **No escopo desta skill (criação inicial do projeto), Domain e Application são layers que co-existem no mesmo slice.** Esta skill é executada logo após `domain-modeling` para o mesmo `<Produto>.<Slice>` e adiciona `Application/`, `Infrastructure/` e `DependencyInjection/` no csproj que já contém `Domain/`.
- Isso **não** é uma regra absoluta da arquitetura: nada impede que, no futuro, slices auxiliares sejam criados referenciando o slice de fundação. Mas isso está fora do escopo da criação inicial e não é tratado por esta skill.

## Processo de Execução

### Passo 1: Criar Estrutura de Diretórios

Dentro de `src/<Produto>.<Slice>/`:

```
Application/
├── Common/
│   └── Exceptions.cs                   # se necessário cross-cutting no slice
├── Commands/                           # records IRequest<T> + handlers — flat, sem subpasta por agregado
│   ├── <Acao1>.cs                      # record + IRequestHandler no mesmo arquivo
│   └── <Acao2>.cs
├── Queries/                            # records IRequest<T> + handlers — flat
│   ├── <Consulta1>.cs
│   └── <Consulta2>.cs
└── EventHandlers/
    └── <Evento>Handler.cs              # INotificationHandler<<Evento>>
Infrastructure/
└── Persistence/
    ├── Interfaces/
    │   └── I<Entidade>Repository.cs
    ├── Repositories/
    │   └── <Entidade>Repository.cs
    └── Configuration/
        └── <Slice>Options.cs
DependencyInjection/
└── ServiceCollectionExtensions.cs
```

E em `tests/`:

```bash
dotnet new xunit -n <Produto>.<Slice>.Tests -o tests/<Produto>.<Slice>.Tests -f net10.0
dotnet sln add tests/<Produto>.<Slice>.Tests/<Produto>.<Slice>.Tests.csproj
dotnet add tests/<Produto>.<Slice>.Tests reference src/<Produto>.<Slice>
```

### Passo 2: Implementar Repositories

```csharp
// src/<Produto>.<Slice>/Infrastructure/Persistence/Interfaces/I<Entidade>Repository.cs
using MongoDB.Bson;
using <Produto>.<Slice>.Domain.Entities;

namespace <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;

public interface I<Entidade>Repository
{
    Task<<Entidade>?> ObterPorIdAsync(ObjectId id, CancellationToken ct = default);
    Task<bool> ExisteAsync(ObjectId id, CancellationToken ct = default);
    Task<List<<Entidade>>> ListarAsync(CancellationToken ct = default);
    Task CriarAsync(<Entidade> entidade, CancellationToken ct = default);
    Task AtualizarAsync(<Entidade> entidade, CancellationToken ct = default);
    Task<bool> RemoverAsync(ObjectId id, CancellationToken ct = default);
}
```

```csharp
// src/<Produto>.<Slice>/Infrastructure/Persistence/Repositories/<Entidade>Repository.cs
using MongoDB.Bson;
using MongoDB.Driver;
using <Produto>.<Slice>.Domain.Entities;
using <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;

namespace <Produto>.<Slice>.Infrastructure.Persistence.Repositories;

public class <Entidade>Repository : I<Entidade>Repository
{
    private const string CollectionName = "<slice>.<entidades>";

    private readonly IMongoCollection<<Entidade>> _collection;
    private readonly IMongoSessionAccessor _sessionAccessor;

    public <Entidade>Repository(IMongoDatabase database, IMongoSessionAccessor sessionAccessor)
    {
        _collection = database.GetCollection<<Entidade>>(CollectionName);
        _sessionAccessor = sessionAccessor;
    }

    public async Task<<Entidade>?> ObterPorIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var filter = Builders<<Entidade>>.Filter.Eq(x => x.Id, id);
        return _sessionAccessor.Session is { } session
            ? await _collection.Find(session, filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExisteAsync(ObjectId id, CancellationToken ct = default)
    {
        var filter = Builders<<Entidade>>.Filter.Eq(x => x.Id, id);
        return _sessionAccessor.Session is { } session
            ? await _collection.Find(session, filter).Limit(1).AnyAsync(ct)
            : await _collection.Find(filter).Limit(1).AnyAsync(ct);
    }

    public async Task<List<<Entidade>>> ListarAsync(CancellationToken ct = default)
    {
        var filter = Builders<<Entidade>>.Filter.Empty;
        return _sessionAccessor.Session is { } session
            ? await _collection.Find(session, filter).ToListAsync(ct)
            : await _collection.Find(filter).ToListAsync(ct);
    }

    public async Task CriarAsync(<Entidade> entidade, CancellationToken ct = default)
    {
        if (_sessionAccessor.Session is { } session)
            await _collection.InsertOneAsync(session, entidade, cancellationToken: ct);
        else
            await _collection.InsertOneAsync(entidade, cancellationToken: ct);
    }

    public async Task AtualizarAsync(<Entidade> entidade, CancellationToken ct = default)
    {
        var filter = Builders<<Entidade>>.Filter.Eq(x => x.Id, entidade.Id);
        if (_sessionAccessor.Session is { } session)
            await _collection.ReplaceOneAsync(session, filter, entidade, cancellationToken: ct);
        else
            await _collection.ReplaceOneAsync(filter, entidade, cancellationToken: ct);
    }

    public async Task<bool> RemoverAsync(ObjectId id, CancellationToken ct = default)
    {
        var filter = Builders<<Entidade>>.Filter.Eq(x => x.Id, id);
        var result = _sessionAccessor.Session is { } session
            ? await _collection.DeleteOneAsync(session, filter, cancellationToken: ct)
            : await _collection.DeleteOneAsync(filter, ct);
        return result.DeletedCount > 0;
    }
}
```

### Passo 3: Implementar Commands

Padrão: `record` + `IRequestHandler` no **mesmo arquivo**.

```csharp
// src/<Produto>.<Slice>/Application/Commands/<Acao>.cs
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using <Produto>.<Slice>.Domain.Entities;
using <Produto>.<Slice>.Domain.Exceptions;
using <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;

namespace <Produto>.<Slice>.Application.Commands;

public record <Acao>Command(
    string <Campo1>,
    int <Campo2>,
    string? <CampoOpcional> = null
) : IRequest<<Entidade>>;

public class <Acao>Handler : IRequestHandler<<Acao>Command, <Entidade>>
{
    private readonly I<Entidade>Repository _repository;
    private readonly ILogger<<Acao>Handler> _logger;

    public <Acao>Handler(
        I<Entidade>Repository repository,
        ILogger<<Acao>Handler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<<Entidade>> Handle(<Acao>Command request, CancellationToken ct)
    {
        // 1. Pré-condições (consultas)
        // if (!await _outroRepository.ExisteAsync(request.OutroId, ct))
        //     throw new <Outro>NotFoundException(request.OutroId.ToString());

        // 2. Criar/mutar entidade via método de fábrica do domínio
        var entidade = <Entidade>.Criar(request.<Campo1>, request.<Campo2>);

        // 3. Persistir
        await _repository.CriarAsync(entidade, ct);

        _logger.LogInformation(
            "<Entidade> criada {Id} <Campo1> {<Campo1>}.",
            entidade.Id, entidade.<Campo1>);

        return entidade;
    }
}
```

### Passo 4: Implementar Queries

Queries podem retornar entidades concretas ou projeções (records).

```csharp
// src/<Produto>.<Slice>/Application/Queries/Obter<Entidade>PorId.cs
using MediatR;
using MongoDB.Bson;
using <Produto>.<Slice>.Domain.Entities;
using <Produto>.<Slice>.Domain.Exceptions;
using <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;

namespace <Produto>.<Slice>.Application.Queries;

public record Obter<Entidade>PorIdQuery(ObjectId Id) : IRequest<<Entidade>>;

public class Obter<Entidade>PorIdHandler
    : IRequestHandler<Obter<Entidade>PorIdQuery, <Entidade>>
{
    private readonly I<Entidade>Repository _repository;

    public Obter<Entidade>PorIdHandler(I<Entidade>Repository repository)
    {
        _repository = repository;
    }

    public async Task<<Entidade>> Handle(Obter<Entidade>PorIdQuery request, CancellationToken ct)
        => await _repository.ObterPorIdAsync(request.Id, ct)
           ?? throw new <Entidade>NotFoundException(request.Id.ToString());
}
```

Query paginada com projeção:

```csharp
// src/<Produto>.<Slice>/Application/Queries/Listar<Entidades>.cs
using MediatR;

namespace <Produto>.<Slice>.Application.Queries;

public record <Entidade>Resumo(string Id, string <Campo1>, DateTimeOffset CriadoEm);

public record Listar<Entidades>Result(
    IReadOnlyList<<Entidade>Resumo> Items,
    long Total,
    int Page,
    int PageSize);

public record Listar<Entidades>Query(int Page = 1, int PageSize = 20)
    : IRequest<Listar<Entidades>Result>;

public class Listar<Entidades>Handler
    : IRequestHandler<Listar<Entidades>Query, Listar<Entidades>Result>
{
    private readonly I<Entidade>Repository _repository;

    public Listar<Entidades>Handler(I<Entidade>Repository repository)
        => _repository = repository;

    public async Task<Listar<Entidades>Result> Handle(
        Listar<Entidades>Query request, CancellationToken ct)
    {
        var entidades = await _repository.ListarAsync(ct);
        var total = entidades.Count;
        var page = entidades
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new <Entidade>Resumo(e.Id.ToString(), e.<Campo1>, e.CriadoEm))
            .ToList();
        return new Listar<Entidades>Result(page, total, request.Page, request.PageSize);
    }
}
```

### Passo 5: Implementar Event Handlers (Opcional)

```csharp
// src/<Produto>.<Slice>/Application/EventHandlers/<Evento>Handler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using <Produto>.<Slice>.Domain.Events;

namespace <Produto>.<Slice>.Application.EventHandlers;

public class <Evento>Handler : INotificationHandler<<Evento>Event>
{
    private readonly ILogger<<Evento>Handler> _logger;

    public <Evento>Handler(ILogger<<Evento>Handler> logger)
        => _logger = logger;

    public Task Handle(<Evento>Event notification, CancellationToken ct)
    {
        _logger.LogInformation("Processando <Evento> para {Id}", notification.<Entidade>Id);
        // ...
        return Task.CompletedTask;
    }
}
```

### Passo 6: DependencyInjection — `Add<Slice>Module`

```csharp
// src/<Produto>.<Slice>/DependencyInjection/ServiceCollectionExtensions.cs
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using <Produto>.<Slice>.Infrastructure.Persistence.Configuration;
using <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;
using <Produto>.<Slice>.Infrastructure.Persistence.Repositories;

namespace <Produto>.<Slice>.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra todos os serviços do slice <Slice> no container de DI.
    /// Inclui: MediatR, Repositories, Options e Hosted Services do slice.
    /// </summary>
    public static IServiceCollection Add<Slice>Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ============================================================
        // MEDIATR — Command/Query/Event handlers deste assembly
        // ============================================================
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(<Slice>Marker).Assembly));

        // ============================================================
        // OPTIONS
        // ============================================================
        services.Configure<<Slice>Options>(configuration.GetSection("<Slice>"));

        // ============================================================
        // REPOSITORIES
        // ============================================================
        services.AddScoped<I<Entidade>Repository, <Entidade>Repository>();

        // ============================================================
        // SERVIÇOS EXTERNOS / INTEGRAÇÕES (se aplicável)
        // ============================================================
        // services.AddSingleton<I<ServicoExterno>Client>(...);

        // ============================================================
        // HOSTED SERVICES (se aplicável)
        // ============================================================
        // services.AddHostedService<<Slice>IndexesHostedService>();

        return services;
    }
}
```

### Passo 7: Registrar o Módulo no WebApi

Editar `src/<Produto>.WebApi/Program.cs` para incluir:

```csharp
using <Produto>.<Slice>.DependencyInjection;

// ... builder = WebApplication.CreateBuilder(args);

builder.Services.Add<Slice>Module(builder.Configuration);
```

### Passo 8: Testes xUnit

```csharp
// tests/<Produto>.<Slice>.Tests/<Produto>.<Slice>.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageReference Include="MongoDB.Driver" Version="3.0.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/<Produto>.<Slice>/<Produto>.<Slice>.csproj" />
  </ItemGroup>
</Project>
```

#### Fixture de MongoDB de teste

```csharp
// tests/<Produto>.<Slice>.Tests/MongoFixture.cs
using MongoDB.Driver;
using Xunit;

namespace <Produto>.<Slice>.Tests;

public class MongoFixture : IAsyncLifetime
{
    private MongoClient? _client;
    public IMongoDatabase Database { get; private set; } = default!;

    public Task InitializeAsync()
    {
        var uri = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                  ?? "mongodb://localhost:27017/?replicaSet=rs0";
        _client = new MongoClient(uri);
        Database = _client.GetDatabase($"test_<slice>_{Guid.NewGuid():N}");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_client is not null)
            await _client.DropDatabaseAsync(Database.DatabaseNamespace.DatabaseName);
    }
}

[CollectionDefinition("Mongo")]
public class MongoCollection : ICollectionFixture<MongoFixture> { }
```

#### Teste de regra crítica

```csharp
// tests/<Produto>.<Slice>.Tests/Application/Commands/<Acao>HandlerTests.cs
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using <Produto>.<Slice>.Application.Commands;
using <Produto>.<Slice>.Infrastructure.Persistence.Interfaces;
using <Produto>.<Slice>.Infrastructure.Persistence.Repositories;
using <Produto>.<Slice>.Infrastructure.Persistence.Transactions;

namespace <Produto>.<Slice>.Tests.Application.Commands;

[Collection("Mongo")]
public class <Acao>HandlerTests
{
    private readonly MongoFixture _mongo;

    public <Acao>HandlerTests(MongoFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task Criar_com_dados_validos_persiste_entidade()
    {
        var sessionAccessor = Substitute.For<IMongoSessionAccessor>();
        var repo = new <Entidade>Repository(_mongo.Database, sessionAccessor);
        var handler = new <Acao>Handler(repo, NullLogger<<Acao>Handler>.Instance);

        var entidade = await handler.Handle(
            new <Acao>Command("<valor>", 42),
            CancellationToken.None);

        Assert.NotEqual(default, entidade.Id);
        var persistida = await repo.ObterPorIdAsync(entidade.Id);
        Assert.NotNull(persistida);
        Assert.Equal("<valor>", persistida!.<Campo1>);
    }

    [Fact]
    public async Task Criar_com_campo_invalido_lanca_ArgumentException()
    {
        var sessionAccessor = Substitute.For<IMongoSessionAccessor>();
        var repo = new <Entidade>Repository(_mongo.Database, sessionAccessor);
        var handler = new <Acao>Handler(repo, NullLogger<<Acao>Handler>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.Handle(new <Acao>Command("", 42), CancellationToken.None));
    }
}
```

### Passo 9: Atualizar README

Adicionar seção de **comportamentos e regras** (não catálogo de operações):

```markdown
## Slice `<Slice>`: Comportamentos e Regras

### Visão Geral

O slice `<Slice>` gerencia <descrição>. <Contexto de negócio>.

### Ciclo de Vida de `<Entidade>`

```
<ESTADO_A> → <ESTADO_B> → <ESTADO_C>
                ↓
         <ESTADO_TERMINAL>
```

**Transições:**
- `<ESTADO_A>` → `<ESTADO_B>`: <comando/condição>
- `<ESTADO_B>` → `<ESTADO_C>`: <comando/condição>

### Regras Críticas

| Regra | Descrição | Onde é aplicada |
|-------|-----------|-----------------|
| <Nome> | <Descrição> | <Handler/entidade> |

### Eventos

- `<Entidade><Evento>Event` — emitido quando <condição>, consumido por <handler>.

### Índices MongoDB

- `<slice>.<entidades>` — índice único em `<Campo>`.
```

### Passo 10: Documento de Saída

Criar `skill-outputs/application-layer-development/<SLICE>_IMPLEMENTED_OPERATIONS.md`:

```markdown
# Operações Implementadas: <Nome do Slice>

Projeto: `src/<Produto>.<Slice>/`
Assembly: `<Produto>.<Slice>.dll`
Marker: `<Produto>.<Slice>.<Slice>Marker`
Módulo DI: `services.Add<Slice>Module(configuration)`

## Commands

| Command | Handler | Retorno | Descrição |
|---------|---------|---------|-----------|
| `<Acao1>Command` | `<Acao1>Handler` | `<Entidade>` | <sucinta> |
| `<Acao2>Command` | `<Acao2>Handler` | `Unit` | <sucinta> |

## Queries

| Query | Handler | Retorno |
|-------|---------|---------|
| `Obter<Entidade>PorIdQuery` | `Obter<Entidade>PorIdHandler` | `<Entidade>` |
| `Listar<Entidades>Query` | `Listar<Entidades>Handler` | `Listar<Entidades>Result` |

## Eventos (INotification)

| Evento | Emitido por | Handlers |
|--------|-------------|----------|
| `<Entidade><Evento>Event` | `<Acao>Handler` | `<Evento>Handler` |

## Repositories

| Repository | Coleção | Operações |
|------------|---------|-----------|
| `<Entidade>Repository` | `<slice>.<entidades>` | ObterPorId, Listar, Criar, Atualizar, Remover |

## DI Wiring

```csharp
// Chamada em Program.cs:
builder.Services.Add<Slice>Module(builder.Configuration);
```

Registros efetuados:
- `AddMediatR` com `<Slice>Marker.Assembly`
- `Configure<<Slice>Options>("<Slice>")`
- `I<Entidade>Repository` → `<Entidade>Repository` (Scoped)

## Para Próxima Etapa (API Endpoints)

Rotas sugeridas (implementar em `src/<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs`):

- `POST /api/<recurso>` → `<Acao1>Command`
- `GET /api/<recurso>` → `Listar<Entidades>Query`
- `GET /api/<recurso>/{id}` → `Obter<Entidade>PorIdQuery`
- `PUT /api/<recurso>/{id}` → `<Acao2>Command`
- `DELETE /api/<recurso>/{id}` → `<Remover<Entidade>>Command`
```

## Outputs Esperados

1. `Application/Commands/*.cs` e `Application/Queries/*.cs` (record + handler).
2. `Infrastructure/Persistence/Interfaces/` + `Infrastructure/Persistence/Repositories/`.
3. `DependencyInjection/ServiceCollectionExtensions.cs` com `Add<Slice>Module`.
4. Chamada a `Add<Slice>Module(...)` adicionada ao `Program.cs` do WebApi.
5. Projeto `tests/<Produto>.<Slice>.Tests` com testes para regras críticas.
6. README atualizado (comportamentos, ciclo de vida, regras, eventos, índices).
7. `skill-outputs/application-layer-development/<SLICE>_IMPLEMENTED_OPERATIONS.md` para a Etapa 3.

## Checklist de Validação

- [ ] Todos os commands/queries do documento de entrada foram implementados.
- [ ] Commands são `record : IRequest<T>` e handlers são `IRequestHandler<TRequest,TResponse>`.
- [ ] Handlers verificam pré-condições antes de mutar estado.
- [ ] Repositories não contêm lógica de negócio.
- [ ] `Add<Slice>Module` concentra TODOS os registros do slice — `Program.cs` não registra nada do slice diretamente.
- [ ] `<Slice>Marker` é usado para descoberta MediatR.
- [ ] Testes cobrem regras críticas listadas no documento de entrada.
- [ ] `dotnet build` passa em toda a solution.
- [ ] `dotnet test` passa para o projeto de testes do slice.
- [ ] README documenta comportamentos, não cataloga operações (catálogo fica no doc de saída).
