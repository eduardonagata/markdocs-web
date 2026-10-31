---
name: dependencies-dump
description: "Revisa e atualiza PackageReferences nos .csproj do repositório .NET, executa dotnet restore e valida que a solution compila. Use quando o usuário pedir para atualizar/adicionar dependências NuGet ou após adicionar uma nova integração."
---

# Dependencies Skill (.NET)

## Objetivo

Manter as referências a pacotes NuGet dos projetos `.csproj` consistentes e atualizadas após a adição de novos recursos, slices ou integrações, garantindo que:
- Cada projeto declare apenas os pacotes que **realmente usa**.
- Versões sejam compatíveis entre projetos (idealmente centralizadas em `Directory.Packages.props` quando disponível).
- `dotnet restore` e `dotnet build` passem em toda a solution.

Não existe um `requirements.txt` em .NET — o "dump" aqui é sobre auditar os `<PackageReference>` e reportar/normalizar.

## Quando Usar

- Após instalar/usar uma nova dependência em código.
- Antes de commit/deploy.
- Após executar a skill `application-layer-development` ou `api-endpoints-development`.
- Usuário solicita atualização de pacotes.

## Processo de Execução

### Passo 1: Identificar Pacotes por Projeto

Listar todos os `.csproj` da solution:

```bash
find src tests -name "*.csproj"
```

Para cada projeto, inspecionar `<PackageReference>` existentes:

```bash
dotnet list <projeto>.csproj package
```

### Passo 2: Pacotes Padrão por Camada

#### `<Produto>.WebApi.csproj` (SDK=`Microsoft.NET.Sdk.Web`)

```xml
<ItemGroup>
  <PackageReference Include="MediatR" Version="12.4.1" />
  <PackageReference Include="DotNetEnv" Version="3.1.1" />
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="6.9.0" />
  <!-- opcional -->
  <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.22.0" />
  <PackageReference Include="AutoMapper" Version="13.0.1" />
</ItemGroup>
```

#### `<Produto>.<Slice>.csproj` (SDK=`Microsoft.NET.Sdk`)

```xml
<ItemGroup>
  <PackageReference Include="MediatR" Version="12.4.1" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.0" />
  <!-- Persistência (escolha um) -->
  <PackageReference Include="MongoDB.Driver" Version="3.0.0" />
  <!-- ou -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
  <!-- Integrações do slice (opcional) -->
  <PackageReference Include="Stripe.net" Version="48.0.0" />
  <PackageReference Include="FirebaseAdmin" Version="3.0.0" />
</ItemGroup>
```

#### `<Produto>.<Slice>.Tests.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="xunit" Version="2.9.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="NSubstitute" Version="5.1.0" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
</ItemGroup>
```

#### `<Produto>.WebApi.IntegrationTests.csproj`

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="xunit" Version="2.9.0" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
  <PackageReference Include="FluentAssertions" Version="6.12.0" />
</ItemGroup>
```

### Passo 3: Adicionar/Remover PackageReference

Preferir o CLI em vez de editar o XML manualmente:

```bash
# Adicionar
dotnet add src/<Produto>.<Slice>/<Produto>.<Slice>.csproj package MongoDB.Driver --version 3.0.0

# Remover
dotnet remove src/<Produto>.<Slice>/<Produto>.<Slice>.csproj package <Pacote>
```

### Passo 4: Restaurar e Compilar

```bash
dotnet restore
dotnet build --no-restore
```

Resolver erros: dependências faltantes no `.csproj` do projeto que usa o tipo/namespace correspondente. Regra: se um projeto `using Foo;` fica vermelho, o `.csproj` **desse projeto** precisa do pacote. Não adicione o pacote ao WebApi torcendo para "subir" por `ProjectReference` — cada projeto declara o que usa.

### Passo 5: Validar Versões Consistentes

Se houver `Directory.Packages.props` (central package management), versões vão para lá e os `.csproj` usam `<PackageReference Include="X" />` sem `Version=`. Se não houver, garanta que versões de um mesmo pacote estejam idênticas em todos os `.csproj`.

Para detectar divergências:

```bash
grep -r "PackageReference Include" src tests --include="*.csproj"
```

### Passo 6: Relatório

Ao final, reporte:

```markdown
## Atualização de Dependências

### Pacotes adicionados
- `src/<Produto>.<Slice>/<Produto>.<Slice>.csproj` → `MongoDB.Driver 3.0.0`

### Pacotes removidos
- `src/<Produto>.WebApi/<Produto>.WebApi.csproj` → `Newtonsoft.Json` (não utilizado)

### Versões alinhadas
- `MediatR` uniformizado em `12.4.1`

### Validação
- [x] `dotnet restore` OK
- [x] `dotnet build` OK
- [x] `dotnet test` OK (se aplicável)
```

## Regras Negativas

| NUNCA | FACA ISSO |
|-------|-----------|
| Adicionar o pacote apenas no `WebApi` para "cobrir" todos os slices | Declarar no projeto que de fato usa o namespace |
| Fixar versões inconsistentes entre projetos | Padronizar versão única ou usar `Directory.Packages.props` |
| Ignorar erros de `dotnet restore` | Resolver ou reverter antes de continuar |
| Editar `.csproj` com comentários extensos | Manter `.csproj` enxuto; documentar decisões em commit/README |
| Adicionar dependências experimentais em produção | Manter apenas pacotes em versão estável |

## Checklist de Validação

- [ ] Cada projeto declara somente os pacotes que realmente usa.
- [ ] Versões uniformes entre projetos (ou gerenciadas por `Directory.Packages.props`).
- [ ] `dotnet restore` roda sem warnings de conflito.
- [ ] `dotnet build -c Release` passa em toda a solution.
- [ ] `dotnet test` passa para os projetos afetados.
- [ ] Nenhum pacote removido deixou `using` órfão.
- [ ] Relatório concluído com lista de mudanças.

## Notas

- `dotnet` CLI é autoritativo sobre NuGet. Evite `packages.config` (legado).
- Para projetos multi-target, `TargetFrameworks` governa qual versão do pacote é usada.
- Pacotes críticos a manter nas versões fixadas pelo usuário (runtime, auth, driver de banco) — alinhe antes de bumpar.
