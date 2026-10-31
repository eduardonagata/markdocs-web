# Template .NET (Vertical Slices)

> **Documento de referencia arquitetural.**
> Utilize este template para construir aplicacoes .NET com vertical slices, MediatR para CQRS, endpoints minimal API auto-registraveis e DI colocalizada por slice via metodos de extensao.

---

## Sumario

1. [Visao Geral da Arquitetura](#1-visao-geral-da-arquitetura)
2. [Estrutura de Diretorios](#2-estrutura-de-diretorios)
3. [Ciclo de Vida da Requisicao](#3-ciclo-de-vida-da-requisicao)
4. [Injecao de Dependencias](#4-injecao-de-dependencias)
5. [Endpoints Auto-Registraveis](#5-endpoints-auto-registraveis)
6. [Multi-Tenancy e Isolamento de Dados](#6-multi-tenancy-e-isolamento-de-dados)
7. [Implementacao de Slices](#7-implementacao-de-slices)
8. [Persistencia e Transacoes](#8-persistencia-e-transacoes)
9. [Variaveis de Ambiente](#9-variaveis-de-ambiente)
10. [Execucao](#10-execucao)
11. [Testes](#11-testes)
12. [Constraints Negativos](#12-constraints-negativos)
13. [Checklist de Validacao](#13-checklist-de-validacao)

---

## 1. Visao Geral da Arquitetura

<arquitetura_rigida>

### 1.1 Principios Fundamentais

| Padrao | Proposito |
|--------|-----------|
| **Vertical Slices** | Cada slice e um projeto .NET autocontido (class library) |
| **CQRS com MediatR** | `IRequest`/`IRequestHandler` separa Commands (escrita) e Queries (leitura) |
| **Endpoint Handlers Auto-Registraveis** | Classes que implementam `IEndpointRouteHandler` sao descobertas por reflection |
| **DI Colocalizada** | Cada slice expoe um `AddXxxModule(IConfiguration)` em `DependencyInjection/ServiceCollectionExtensions.cs` |
| **Nada de repositorio centralizado** | Servicos/repositories sao registrados apenas no modulo que os fornece |

### 1.2 Stack

```
Runtime:        .NET 10 (LTS), C# 14
Framework:      ASP.NET Core Minimal APIs
CQRS:           MediatR 12.x
Database:       MongoDB.Driver (configuravel)
Auth:           JWT Bearer (Firebase/Auth0/OIDC compativel)
DI:             Microsoft.Extensions.DependencyInjection (nativo)
Logging:        Microsoft.Extensions.Logging (Application Insights opcional)
Mapping:        AutoMapper (opcional por slice)
```

### 1.3 Camadas da Aplicacao

```
┌────────────────────────────────────────────────────────────────────┐
│                     Presentation (WebApi)                          │
│  - Program.cs: compoe modulos + pipeline                           │
│  - EndpointHandlers/<Slice>/XxxHandler.cs : IEndpointRouteHandler  │
│  - Dto/ : Request/Response records (JSON contract)                 │
│                                                                    │
│                            ▼ ProjectReference                      │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    Slice (class library)                     │  │
│  │  Domain/        -> entidades, value objects, eventos         │  │
│  │  Application/   -> Commands, Queries, Handlers (MediatR)     │  │
│  │  Infrastructure/-> Repositories, integracoes externas        │  │
│  │  DependencyInjection/ServiceCollectionExtensions.cs          │  │
│  │  <Slice>Marker.cs  (para descoberta de MediatR/AutoMapper)   │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
```

</arquitetura_rigida>

---

## 2. Estrutura de Diretorios

<arquitetura_rigida>

```
<Produto>/                              # raiz do repositorio
├── <produto>.sln                       # solution
├── src/
│   ├── <Produto>.WebApi/               # unico projeto web (SDK=Microsoft.NET.Sdk.Web)
│   │   ├── <Produto>.WebApi.csproj
│   │   ├── Program.cs                  # compoe AddXxxModule(...)
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── EndpointHandlers/
│   │   │   ├── IEndpointRouteHandler.cs
│   │   │   ├── IEndpointRouteBuilderExtensions.cs
│   │   │   ├── Health/
│   │   │   │   └── VersionHandler.cs
│   │   │   └── <Slice>/
│   │   │       ├── <Recurso>Handler.cs # implementa IEndpointRouteHandler
│   │   │       └── Dto/
│   │   │           ├── <Acao>Request.cs
│   │   │           └── <Entidade>Response.cs
│   │   ├── Configuration/              # options/records de config especificos da api
│   │   └── Policies/                   # policies, rate limiting, authz
│   │
│   ├── <Produto>.<Slice>/              # class library (SDK=Microsoft.NET.Sdk)
│   │   ├── <Produto>.<Slice>.csproj
│   │   ├── <Slice>Marker.cs            # marker para MediatR/AutoMapper
│   │   ├── Domain/
│   │   │   ├── Entities/
│   │   │   │   └── <Entidade>.cs
│   │   │   ├── ValueObjects/
│   │   │   │   └── <ValueObject>.cs
│   │   │   ├── Events/
│   │   │   │   └── <Evento>Event.cs
│   │   │   └── Exceptions/
│   │   │       └── <Excecao>Exception.cs
│   │   ├── Application/
│   │   │   ├── Common/                 # cross-cutting dentro do slice
│   │   │   ├── <Agregado>/
│   │   │   │   ├── Commands/
│   │   │   │   │   └── <Acao>.cs       # record + IRequestHandler no mesmo arquivo
│   │   │   │   └── Queries/
│   │   │   │       └── <Consulta>.cs
│   │   │   └── EventHandlers/
│   │   │       └── <Evento>Handler.cs
│   │   ├── Infrastructure/
│   │   │   ├── Persistence/
│   │   │   │   ├── Interfaces/
│   │   │   │   │   └── I<Entidade>Repository.cs
│   │   │   │   ├── Repositories/
│   │   │   │   │   └── <Entidade>Repository.cs
│   │   │   │   └── Configuration/
│   │   │   │       └── MongoDBSettings.cs
│   │   │   ├── Services/               # integracoes externas (stripe, e-mail, etc)
│   │   │   └── Workers/                # IHostedService, background jobs
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs  # Add<Slice>Module
│   │
│   └── <Produto>.<OutroSlice>/         # novo slice => novo projeto
│       └── ...
│
├── tests/
│   ├── <Produto>.<Slice>.Tests/
│   │   ├── <Produto>.<Slice>.Tests.csproj
│   │   └── Application/
│   │       └── <Agregado>/
│   │           └── <Acao>HandlerTests.cs
│   └── <Produto>.WebApi.IntegrationTests/
│       └── <Recurso>IntegrationTests.cs
│
├── docs/
│   ├── <SLICE>_APPLICATION_OPERATIONS.md   # output de domain-modeling
│   ├── <SLICE>_IMPLEMENTED_OPERATIONS.md   # output de application-layer
│   └── <SLICE>_API_TYPES.md                # output de api-endpoints
│
├── .claude/
│   ├── README.md
│   └── skills/
│       ├── domain-modeling/SKILL.md
│       ├── application-layer-development/SKILL.md
│       ├── api-endpoints-development/SKILL.md
│       └── dependencies-dump/SKILL.md
│
├── skill-inputs/
├── skill-outputs/
├── Dockerfile
├── .env.example
└── README.md                           # este documento
```

### 2.1 Nomenclatura de Projetos

| Projeto | SDK | Papel |
|---------|-----|-------|
| `<Produto>.WebApi` | `Microsoft.NET.Sdk.Web` | Unico host HTTP. Referencia todos os slices. |
| `<Produto>.<Slice>` | `Microsoft.NET.Sdk` | Vertical slice como class library. |
| `<Produto>.<Slice>.Tests` | `Microsoft.NET.Sdk` | Testes do slice (xUnit). |
| `<Produto>.WebApi.IntegrationTests` | `Microsoft.NET.Sdk` | Testes de integracao HTTP. |

Um slice **nao** referencia outro slice diretamente. Quando houver acoplamento necessario, expose uma interface publica minima e deixe o consumidor depender da interface — o registro da implementacao permanece no modulo de origem.

</arquitetura_rigida>

---

## 3. Ciclo de Vida da Requisicao

<arquitetura_rigida>

### 3.1 Startup (Program.cs)

O `Program.cs` e curto: registra modulos, configura o pipeline HTTP, chama `app.MapEndpoints(Assembly.GetExecutingAssembly())`.

```csharp
using System.Reflection;
using <Produto>.Core.DependencyInjection;
using <Produto>.Accounting.DependencyInjection;
using <Produto>.WebApi.EndpointHandlers;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// 1. Modulos (cada slice registra seus proprios servicos)
builder.Services.AddCoreModule(builder.Configuration);
builder.Services.AddAccountingModule(builder.Configuration);

// 2. Cross-cutting da camada web
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(/* ... */).AddJwtBearer(/* ... */);
builder.Services.AddAuthorization();
builder.Services.AddCors(/* ... */);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Pipeline
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

// 4. Descoberta e registro automatico dos IEndpointRouteHandler do assembly
app.MapEndpoints(Assembly.GetExecutingAssembly());

app.Run();
```

### 3.2 Fluxo de Requisicao

```
1. Request HTTP chega
       │
       ▼
2. Pipeline ASP.NET: CORS -> Auth -> Authorization -> RateLimit
       │
       ▼
3. Rota mapeada por um IEndpointRouteHandler (ex: ItensHandler.MapEndpoints)
       │
       ▼
4. Metodo do handler recebe deps via [FromServices] ou parametros da minimal API
   - IMediator, ILogger<T>, IOptions<...>, HttpContext, request body
       │
       ▼
5. mediator.Send(new XxxCommand(...)) resolve IRequestHandler<TRequest,TResponse>
       │
       ▼
6. IRequestHandler recebe repositories/servicos via construtor (DI)
       │
       ▼
7. Repository executa operacao no banco e retorna dominio
       │
       ▼
8. Handler devolve resposta; endpoint serializa como Results.Ok/Created/...
```

</arquitetura_rigida>

---

## 4. Injecao de Dependencias

<arquitetura_rigida>

### 4.1 Principio: DI Colocalizada

Cada slice expoe exatamente **um** metodo de extensao em `DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
// src/<Produto>.<Slice>/DependencyInjection/ServiceCollectionExtensions.cs
namespace <Produto>.<Slice>.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add<Slice>Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // MediatR e AutoMapper sao registrados pelo assembly do slice via marker
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(<Slice>Marker).Assembly));
        services.AddAutoMapper(typeof(<Slice>Marker).Assembly);

        // Options
        services.Configure<<Slice>Options>(configuration.GetSection("<Slice>"));

        // Repositories
        services.AddScoped<I<Entidade>Repository, <Entidade>Repository>();

        // Integracoes externas
        services.AddSingleton<I<ServicoExterno>Client>(/* ... */);

        // Hosted services
        services.AddHostedService<<Slice>IndexesHostedService>();

        return services;
    }
}

public class <Slice>Marker { }
```

`<Produto>.WebApi/Program.cs` so chama `services.Add<Slice>Module(config)` — nao conhece a lista de repositorios do slice.

### 4.2 Marker Class

Cada slice declara `public class <Slice>Marker { }` no root do projeto. Ele serve de apontador para:
- `RegisterServicesFromAssembly(typeof(<Slice>Marker).Assembly)` (MediatR)
- `AddAutoMapper(typeof(<Slice>Marker).Assembly)` (AutoMapper)

### 4.3 Resolucao em Handlers

Handlers recebem dependencias pelo **construtor**. Nunca use service locator (`IServiceProvider.GetService`) fora do registro de DI.

```csharp
public class <Acao>CommandHandler : IRequestHandler<<Acao>Command, <Resultado>>
{
    private readonly I<Entidade>Repository _repository;
    private readonly ILogger<<Acao>CommandHandler> _logger;

    public <Acao>CommandHandler(
        I<Entidade>Repository repository,
        ILogger<<Acao>CommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<<Resultado>> Handle(<Acao>Command request, CancellationToken ct)
    {
        // ...
    }
}
```

### 4.4 Resolucao em Endpoint Handlers

Metodos de endpoint recebem dependencias como parametros da minimal API — ASP.NET injeta automaticamente servicos registrados no container:

```csharp
internal static async Task<IResult> ObterItensAsync(
    IMediator mediator,
    ILogger<ItensHandler> logger)
{
    var itens = await mediator.Send(new ObterItensQuery());
    return Results.Ok(itens.Select(ToDto).ToList());
}
```

</arquitetura_rigida>

---

## 5. Endpoints Auto-Registraveis

<arquitetura_rigida>

### 5.1 Interface `IEndpointRouteHandler`

```csharp
// src/<Produto>.WebApi/EndpointHandlers/IEndpointRouteHandler.cs
namespace <Produto>.WebApi.EndpointHandlers;

public interface IEndpointRouteHandler
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
```

### 5.2 Extensao de Descoberta

```csharp
// src/<Produto>.WebApi/EndpointHandlers/IEndpointRouteBuilderExtensions.cs
using System.Reflection;

namespace <Produto>.WebApi.EndpointHandlers;

public static class IEndpointRouteBuilderExtensions
{
    public static void MapEndpoints(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var handlerType = typeof(IEndpointRouteHandler);
        var handlers = assembly.GetTypes().Where(t =>
            t.IsClass
            && !t.IsAbstract
            && !t.IsGenericType
            && t.GetConstructor(Type.EmptyTypes) != null
            && handlerType.IsAssignableFrom(t));

        foreach (var type in handlers)
        {
            var instance = (IEndpointRouteHandler)Activator.CreateInstance(type)!;
            instance.MapEndpoints(app);
        }
    }
}
```

### 5.3 Padrao de um Endpoint Handler

```csharp
// src/<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs
using MediatR;
using <Produto>.<Slice>.Application.<Agregado>.Commands;
using <Produto>.<Slice>.Application.<Agregado>.Queries;
using <Produto>.WebApi.EndpointHandlers.<Slice>.Dto;

namespace <Produto>.WebApi.EndpointHandlers.<Slice>;

public class <Recurso>Handler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/<recurso>", CriarAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/api/<recurso>", ListarAsync)
            .RequireAuthorization()
            .Produces<List<<Entidade>Response>>(StatusCodes.Status200OK);

        app.MapGet("/api/<recurso>/{id}", ObterPorIdAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> CriarAsync(
        IMediator mediator,
        Criar<Entidade>Request request,
        ILogger<<Recurso>Handler> logger)
    {
        try
        {
            var entidade = await mediator.Send(new Criar<Entidade>Command(
                request.Campo1,
                request.Campo2));
            return Results.Created($"/api/<recurso>/{entidade.Id}", ToDto(entidade));
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    }

    internal static async Task<IResult> ListarAsync(IMediator mediator)
    {
        var itens = await mediator.Send(new Obter<Entidades>Query());
        return Results.Ok(itens.Select(ToDto).ToList());
    }

    internal static async Task<IResult> ObterPorIdAsync(IMediator mediator, string id)
    {
        try
        {
            var entidade = await mediator.Send(new Obter<Entidade>PorIdQuery(id));
            return Results.Ok(ToDto(entidade));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static <Entidade>Response ToDto(<Entidade> e) => new()
    {
        Id = e.Id.ToString(),
        Campo1 = e.Campo1,
        Campo2 = e.Campo2,
        CriadoEm = e.CriadoEm,
    };
}
```

### 5.4 Regras do Padrao

| Regra | Justificativa |
|-------|---------------|
| Handler e classe publica, nao abstrata, nao generica, com **construtor sem parametros**. | `Activator.CreateInstance` e usado na descoberta. |
| Metodos de endpoint sao `internal static` — dependencias via parametros. | Permite testabilidade direta sem instanciar o handler. |
| DTOs ficam em `EndpointHandlers/<Slice>/Dto/`. | Contratos HTTP nao vazam para o slice. |
| Conversao entidade -> DTO e feita no handler (metodo `ToDto`). | Mantem o slice sem conhecer JSON. |

</arquitetura_rigida>

---

## 6. Multi-Tenancy e Isolamento de Dados

<arquitetura_rigida>

O template suporta dois modos; escolha na implementacao do `Core.<Infrastructure>` e nao mude depois:

| Modo | Descricao |
|------|-----------|
| **Single-tenant** | Um unico database definido em `appsettings`. `IMongoDatabase` e registrado `Singleton` resolvendo `DatabaseName` uma vez. |
| **Multi-tenant por database** | `tenant_id` extraido do JWT (`ClaimTypes.NameIdentifier` + claim custom). `IMongoDatabase` e registrado `Scoped`, e um `ITenantAccessor` resolve `db-{tenant_id}` por requisicao. |

### 6.1 Regras (multi-tenant)

| Regra | Descricao |
|-------|-----------|
| **Database por tenant** | Cada tenant tem seu database: `tid-{tenantId}` (prefixo `tid-` fixo por convencao do template, hardcoded no resolver de `IMongoDatabase` de cada slice). |
| **Nunca filtrar por `TenantId` em queries** | O isolamento e por database, nao por filtro |
| **`tenantId` vem da claim `tid` do JWT** | Nunca aceitar tenantId do body/params |
| **Repositories sao tenant-scoped** | Recebem `IMongoDatabase` ja resolvido por tenant |

### 6.2 Esqueleto de Resolucao Multi-Tenant

Cada slice tem seu proprio `ITenantAccessor` + `IMongoDatabase` scoped.
Exemplo em `src/AppTemplate.Contatos/Infrastructure/Tenancy/`:

```csharp
// ITenantAccessor.cs
public interface ITenantAccessor { string TenantId { get; } }

// HttpContextTenantAccessor.cs
public class HttpContextTenantAccessor : ITenantAccessor
{
    public const string TenantClaimType = "tid";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "Tentativa de resolver TenantId fora de uma requisicao HTTP.");

            var tid = httpContext.User.FindFirstValue(TenantClaimType);
            if (string.IsNullOrWhiteSpace(tid))
                throw new InvalidOperationException("JWT nao carrega a claim \"tid\".");

            return tid;
        }
    }
}
```

Registro em `DependencyInjection/ServiceCollectionExtensions.cs` do slice:

```csharp
// Tenant accessor scoped — resolve por request via JWT claim.
services.AddScoped<ITenantAccessor, HttpContextTenantAccessor>();

// MongoClient singleton (thread-safe, pool compartilhado).
services.AddSingleton<IMongoClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<ContatosOptions>>().Value;
    return new MongoClient(opts.ConnectionString);
});

// IMongoDatabase scoped — nome fixo "tid-{tenantId}" por convencao do template.
services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var tenant = sp.GetRequiredService<ITenantAccessor>().TenantId;
    return client.GetDatabase($"tid-{tenant}");
});
```

O slice define em `<Slice>Options` apenas `ConnectionString`. O prefixo
`tid-` e hardcoded no resolver — e convencao do template, nao configuracao.
Se um produto precisar de outro prefixo, o lugar para mudar e este
resolver (afeta todos os slices consistentemente). O tenantId vem do JWT
em runtime.

**Precondicao de pipeline**: `Program.cs` deve chamar
`builder.Services.AddHttpContextAccessor()` antes dos `Add<Slice>Module`.

### 6.3 Emissao de JWT com `tid`

Em producao, o IdP (Firebase/Entra ID/Auth0) deve ser configurado
para incluir a claim `tid` no JWT emitido. Caminhos tipicos:

- **Firebase/GIP**: `tid` automatico se o usuario pertence a um
  tenant do Identity Platform, via `auth.tenantId` no cliente.
- **Entra ID**: custom claim mapeada no App Registration, ou
  extraida do `oid` + lookup em um servico de tenants.
- **Auth0**: rule ou action adiciona `tid` ao token.

Em desenvolvimento, o projeto `AppTemplate.TestStubs` emite JWTs
com `tid` configuravel (ver secao 10).

</arquitetura_rigida>

---

## 7. Implementacao de Slices

<implementacao_livre>

### 7.1 Passos para Criar um Novo Slice

1. Criar projeto: `dotnet new classlib -n <Produto>.<Slice> -o src/<Produto>.<Slice>` (TargetFramework `net10.0`).
2. Adicionar ao `<produto>.sln`: `dotnet sln add src/<Produto>.<Slice>/<Produto>.<Slice>.csproj`.
3. Adicionar `ProjectReference` em `<Produto>.WebApi.csproj` apontando para o novo slice.
4. Criar `<Slice>Marker.cs` vazio.
5. Criar estrutura `Domain/`, `Application/`, `Infrastructure/`, `DependencyInjection/` conforme secao 2.
6. Implementar `Domain/Entities/<Entidade>.cs` com atributos obrigatorios (`Id`, `CriadoEm` — e `TenantId` se aplicavel).
7. Implementar `Infrastructure/Persistence/Interfaces/I<Entidade>Repository.cs` + implementacao.
8. Implementar `Application/<Agregado>/Commands/<Acao>.cs` (record + `IRequestHandler` no mesmo arquivo).
9. Criar `DependencyInjection/ServiceCollectionExtensions.cs` com `Add<Slice>Module(IConfiguration)`.
10. Registrar `builder.Services.Add<Slice>Module(builder.Configuration)` em `Program.cs`.
11. Criar `EndpointHandlers/<Slice>/<Recurso>Handler.cs` no projeto WebApi — ele e descoberto automaticamente.

### 7.2 Acoplamento entre Slices

Quando slice A precisar de algo de slice B:
- Slice B expoe uma **interface publica minima** em `Application/PublicApi/IXxx.cs`.
- Slice A declara `ProjectReference` para slice B somente na direcao do grafo de dependencia.
- Nunca crie referencias ciclicas. Se aparecer ciclo, extraia as interfaces para um slice `.Shared` ou mova a orquestracao para um slice superior.

</implementacao_livre>

---

## 8. Persistencia e Transacoes

<arquitetura_rigida>

### 8.1 MongoDB Replica Set (se usar MongoDB)

Obrigatorio para transacoes. String de conexao deve conter `replicaSet=rs0` (ou equivalente).

### 8.2 Padrao de Repository

```csharp
public class <Entidade>Repository : I<Entidade>Repository
{
    private readonly IMongoCollection<<Entidade>> _collection;
    private readonly IMongoSessionAccessor _sessionAccessor;

    public <Entidade>Repository(IMongoDatabase db, IMongoSessionAccessor sessionAccessor)
    {
        _collection = db.GetCollection<<Entidade>>("<slice>.<entidades>");
        _sessionAccessor = sessionAccessor;
    }

    public async Task<<Entidade>?> ObterPorIdAsync(ObjectId id)
    {
        var filter = Builders<<Entidade>>.Filter.Eq(x => x.Id, id);
        var find = _sessionAccessor.Session is { } s
            ? _collection.Find(s, filter)
            : _collection.Find(filter);
        return await find.FirstOrDefaultAsync();
    }

    public async Task CriarAsync(<Entidade> entidade)
    {
        if (_sessionAccessor.Session is { } s)
            await _collection.InsertOneAsync(s, entidade);
        else
            await _collection.InsertOneAsync(entidade);
    }
}
```

### 8.3 Ambient Session para Transacoes

```csharp
public interface IMongoSessionAccessor
{
    IClientSessionHandle? Session { get; }
}

// Transacao:
using var session = await client.StartSessionAsync();
session.StartTransaction();
// sessionAccessor.Session passa a ser 'session' neste escopo
// ...
await session.CommitTransactionAsync();
```

### 8.4 Convencao de Colecoes

Prefixar com nome do slice: `<slice>.<entidades>` (snake_case ou minusculo). Ex.: `contatos.entidades_externas`, `accounting.journal_entries`.

</arquitetura_rigida>

---

## 9. Variaveis de Ambiente

### 9.1 Convencao

Env vars seguem convencao UNIX flat: **UPPER_CASE com underscores**, prefixadas com o nome do slice ou do escopo cross-cutting.

| Prefixo | Escopo |
|---------|--------|
| `<SLICE>_*` | Especifico de um slice. Ex.: `CONTATOS_CONNECTION_STRING`. |
| `JWT_*` | Autenticacao em producao. |
| `DEV_JWT_*` | Autenticacao em desenvolvimento via AppTemplate.TestStubs. |
| `CORS_*` | Cross-cutting de web. |

**Nao usamos** o estilo `Slice__PropertyName` do binder hierarquico do ASP.NET (com `__` como separador). A razao: env vars com mistura de case ficam inconsistentes com o resto do ambiente (`JWT_*`, `CORS_*` ja sao UPPER_CASE). Em vez disso, cada slice le suas env vars explicitamente no `Add<Slice>Module` e popula o `Options` manualmente.

**Nota sobre `CONTATOS_*`**: o slice Contatos e apenas o exemplo de referencia embarcado no template. `CONTATOS_CONNECTION_STRING` existe para demonstrar o padrao `<SLICE>_CONNECTION_STRING`. Em produtos reais, cada slice exporta sua propria variavel seguindo o mesmo prefixo (`COMPRAS_CONNECTION_STRING`, `VENDAS_CONNECTION_STRING`, etc.).

**Flexibilidade de cluster por deploy**: ter uma env var por slice e deliberado — e o ponto de escolha entre duas topologias validas:

| Topologia | Configuracao | Quando |
|-----------|--------------|--------|
| **MongoDB compartilhado** | Todas as `<SLICE>_CONNECTION_STRING` apontam para a mesma URI. | Caso comum em MVP/SaaS. Um unico cluster, databases diferentes por tenant (`tid-{tenantId}`), colecoes prefixadas por slice (`contatos.entidades_externas`, `compras.pedidos`). Simples de operar. |
| **Cluster por slice** | Cada `<SLICE>_CONNECTION_STRING` aponta para uma URI distinta. | Quando um slice tem requisitos especiais (isolamento regulatorio, carga desproporcional, ou foi extraido para outro servico/repositorio). Permite mover o slice sem reescrever. |

Trocar entre as topologias e apenas ajustar env vars — o codigo nao muda. O isolamento por tenant (`tid-{tenantId}` por database) permanece consistente em ambas.

### 9.2 Leitura

Em cada slice, no `DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
services.Configure<ContatosOptions>(options =>
{
    options.ConnectionString = configuration["CONTATOS_CONNECTION_STRING"] ?? string.Empty;
    options.DatabaseNamePrefix = configuration["CONTATOS_DATABASE_NAME_PREFIX"] ?? "tid-";
});
```

**Nunca leia `configuration["..."]` fora de `ServiceCollectionExtensions.cs`**. Dependencias recebem `IOptions<TOptions>` injetado.

### 9.3 Localizacao do `.env`

Raiz do repo, ao lado de `.env.example`. O `DotNetEnv.Env.TraversePath().Load()` no topo de `Program.cs` sobe a arvore de diretorios a partir de onde o processo executa ate encontrar o arquivo.

**Em producao**, o `.env` geralmente nao existe — as variaveis sao injetadas diretamente pelo host (Docker `-e`, `systemd Environment=`, Kubernetes Secrets, Azure App Service, etc.). `DotNetEnv.Env.Load()` nao lanca erro se nao encontrar o arquivo; o pipeline padrao do ASP.NET Core le as variaveis direto do ambiente do processo.

### 9.4 Exemplo

```env
# MongoDB — cada slice com seu <SLICE>_CONNECTION_STRING.
# No template, Contatos e a fatia de exemplo.
CONTATOS_CONNECTION_STRING=mongodb://localhost:27017/?replicaSet=rs0

# JWT Bearer (producao)
JWT_AUTHORITY=https://auth.example.com
JWT_ISSUER=https://auth.example.com
JWT_AUDIENCE=app-api

# JWT Bearer (dev, via AppTemplate.TestStubs — ver secao 10.2)
# DEV_JWT_SIGNING_KEY=<32+ bytes aleatorios>
# DEV_JWT_TENANT_ID=dev
# DEV_JWT_ISSUER=apptemplate-dev
# DEV_JWT_AUDIENCE=apptemplate-api

# CORS
CORS_ALLOWED_ORIGINS=http://localhost:3000
```

---

## 10. Execucao

### 10.1 Comandos

```bash
# Restaurar pacotes
dotnet restore

# Desenvolvimento (hot reload)
dotnet watch --project src/<Produto>.WebApi run

# Build
dotnet build -c Release

# Publish
dotnet publish src/<Produto>.WebApi -c Release -o out

# Docker
docker build -t <produto>-webapi .
docker run -p 8080:8080 --env-file .env <produto>-webapi
```

### 10.2 Teste local com o frontend (AppTemplate.TestStubs)

O projeto `src/AppTemplate.TestStubs` existe para destravar testes
ponta-a-ponta com o frontend (`reactjs-app-template`) sem exigir IdP
real configurado nem endpoints transversais implementados.

**Escopo**: o `DEV_JWT_*` e a emissao de JWT sao **facility genericos
do template** — nao especificos do slice Contatos. Qualquer endpoint em
qualquer slice que tenha `.RequireAuthorization()` valida o JWT emitido
por `POST /api/dev/login` normalmente, porque o pipeline de autenticacao
e transversal. Contatos apenas e, no momento, o unico slice de exemplo
que consome e exerce esse JWT; conforme novos slices forem adicionados,
eles participam do mesmo fluxo sem configuracao adicional.

**Em desenvolvimento** (`ASPNETCORE_ENVIRONMENT=Development`), o
`Program.cs` automaticamente:

1. Configura `JwtBearer` para validar contra `DEV_JWT_SIGNING_KEY` (chave
   simetrica HS256) em vez de `JWT_AUTHORITY`.
2. Registra o endpoint `POST /api/dev/login` que aceita qualquer email/senha
   e emite um JWT carregando claim `tid = DEV_JWT_TENANT_ID`.
3. Registra stubs de `GET /api/me` e `GET /api/tenants?email=...`.

**Em producao**, nada disto e ativado.

**Fluxo de teste**:

```
frontend (mainProvider = createDevLoginAuthProvider)
   POST /api/dev/login { email, password }
       -> JWT HS256 com claim tid = "dev"

   GET /api/entidades-externas  (Bearer JWT)
       -> JwtBearer valida com DEV_JWT_SIGNING_KEY
       -> ITenantAccessor le claim "tid"
       -> IMongoDatabase aponta para database "tid-dev"
       -> EntidadeExternaRepository le/escreve em tid-dev.contatos.entidades_externas
```

O fluxo e identico ao de producao — muda apenas o emissor/validador
do JWT. `RequireAuthorization()` permanece em todos os endpoints, e o
multi-tenancy por database e exercitado normalmente.

**Variaveis relevantes** (todas opcionais — defaults em `DevJwtOptions.FromConfiguration`):

- `DEV_JWT_SIGNING_KEY` (min 32 bytes; se ausente, chave volatil por processo)
- `DEV_JWT_TENANT_ID` (default `dev`)
- `DEV_JWT_ISSUER` (default `apptemplate-dev`)
- `DEV_JWT_AUDIENCE` (default `apptemplate-api`)
- `DEV_JWT_EXPIRATION_HOURS` (default `24`)

Ver `src/AppTemplate.TestStubs/README.md` para detalhes.

---

## 11. Testes

| Projeto | Foco |
|---------|------|
| `<Produto>.<Slice>.Tests` | Unit/integration do slice: testa `IRequestHandler` com `IMongoDatabase` real (ou mock) e repositorios. |
| `<Produto>.WebApi.IntegrationTests` | End-to-end HTTP via `WebApplicationFactory<Program>`. |

```bash
# Todos os testes
dotnet test

# Um projeto especifico
dotnet test tests/<Produto>.<Slice>.Tests
```

Nao use mocks de banco em integracao — aponte para um MongoDB de testes (replica set) via `MONGODB_CONNECTION_STRING` de teste. Valide o modo de isolamento por database de teste (ex: `test-{Guid.NewGuid()}`).

---

## 12. Constraints Negativos

<arquitetura_rigida>

| NUNCA | FACA ISSO |
|-------|-----------|
| Criar um unico projeto "App" monolitico | Cada slice e um projeto .NET separado |
| Registrar repositorios do slice A no `Program.cs` | Apenas `Add<Slice>Module` do proprio slice |
| Controllers baseados em `[ApiController]` | Minimal API + `IEndpointRouteHandler` auto-registravel |
| Ler `IConfiguration` diretamente em handlers | Usar `IOptions<T>` injetado |
| Injecao via service locator | Injecao por construtor |
| Filtrar por `TenantId` quando o modo e multi-database | Resolver `IMongoDatabase` tenant-scoped |
| Endpoint handlers chamando repositories diretamente | Sempre via `mediator.Send(...)` |
| Entidade de dominio usada como DTO da API | Mapear para `<Entidade>Response` em `EndpointHandlers/.../Dto` |
| Dependencia ciclica entre slices | Inverter com interface publica; nunca aceitar ciclo |
| Marcar metodos de endpoint como `public` | `internal static` — sao detalhes do host |

</arquitetura_rigida>

---

## 13. Checklist de Validacao

### Arquitetura (Obrigatorio)

- [ ] Cada slice e um projeto `.csproj` separado sob `src/`
- [ ] `Program.cs` apenas compoe `Add<Slice>Module(...)` — nao registra repositorios/servicos por conta propria
- [ ] Cada slice possui `<Slice>Marker.cs` e `DependencyInjection/ServiceCollectionExtensions.cs`
- [ ] Endpoint handlers implementam `IEndpointRouteHandler` e sao descobertos por `app.MapEndpoints(Assembly)`
- [ ] Cada handler tem construtor publico sem parametros
- [ ] Commands/Queries sao `record` com `IRequest<T>`, handlers sao `IRequestHandler<TRequest,TResponse>`
- [ ] Repositories nao contem logica de negocio

### Seguranca (Obrigatorio)

- [ ] `RequireAuthorization()` em todos os endpoints privados
- [ ] `tenant_id` extraido do JWT, nunca de body/params
- [ ] CORS configurado com origens explicitas
- [ ] Nenhum DTO de resposta vaza `TenantId` a menos que propositado

### Codigo (Recomendado)

- [ ] Commands com nomes de verbo: `Criar<Entidade>Command`, `Atualizar<Entidade>Command`
- [ ] Queries com nomes de substantivo: `Obter<Entidade>PorIdQuery`, `Listar<Entidades>Query`
- [ ] Colecoes prefixadas com nome do slice
- [ ] DTOs de API em `records` com `required` properties

---

## Referencias

- [MediatR](https://github.com/jbogard/MediatR)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
- [MongoDB.Driver for .NET](https://mongodb.github.io/mongo-csharp-driver/)
- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
