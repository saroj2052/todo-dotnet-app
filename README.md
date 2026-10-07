# Task App

A small .NET 10 Razor Pages app to list, create, edit, complete, and delete tasks.
SQLite stores tasks without a separate database server. No test project is included.

## Run locally

Install the .NET 10 SDK, then run:

```sh
dotnet run --urls http://localhost:5080
```

Open http://localhost:5080. Tasks are stored in `data/tasks.db`.
The `DataDirectory` environment variable overrides the storage directory.

## Run with Docker

```sh
docker build -t task-app .
docker run --rm -p 5080:8080 -v task-data:/home/data task-app
```

Open http://localhost:5080. The named volume keeps tasks when the container is replaced.

## Azure setup (once)

The workflow builds a Linux Docker image, pushes it to Azure Container Registry (ACR),
and deploys it to an existing Linux Azure App Service. It uses GitHub OIDC to sign in
to Azure and the web app's managed identity to pull images. No registry passwords
or publish profiles are needed.

Install Azure CLI and sign in with `az login`. You need permission to create resources,
Entra applications, and role assignments. Choose globally unique registry and app names:

```sh
SUBSCRIPTION_ID="your-subscription-id"
RESOURCE_GROUP="task-app-rg"
LOCATION="eastus"
ACR_NAME="youruniquetaskregistry"
APP_NAME="your-unique-task-app"
GITHUB_REPO="your-github-user/your-repo"

az account set --subscription "$SUBSCRIPTION_ID"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
az acr create --resource-group "$RESOURCE_GROUP" --name "$ACR_NAME" --sku Basic
az appservice plan create --resource-group "$RESOURCE_GROUP" --name task-plan --is-linux --sku B1
az webapp create --resource-group "$RESOURCE_GROUP" --plan task-plan --name "$APP_NAME" \
	--container-image-name mcr.microsoft.com/dotnet/samples:aspnetapp
az webapp update --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" --https-only true
az webapp config appsettings set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--settings WEBSITES_PORT=8080 WEBSITES_ENABLE_APP_SERVICE_STORAGE=true DataDirectory=/home/data

ACR_ID=$(az acr show --name "$ACR_NAME" --query id -o tsv)
WEBAPP_PRINCIPAL_ID=$(az webapp identity assign --resource-group "$RESOURCE_GROUP" \
	--name "$APP_NAME" --query principalId -o tsv)
az role assignment create --assignee-object-id "$WEBAPP_PRINCIPAL_ID" \
	--assignee-principal-type ServicePrincipal --role AcrPull --scope "$ACR_ID"
az webapp config set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--generic-configurations '{"acrUseManagedIdentityCreds":true}'
```

Create the deployment identity and trust the GitHub `production` environment:

```sh
CLIENT_ID=$(az ad app create --display-name task-app-github --query appId -o tsv)
DEPLOY_PRINCIPAL_ID=$(az ad sp create --id "$CLIENT_ID" --query id -o tsv)
GROUP_ID=$(az group show --name "$RESOURCE_GROUP" --query id -o tsv)

az role assignment create --assignee-object-id "$DEPLOY_PRINCIPAL_ID" \
	--assignee-principal-type ServicePrincipal --role Contributor --scope "$GROUP_ID"
az role assignment create --assignee-object-id "$DEPLOY_PRINCIPAL_ID" \
	--assignee-principal-type ServicePrincipal --role AcrPush --scope "$ACR_ID"
az ad app federated-credential create --id "$CLIENT_ID" --parameters "{
	\"name\": \"github-production\",
	\"issuer\": \"https://token.actions.githubusercontent.com\",
	\"subject\": \"repo:$GITHUB_REPO:environment:production\",
	\"audiences\": [\"api://AzureADTokenExchange\"]
}"

echo "AZURE_CLIENT_ID=$CLIENT_ID"
az account show --query '{AZURE_TENANT_ID:tenantId, AZURE_SUBSCRIPTION_ID:id}'
```

Role assignments can take several minutes to propagate before the first deployment.
Azure resources incur charges; delete the resource group when it is no longer needed.

## GitHub Actions setup

In the GitHub repository, create an environment named **production** under
**Settings > Environments**. Add these environment secrets:

| Secret | Value |
| --- | --- |
| `AZURE_CLIENT_ID` | Deployment application's client ID printed above |
| `AZURE_TENANT_ID` | Azure tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

Add these environment variables:

| Variable | Value |
| --- | --- |
| `ACR_NAME` | Registry name, without `.azurecr.io` |
| `AZURE_RESOURCE_GROUP` | Resource group name |
| `AZURE_WEBAPP_NAME` | App Service name |

Push to `main`, or run **Build and deploy** manually from the Actions tab.
Change the branch in [.github/workflows/deploy.yml](.github/workflows/deploy.yml)
if your deployment branch is different. Configure environment branch restrictions
and approvals in GitHub as appropriate.

The app will be available at `https://<app-name>.azurewebsites.net`.
Each deployment uses the commit SHA as its image tag. `/health` is a health endpoint.

## Storage and access

Azure stores the SQLite database under `/home/data` with App Service storage enabled,
so tasks survive restarts and deployments. Use **one App Service instance** with this
simple SQLite design; do not scale out. Back up the data directory before deleting
the app. For multiple instances, use a managed database instead.

There is intentionally no sign-in: every visitor shares the same task list and can
change or delete tasks. Do not store sensitive data. Before exposing a private list,
enable App Service Authentication (Easy Auth) and require authentication, or restrict
access in Azure. Form submissions include Razor Pages' built-in CSRF protection.