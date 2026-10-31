---
name: api-endpoints-development
description: "Implementa endpoints HTTP em ASP.NET Core Minimal API via IEndpointRouteHandler auto-registrável, DTOs records (Request/Response) no projeto WebApi, e gera tipos TypeScript para consumo no frontend. Use quando o usuário fornecer rotas/ações a expor via REST."
---

# API Endpoints Skill (.NET)

## Objetivo

Implementar a camada de apresentação HTTP:
- `IEndpointRouteHandler` em `src/<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs`.
- DTOs de request/response em `EndpointHandlers/<Slice>/Dto/`.
- Documentação de tipos TypeScript para o frontend.
- Registro automático via `app.MapEndpoints(Assembly.GetExecutingAssembly())` — **sem** `app.MapXxx` direto no `Program.cs`.

## Quando Usar

Quando explicitamente instruído pelo usuário E houver `skill-outputs/application-layer-development/<SLICE>_IMPLEMENTED_OPERATIONS.md` E um documento com instruções/rotas em `skill-inputs/api-endpoints-development/`.

## Entrada Esperada

Documento ou texto com:
- Rotas HTTP desejadas (método + path).
- Mapeamento para Commands/Queries existentes.
- Campos esperados no request body.
- Formato esperado do response.

## Hierarquia de dependências (ProjectReference)

```
                    <Produto>                      executavel — Program.cs (~5% do codigo)
                   ╱      │      ╲                 chama Add<Slice>Module de cada slice
                  ╱       │       ╲                + app.MapEndpoints(WebApiMarker.Assembly)
                 ╱        ▼        ╲
                ╱   <Produto>.WebApi╲              class library — camada HTTP
               ╱          │          ╲             IEndpointRouteHandler, handlers, DTOs
              ╱           │           ╲            (handlers consomem Commands/Queries
             ╱            │            ╲            de cada slice via IMediator)
            ▼             ▼             ▼
        <Produto>     <Produto>     <Produto>     <Produto>
        .Pedidos      .Contatos     .Catalogo     .Faturamento    ← slices de dominio
                                        │             │             (independentes entre si)
                                        └──────┬──────┘
                                               ▼
                                    <Produto>.Notificacoes         ← slice de servico auxiliar
                                                                     (compartilhado por slices
                                                                      que precisam dele)
```

Cada slice de domínio é referenciado por **dois** projetos a montante:

- **`<Produto>` (executável)** — porque `Program.cs` chama `services.Add<Slice>Module(...)`, que é método de extensão exposto pelo `DependencyInjection/` do slice. Uso direto exige reference explícita.
- **`<Produto>.WebApi`** — porque os endpoint handlers consomem Commands/Queries via `IMediator` e usam tipos de domínio nos DTOs. Também uso direto, também reference explícita.

Regras do grafo (como diretriz, não como dogma):

- Cada projeto declara `ProjectReference` para tudo que usa **diretamente** — não dependa de transitividade para uso direto. Transitividade é só um fallback de compilação; manter as references explícitas mantém o grafo legível e as fronteiras intencionais.
- Slices de domínio em geral **não** se referenciam entre si. Compartilhamento de comportamento acontece preferencialmente via **slices auxiliares** (notificações, billing, feature flags, identidade, etc.) que ambos podem referenciar.
- Eventualmente um slice de domínio pode precisar referenciar outro — esse acoplamento direto é aceitável quando bem justificado e devidamente mapeado (anote a razão num README do slice ou no doc de saída da skill). O default é evitar; a exceção é consciente.
- Slices auxiliares também são class libraries com a mesma estrutura interna (Domain/Application/Infrastructure/DI quando aplicável); eles não são especiais — apenas podem ter múltiplos consumidores em vez de um.

`<Produto>.WebApi` é, ele próprio, um slice — só que da camada HTTP. Concentra interface, handlers e DTOs de todos os recursos juntos. Os slices de domínio permanecem **livres de qualquer artefato HTTP/JSON**.

## Processo de Execução

### Passo 1: Verificar Infraestrutura de Descoberta no WebApi

O projeto `src/<Produto>.WebApi/` (class library — `Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App`) deve conter, **uma única vez**:

```csharp
// src/<Produto>.WebApi/WebApiMarker.cs
namespace <Produto>.WebApi;

/// <summary>
/// Marker para identificar o assembly da camada Web. O Host usa
/// <c>typeof(WebApiMarker).Assembly</c> para descobrir os
/// IEndpointRouteHandler via reflection.
/// </summary>
public class WebApiMarker { }
```

```csharp
// src/<Produto>.WebApi/EndpointHandlers/IEndpointRouteHandler.cs
namespace <Produto>.WebApi.EndpointHandlers;

public interface IEndpointRouteHandler
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
```

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

