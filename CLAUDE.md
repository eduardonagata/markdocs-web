# CLAUDE.md — Politicas de Desenvolvimento (.NET)

## Organizacao do codigo

### Modelo mental: Slice vs Layer

- **Slice = projeto .NET** (uma `.csproj` em `src/`). Ex.: `<Produto>.Pedidos`, `ServicoDfe.Certificados`. Unidade de empacotamento, deploy e ownership.
- **Layer = camada arquitetural** que vive **dentro** de um slice: `Domain/`, `Application/`, `Infrastructure/`, `DependencyInjection/`. Unidade de organizacao **dentro** de um projeto, nao um projeto separado.
- Em um slice autocontido, Domain e Application coexistem no mesmo `.csproj` — nao ha projeto `<Slice>.Application` ou `<Slice>.Domain` separados. As layers sao subpastas com namespaces correspondentes.
- Isso **nao** e regra absoluta: nada impede que um slice se restrinja so a `Domain/` (slice de modelos puros, consumido por outros), ou que existam slices auxiliares (`<Produto>.Notificacoes`) referenciados por varios slices de dominio. O que **nao** se faz e quebrar um unico slice em multiplos projetos por camada.

### Topologia

O codigo e organizado em vertical slices, cada um como um projeto .NET (class library) separado sob `src/`. Tres papeis de projeto:

1. **`<Produto>/`** — Host. Unico projeto executavel (SDK=`Microsoft.NET.Sdk.Web`). Contem `Program.cs` e e o **ponto de entrada** da aplicacao. Sua razao de existir e bootstrap puro: composicao do host, registro de DI, middlewares, auth, descoberta de endpoints. Nao contem endpoint handlers, nao contem logica de negocio.
2. **`<Produto>.WebApi/`** — Camada HTTP empacotada como class library (SDK=`Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App`). Contem os `IEndpointRouteHandler` (handlers de rota), DTOs Request/Response, e o `WebApiMarker` que aponta para este assembly. O Host descobre os endpoints via `app.MapEndpoints(typeof(WebApiMarker).Assembly)`.
3. **`<Produto>.<Slice>/`** — Slice de negocio (class library). Domain + Application + Infrastructure + DependencyInjection do agregado.

```
src/
├── <Produto>/                          # SDK=Microsoft.NET.Sdk.Web — Host (entry point)
│   ├── Program.cs                      # ~5% do codigo: DI, middlewares, auth, descoberta de endpoints
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Properties/launchSettings.json
├── <Produto>.WebApi/                   # SDK=Microsoft.NET.Sdk + FrameworkReference Microsoft.AspNetCore.App
│   ├── WebApiMarker.cs                 # marker para `app.MapEndpoints(typeof(WebApiMarker).Assembly)`
│   └── EndpointHandlers/
│       ├── IEndpointRouteHandler.cs
│       ├── IEndpointRouteBuilderExtensions.cs
│       └── <Slice>/
│           ├── <Recurso>Handler.cs     # IEndpointRouteHandler
│           └── Dto/
├── <Produto>.<Slice>/                  # SDK=Microsoft.NET.Sdk (class library)
│   ├── <Slice>Marker.cs
│   ├── Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Events/
│   │   └── Exceptions/
│   ├── Application/
│   │   ├── Commands/                   # records IRequest<T> + handlers — flat, sem subpasta por agregado
│   │   ├── Queries/                    # records IRequest<T> + handlers — flat
│   │   ├── EventHandlers/              # INotificationHandler<TEvent>
│   │   ├── Services/                   # logica de aplicacao nao-CQRS (opcional)
│   │   └── PublicApi/                  # interfaces consumidas por outros slices (opcional)
│   ├── Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── Interfaces/
│   │   │   └── Repositories/
│   │   ├── Services/
│   │   └── Workers/
│   └── DependencyInjection/
│       └── ServiceCollectionExtensions.cs  # Add<Slice>Module
└── <Produto>.<OutroSlice>/             # slice adicional
```

