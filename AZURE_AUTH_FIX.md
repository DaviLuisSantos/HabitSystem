# Script de Deploy e Verificação - Azure

## 🎯 Resumo do Problema e Solução

### ❌ Problema Encontrado
O erro 500 ao fazer login/registro estava ocorrendo porque o arquivo `appsettings.Production.json` **não tinha as configurações JWT**.

### ✅ Solução Aplicada
1. ✅ Adicionadas configurações JWT ao `appsettings.Production.json`
2. ✅ Documentado como configurar secret seguro no Azure

---

## 🚀 Passo a Passo para Corrigir no Azure

### Opção 1: Deploy Rápido (GitHub Actions já configurado)

Se você tem GitHub Actions configurado, basta fazer:

```bash
git add .
git commit -m "fix: Add JWT settings to Production config"
git push
```

O pipeline do Azure vai fazer o deploy automaticamente.

### Opção 2: Deploy Manual via Azure CLI

```bash
# 1. Build do projeto
dotnet publish -c Release -o ./publish

# 2. Comprimir arquivos
cd publish
zip -r ../app.zip .
cd ..

# 3. Deploy para Azure
az webapp deployment source config-zip \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME \
  --src app.zip
```

### Opção 3: Deploy via VS Code

1. Instale a extensão "Azure App Service"
2. Clique com botão direito no projeto
3. Selecione "Deploy to Web App"
4. Escolha seu App Service

---

## ⚙️ Configurar Secret JWT no Azure (OBRIGATÓRIO)

### Via Azure Portal (Mais Fácil)

1. Acesse: https://portal.azure.com
2. Vá em **App Services** → Seu app
3. No menu lateral, clique em **Configuration**
4. Em **Application settings**, clique em **+ New application setting**
5. Adicione:
   ```
   Name: JwtSettings__Secret
   Value: [cole o secret gerado abaixo]
   ```
6. Clique em **OK** e depois em **Save**
7. Reinicie o App Service

### Gerar Secret Forte

Execute este comando no PowerShell para gerar um secret aleatório e forte:

```powershell
# Gerar secret de 64 caracteres
$secret = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object {[char]$_})
Write-Host "Seu JWT Secret (copie e cole no Azure):" -ForegroundColor Green
Write-Host $secret -ForegroundColor Yellow
Write-Host "`nGuarde este secret em local seguro!" -ForegroundColor Red
```

### Via Azure CLI

```bash
# Substitua os valores entre <>
az webapp config appsettings set \
  --resource-group <SEU_RESOURCE_GROUP> \
  --name <SEU_APP_NAME> \
  --settings JwtSettings__Secret="<SEU_SECRET_GERADO>"
```

---

## 🔍 Verificar se Funcionou

### Teste 1: Health Check
```bash
curl https://SEU_APP.azurewebsites.net/health
```

**Resposta esperada:**
```json
{"status":"healthy"}
```

### Teste 2: Registrar Usuário
```bash
curl -X POST https://SEU_APP.azurewebsites.net/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Teste",
    "email": "teste@example.com",
    "password": "senha123"
  }'
```

**Resposta esperada (200 OK):**
```json
{
  "userId": "guid-aqui",
  "email": "teste@example.com",
  "name": "Teste",
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123..."
}
```

**Se ainda der erro 500:**
- Verifique os logs (próxima seção)
- Confirme que o secret foi configurado
- Reinicie o App Service

---

## 📊 Ver Logs do Azure

### Via Portal
1. Azure Portal → Seu App Service
2. Menu lateral → **Monitoring** → **Log stream**
3. Aguarde os logs aparecerem
4. Faça uma requisição de teste
5. Veja o erro detalhado nos logs

### Via CLI
```bash
az webapp log tail \
  --resource-group SEU_RESOURCE_GROUP \
  --name SEU_APP_NAME
```

---

## 🗄️ Verificar Banco de Dados

O banco de dados precisa ter a tabela Users com os campos de autenticação.

### Conectar ao Console do Azure

1. Azure Portal → Seu App Service
2. Menu lateral → **Development Tools** → **SSH** ou **Console**
3. Execute:

```bash
cd /home/data
ls -la