Como class library não recebe os `using` globais do `Microsoft.NET.Sdk.Web`, declarar no `.csproj` para evitar repetir em cada handler:

```xml
<ItemGroup>
  <Using Include="Microsoft.AspNetCore.Builder" />
  <Using Include="Microsoft.AspNetCore.Http" />
  <Using Include="Microsoft.AspNetCore.Routing" />
</ItemGroup>
```

E no `Program.cs` do **Host** (`src/<Produto>/Program.cs`, projeto `Microsoft.NET.Sdk.Web` separado do WebApi):

```csharp
using <Produto>.WebApi;                  // WebApiMarker
using <Produto>.WebApi.EndpointHandlers; // extensão MapEndpoints

app.MapEndpoints(typeof(WebApiMarker).Assembly);
```

Usar `typeof(WebApiMarker).Assembly` em vez de `Assembly.GetExecutingAssembly()` é o que permite o Host não hospedar handlers próprios — todos vivem em `<Produto>.WebApi`. Se algum dos itens acima não existir, crie.

### Passo 2: Criar Estrutura de Pastas no WebApi

```
src/<Produto>.WebApi/EndpointHandlers/
└── <Slice>/
    ├── <Recurso>Handler.cs           # IEndpointRouteHandler
    └── Dto/
        ├── <Acao>Request.cs          # record
        ├── <Acao>Response.cs         # record
        └── <Entidade>Response.cs     # record
```

### Passo 3: Implementar DTOs como Records

Se algum campo é um enum do domínio do slice, importe-o do namespace `<Produto>.<Slice>.Domain.Enums` — não duplique enums no WebApi.

```csharp
// src/<Produto>.WebApi/EndpointHandlers/<Slice>/Dto/<Acao>Request.cs
namespace <Produto>.WebApi.EndpointHandlers.<Slice>.Dto;

public record <Acao>Request
{
    public required string <Campo1> { get; init; }
    public required int <Campo2> { get; init; }
    public string? <CampoOpcional> { get; init; }
}
```

```csharp
// src/<Produto>.WebApi/EndpointHandlers/<Slice>/Dto/<Entidade>Response.cs
namespace <Produto>.WebApi.EndpointHandlers.<Slice>.Dto;

public record <Entidade>Response
{
    public required string Id { get; init; }
    public required string <Campo1> { get; init; }
    public required int <Campo2> { get; init; }
    public string? <CampoOpcional> { get; init; }
    public required DateTimeOffset CriadoEm { get; init; }
}
```

**Não** exponha `TenantId` no response sem necessidade — é identificador interno.

### Passo 4: Implementar o `IEndpointRouteHandler`

```csharp
// src/<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs
using MediatR;
using MongoDB.Bson;
using <Produto>.<Slice>.Application.Commands;
using <Produto>.<Slice>.Application.Queries;
using <Produto>.<Slice>.Domain.Entities;
using <Produto>.<Slice>.Domain.Exceptions;
using <Produto>.WebApi.EndpointHandlers.<Slice>.Dto;

namespace <Produto>.WebApi.EndpointHandlers.<Slice>;

public class <Recurso>Handler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/<recurso>", CriarAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/<recurso>", ListarAsync)
            .RequireAuthorization()
            .Produces<List<<Entidade>Response>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/<recurso>/{id}", ObterPorIdAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapPut("/api/<recurso>/{id}", AtualizarAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/<recurso>/{id}", RemoverAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Ações específicas:
        app.MapPost("/api/<recurso>/{id}/<acao>", <AcaoEspecifica>Async)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> CriarAsync(
        IMediator mediator,
        <Acao>Request request,
        ILogger<<Recurso>Handler> logger)
    {
        try
        {
            var entidade = await mediator.Send(new <Acao>Command(
                request.<Campo1>,
                request.<Campo2>,
                request.<CampoOpcional>));

            return Results.Created(
                $"/api/<recurso>/{entidade.Id}",
                ToDto(entidade));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    internal static async Task<IResult> ListarAsync(IMediator mediator)
    {
        var result = await mediator.Send(new Listar<Entidades>Query());
        return Results.Ok(result.Items);
    }

    internal static async Task<IResult> ObterPorIdAsync(IMediator mediator, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return Results.BadRequest("id invalido.");

        try
        {
            var entidade = await mediator.Send(new Obter<Entidade>PorIdQuery(objectId));
            return Results.Ok(ToDto(entidade));
        }
        catch (<Entidade>NotFoundException)
        {
            return Results.NotFound();
        }
    }

    internal static async Task<IResult> AtualizarAsync(
        IMediator mediator, string id, Atualizar<Entidade>Request request)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return Results.BadRequest("id invalido.");

        try
        {
            var entidade = await mediator.Send(new Atualizar<Entidade>Command(
                objectId, request.<Campo1>));
            return Results.Ok(ToDto(entidade));
        }
        catch (<Entidade>NotFoundException) { return Results.NotFound(); }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    }

    internal static async Task<IResult> RemoverAsync(IMediator mediator, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return Results.BadRequest("id invalido.");

        try
        {
            await mediator.Send(new Remover<Entidade>Command(objectId));
            return Results.NoContent();
        }
        catch (<Entidade>NotFoundException) { return Results.NotFound(); }
    }

    internal static async Task<IResult> <AcaoEspecifica>Async(IMediator mediator, string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return Results.BadRequest("id invalido.");

        try
        {
            var entidade = await mediator.Send(new <AcaoEspecifica>Command(objectId));
            return Results.Ok(ToDto(entidade));
        }
        catch (<Entidade>NotFoundException) { return Results.NotFound(); }
        catch (InvalidOperationException ex) { return Results.Conflict(ex.Message); }
    }

    private static <Entidade>Response ToDto(<Entidade> e) => new()
    {
        Id = e.Id.ToString(),
        <Campo1> = e.<Campo1>,
        <Campo2> = e.<Campo2>,
        <CampoOpcional> = e.<CampoOpcional>,
        CriadoEm = e.CriadoEm,
    };
}
```

