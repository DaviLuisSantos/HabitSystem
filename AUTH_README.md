# Sistema de Autenticação JWT - HabitSystem

## ✅ Implementação Completa

O sistema de autenticação JWT foi implementado com sucesso no HabitSystem!

## 🔐 Funcionalidades Implementadas

### 1. **Registro de Usuários** (`POST /api/auth/register`)
- Criação de novos usuários
- Hash seguro de senhas com BCrypt
- Validação de email único
- Geração automática de tokens JWT

### 2. **Login** (`POST /api/auth/login`)
- Autenticação com email e senha
- Retorna Access Token (válido por 15 minutos)
- Retorna Refresh Token (válido por 7 dias)

### 3. **Refresh Token** (`POST /api/auth/refresh`)
- Renovação do Access Token sem necessidade de novo login
- Gera novo par de tokens quando o Access Token expira

### 4. **Usuário Atual** (`GET /api/auth/me`)
- Retorna informações do usuário autenticado
- Requer autenticação

### 5. **Proteção de Endpoints**
Todos os endpoints de negócio agora exigem autenticação:
- ✅ Habits (criar, listar, atualizar, arquivar)
- ✅ Check-ins (criar, listar, atualizar)
- ✅ Scores (visualizar diário e semanal)

## 📋 Configuração

### JWT Settings (appsettings.json)
```json
{
  "JwtSettings": {
    "Secret": "your-super-secret-key-change-this-in-production-minimum-32-characters",
    "Issuer": "HabitSystem",
    "Audience": "HabitSystemUsers",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

### ⚠️ IMPORTANTE: Produção
- Altere o `Secret` no appsettings.Production.json
- Use um secret forte e único (mínimo 32 caracteres)
- Considere usar variáveis de ambiente ou Azure Key Vault

## 🚀 Como Usar

### 1. Registrar Novo Usuário
```bash
POST /api/auth/register
Content-Type: application/json

{
  "name": "João Silva",
  "email": "joao@example.com",
  "password": "senha123",
  "timezone": "America/Sao_Paulo"
}
```

**Resposta:**
```json
{
  "userId": "guid",
  "email": "joao@example.com",
  "name": "João Silva",
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123..."
}
```

### 2. Fazer Login
```bash
POST /api/auth/login
Content-Type: application/json

{
  "email": "joao@example.com",
  "password": "senha123"
}
```

### 3. Usar o Access Token
Adicione o header de autorização em todas as requisições:

```
Authorization: Bearer {accessToken}
```

Exemplo:
```bash
GET /api/habits
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 4. Renovar Token Expirado
```bash
POST /api/auth/refresh
Content-Type: application/json

{
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123..."
}
```

## 🔒 Segurança

### Implementações de Segurança:
- ✅ Senhas nunca armazenadas em texto plano (BCrypt)
- ✅ Tokens JWT assinados com HMAC SHA-256
- ✅ Refresh tokens únicos por usuário
- ✅ Expiração automática de tokens
- ✅ Validação de email único
- ✅ Proteção contra acesso não autorizado
- ✅ CORS configurado
- ✅ HTTPS recomendado para produção

## 📦 Pacotes Adicionados

- `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.0)
- `BCrypt.Net-Next` (4.0.3)

## 🗄️ Alterações no Banco de Dados

Novos campos na tabela `Users`:
- `PasswordHash` (TEXT, NOT NULL)
- `RefreshToken` (TEXT, NULL)
- `RefreshTokenExpiryTime` (DATETIME, NULL)

Migration: `20260407000703_AddAuthenticationFields`

## ✅ Testes Realizados

Todos os cenários foram testados com sucesso:
- ✅ Registro de usuários
- ✅ Login com credenciais corretas
- ✅ Login com credenciais incorretas (falha esperada)
- ✅ Registro com email duplicado (falha esperada)
- ✅ Acesso a endpoint protegido sem token (falha esperada)
- ✅ Acesso a endpoint protegido com token válido
- ✅ Refresh token funcional
- ✅ Refresh com token inválido (falha esperada)
- ✅ Obter usuário atual
- ✅ Criar hábitos autenticado
- ✅ Filtragem de dados por usuário

## 📝 Arquivo de Testes

Use o arquivo `HabitSystem-Auth-Tests.http` para testar os endpoints com VSCode REST Client ou ferramentas similares.

## 🎯 Próximos Passos (Opcionais)

- [ ] Implementar logout (invalidação de tokens)
- [ ] Suporte a múltiplos dispositivos (refresh tokens por dispositivo)
- [ ] Implementar "esqueci minha senha"
- [ ] Adicionar rate limiting para proteção contra força bruta
- [ ] Implementar two-factor authentication (2FA)
- [ ] Logs de auditoria de login

## 👤 Isolamento de Dados

Todos os dados agora são isolados por usuário:
- Cada usuário só vê seus próprios hábitos
- Cada usuário só vê seus próprios check-ins
- Cada usuário só vê seus próprios scores
- Não há possibilidade de vazamento de dados entre usuários

## 🎉 Status

✅ **Sistema de autenticação totalmente funcional e testado!**
