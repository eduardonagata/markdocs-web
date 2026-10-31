# AppTemplate.TestStubs

Classlib de stubs para desenvolvimento e teste do frontend
(`reactjs-app-template`). **Não usar em produção.**

Provê três capacidades que em produção virão de serviços reais
(Firebase/Entra ID, serviço de tenants, API de sessão):

1. **Emissão de JWT de dev** (`POST /api/dev/login`) — aceita qualquer
   credencial e devolve um JWT HS256 assinado, carregando claim `tid`
   com o tenantId configurado.
2. **Endpoints transversais hardcoded** (`GET /api/me`, `GET /api/tenants`)
   — aquilo que o frontend espera encontrar no backend e que ainda não
   foi implementado.
3. **Parâmetros de validação** para o pipeline `JwtBearer` aceitar os
   JWTs emitidos pelo item 1, sem exigir IdP real configurado.

**Escopo — importante**: estes três itens são **transversais**. Não são
específicos do slice Contatos nem de nenhum slice em particular. O JWT
emitido vale para qualquer endpoint com `.RequireAuthorization()` em
qualquer slice. No template, o slice `Contatos` é o único exemplo de
referência que consome e exerce o JWT (pelo seu `ITenantAccessor` +
`IMongoDatabase` scoped), mas conforme novos slices forem criados eles
participam do mesmo fluxo sem nada aqui precisar mudar.

## Por que existe

Sem isto, o fluxo de teste do frontend trava em três pontos:

- Login (frontend chama IdP — não configurado em dev).
- `/api/me` e `/api/tenants` (não existem no backend).
- Endpoints reais com `.RequireAuthorization()` (rejeitam JWT inválido).

Com isto, o fluxo ponta-a-ponta é **idêntico ao de produção**, mudando
apenas o emissor/validador do JWT:

```
[Login.tsx]                              [Program.cs]
  createDevLoginAuthProvider         ---> POST /api/dev/login
       ↓ JWT real (HS256)                  (TestStubs)
  apiClient (injeta Bearer)          ---> GET /api/entidades-externas
                                           ↓
                                        JwtBearer valida com
                                        DEV_JWT_SIGNING_KEY
                                           ↓
                                        ITenantAccessor lê claim "tid"
                                           ↓
                                        IMongoDatabase aponta p/ tid-{tid}
```

## Variáveis de ambiente (apenas dev)

Todas opcionais — se ausentes, defaults são aplicados (e a chave é
volátil, regerada a cada inicialização).

| Variável | Default | Finalidade |
|----------|---------|------------|
| `DEV_JWT_SIGNING_KEY` | chave volátil por processo | HS256 — mínimo 32 bytes. Se omitida, JWTs emitidos são invalidados ao reiniciar o backend. |
| `DEV_JWT_TENANT_ID` | `dev` | Valor gravado como claim `tid` no JWT. Define qual `tid-{tenantId}` database os endpoints usarão. |
| `DEV_JWT_ISSUER` | `apptemplate-dev` | Claim `iss` do JWT + `ValidIssuer` da validação. |
| `DEV_JWT_AUDIENCE` | `apptemplate-api` | Claim `aud` do JWT + `ValidAudience` da validação. |
| `DEV_JWT_EXPIRATION_HOURS` | `24` | Validade do JWT emitido. |

## Wiring (em `Program.cs`)

```csharp
if (builder.Environment.IsDevelopment())
{
    var devJwtOptions = DevJwtOptions.FromConfiguration(builder.Configuration);
    builder.Services.AddTestStubs(devJwtOptions);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters =
                DevTokenIssuer.BuildValidationParameters(devJwtOptions);
        });
}
else
{
    // JwtBearer normal contra JWT_AUTHORITY
}

// ... app.UseAuthentication(); app.UseAuthorization();
// ... app.MapEndpoints(Assembly.GetExecutingAssembly());

if (app.Environment.IsDevelopment())
{
    app.MapTestStubsEndpoints();
}
```

Os `if (IsDevelopment())` garantem que nada deste classlib atinge produção.

## Endpoints

### POST /api/dev/login

Request:
```json
{ "email": "qualquer@coisa.com", "password": "qualquer" }
```

Response 200:
```json
{ "token": "<JWT HS256>", "tenantId": "dev" }
```

### GET /api/me

Response 200:
```json
{
  "tenantId": "dev",
  "userId": "000000000000000000000000",
  "subscriptionStatus": "active",
  "admLocatario": true,
  "scope": ["ex:all"]
}
```

### GET /api/tenants?email=...

Response 200:
```json
[{ "tenantId": "dev", "name": "Dev" }]
```

## Quando remover

Quando o backend tiver:

- Endpoints reais `/api/me` e `/api/tenants` (ou alternativa equivalente).
- IdP real configurado em dev via `JWT_AUTHORITY` (Firebase emulator,
  OAuth playground, etc.).

A remoção é:

1. Em `Program.cs`: reverter o bloco `if (IsDevelopment())` de autenticação
   para usar só o `AddJwtBearer` com `JWT_AUTHORITY`.
2. Em `Program.cs`: remover as linhas `AddTestStubs(...)` e `MapTestStubsEndpoints()`.
3. Remover o `ProjectReference` ao TestStubs em `AppTemplate.WebApi.csproj`.
4. Remover o projeto da solution.
5. Apagar o diretório `src/AppTemplate.TestStubs/`.