### Passo 5: (Não) Registrar Router

Não há `app.MapPost(...)` manual no `Program.cs`. A descoberta via reflection em `app.MapEndpoints(Assembly.GetExecutingAssembly())` encontra o novo handler automaticamente. Se o endpoint não aparecer, verifique:

1. Classe é `public`, não-abstrata, não-genérica.
2. Construtor público **sem parâmetros**.
3. Implementa `IEndpointRouteHandler`.
4. Está no assembly do WebApi (`Program.cs`).

### Passo 6: Autenticação/Autorização

| Situação | Como aplicar |
|----------|--------------|
| Endpoint público | `.AllowAnonymous()` |
| Endpoint autenticado (qualquer usuário) | `.RequireAuthorization()` |
| Endpoint com política | `.RequireAuthorization("<PolicyName>")` — política registrada em `Program.cs` via `builder.Services.AddAuthorization(opts => opts.AddPolicy(...))` |
| Rate limiting | `.RequireRateLimiting("<PolicyName>")` |

### Passo 7: Tratamento de Erros

Mapeamento canônico de exceções do domínio/aplicação para HTTP:

| Exceção | Status | `Results.*` |
|---------|--------|-------------|
| `ArgumentException`, `ArgumentNullException` | 400 | `Results.BadRequest(ex.Message)` |
| `<Entidade>NotFoundException` / `KeyNotFoundException` | 404 | `Results.NotFound()` |
| `InvalidOperationException` (conflito de estado) | 409 | `Results.Conflict(ex.Message)` |
| `UnauthorizedAccessException` | 403 | `Results.Forbid()` |
| Erro inesperado | 500 | deixar o middleware de exceção tratar |

**Não** use `throw` para controle de fluxo em endpoint handlers. Capture e traduza.

### Passo 8: Gerar Documento TypeScript para Frontend

Criar `skill-outputs/api-endpoints-development/<SLICE>_API_TYPES.md`:

```markdown
# Tipos TypeScript: API de <Nome do Slice>

Este documento define os tipos TypeScript correspondentes aos schemas da API.

## Conversão de Tipos C# → TypeScript

| C# | TypeScript |
|----|------------|
| `string` | `string` |
| `int`, `long`, `double`, `decimal` | `number` (usar `string` para decimal quando precisão for crítica) |
| `bool` | `boolean` |
| `DateTime`, `DateTimeOffset` | `string` (ISO 8601) |
| `Guid`, `ObjectId` | `string` |
| `Nullable<T>` / `T?` | `T \| null` |
| `List<T>`, `IReadOnlyList<T>` | `T[]` |
| `Dictionary<string, T>` | `Record<string, T>` |
| `enum` | união de string literals |

## Schemas

Transcreva cada request/response record do C# para uma interface TypeScript.
Liste as interfaces na ordem em que aparecem nos endpoints.

```ts
/** POST /api/<recurso> — request */
interface <Acao>Request {
  <campo1>: string;
  <campo2>: number;
  <campoOpcional>?: string | null;
}

/** POST /api/<recurso>, GET /api/<recurso>/{id} — response */
interface <Entidade>Response {
  id: string;
  <campo1>: string;
  <campo2>: number;
  <campoOpcional>?: string | null;
  criadoEm: string;
}