Importante: dentro de `Application/`, **commands e queries ficam diretamente sob `Commands/` e `Queries/`, sem subpasta por agregado**. Isso evita aninhamento desnecessario quando um slice tem poucos agregados (caso comum). Se o slice crescer ao ponto de a listagem em `Commands/` ficar dificil de navegar, e melhor extrair um sub-slice do que reintroduzir o nivel de agregado.

### Hierarquia de dependencias (ProjectReference)

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

Cada slice de dominio e referenciado por **dois** projetos a montante:

- **`<Produto>` (executavel)** — porque `Program.cs` chama `services.Add<Slice>Module(...)`, que e metodo de extensao exposto pelo `DependencyInjection/` do slice. Uso direto exige reference explicita.
- **`<Produto>.WebApi`** — porque os endpoint handlers consomem Commands/Queries via `IMediator` e usam tipos de dominio nos DTOs. Tambem uso direto, tambem reference explicita.

Regras do grafo (como diretriz, nao como dogma):

- Cada projeto declara `ProjectReference` para tudo que usa **diretamente** — nao dependa de transitividade para uso direto. Transitividade e so um fallback de compilacao; manter as references explicitas mantem o grafo legivel e as fronteiras intencionais.
- Slices de dominio em geral **nao** se referenciam entre si. Compartilhamento de comportamento acontece preferencialmente via **slices auxiliares** (notificacoes, billing, feature flags, identidade, etc.) que ambos podem referenciar.
- Eventualmente um slice de dominio pode precisar referenciar outro — quando ocorrer, exponha uma interface minima em `Application/PublicApi/I*` no slice consumido em vez de acoplar a tipos internos. O default e evitar; a excecao e consciente, justificada e mapeada (anote a razao num README do slice ou no doc de saida da skill).
- Slices auxiliares tambem sao class libraries com a mesma estrutura interna (Domain/Application/Infrastructure/DI quando aplicavel); eles nao sao especiais — apenas podem ter multiplos consumidores em vez de um.

`<Produto>.WebApi` e, ele proprio, um slice — so que da camada HTTP. Concentra interface, handlers e DTOs de todos os recursos juntos. Os slices de dominio permanecem **livres de qualquer artefato HTTP/JSON**.

### Variantes de slice quanto a profundidade

Um slice pode ser **autocontido** (todas as camadas — `Domain/Application/Infrastructure/DependencyInjection/` — com endpoint handlers no WebApi) ou **somente de modelos** (apenas `Domain/`, sem persistencia nem aplicacao; ex.: um slice que descreve um padrao de documento fiscal e e consumido por outro slice — nesse caso ele e na pratica um slice auxiliar de tipos puros). O nivel certo depende do escopo: se o slice precisa persistir estado, expor operacoes ou orquestrar integracoes, ele cresce ate as quatro camadas; se for so um vocabulario tipado para outros slices, fica em `Domain/`.

---

## Tenancy — pergunte antes de implementar persistencia

O produto **nao fixa um modelo de tenancy unico**. Cada slice escolhe conforme seu requisito operacional. Modelos suportados:

- **Database-per-tenant** — cada tenant tem seu proprio database (ex.: `tid-{tenantId}`). `IMongoDatabase` e resolvido por requisicao via um `ITenantAccessor` que le o tenant da claim do JWT. **Sem** `TenantId` na entidade. Forte isolamento; complica `BackgroundService` (sem `HttpContext` para resolver tenant) e relatorios cross-tenant.
- **Single-database com `TenantId`** — todos os tenants compartilham o database; cada entidade carrega `TenantId` e queries filtram por ele. Workers varrem todos os tenants em uma query so. Isolamento depende de filtros corretos em todo lugar.
- **Sem tenancy** — slice de dominio puro (so `Domain/`) ou slice cujo dado nao tem dimensao de tenant (catalogo global, dados de referencia). Nao toca tenancy.

**Regra: antes de implementar a camada de persistencia de um slice novo (ou de adicionar persistencia a um slice de dominio puro), pergunte ao usuario qual modelo aplicar.** A pergunta deve emergir antes da implementacao — mudar o modelo depois envolve migracao de dados.

Use estes criterios para sugerir a recomendacao na pergunta:

