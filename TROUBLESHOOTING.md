# 🔧 Troubleshooting - Erro 500 em Autenticação

## ❌ Problema Identificado

**Erro:** Status 500 ao tentar fazer login ou registro  
**Causa:** Configurações JWT ausentes no `appsettings.Production.json`

## ✅ Solução Aplicada

As configurações JWT foram adicionadas ao `appsettings.Production.json`.

---

## 🚀 Passos para Deploy no Azure

### 1. Verificar Banco de Dados

O banco de dados no Azure precisa ter a migration de autenticação aplicada.

**Opção A: Aplicar migration automaticamente (recomendado para desenvolvimento)**

No `Program.cs`, a migration já está configurada para criar o banco automaticamente:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Database.CanConnect())
    {
        db.Database.EnsureCreated();
    }
}
```

**Opção B: Aplicar migrations manualmente**

Se você precisar aplicar migrations manualmente em produção, conecte-se ao Azure App Service via SSH ou Console e execute:

```bash
cd /home/site/wwwroot
dotnet ef database update
```

### 2. Configurar Secret JWT no Azure

⚠️ **IMPORTANTE:** Não use o secret padrão em produção!

**Método 1: Via Azure Portal (Recomendado)**

1. Vá para o Azure Portal
2. Navegue até seu App Service
3. Clique em **Configuration** (Configuração)
4. Em **Application settings**, adicione:
   - Nome: `JwtSettings__Secret`
   - Valor: `[gere-um-secret-forte-aqui-minimo-32-caracteres]`
5. Clique em **Save**

**Método 2: Via CLI**

```bash
az webapp config appsettings set \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME \
  --settings JwtSettings__Secret="seu-secret-forte-aqui"
```

**Gerar um Secret Forte:**

Use este comando PowerShell para gerar um secret aleatório:

```powershell
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object {[char]$_})
```

Ou online: https://randomkeygen.com/ (escolha "Fort Knox Passwords")

### 3. Verificar Variáveis de Ambiente

Certifique-se de que as seguintes variáveis estão configuradas no Azure:

```
ASPNETCORE_ENVIRONMENT=Production
JwtSettings__Secret=[seu-secret-forte]
JwtSettings__Issuer=HabitSystem
JwtSettings__Audience=HabitSystemUsers
JwtSettings__AccessTokenExpirationMinutes=15
JwtSettings__RefreshTokenExpirationDays=7
```

### 4. Verificar CORS

Se estiver acessando de um frontend em domínio diferente, verifique se o CORS está configurado corretamente no `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Type", "X-Total-Count");
    });
});
```

**Para produção, é recomendado restringir as origens:**

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://seu-frontend.vercel.app")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

E no middleware:
```csharp
app.UseCors("AllowFrontend");
```

### 5. Verificar Logs no Azure

Para ver os erros detalhados:

1. No Azure Portal, vá até seu App Service
2. Clique em **Log stream** (Fluxo de logs)
3. Ou acesse **Monitoring > Log Analytics**

**Via CLI:**

```bash
az webapp log tail --resource-group SEU_RESOURCE_GROUP --name SEU_APP_NAME
```

---

## 🔍 Diagnóstico Local vs Produção

### Testar Localmente com Configurações de Produção

```bash
# Windows PowerShell
$env:ASPNETCORE_ENVIRONMENT="Production"
dotnet run

# Linux/Mac
ASPNETCORE_ENVIRONMENT=Production dotnet run
```

### Verificar se o Secret JWT está sendo carregado

Adicione este endpoint temporário para debug (REMOVA DEPOIS!):

```csharp
// NO Program.cs - APENAS PARA DEBUG
app.MapGet("/debug/config", (IConfiguration config) =>
{
    var secret = config["JwtSettings:Secret"];
    return Results.Ok(new {
        hasSecret = !string.IsNullOrEmpty(secret),
        secretLength = secret?.Length ?? 0,
        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    });
}).AllowAnonymous();
```

Acesse: `https://seu-app.azurewebsites.net/debug/config`

Se `hasSecret` for `false`, as configurações JWT não estão sendo carregadas.

---

## 🐛 Erros Comuns e Soluções

### Erro 500: "JWT Secret not configured"

**Causa:** Configuração `JwtSettings:Secret` ausente ou vazia

**Solução:**
1. Adicione a configuração no Azure App Settings
2. Ou certifique-se de que `appsettings.Production.json` tem a seção JwtSettings

### Erro 500: "The ConnectionString property has not been initialized"

**Causa:** Banco de dados não configurado corretamente

**Solução:**
1. Verifique se a ConnectionString está correta
2. Certifique-se de que o diretório `/home/data/` existe no Azure
3. Verifique permissões de escrita

### Erro 401 ao acessar endpoints protegidos

**Causa:** Token não está sendo enviado ou é inválido

**Solução:**
1. Verifique se o header `Authorization: Bearer {token}` está sendo enviado
2. Verifique se o token não expirou
3. Use https://jwt.io para decodificar e validar o token

### Erro de CORS

**Causa:** Frontend em domínio diferente do backend

**Solução:**
1. Configure CORS adequadamente (veja seção acima)
2. Certifique-se de que `app.UseCors()` vem ANTES de `app.UseAuthorization()`

---

## 📋 Checklist de Deploy

Antes de fazer deploy para produção:

- [ ] `appsettings.Production.json` tem seção `JwtSettings`
- [ ] Secret JWT forte configurado no Azure App Settings
- [ ] Migration de autenticação aplicada ao banco de dados
- [ ] CORS configurado corretamente
- [ ] Variáveis de ambiente configuradas no Azure
- [ ] Logs habilitados para diagnóstico
- [ ] Testado localmente com `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Banco de dados acessível e com permissões corretas
- [ ] HTTPS habilitado (certificado SSL configurado)

---

## 🔐 Configuração de Segurança Recomendada para Produção

### Azure Key Vault (Opção Mais Segura)

Para produção enterprise, use Azure Key Vault para armazenar o JWT Secret:

```bash
# Criar Key Vault
az keyvault create \
  --name seu-keyvault \
  --resource-group SEU_RESOURCE_GROUP \
  --location eastus

# Adicionar secret
az keyvault secret set \
  --vault-name seu-keyvault \
  --name JwtSecret \
  --value "seu-secret-forte"

# Dar permissão ao App Service
az webapp identity assign \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME

# Configurar App Service para usar Key Vault
az webapp config appsettings set \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME \
  --settings JwtSettings__Secret="@Microsoft.KeyVault(SecretUri=https://seu-keyvault.vault.azure.net/secrets/JwtSecret/)"
```

---

## 📞 Suporte Adicional

Se o problema persistir:

1. **Verifique os logs detalhados** no Azure
2. **Teste localmente** com configurações de produção
3. **Verifique a migration** do banco de dados
4. **Confirme as variáveis de ambiente** no Azure

### Comando para verificar configurações no Azure:

```bash
az webapp config appsettings list \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME
```

---

**Última atualização:** 2026-04-07  
**Status:** ✅ Configurações corrigidas no `appsettings.Production.json`
