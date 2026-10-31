---
name: domain-modeling
description: "Transcreve documentos markdown que descrevem modelos de domínio para classes C# com validações em um projeto de slice (.NET class library), separando lógica de modelo da lógica de aplicação. Use quando o usuário fornecer um documento descrevendo entidades, value objects, agregados ou regras de um subdomínio/slice."
---

# Domain Modeling Skill (.NET)

## Objetivo

Transformar especificações de domínio em markdown para código C# estruturado, garantindo:
- Cada slice é um projeto .NET separado (class library) sob `src/<Produto>.<Slice>/`.
- Modelos com validações que asseguram estados consistentes.
- Separação clara entre lógica de modelo (na classe) e lógica de aplicação (em handlers).
- Documentação das operações a serem implementadas na camada de aplicação.

## Quando Usar

- Usuário fornece documento markdown descrevendo entidades de um slice em `skill-inputs/domain-modeling/`.

## Entrada Esperada

Documento markdown com:
- Nome do slice/subdomínio (ex: `Pedidos`, `Contatos`, `Accounting`).
- Descrição das entidades e seus atributos (tipo, obrigatoriedade).
- Regras de validação e invariantes.
- Relacionamentos entre entidades.
- Comportamentos esperados (opcional — transições de estado, cálculos).

## Processo de Execução

### Passo 1: Análise do Documento

Extrair do markdown:
1. **Entidades** — objetos com identidade (`Id`).
2. **Value Objects** — imutáveis, comparados por valor.
3. **Agregados** — raiz + entidades internas.
4. **Invariantes** — regras sempre verdadeiras.
5. **Regras de Validação** — constraints em campos individuais.
6. **Enums** — conjuntos fechados de valores nomeados.

### Passo 2: Classificação da Lógica

| MODELO (na classe) | APLICAÇÃO (documentar para próxima etapa) |
|--------------------|-------------------------------------------|
| Validação de formato (email, CPF, enum válido) | Verificações que dependem de outros agregados |
| Validação de range (min/max) | Consultas ao banco |
| Invariantes internos ao agregado | Orquestração de múltiplas entidades |
| Transições de estado internas | Integrações com serviços externos |
| Propriedades calculadas | Políticas configuráveis por tenant/usuário |

### Passo 3: Criar/Atualizar Projeto do Slice

Se o projeto ainda não existe:

```bash
# a partir da raiz do repositorio
dotnet new classlib -n <Produto>.<Slice> -o src/<Produto>.<Slice> -f net10.0
dotnet sln add src/<Produto>.<Slice>/<Produto>.<Slice>.csproj
```

Adicionar ao `src/<Produto>.WebApi/<Produto>.WebApi.csproj`:

```xml
<ProjectReference Include="../<Produto>.<Slice>/<Produto>.<Slice>.csproj" />
```

Configurar o `<Produto>.<Slice>.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace><Produto>.<Slice></RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.4.1" />
    <!-- Adicione MongoDB.Driver, FluentValidation, etc, conforme necessário -->
  </ItemGroup>
</Project>
```

Estrutura de diretórios a criar no slice:

```
src/<Produto>.<Slice>/
├── <Slice>Marker.cs
├── Domain/
│   ├── Entities/
│   │   ├── <EntidadeA>.cs
│   │   └── <EntidadeB>.cs
│   ├── ValueObjects/
│   │   └── <ValueObject>.cs
│   ├── Events/
│   └── Exceptions/
└── Infrastructure/
    └── Persistence/
        └── Configuration/
            └── MongoDBSettings.cs        # apenas se MongoDB
```

### Passo 4: Implementar Modelos

#### Template de Marker

```csharp
// src/<Produto>.<Slice>/<Slice>Marker.cs
namespace <Produto>.<Slice>;

/// <summary>
/// Marker para identificar o assembly do slice <Slice>.
/// Usado para descoberta de handlers MediatR e AutoMapper.
/// </summary>
public class <Slice>Marker { }
```

#### Template de Entidade (MongoDB)

```csharp
// src/<Produto>.<Slice>/Domain/Entities/<Entidade>.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace <Produto>.<Slice>.Domain.Entities;

public class <Entidade>
{
    [BsonId]
    public required ObjectId Id { get; set; }

    public required string <CampoObrigatorio> { get; set; }

    public <CampoOpcional>? <CampoOpcional> { get; set; }

    public <EntidadeStatus> Status { get; set; } = <EntidadeStatus>.<INICIAL>;

    public DateTimeOffset CriadoEm { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AtualizadoEm { get; set; }

    // ===== Construtor de criação (validações de formato) =====
    public static <Entidade> Criar(string <campoObrigatorio>, /* ... */)
    {
        if (string.IsNullOrWhiteSpace(<campoObrigatorio>))
            throw new ArgumentException("<CampoObrigatorio> e obrigatorio.", nameof(<campoObrigatorio>));

        return new <Entidade>
        {
            Id = ObjectId.GenerateNewId(),
            <CampoObrigatorio> = <campoObrigatorio>,
        };
    }

    // ===== Transições de estado (invariantes do agregado) =====
    public void <TransicaoDeEstado>()
    {
        if (Status != <EntidadeStatus>.<EstadoPermitido>)
            throw new InvalidOperationException(
                $"Nao e possivel <transicao> quando status e {Status}.");

        Status = <EntidadeStatus>.<NovoEstado>;
        AtualizadoEm = DateTimeOffset.UtcNow;
    }

    // ===== Propriedades calculadas =====
    [BsonIgnore]
    public <Tipo> <PropriedadeCalculada> => /* calculo */;
}

public enum <Entidade>Status
{
    <INICIAL>,
    <PROXIMO>,
    <FINAL>,
}
```