- O slice tem `BackgroundService` que precisa varrer dados de todos os tenants? Favoreca **single-db com `TenantId`** — workers nao tem `HttpContext`, e enumerar tenants para iterar databases vira uma fonte de bug.
- Compliance ou regulatorio exige isolamento fisico de dados por tenant? Favoreca **database-per-tenant**.
- O slice e so dominio (sem persistencia) ou os dados sao compartilhados entre tenants? **Sem tenancy**.
- Caso de duvida ou ambiguidade nos sinais, **nao escolha sozinho** — apresente o tradeoff e peca a decisao.

Slices ja existentes podem ter modelos diferentes (ex.: um slice em database-per-tenant e outro em single-db com `TenantId`). Isso e aceitavel: a topologia abraca essa heterogeneidade. Mas **dentro de um slice**, o modelo e unico.

---

## Entidades

Entidades sao objetos com identidade propria e ciclo de vida independente. Ficam em `<Slice>/Domain/Entities/`.

Toda entidade deve conter pelo menos:

- `Id` — identificador (`ObjectId`, `Guid` ou equivalente) com `[BsonId]` se MongoDB
- `CriadoEm: DateTimeOffset` — timestamp preenchido pelo sistema
- `TenantId` — apenas se o modo de tenancy do slice for single-database multi-tenant (no modo database-por-tenant nao usar filtro por TenantId; ver secao "Tenancy" acima)
- `CriadoPor` — quando a entidade nasce por acao de usuario autenticado

Nao use heranca de classe base para prover esses campos. Cada entidade os declara explicitamente com `required` onde aplicavel.

### Value Objects

Objetos imutaveis definidos por seus atributos, sem identidade. Ficam em `<Slice>/Domain/ValueObjects/`. Declare como `record` ou `readonly record struct` para obter igualdade estrutural automatica.

---

## Colecoes (MongoDB)

Nomes prefixados com o nome do slice (ex: `contatos.entidades_externas`, `accounting.journal_entries`).

---

## Repositories

Ficam em `<Slice>/Infrastructure/Persistence/Repositories/` com interface em `<Slice>/Infrastructure/Persistence/Interfaces/`. Cada entidade persistente tem seu proprio repository. Repositories nao contem logica de negocio — apenas acesso a dados. Repositories propagam uma `IClientSessionHandle` opcional via `IMongoSessionAccessor` quando houver transacao ambient.

---

## Injecao de Dependencias — Metodos de Extensao Colocalizados

Cada slice expoe exatamente **um** metodo de extensao:

```csharp
// src/<Produto>.<Slice>/DependencyInjection/ServiceCollectionExtensions.cs
namespace <Produto>.<Slice>.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection Add<Slice>Module(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(<Slice>Marker).Assembly));
        services.Configure<<Slice>Options>(configuration.GetSection("<Slice>"));
        services.AddScoped<I<Entidade>Repository, <Entidade>Repository>();
        return services;
    }
}

public class <Slice>Marker { }
```

O `Program.cs` **nunca** registra servicos internos do slice diretamente. Se o registro precisar mudar, a mudanca ocorre no `ServiceCollectionExtensions.cs` daquele slice. Isso mantem o grafo de DI unidirecional e permite extrair um slice para outro repositorio sem editar `Program.cs`.

Handlers e repositories recebem dependencias via **construtor**. Proibido `IServiceProvider.GetService(...)` fora dos pontos de registro.

---

## Host (`<Produto>`) e WebApi (`<Produto>.WebApi`) — separacao de responsabilidades

A camada de hosting e a camada HTTP sao dois projetos distintos. Confundir os dois (Program.cs convivendo com endpoint handlers no mesmo projeto) acopla bootstrap a HTTP — o Host fica preso a `Microsoft.NET.Sdk.Web` mesmo se quisermos um worker headless, e a camada HTTP nao pode ser reaproveitada por outro host (testes de integracao com `WebApplicationFactory<TEntryPoint>`, segundo executavel para admin, etc.).

**`<Produto>/` (Host) — SDK `Microsoft.NET.Sdk.Web`:**