type <Entidade>Status = '<Estado1>' | '<Estado2>' | '<Estado3>';
```

## Mapeamento de Endpoints

| Método | Path | Request | Response | Status |
|--------|------|---------|----------|--------|
| POST | `/api/<recurso>` | `<Acao>Request` | `<Entidade>Response` | 201 |
| GET | `/api/<recurso>` | — | `<Entidade>Response[]` | 200 |
| GET | `/api/<recurso>/{id}` | — | `<Entidade>Response` | 200 |
| PUT | `/api/<recurso>/{id}` | `Atualizar<Entidade>Request` | `<Entidade>Response` | 200 |
| DELETE | `/api/<recurso>/{id}` | — | — | 204 |

## Códigos de Erro

| Status | Quando ocorre |
|--------|---------------|
| 400 | Request inválido (ArgumentException) |
| 401 | Sem autenticação |
| 403 | Autenticado mas sem permissão |
| 404 | <Entidade>NotFoundException |
| 409 | Conflito de estado (InvalidOperationException) |
```

### Passo 9: Atualizar README

```markdown
## API REST: <Nome do Slice>

Base path: `/api/<recurso>`

### Autenticação

Todos os endpoints requerem `Authorization: Bearer <jwt_token>`.

### Endpoints

| Método | Path | Descrição |
|--------|------|-----------|
| POST | `/api/<recurso>` | Criar <entidade> |
| GET | `/api/<recurso>` | Listar <entidades> |
| GET | `/api/<recurso>/{id}` | Obter por id |
| PUT | `/api/<recurso>/{id}` | Atualizar |
| DELETE | `/api/<recurso>/{id}` | Remover |
| POST | `/api/<recurso>/{id}/<acao>` | Ação específica |

### Códigos de Status

| Código | Significado |
|--------|-------------|
| 200 | Sucesso |
| 201 | Criado |
| 204 | Sem conteúdo |
| 400 | Dados inválidos |
| 401 | Não autenticado |
| 403 | Sem permissão |
| 404 | Não encontrado |
| 409 | Conflito |
```

## Outputs Esperados

1. `IEndpointRouteHandler` + extensão `MapEndpoints(Assembly)` existentes no WebApi.
2. `src/<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs` implementando `IEndpointRouteHandler`.
3. DTOs em `src/<Produto>.WebApi/EndpointHandlers/<Slice>/Dto/` como `records`.
4. `Program.cs` chama `app.MapEndpoints(Assembly.GetExecutingAssembly())` (uma única vez).
5. `skill-outputs/api-endpoints-development/<SLICE>_API_TYPES.md` com tipos TypeScript.
6. README atualizado com documentação das rotas.
7. `<Produto>.csproj` referencia `<Produto>.WebApi.csproj` **e** cada slice de domínio cujo `Add<Slice>Module` é chamado em `Program.cs`.
8. `<Produto>.WebApi.csproj` referencia cada slice de domínio cujos Commands/Queries os handlers consomem.

## Checklist de Validação

- [ ] `IEndpointRouteHandler` e `IEndpointRouteBuilderExtensions` existem no WebApi.
- [ ] Handler é `public`, não abstrato, com construtor sem parâmetros.
- [ ] Métodos de endpoint são `internal static`.
- [ ] Todos os endpoints privados têm `.RequireAuthorization()`.
- [ ] Request/Response são `records` com `required` properties.
- [ ] Exceções de domínio são traduzidas para `Results.BadRequest/NotFound/Conflict`.
- [ ] `ObjectId` vindos de path params são validados com `ObjectId.TryParse`.
- [ ] DTO do response não expõe campos sensíveis (TenantId, claims internas).
- [ ] TypeScript inclui todos os schemas referenciados.
- [ ] `dotnet build` passa.
- [ ] Smoke test manual: endpoint responde conforme esperado (Swagger/curl).

## Convenções de Nomenclatura

### Rotas
- `POST /api/<recurso>` — criar
- `GET /api/<recurso>` — listar
- `GET /api/<recurso>/{id}` — obter
- `PUT /api/<recurso>/{id}` — atualizar
- `DELETE /api/<recurso>/{id}` — remover
- `POST /api/<recurso>/{id}/<acao>` — ação específica
- `POST /api/admin/<recurso>` — endpoints administrativos

### Métodos
- `CriarAsync`, `ListarAsync`, `ObterPorIdAsync`, `AtualizarAsync`, `RemoverAsync`, `<Acao>Async`

### TypeScript
- Interfaces: PascalCase (`CriarPedidoRequest`)
- Propriedades: camelCase (`criadoEm`)
- Uniões de literais: PascalCase (`PedidoStatus`)
