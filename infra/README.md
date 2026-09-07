# SwitchPoint infrastructure (Bicep)

One resource-group-scoped template, `main.bicep`, provisions the whole production environment on
free or near-free tiers. `.github/workflows/deploy.yml` runs it on every push to `main`; the same
template can be deployed by hand with `main.bicepparam`.

```
main.bicep                    parameters, naming, module wiring, outputs
main.bicepparam               parameters for a manual deployment (secrets read from env vars)
modules/monitoring.bicep      Log Analytics workspace + workspace-based Application Insights
modules/sql.bicep             Azure SQL logical server + serverless database (free offer)
modules/keyvault.bicep        Key Vault (RBAC) + secrets + audit diagnostics
modules/webapp.bicep          Linux App Service plan + Web App for Containers + diagnostics
modules/keyvault-access.bicep Key Vault Secrets User role for the web app's managed identity
```

## What gets created

| Resource | Name | Notes |
|---|---|---|
| Log Analytics workspace | `log-<appName>` | PerGB2018, 30-day retention, 1 GB/day cap (inside the 5 GB/month free allowance) |
| Application Insights | `appi-<appName>` | workspace-based; connection string is an app setting |
| App Service plan | `plan-<appName>` | Linux; `F1` by default, `B1` or `P0v3` via `planSku` |
| Web App for Containers | `<appName>` | system-assigned identity, HTTPS only, TLS 1.2, FTPS off, health check `/healthz`, Always On when not F1, `DOCKER\|<imageRef>` from public GHCR, persistent `/home` storage on |
| SQL logical server | `sql-<appname>-<hash>` | TLS 1.2, public endpoint, `AllowAllWindowsAzureIps` firewall rule (0.0.0.0) so the web app can connect |
| SQL database | `switchpoint` | `GP_S_Gen5` capacity 1, min 0.5 vCore, auto-pause after 60 min, `useFreeLimit` with `AutoPause` when the free allowance is exhausted, 32 GB, locally redundant backups |
| Key Vault | `kv-<7 chars>-<hash>` | RBAC authorisation, soft delete (7 days), **no** purge protection so the environment can be deleted and recreated |
| Key Vault secrets | `SqlConnectionString`, `JwtSigningKey`, optional `Morningstar--ApiKey`, `Intelliflo--ClientSecret` | optional ones are only written when the parameter is non-empty |
| Role assignment | deterministic GUID | Key Vault Secrets User for the web app identity |
| Diagnostic settings | `to-log-analytics` | web app HTTP/console/app logs + metrics, Key Vault audit events, both to the workspace |

`<hash>` is `uniqueString(resourceGroup().id, appName)`, so names are stable across redeployments
and unique across subscriptions.

## Parameters

| Parameter | Default | Purpose |
|---|---|---|
| `appName` | required | web app name and base for every other name; becomes `<appName>.azurewebsites.net` |
| `location` | resource group location | region for everything |
| `planSku` | `F1` | `F1` (free), `B1`, `P0v3` |
| `imageRef` | `ghcr.io/callan-jackson/switchpoint-api:latest` | container image; the workflow passes the commit sha tag |
| `sqlAdminLogin` | `switchpointadmin` | SQL administrator login |
| `sqlAdminPassword` | required, secure | 8-128 chars, three of upper/lower/digit/symbol |
| `jwtSigningKey` | required, secure | HMAC key for API bearer tokens, at least 32 chars |
| `useFreeLimit` | `true` | opt the database into the Azure SQL free offer (one per subscription) |
| `authIssuer` | `switchpoint` | `Auth__Issuer` |
| `authAudience` | `switchpoint-api` | `Auth__Audience` |
| `seedDemo` | `true` | `Seed__Demo`: seed the demo firm, users and clients on startup |
| `migrateOnStartup` | `true` | `Database__MigrateOnStartup`; set `false` if the workflow applies a migrations bundle instead |
| `morningstarApiKey` | `''`, secure | written to Key Vault as `Morningstar--ApiKey` when non-empty |
| `intellifloClientSecret` | `''`, secure | written to Key Vault as `Intelliflo--ClientSecret` when non-empty |
| `tags` | project/environment/managedBy | applied to every resource |

App settings written to the web app (see `modules/webapp.bicep`): `WEBSITES_PORT=8080`,
`DOCKER_REGISTRY_SERVER_URL=https://ghcr.io`, `WEBSITES_ENABLE_APP_SERVICE_STORAGE=true`,
`ASPNETCORE_ENVIRONMENT=Production`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `KeyVault__Uri`,
`Database__Provider=SqlServer`, `Database__MigrateOnStartup`, `Seed__Demo`, `Auth__Issuer`,
`Auth__Audience`, `Reports__Path=/home/data/reports`, `Integrations__{Intelliflo,Xplan,TruePotential,OrigoHub,Morningstar}__Mode=Sandbox`,
and Key Vault references for `ConnectionStrings__SwitchPoint` and `Auth__SigningKey`.

## Outputs

`webAppName`, `webAppUrl`, `sqlServerFqdn`, `keyVaultName`, `appInsightsName`.

## Validate

```bash
az bicep build --file infra/main.bicep --stdout > /dev/null
az bicep lint  --file infra/main.bicep
# Preflight against the real resource group without creating anything:
az deployment group validate -g switchpoint-rg -f infra/main.bicep \
  -p appName=switchpoint-callan sqlAdminPassword='<pw>' jwtSigningKey='<32+ chars>'
```

## Deploy by hand

```bash
export AZURE_WEBAPP_NAME=switchpoint-callan
export SQL_ADMIN_PASSWORD='...'                    # 8-128 chars, three of upper/lower/digit/symbol
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
az deployment group create -g switchpoint-rg -f infra/main.bicep -p infra/main.bicepparam
```

Or pass parameters inline exactly as the workflow does:

```bash
az deployment group create -g switchpoint-rg -f infra/main.bicep \
  -p appName=switchpoint-callan \
     imageRef=ghcr.io/callan-jackson/switchpoint-api:latest \
     sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
     jwtSigningKey="$JWT_SIGNING_KEY"
```

## Tear down

```bash
az group delete -n switchpoint-rg --yes
# Soft-deleted vault names stay reserved for 7 days unless purged:
az keyvault purge --name <keyVaultName> --location uksouth
```

## Notes

- **Free SQL offer**: only one free database per subscription. If the deployment fails saying the
  free limit is unavailable, deploy with `useFreeLimit=false` (or set the GitHub variable
  `AZURE_SQL_USE_FREE_LIMIT=false`); the serverless database then costs roughly £4/month at
  demo-level usage because it auto-pauses after an hour idle.
- **F1 plan**: no Always On, 60 CPU-minutes/day, cold start of a minute or two after idle. The
  container start limit is raised to 600 s to cover migrations, seeding and resuming a paused
  database on the first request.
- **Registry**: the image must be public on GHCR (package settings -> Change visibility) because
  the web app pulls anonymously. Private images need `DOCKER_REGISTRY_SERVER_USERNAME/PASSWORD`
  app settings with a GitHub PAT.
- **Purge protection** is deliberately off (ARM cannot set it back to false once on).