- `Program.cs` — composicao do host: `Env.TraversePath().Load()`, `WebApplication.CreateBuilder(args)`, encadeamento de `Add<Slice>Module(builder.Configuration)` para cada slice, registro de auth (JWT bearer, policies), CORS, rate limiting, logging, Swagger, middlewares globais (exception handler, request logging), e a chamada unica `app.MapEndpoints(typeof(WebApiMarker).Assembly)`.
- `appsettings.json`, `appsettings.<Env>.json`, `Properties/launchSettings.json`.
- Cerca de 5% do codigo do produto. Cresce devagar: cada slice novo adiciona uma linha (`Add<Slice>Module`).

**`<Produto>.WebApi/` (camada HTTP) — SDK `Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App`:**

- `WebApiMarker.cs` — classe marcadora para o Host descobrir o assembly por reflection.
- `EndpointHandlers/IEndpointRouteHandler.cs` + extensao `MapEndpoints(Assembly)`.
- `EndpointHandlers/<Slice>/<Recurso>Handler.cs` — implementacoes de `IEndpointRouteHandler` que traduzem HTTP ↔ MediatR (commands/queries) e mapeiam excecoes de dominio para `Results.*` HTTP.
- `EndpointHandlers/<Slice>/Dto/` — `Request`/`Response` records do contrato HTTP do slice.
- Como class library nao recebe os `using` globais do `Microsoft.NET.Sdk.Web`, declare-os no `.csproj` para evitar repetir em cada handler:
  ```xml
  <ItemGroup>
    <Using Include="Microsoft.AspNetCore.Builder" />
    <Using Include="Microsoft.AspNetCore.Http" />
    <Using Include="Microsoft.AspNetCore.Routing" />
  </ItemGroup>
  ```

**O que NAO vive nem no Host nem no WebApi:**

- Logica de negocio, validacoes de dominio, regras de calculo, orquestracao multi-passo. Isso fica no slice — em `Domain/` ou em handlers de `Application/`.
- Acesso direto a banco, clientes de servicos externos, repositorios concretos. Isso fica em `Infrastructure/` do slice.
- Registros de DI especificos de slice. Cada slice expoe seu `Add<Slice>Module(...)` e o `Program.cs` (Host) apenas chama.

**Descoberta de endpoints:**

```csharp
// no Program.cs do Host:
using AppTemplate.WebApi;                  // WebApiMarker
using AppTemplate.WebApi.EndpointHandlers; // extensao MapEndpoints

app.MapEndpoints(typeof(WebApiMarker).Assembly);
```

Usar o `WebApiMarker` (e nao `Assembly.GetExecutingAssembly()`) e o que permite o Host nao hospedar handlers proprios — todos vivem em `<Produto>.WebApi`. Para um eventual segundo executavel (worker, admin CLI), basta criar outro projeto Host que aponte para o mesmo `WebApiMarker` ou para um marcador alternativo.

---

## Endpoints — `IEndpointRouteHandler`

Handlers de rota sao classes que implementam `IEndpointRouteHandler` com **construtor publico sem parametros**:

```csharp
public class <Recurso>Handler : IEndpointRouteHandler
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/<recurso>", ObterAsync)
            .RequireAuthorization()
            .Produces<<Entidade>Response>(StatusCodes.Status200OK);

        app.MapPost("/api/<recurso>", CriarAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }

    internal static async Task<IResult> ObterAsync(IMediator mediator, /* ... */)
    {
        var resultado = await mediator.Send(new Obter<Entidade>Query());
        return Results.Ok(/* dto */);
    }

    internal static async Task<IResult> CriarAsync(
        IMediator mediator, Criar<Entidade>Request request)
    {
        try
        {
            var entidade = await mediator.Send(new Criar<Entidade>Command(
                request.Campo1, request.Campo2));
            return Results.Created($"/api/<recurso>/{entidade.Id}", /* dto */);
        }
        catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
    }
}
```

- Handler mora em `<Produto>.WebApi/EndpointHandlers/<Slice>/<Recurso>Handler.cs`.
- DTOs em `<Produto>.WebApi/EndpointHandlers/<Slice>/Dto/*.cs` (Request/Response).
- Metodos de endpoint sao `internal static` — nunca `public`.
- Descoberta automatica: `app.MapEndpoints(Assembly.GetExecutingAssembly())` em `Program.cs`. Nao ha registro manual no `Program.cs`.
- Conversao entidade -> DTO e local ao endpoint handler (metodo privado `ToDto`), mantendo o slice livre de artefatos HTTP/JSON.

