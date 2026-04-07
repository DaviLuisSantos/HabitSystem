# Guia de Implementação - Frontend de Autenticação JWT

## 📋 Visão Geral

Este documento contém todas as informações necessárias para implementar o sistema de autenticação no frontend que se conecta à API HabitSystem.

---

## 🔌 Endpoints da API

### Base URL
- **Desenvolvimento**: `http://localhost:5232`
- **Produção**: `https://seu-dominio.com`

### 1. Registro de Usuário
```http
POST /api/auth/register
Content-Type: application/json

Request Body:
{
  "name": "string",          // Obrigatório
  "email": "string",         // Obrigatório, único
  "password": "string",      // Obrigatório, mínimo 6 caracteres
  "timezone": "string"       // Opcional, padrão: "America/Sao_Paulo"
}

Response (200 OK):
{
  "userId": "guid",
  "email": "string",
  "name": "string",
  "accessToken": "string",   // Token JWT (válido por 15 minutos)
  "refreshToken": "string"   // Token de renovação (válido por 7 dias)
}

Response (400 Bad Request):
{
  "error": "Email already registered" // ou outra mensagem de erro
}
```

### 2. Login
```http
POST /api/auth/login
Content-Type: application/json

Request Body:
{
  "email": "string",
  "password": "string"
}

Response (200 OK):
{
  "userId": "guid",
  "email": "string",
  "name": "string",
  "accessToken": "string",
  "refreshToken": "string"
}

Response (400 Bad Request):
{
  "error": "Invalid email or password"
}
```

### 3. Obter Usuário Atual
```http
GET /api/auth/me
Authorization: Bearer {accessToken}

Response (200 OK):
{
  "userId": "guid",
  "email": "string",
  "name": "string",
  "timezone": "string",
  "createdAt": "datetime"
}

Response (401 Unauthorized):
{} // Sem corpo, apenas status 401
```

### 4. Renovar Token (Refresh)
```http
POST /api/auth/refresh
Content-Type: application/json

Request Body:
{
  "accessToken": "string",   // Token expirado ou próximo da expiração
  "refreshToken": "string"
}

Response (200 OK):
{
  "accessToken": "string",    // Novo access token
  "refreshToken": "string"    // Novo refresh token
}

Response (400 Bad Request):
{
  "error": "Invalid refresh token" // ou "Refresh token expired"
}
```

---

## 🔐 Fluxo de Autenticação

### 1. Fluxo de Registro/Login
```
1. Usuário preenche formulário
2. Frontend envia POST para /api/auth/register ou /api/auth/login
3. Backend retorna accessToken e refreshToken
4. Frontend armazena tokens (localStorage ou cookies)
5. Frontend redireciona para dashboard
6. Todas as requisições subsequentes incluem: Authorization: Bearer {accessToken}
```

### 2. Fluxo de Renovação Automática
```
1. Antes de cada requisição, verificar se token está próximo da expiração
2. Se sim, chamar /api/auth/refresh
3. Atualizar tokens armazenados
4. Continuar com a requisição original usando novo token
```

### 3. Fluxo de Logout
```
1. Remover tokens do armazenamento local
2. Redirecionar para página de login
3. (Opcional futuro) Chamar endpoint de logout no backend para invalidar tokens
```

---

## 💾 Armazenamento de Tokens

### Opção 1: localStorage (Mais simples)
```javascript
// Salvar tokens
localStorage.setItem('accessToken', response.accessToken);
localStorage.setItem('refreshToken', response.refreshToken);

// Recuperar tokens
const accessToken = localStorage.getItem('accessToken');
const refreshToken = localStorage.getItem('refreshToken');

// Remover tokens (logout)
localStorage.removeItem('accessToken');
localStorage.removeItem('refreshToken');
```

**Prós:** Simples, persiste entre sessões
**Contras:** Vulnerável a XSS

### Opção 2: Cookies HttpOnly (Mais seguro - requer mudanças no backend)
```javascript
// Backend define cookies com HttpOnly flag
// Frontend não precisa gerenciar manualmente
```