# Ver se habitdb.sqlite existe
```

### Verificar Migrations

Se o banco não tiver os campos de autenticação, você pode:

1. **Deletar o banco atual** (cuidado em produção!):
```bash
rm /home/data/habitdb.sqlite
```

2. **Reiniciar o app** para criar novo banco com migrations:
```bash
# Via Portal: Overview → Restart
# Via CLI:
az webapp restart --resource-group SEU_RESOURCE_GROUP --name SEU_APP_NAME
```

O banco será recriado automaticamente com todas as tabelas corretas.

---

## 🔧 Script PowerShell Completo de Verificação

Salve como `verify-azure-auth.ps1`:

```powershell
# Configurações
$appUrl = "https://SEU_APP.azurewebsites.net"  # ALTERE AQUI

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Verificação de Autenticação - Azure" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Teste 1: Health Check
Write-Host "1. Testando Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$appUrl/health" -Method Get
    Write-Host "   ✅ Health Check OK: $($health.status)" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Health Check falhou: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Teste 2: Registro
Write-Host ""
Write-Host "2. Testando Registro de Usuário..." -ForegroundColor Yellow
$registerBody = @{
    name = "Teste Azure"
    email = "teste$(Get-Random -Maximum 9999)@example.com"
    password = "senha123"
} | ConvertTo-Json

try {
    $register = Invoke-RestMethod -Uri "$appUrl/api/auth/register" -Method Post -Body $registerBody -ContentType "application/json"
    Write-Host "   ✅ Registro bem-sucedido!" -ForegroundColor Green
    Write-Host "   UserId: $($register.userId)" -ForegroundColor Gray
    $token = $register.accessToken
} catch {
    Write-Host "   ❌ Registro falhou!" -ForegroundColor Red
    Write-Host "   Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    Write-Host "   Erro: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Detalhes: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

# Teste 3: Login
Write-Host ""
Write-Host "3. Testando Login..." -ForegroundColor Yellow
$loginBody = @{
    email = $register.email
    password = "senha123"
} | ConvertTo-Json

try {
    $login = Invoke-RestMethod -Uri "$appUrl/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    Write-Host "   ✅ Login bem-sucedido!" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Login falhou: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Teste 4: Endpoint Protegido
Write-Host ""
Write-Host "4. Testando Endpoint Protegido..." -ForegroundColor Yellow
$headers = @{
    "Authorization" = "Bearer $token"
}

try {
    $habits = Invoke-RestMethod -Uri "$appUrl/api/habits?activeOnly=true" -Method Get -Headers $headers
    Write-Host "   ✅ Acesso autorizado! Total hábitos: $($habits.Count)" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Falha ao acessar endpoint protegido: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✅ TODOS OS TESTES PASSARAM!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
```

**Para executar:**
```powershell
.\verify-azure-auth.ps1
```

---

## 📝 Checklist Final

Antes de testar novamente no Azure:

- [ ] Código atualizado com `appsettings.Production.json` corrigido
- [ ] Deploy feito para o Azure
- [ ] Secret JWT configurado no Azure App Settings
- [ ] App Service reiniciado
- [ ] Logs habilitados para diagnóstico
- [ ] Testes executados (health, register, login)

---

## ⚡ TL;DR - Correção Rápida

Se você só quer corrigir rápido:

```bash
# 1. Já fizemos: Corrigir appsettings.Production.json ✅

# 2. Fazer deploy
git add .
git commit -m "fix: Add JWT settings to production"
git push

# 3. Gerar e configurar secret
# Gere no PowerShell:
-join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object {[char]$_})

# 4. Adicione no Azure Portal:
# App Service → Configuration → Application settings
# Nome: JwtSettings__Secret
# Valor: [secret gerado acima]

# 5. Reinicie o app
# Azure Portal → Overview → Restart

# 6. Teste!
```

---

**Criado em:** 2026-04-07  
**Status:** ✅ Problema identificado e corrigido