---

## Commands, Queries e Handlers (MediatR)

Commands e Queries sao `record` com `IRequest<T>`. Handler na mesma unidade de compilacao:

```csharp
// src/<Produto>.<Slice>/Application/<Agregado>/Commands/<Acao>.cs
public record <Acao>Command(string Campo1, int Campo2) : IRequest<<Entidade>>;

public class <Acao>Handler : IRequestHandler<<Acao>Command, <Entidade>>
{
    private readonly I<Entidade>Repository _repository;
    private readonly ILogger<<Acao>Handler> _logger;

    public <Acao>Handler(I<Entidade>Repository repository, ILogger<<Acao>Handler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<<Entidade>> Handle(<Acao>Command request, CancellationToken ct)
    {
        // Validacao de pre-condicoes que dependem de consulta ao banco
        // Criacao/alteracao de entidade de dominio
        // Persistencia via repository
        // Log estruturado
        throw new NotImplementedException();
    }
}
```

- Commands usam verbo no infinitivo: `CriarX`, `AtualizarY`, `RemoverZ`, `Processar<Evento>`.
- Queries usam substantivo: `Obter<Entidade>PorIdQuery`, `Listar<Entidades>Query`.
- Handler dispara excecoes de dominio; o endpoint handler traduz para `Results.BadRequest/NotFound/Conflict`.
- Nao use `MediatR` dentro de outro handler sem necessidade — prefira injetar diretamente o servico ou repository.

---

## Configuracao

Variaveis de ambiente sao lidas via `IConfiguration` e expostas por **options classes** vinculadas a secoes:

```csharp
public class <Slice>Options
{
    public string ConnectionString { get; set; } = string.Empty;
}

// DI:
services.Configure<<Slice>Options>(configuration.GetSection("<Slice>"));

// Uso:
public class <Handler>(IOptions<<Slice>Options> options) { /* options.Value.ConnectionString */ }
```

Nao leia `configuration["..."]` fora do `ServiceCollectionExtensions.cs`. Se for uma nova variavel, adicione-a a options class e documente no `.env.example`.

---

## Dependencias entre slices

Slice A depende de slice B apenas por uma **interface publica minima** exposta pelo slice B em `Application/PublicApi/IXxx.cs`. `ProjectReference` segue o grafo de dependencias e **nunca** forma ciclo. Se aparecer ciclo, extraia a interface para um slice `.Shared` ou suba a orquestracao para um slice superior.

Deve ser evitado na medida do possível, quando for necessário peça a permissão do usuário para replicar esse padrão.

---

## Testes

- `tests/<Produto>.<Slice>.Tests/` — xUnit. Testa handlers instanciados diretamente (DI manual) contra repositories reais apontados para MongoDB de teste. Nao mocke o banco a menos que a verificacao nao dependa dele.
- `tests/<Produto>.WebApi.IntegrationTests/` — `WebApplicationFactory<Program>` para testes E2E HTTP.
- Use `MONGODB_CONNECTION_STRING` apontando para um replica set de teste.

---

## Semantica

O sistema nao opina sobre o significado dos registros. Nomes de entidades sao alinhados a linguagem do usuario (pt-BR). Toda informacao derivavel (totais, saldos, medias) e calculada, nunca armazenada, a não ser que o requisito apresentado exija explicitamente, caso em que deverá alertar o usuário para as consequências.

---

## Convencoes Gerais de C#

- `Nullable` habilitado em todos os projetos.
- `ImplicitUsings` habilitado.
- `TargetFramework` consistente em todos os projetos (default: `net10.0`).
- Prefira `record` para DTOs, Commands, Queries, Events e Value Objects.
- Prefira `required` sobre `[Required]` para propriedades obrigatorias.
- Propriedades de dominio usam PascalCase; enums usam PascalCase.
- `async` methods sempre retornam `Task`/`Task<T>`/`ValueTask<T>` e recebem `CancellationToken` (por padrao).
- Evite comentarios redundantes — bons nomes ja documentam.