**Prós:** Mais seguro contra XSS
**Contras:** Requer implementação adicional no backend

---

## 🛠️ Exemplos de Implementação

### React/TypeScript

#### 1. Tipos TypeScript
```typescript
// types/auth.ts
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  name: string;
  email: string;
  password: string;
  timezone?: string;
}

export interface AuthResponse {
  userId: string;
  email: string;
  name: string;
  accessToken: string;
  refreshToken: string;
}

export interface UserProfile {
  userId: string;
  email: string;
  name: string;
  timezone: string;
  createdAt: string;
}

export interface RefreshRequest {
  accessToken: string;
  refreshToken: string;
}

export interface RefreshResponse {
  accessToken: string;
  refreshToken: string;
}
```

#### 2. Service de Autenticação
```typescript
// services/authService.ts
import axios from 'axios';
import { 
  LoginRequest, 
  RegisterRequest, 
  AuthResponse, 
  UserProfile, 
  RefreshRequest, 
  RefreshResponse 
} from '../types/auth';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5232';

class AuthService {
  async register(data: RegisterRequest): Promise<AuthResponse> {
    const response = await axios.post(`${API_URL}/api/auth/register`, data);
    if (response.data.accessToken) {
      this.setTokens(response.data.accessToken, response.data.refreshToken);
    }
    return response.data;
  }

  async login(data: LoginRequest): Promise<AuthResponse> {
    const response = await axios.post(`${API_URL}/api/auth/login`, data);
    if (response.data.accessToken) {
      this.setTokens(response.data.accessToken, response.data.refreshToken);
    }
    return response.data;
  }

  async getCurrentUser(): Promise<UserProfile> {
    const response = await axios.get(`${API_URL}/api/auth/me`, {
      headers: { Authorization: `Bearer ${this.getAccessToken()}` }
    });
    return response.data;
  }

  async refreshToken(): Promise<RefreshResponse> {
    const accessToken = this.getAccessToken();
    const refreshToken = this.getRefreshToken();
    
    if (!accessToken || !refreshToken) {
      throw new Error('No tokens available');
    }

    const response = await axios.post(`${API_URL}/api/auth/refresh`, {
      accessToken,
      refreshToken
    });

    this.setTokens(response.data.accessToken, response.data.refreshToken);
    return response.data;
  }

  logout(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
  }

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken');
  }

  getRefreshToken(): string | null {
    return localStorage.getItem('refreshToken');
  }

  setTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
  }

  isAuthenticated(): boolean {
    return !!this.getAccessToken();
  }
}

export default new AuthService();
```

#### 3. Axios Interceptor (Renovação Automática)
```typescript
// utils/axiosInterceptor.ts
import axios from 'axios';
import authService from '../services/authService';

let isRefreshing = false;
let failedQueue: any[] = [];

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach(prom => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  
  failedQueue = [];
};

// Interceptor para adicionar token em todas as requisições
axios.interceptors.request.use(
  (config) => {
    const token = authService.getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Interceptor para renovar token quando expirado
axios.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        }).then(token => {
          originalRequest.headers.Authorization = `Bearer ${token}`;
          return axios(originalRequest);
        }).catch(err => {
          return Promise.reject(err);
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        const response = await authService.refreshToken();
        const { accessToken } = response;
        
        processQueue(null, accessToken);
        
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return axios(originalRequest);
      } catch (err) {
        processQueue(err, null);
        authService.logout();
        window.location.href = '/login';
        return Promise.reject(err);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);
```