Para entidades sem MongoDB, omita atributos `[BsonId]`/`[BsonIgnore]` e use `Guid` como `Id`.

#### Template de Value Object

```csharp
// src/<Produto>.<Slice>/Domain/ValueObjects/<ValueObject>.cs
namespace <Produto>.<Slice>.Domain.ValueObjects;

public readonly record struct Dinheiro(long Centavos, string Moeda = "BRL")
{
    public static Dinheiro Zero(string moeda = "BRL") => new(0, moeda);

    public decimal Reais => Centavos / 100m;

    public Dinheiro Somar(Dinheiro outro)
    {
        if (Moeda != outro.Moeda)
            throw new InvalidOperationException("Moedas diferentes.");
        return new Dinheiro(Centavos + outro.Centavos, Moeda);
    }
}

public sealed record Email
{
    public string Valor { get; }

    public Email(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("Email nao pode ser vazio.", nameof(valor));
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                valor, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email invalido.", nameof(valor));
        Valor = valor.ToLowerInvariant();
    }

    public override string ToString() => Valor;
}
```

#### Template de Exception de Domínio

```csharp
// src/<Produto>.<Slice>/Domain/Exceptions/<Entidade>NotFoundException.cs
namespace <Produto>.<Slice>.Domain.Exceptions;

public class <Entidade>NotFoundException : KeyNotFoundException
{
    public <Entidade>NotFoundException(string id)
        : base($"<Entidade> '{id}' nao encontrada.") { }
}
```

#### Template de Evento de Domínio

```csharp
// src/<Produto>.<Slice>/Domain/Events/<Entidade><Evento>Event.cs
using MediatR;
using MongoDB.Bson;

namespace <Produto>.<Slice>.Domain.Events;

public record <Entidade><Evento>Event(ObjectId <Entidade>Id) : INotification;
```

### Passo 5: Gerar Documento de Operações

Criar `skill-outputs/domain-modeling/<SLICE>_APPLICATION_OPERATIONS.md`:

```markdown
# Operações de Aplicação: <Nome do Slice>

## Projeto

`src/<Produto>.<Slice>/`

## Resumo

Este documento descreve as operações que precisam ser implementadas na camada de
aplicação do slice `<Slice>`. Envolvem lógica que não reside no modelo (consultas
ao banco, outros agregados, serviços externos).

## Commands (Escrita)

### <Acao>Command

**Descrição:** <O que esta operação faz>

**Parâmetros:**
- `<Campo1>` (`<tipo C#>`) — <descrição>
- `<Campo2>` (`<tipo C#>`) — <descrição>

**Pré-condições (verificar no handler):**
- <Condição que requer consulta ao banco>
- <Condição envolvendo outro agregado>

**Efeitos:**
- <Efeito 1>
- <Efeito 2>

**Eventos emitidos:**
- `<Entidade><Evento>Event` — <quando emitir>

**Retorno:** `<Entidade>` ou `ObjectId`

---

### <OutroCommand>Command
...

## Queries (Leitura)

### <Consulta>Query

**Descrição:** <O que esta consulta retorna>

**Parâmetros de filtro:**
- `<Filtro1>` (`<tipo>`) — <descrição>

**Retorno:** `<Tipo>` (entidade concreta ou DTO de projeção)

---

## Regras de Negócio na Aplicação

| Regra | Onde verificar | Descrição |
|-------|----------------|-----------|
| <Nome> | <Command/Handler> | <detalhe> |

## Dependências entre Operações

```
<AcaoA>Command
  ├── verifica → <ConsultaX>
  └── emite    → <Evento>
       └── dispara → <AcaoB>Command (via INotificationHandler)
```
```

### Passo 6: Atualizar README do Projeto

Adicionar/atualizar seção no `README.md` do repositório:

```markdown
## Slice `<Slice>` — `src/<Produto>.<Slice>/`

<Descrição do slice e seu propósito>.

### Entidades

- `<EntidadeA>` — <descrição>
- `<EntidadeB>` — <descrição>

### Value Objects

- `<VO1>` — <descrição>

### Invariantes Principais

- <Invariante 1>
- <Invariante 2>
```

## Outputs Esperados

1. Projeto `src/<Produto>.<Slice>/` criado (se não existir) e adicionado ao `.sln`.
2. `ProjectReference` para o slice adicionado em `<Produto>.csproj` (executável, para chamar `Add<Slice>Module` em `Program.cs`) **e** em `<Produto>.WebApi.csproj` (handlers consomem Commands/Queries).
3. `<Slice>Marker.cs` no root do slice.
4. Entidades em `Domain/Entities/`, value objects em `Domain/ValueObjects/`, enums em `Domain/Enums/`, eventos em `Domain/Events/`, exceções em `Domain/Exceptions/`.
5. `skill-outputs/domain-modeling/<SLICE>_APPLICATION_OPERATIONS.md` para a próxima etapa.
6. `README.md` do repositório atualizado com descrição do slice.

## Checklist de Validação

- [ ] Projeto do slice existe como class library separada e foi adicionado ao `.sln`.
- [ ] `ProjectReference` do slice está em `<Produto>.csproj` e em `<Produto>.WebApi.csproj`.
- [ ] `<Slice>Marker.cs` existe.
- [ ] Todas as invariantes do documento estão implementadas como guards em métodos/construtor.
- [ ] Validações de formato levantam `ArgumentException` com `nameof(...)`.
- [ ] Transições de estado levantam `InvalidOperationException`.
- [ ] Value objects são imutáveis (`record` / `readonly record struct`).
- [ ] Lógica que requer banco está documentada para a camada de aplicação.
- [ ] Documento de operações lista todos os commands/queries necessários.
- [ ] README do repositório foi atualizado.
- [ ] `dotnet build` da solution passa.
