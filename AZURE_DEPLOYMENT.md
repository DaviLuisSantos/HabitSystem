# HabitSystem API - Guia de Deploy no Azure

## Pré-requisitos

- Conta Azure
- Azure CLI instalado
- Docker (opcional, para testes locais)
- .NET 9 SDK

## Opções de Hospedagem no Azure

### 1. **Azure App Service** (Recomendado para iniciantes)

A forma mais simples de hospedar sua API.

#### Passos:

1. **Criar um Resource Group:**
```bash
az group create --name HabitSystemRG --location eastus
```

2. **Criar um App Service Plan:**
```bash
az appservice plan create --name HabitSystemPlan --resource-group HabitSystemRG --sku B1 --is-linux
```

3. **Criar App Service com .NET 9:**
```bash
az webapp create --resource-group HabitSystemRG --plan HabitSystemPlan --name HabitSystemAPI --runtime "DOTNET|9.0"
```

4. **Deploy pelo Git (GitHub Actions):**
   - Configure GitHub Actions no seu repositório
   - Azure App Service criará automaticamente um workflow
   - Push para `master` fará deploy automaticamente

5. **Deploy manual:**
```bash
# Publicar aplicação
dotnet publish -c Release -o ./publish

# Deploy via zip
cd publish
zip -r ../deploy.zip .
az webapp deployment source config-zip --resource-group HabitSystemRG --name HabitSystemAPI --src ../deploy.zip
```

---

### 2. **Azure Container Instances (ACI)** (Simples + Containers)

Perfeito se quiser usar Docker.

#### Passos:

1. **Build da imagem Docker:**
```bash
docker build -t habitapi:latest .
```

2. **Teste local:**
```bash
docker run -p 8080:8080 -v $(pwd)/data:/app/data habitapi:latest
```

3. **Fazer upload para Azure Container Registry:**
```bash
# Criar registry
az acr create --resource-group HabitSystemRG --name habitregistry --sku Basic

# Build e push
az acr build --registry habitregistry --image habitapi:latest .
```

4. **Deploy no ACI:**
```bash
az container create \
  --resource-group HabitSystemRG \
  --name habitapi-container \
  --image habitregistry.azurecr.io/habitapi:latest \
  --cpu 1 --memory 1 \
  --ports 80 \
  --environment-variables PORT=80 \
  --registry-login-server habitregistry.azurecr.io \
  --registry-username <username> \
  --registry-password <password>
```

---

### 3. **Azure Kubernetes Service (AKS)** (Avançado + Escalabilidade)

Para escalabilidade produção.

```bash
# Criar cluster
az aks create --resource-group HabitSystemRG --name habitcluster --node-count 1

# Deploy via Helm ou kubectl
kubectl apply -f k8s/deployment.yaml
```

---

## Configurações Importantes

### Variáveis de Ambiente

```
ASPNETCORE_ENVIRONMENT=Production
PORT=8080 (Azure App Service atribui dinamicamente)
ConnectionStrings__DefaultConnection=Data Source=/app/data/habitdb.sqlite
```

### Persistência de Dados (SQLite)

Para manter o banco de dados entre restarts:

**Azure App Service:**
- Use Azure Files ou anexe um armazenamento persistente
- Ou implante em um container com volume persistente

**Com Docker/ACI:**
```bash
# Montar volume
-v habitdata:/app/data
```

---

## Monitoramento e Logging

1. **Application Insights:**
```bash
az resource create --resource-group HabitSystemRG \
  --name habitapi-insights \
  --resource-type "Microsoft.Insights/components" \
  --properties '{"Application_Type":"web"}'
```

2. **Logs em tempo real:**
```bash
az webapp log tail --resource-group HabitSystemRG --name HabitSystemAPI
```

---

## Custos Estimados

| Serviço | Plano | Custo Mensal |
|---------|-------|------------|
| App Service | B1 | ~$10 USD |
| Container Registry | Basic | ~$5 USD |
| AKS | Básico | ~$73 USD |

---

## Health Check

A API expõe um endpoint `/health` para monitoramento:

```bash
curl https://seu-app.azurewebsites.net/health
```

---

## Troubleshooting

**Problema:** Banco de dados não persiste
- **Solução:** Configure armazenamento persistente no Azure

**Problema:** Porta não encontrada
- **Solução:** Azure App Service atribui PORT dinamicamente. Já está configurado em `Program.cs`

**Problema:** CORS não funciona
- **Solução:** Verifique `AllowedHosts` em `appsettings.json` (está como `*`)

---

## Próximos Passos

1. Escolha a opção de hospedagem (recomendo App Service para começar)
2. Crie os recursos no Azure
3. Configure o deploy (Git ou Docker)
4. Monitore via Azure Portal
5. Configure backups do banco de dados SQLite