#### 4. Context de Autenticação (React)
```typescript
// context/AuthContext.tsx
import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import authService from '../services/authService';
import { AuthResponse, LoginRequest, RegisterRequest, UserProfile } from '../types/auth';

interface AuthContextData {
  user: UserProfile | null;
  loading: boolean;
  login: (data: LoginRequest) => Promise<void>;
  register: (data: RegisterRequest) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextData>({} as AuthContextData);

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadUser();
  }, []);

  async function loadUser() {
    try {
      if (authService.isAuthenticated()) {
        const userData = await authService.getCurrentUser();
        setUser(userData);
      }
    } catch (error) {
      console.error('Failed to load user', error);
      authService.logout();
    } finally {
      setLoading(false);
    }
  }

  async function login(data: LoginRequest) {
    const response = await authService.login(data);
    setUser({
      userId: response.userId,
      email: response.email,
      name: response.name,
      timezone: 'America/Sao_Paulo',
      createdAt: new Date().toISOString()
    });
  }

  async function register(data: RegisterRequest) {
    const response = await authService.register(data);
    setUser({
      userId: response.userId,
      email: response.email,
      name: response.name,
      timezone: data.timezone || 'America/Sao_Paulo',
      createdAt: new Date().toISOString()
    });
  }

  function logout() {
    authService.logout();
    setUser(null);
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        loading,
        login,
        register,
        logout,
        isAuthenticated: !!user
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
```

#### 5. Componente de Login
```typescript
// pages/Login.tsx
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export const Login: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await login({ email, password });
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.response?.data?.error || 'Falha no login');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <h1>Login</h1>
      <form onSubmit={handleSubmit}>
        <div>
          <label>Email:</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div>
          <label>Senha:</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={6}
          />
        </div>
        {error && <div className="error">{error}</div>}
        <button type="submit" disabled={loading}>
          {loading ? 'Entrando...' : 'Entrar'}
        </button>
      </form>
    </div>
  );
};
```

#### 6. Rota Protegida
```typescript
// components/PrivateRoute.tsx
import React from 'react';
import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

interface PrivateRouteProps {
  children: React.ReactElement;
}

export const PrivateRoute: React.FC<PrivateRouteProps> = ({ children }) => {
  const { isAuthenticated, loading } = useAuth();

  if (loading) {
    return <div>Carregando...</div>;
  }

  return isAuthenticated ? children : <Navigate to="/login" />;
};

// Uso no Router:
// <Route path="/dashboard" element={<PrivateRoute><Dashboard /></PrivateRoute>} />
```

---

### Vanilla JavaScript

```javascript
// auth.js
const API_URL = 'http://localhost:5232';

// Função de login
async function login(email, password) {
  try {
    const response = await fetch(`${API_URL}/api/auth/login`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.error || 'Falha no login');
    }

    const data = await response.json();
    
    // Salvar tokens
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    
    return data;
  } catch (error) {
    console.error('Erro no login:', error);
    throw error;
  }
}

// Função para fazer requisições autenticadas
async function fetchWithAuth(url, options = {}) {
  const token = localStorage.getItem('accessToken');
  
  const headers = {
    ...options.headers,
    'Authorization': `Bearer ${token}`,
  };

  const response = await fetch(url, { ...options, headers });

  // Se token expirado, tentar renovar
  if (response.status === 401) {
    await refreshToken();
    // Tentar novamente com novo token
    const newToken = localStorage.getItem('accessToken');
    headers['Authorization'] = `Bearer ${newToken}`;
    return fetch(url, { ...options, headers });
  }

  return response;
}

// Função de refresh
async function refreshToken() {
  const accessToken = localStorage.getItem('accessToken');
  const refreshToken = localStorage.getItem('refreshToken');

  const response = await fetch(`${API_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ accessToken, refreshToken }),
  });

  if (!response.ok) {
    // Token de refresh inválido, fazer logout
    logout();
    window.location.href = '/login.html';
    throw new Error('Sessão expirada');
  }

  const data = await response.json();
  localStorage.setItem('accessToken', data.accessToken);
  localStorage.setItem('refreshToken', data.refreshToken);
}

// Função de logout
function logout() {
  localStorage.removeItem('accessToken');
  localStorage.removeItem('refreshToken');
}

