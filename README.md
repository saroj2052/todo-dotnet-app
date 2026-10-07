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

The workflow publishes the .NET 10 app and deploys the published files directly to
Linux Azure App Service using an App Service publish profile stored in a GitHub
secret. No ACR, Docker image, registry credentials, or federated OIDC setup is needed.

Install Azure CLI and sign in with `az login` for the one-time resource setup.
You need permission to create and configure App Service resources. The workflow
targets `tododevday-app`; if you choose a different globally unique app name,
update `app-name` in [.github/workflows/deploy.yml](.github/workflows/deploy.yml).

```sh
SUBSCRIPTION_ID="your-subscription-id"
RESOURCE_GROUP="RG-NCP-Dev-Days"
LOCATION="eastus"
APP_NAME="tododevday-app"

az account set --subscription "$SUBSCRIPTION_ID"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
az appservice plan create --resource-group "$RESOURCE_GROUP" --name task-plan --is-linux --sku B1
az webapp create --resource-group "$RESOURCE_GROUP" --plan task-plan --name "$APP_NAME" \
	--runtime "DOTNETCORE:10.0"
az webapp update --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" --https-only true
az webapp config set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--startup-file "dotnet TaskApp.dll"
az webapp config appsettings set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--settings DataDirectory=/home/data
```

If the app already exists as a Linux container app, skip resource creation and
switch it to the built-in .NET runtime once. Keep the same resource group and app
name variables, and back up `/home/data` before changing hosting settings:

```sh
az webapp config set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--linux-fx-version "DOTNETCORE|10.0" --startup-file "dotnet TaskApp.dll"
az webapp config appsettings set --resource-group "$RESOURCE_GROUP" --name "$APP_NAME" \
	--settings DataDirectory=/home/data
```

The app will start serving requests after the first successful code deployment.

### Download the publish profile

1. In Azure Portal, open `tododevday-app` and go to **Settings > Configuration > General settings**.
2. Enable **SCM Basic Auth Publishing Credentials** and save. FTP basic authentication is not required.
3. Return to **Overview** and select **Download publish profile**.

If download reports "Basic authentication is disabled", check the SCM setting.
If an organizational Azure policy prevents enabling it, contact your administrator;
publish-profile deployment requires SCM basic authentication.

The downloaded XML contains deployment credentials. Do not commit it, share it,
or include it in logs. Reset the publish profile in Azure and replace the GitHub
secret if the credentials are exposed.

Azure resources incur charges. Delete only resources dedicated to this app when
they are no longer needed; do not delete a shared resource group.

## GitHub Actions setup

In the GitHub repository, create an environment named **dev** under
**Settings > Environments**. Add this environment secret:

| Secret | Value |
| --- | --- |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Entire XML content of the publish profile downloaded for `tododevday-app` |

No Azure identity secrets or GitHub environment variables are required by this
workflow. The App Service name is set directly in the deployment step.

Push to `dev`, or run **Build and deploy** manually from the Actions tab.
Change the branch in [.github/workflows/deploy.yml](.github/workflows/deploy.yml)
if your deployment branch is different. Configure environment branch restrictions
and approvals in GitHub as appropriate.

The workflow runs on a GitHub-hosted Ubuntu runner and:

1. Checks out the repository using `actions/checkout@v4`.
2. Installs the .NET 10 SDK using `actions/setup-dotnet@v4`.
3. Runs `dotnet publish` in Release mode, writing deployable files to the runner's temporary `task-app` directory.
4. Uploads those files to `tododevday-app` using `azure/webapps-deploy@v3` and the publish-profile secret.

App Service restarts the app using the configured `dotnet TaskApp.dll` startup
command. Deployments are serialized by the workflow's concurrency group; an
active deployment is not cancelled by a new run.

The app is available at https://tododevday-app.azurewebsites.net.
Use https://tododevday-app.azurewebsites.net/health to check its health.

## Storage and access

Azure stores the SQLite database under `/home/data` on persistent App Service storage,
so tasks survive restarts and deployments. Use **one App Service instance** with this
simple SQLite design; do not scale out. Back up the data directory before deleting
the app. For multiple instances, use a managed database instead.

There is intentionally no sign-in: every visitor shares the same task list and can
change or delete tasks. Do not store sensitive data. Before exposing a private list,
enable App Service Authentication (Easy Auth) and require authentication, or restrict
access in Azure. Form submissions include Razor Pages' built-in CSRF protection.