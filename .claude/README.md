# .NET Template Skills

## Visão Geral

Este conjunto de skills guia a construção incremental de uma aplicação .NET seguindo o template arquitetural definido em [../README.md](../README.md) e as políticas em [../CLAUDE.md](../CLAUDE.md).

Cada slice é um projeto .NET (class library) separado sob `src/`. Um único projeto `WebApi` referencia os slices e expõe endpoints HTTP via `IEndpointRouteHandler` auto-registráveis. O registro de DI acontece por `Add<Slice>Module(IConfiguration)` em cada slice.

## Skills Disponíveis

| Skill | Diretório | Propósito |
|-------|-----------|-----------|
| **domain-modeling** | `/skills/domain-modeling/` | Transcreve especificações de domínio para classes C# com validações no projeto do slice |
| **application-layer-development** | `/skills/application-layer-development/` | Implementa commands, queries (MediatR), handlers, repositories e testes |
| **api-endpoints-development** | `/skills/api-endpoints-development/` | Cria `IEndpointRouteHandler`, DTOs e gera tipos TypeScript para frontend |
| **dependencies-dump** | `/skills/dependencies-dump/` | Atualiza `PackageReference`s e roda `dotnet restore` |

## Workflow Recomendado

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│   1. DOMAIN MODELING                                                │
│   ─────────────────                                                 │
│   Input:  skill-inputs/domain-modeling/<spec>.md                    │
│   Output: • Projeto src/<Produto>.<Slice>/ criado ou atualizado     │
│           • Classes em Domain/Entities/, Domain/ValueObjects/       │
│           • skill-outputs/.../<SLICE>_APPLICATION_OPERATIONS.md     │
│           • README do repositorio atualizado (seção do slice)       │
│                                                                     │
│                              ↓                                      │
│                                                                     │
│   2. APPLICATION LAYER                                              │
│   ────────────────────                                              │
│   Input:  skill-inputs/application-layer-development/<ops>.md       │
│           + <SLICE>_APPLICATION_OPERATIONS.md                       │
│   Output: • Application/<Agregado>/Commands/*.cs (record + handler) │
│           • Application/<Agregado>/Queries/*.cs                     │
│           • Infrastructure/Persistence/Interfaces/ + Repositories/  │
│           • DependencyInjection/ServiceCollectionExtensions.cs      │
│           • <Slice>Marker.cs                                        │
│           • tests/<Produto>.<Slice>.Tests/                          │
│           • Program.cs do WebApi chama Add<Slice>Module(...)        │
│           • skill-outputs/.../<SLICE>_IMPLEMENTED_OPERATIONS.md     │
│           • README atualizado (comportamentos)                      │
│                                                                     │
│                              ↓                                      │
│                                                                     │
│   3. API ENDPOINTS                                                  │
│   ────────────────                                                  │
│   Input:  skill-inputs/api-endpoints-development/<rotas>.md         │
│           + <SLICE>_IMPLEMENTED_OPERATIONS.md                       │
│   Output: • src/<Produto>.WebApi/EndpointHandlers/<Slice>/          │
│             <Recurso>Handler.cs : IEndpointRouteHandler             │
│           • Dto/*.cs (Request/Response records)                     │
│           • skill-outputs/.../<SLICE>_API_TYPES.md (TypeScript)     │
│           • README atualizado (rotas)                               │
│                                                                     │
│                              ↓                                      │
│                                                                     │
│   4. DEPENDENCIES                                                   │
│   ───────────────                                                   │
│   Output: • PackageReference atualizados em .csproj                 │
│           • dotnet restore executado                                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Documentos Intermediários

```
Etapa 1 → <SLICE>_APPLICATION_OPERATIONS.md → Etapa 2
Etapa 2 → <SLICE>_IMPLEMENTED_OPERATIONS.md → Etapa 3
Etapa 3 → <SLICE>_API_TYPES.md              → Frontend
```

## Exemplo de Uso

### Conversa 1: Modelagem de Domínio

```
Usuário: [anexa spec.md em skill-inputs/domain-modeling descrevendo Pedidos]

Claude: [usa skill domain-modeling]
- Cria projeto src/AppX.Pedidos/AppX.Pedidos.csproj (classlib, net10.0)
- Adiciona ao .sln
- Cria src/AppX.Pedidos/Domain/Entities/Pedido.cs, ItemPedido.cs
- Cria src/AppX.Pedidos/PedidosMarker.cs
- Gera skill-outputs/domain-modeling/PEDIDOS_APPLICATION_OPERATIONS.md
- Atualiza README.md do repositório
```

### Conversa 2: Camada de Aplicação

```
Usuário: [anexa PEDIDOS_APPLICATION_OPERATIONS.md em skill-inputs/application-layer-development]

Claude: [usa skill application-layer-development]
- Cria src/AppX.Pedidos/Application/Pedidos/Commands/CriarPedido.cs etc
- Cria src/AppX.Pedidos/Infrastructure/Persistence/Interfaces + Repositories
- Cria src/AppX.Pedidos/DependencyInjection/ServiceCollectionExtensions.cs (AddPedidosModule)
- Registra AddPedidosModule(builder.Configuration) em Program.cs do WebApi
- Cria tests/AppX.Pedidos.Tests com testes de regras críticas
- Gera PEDIDOS_IMPLEMENTED_OPERATIONS.md
```

### Conversa 3: Endpoints da API

```
Usuário: Implemente as rotas:
- POST /api/pedidos
- GET /api/pedidos
- GET /api/pedidos/{id}
- POST /api/pedidos/{id}/confirmar

[anexa PEDIDOS_IMPLEMENTED_OPERATIONS.md]

Claude: [usa skill api-endpoints-development]
- Cria src/AppX.WebApi/EndpointHandlers/Pedidos/PedidosHandler.cs : IEndpointRouteHandler
- Cria Dto/CriarPedidoRequest.cs, PedidoResponse.cs
- Gera PEDIDOS_API_TYPES.md
```

### Conversa 4: Finalização

```
Usuário: Atualize os pacotes NuGet necessários

Claude: [usa skill dependencies-dump]
- Adiciona PackageReferences necessários nos .csproj
- Executa dotnet restore
```

## Estrutura Final do Projeto

Após executar todas as skills para o slice `Pedidos`:

```
<produto>.sln
src/
├── AppX.WebApi/
│   ├── AppX.WebApi.csproj                     # +ProjectReference AppX.Pedidos
│   ├── Program.cs                             # +AddPedidosModule
│   └── EndpointHandlers/
│       └── Pedidos/                           # [Etapa 3]
│           ├── PedidosHandler.cs
│           └── Dto/
│               ├── CriarPedidoRequest.cs
│               └── PedidoResponse.cs
└── AppX.Pedidos/                              # [Etapa 1 + 2]
    ├── AppX.Pedidos.csproj
    ├── PedidosMarker.cs
    ├── Domain/
    │   ├── Entities/
    │   │   ├── Pedido.cs
    │   │   └── ItemPedido.cs
    │   └── ValueObjects/
    ├── Application/
    │   └── Pedidos/
    │       ├── Commands/
    │       │   ├── CriarPedido.cs
    │       │   └── ConfirmarPedido.cs
    │       └── Queries/
    │           └── ObterPedidoPorId.cs
    ├── Infrastructure/
    │   └── Persistence/
    │       ├── Interfaces/
    │       │   └── IPedidoRepository.cs
    │       └── Repositories/
    │           └── PedidoRepository.cs
    └── DependencyInjection/
        └── ServiceCollectionExtensions.cs     # AddPedidosModule

tests/
└── AppX.Pedidos.Tests/                        # [Etapa 2]
    └── Application/Pedidos/Commands/CriarPedidoHandlerTests.cs

docs/
├── PEDIDOS_APPLICATION_OPERATIONS.md          # [Etapa 1 → Etapa 2]
├── PEDIDOS_IMPLEMENTED_OPERATIONS.md          # [Etapa 2 → Etapa 3]
└── PEDIDOS_API_TYPES.md                       # [Etapa 3 → Frontend]
```

## Notas Importantes

1. **Cada skill é idempotente** — pode ser re-executada para atualizar arquivos existentes.
2. **Documentos intermediários são a interface** — garantem que cada etapa tem contexto completo.
3. **README é atualizado incrementalmente** — cada skill adiciona sua seção relevante.
4. **Testes são obrigatórios na Etapa 2** — regras críticas devem ter cobertura em xUnit.
5. **TypeScript é gerado na Etapa 3** — pronto para consumo no frontend.
6. **Slice = projeto** — nunca colapse slices em subdiretórios de um projeto único.