// Verificar se está autenticado
function isAuthenticated() {
  return !!localStorage.getItem('accessToken');
}
```

---

## 🎨 UI/UX Recomendações

### Formulário de Registro
- **Campos obrigatórios:** Nome, Email, Senha
- **Validações:**
  - Email: formato válido
  - Senha: mínimo 6 caracteres (recomendado 8+)
  - Confirmar senha
- **Mensagens de erro claras**
- **Loading state durante registro**

### Formulário de Login
- **Campos:** Email, Senha
- **Opção:** "Lembrar-me" (manter sessão)
- **Link:** "Esqueci minha senha" (futuro)
- **Link:** "Criar conta"

### Dashboard
- **Header:** Mostrar nome do usuário
- **Botão:** Logout visível
- **Feedback:** Loading states para requisições

---

## 🔍 Debugging e Testes

### Testar Autenticação Manualmente

```javascript
// Console do navegador

// 1. Login
const loginData = { email: "joao@example.com", password: "senha123" };
fetch('http://localhost:5232/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(loginData)
})
  .then(r => r.json())
  .then(data => {
    console.log('Tokens:', data);
    localStorage.setItem('accessToken', data.accessToken);
  });

// 2. Testar endpoint protegido
const token = localStorage.getItem('accessToken');
fetch('http://localhost:5232/api/habits', {
  headers: { 'Authorization': `Bearer ${token}` }
})
  .then(r => r.json())
  .then(data => console.log('Habits:', data));
```

### Erros Comuns

| Erro | Causa | Solução |
|------|-------|---------|
| 401 Unauthorized | Token ausente ou inválido | Verificar se token está sendo enviado no header |
| 400 Bad Request | Dados inválidos | Verificar formato dos dados enviados |
| CORS Error | Configuração CORS | Backend já configurado, verificar URL |
| Token expirado | Access token venceu | Implementar refresh automático |

---

## 📊 Estados da Aplicação

### Estados de Autenticação
```
1. Não autenticado (guest)
   - Mostrar páginas públicas (login, registro)
   - Redirecionar rotas protegidas para login

2. Autenticando (loading)
   - Mostrar spinner/loading
   - Aguardar resposta da API

3. Autenticado (logged in)
   - Acesso a todas as rotas protegidas
   - Token válido no localStorage

4. Token expirando
   - Renovar automaticamente em background
   - Usuário não percebe

5. Sessão expirada
   - Logout automático
   - Redirecionar para login
   - Mostrar mensagem "Sessão expirada"
```

---

## ✅ Checklist de Implementação

- [ ] Criar tipos/interfaces TypeScript
- [ ] Implementar serviço de autenticação (authService)
- [ ] Configurar axios/fetch com interceptors
- [ ] Criar Context de autenticação (React) ou store (Vue/Angular)
- [ ] Implementar formulário de registro
- [ ] Implementar formulário de login
- [ ] Criar componente de rota protegida
- [ ] Implementar renovação automática de tokens
- [ ] Implementar logout
- [ ] Testar todos os fluxos
- [ ] Adicionar tratamento de erros
- [ ] Adicionar loading states
- [ ] Testar expiração de tokens
- [ ] Testar em diferentes navegadores

---

## 🚀 Próximos Passos (Futuro)

- Implementar "Esqueci minha senha"
- Adicionar verificação de email
- Implementar 2FA (autenticação de dois fatores)
- Adicionar login social (Google, Facebook)
- Implementar "Lembrar-me" com tokens persistentes
- Adicionar gestão de múltiplos dispositivos

---

## 📚 Recursos Úteis

- [JWT.io](https://jwt.io/) - Decodificar e debugar tokens JWT
- [MDN - Authorization Header](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Authorization)
- [React Router - Protected Routes](https://reactrouter.com/en/main/start/tutorial)
- [Axios Documentation](https://axios-http.com/docs/intro)

---

## 💡 Dicas Importantes

1. **NUNCA** armazene o token no código fonte
2. **SEMPRE** use HTTPS em produção
3. **Implemente** renovação automática de tokens
4. **Trate** todos os erros da API com mensagens amigáveis
5. **Teste** o fluxo de expiração de tokens
6. **Limpe** os tokens ao fazer logout
7. **Valide** os inputs no frontend (além do backend)
8. **Use** variáveis de ambiente para URLs da API

---

**Documento criado em:** 2026-04-07  
**Versão da API:** 1.0  
**Última atualização:** 2026-04-07
