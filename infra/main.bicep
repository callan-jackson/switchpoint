// SwitchPoint - production-shaped, free-tier Azure footprint.
//
//   Log Analytics + Application Insights   (monitoring.bicep)
//   Azure SQL serverless, free offer        (sql.bicep)
//   Key Vault (RBAC) + secrets              (keyvault.bicep)
//   Linux App Service plan + Web App        (webapp.bicep)
//   Key Vault Secrets User for the web app  (keyvault-access.bicep)
//
// Every runtime setting the API reads (Database__*, Seed__*, Auth__*, Reports__Path, Integrations__*__Mode)
// is written as an App Service app setting in modules/webapp.bicep; secrets are Key Vault references.
//
// Deploy into one resource group (default switchpoint-rg, uksouth):
//   az deployment group create -g switchpoint-rg -f infra/main.bicep -p infra/main.bicepparam
// The GitHub deploy workflow passes the same parameters on the command line instead.

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------------------------

@description('Web app name and base for every other resource name. Becomes <appName>.azurewebsites.net, so it must be globally unique.')
@minLength(3)
@maxLength(40)
param appName string

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('App Service plan SKU. F1 (free) by default; B1 or P0v3 to scale up.')
@allowed(['F1', 'B1', 'P0v3'])
param planSku string = 'F1'

@description('Container image reference the web app pulls. The deploy workflow passes an immutable sha tag.')
param imageRef string = 'ghcr.io/callan-jackson/switchpoint-api:latest'

@description('SQL administrator login name.')
@minLength(1)
param sqlAdminLogin string = 'switchpointadmin'

@description('SQL administrator password (8-128 characters, three of: upper, lower, digit, symbol).')
@secure()
@minLength(8)
param sqlAdminPassword string

@description('HMAC signing key for JWT bearer tokens (at least 32 characters).')
@secure()
@minLength(32)
param jwtSigningKey string

@description('Opt the database into the Azure SQL free offer (one per subscription).')
param useFreeLimit bool = true

@description('''Provision Azure SQL. Set false to run on SQLite in App Service persistent storage, which is free and
needs no database resource. Some subscriptions (including free trials in certain regions such as UK South) are
restricted from provisioning Microsoft.Sql; deploy to another region or set this to false.''')
param useSqlServer bool = true

@description('JWT issuer written to Auth__Issuer. The API validates tokens against this exact value.')
param authIssuer string = 'switchpoint'

@description('JWT audience written to Auth__Audience.')
param authAudience string = 'switchpoint-api'

@description('Seed demo users and sample data on startup.')
param seedDemo bool = true

@description('Apply EF Core migrations on startup. Set false when the deploy workflow runs the migrations bundle instead.')
param migrateOnStartup bool = true

@description('Morningstar API key. Empty (default) skips writing the Key Vault secret.')
@secure()
param morningstarApiKey string = ''

@description('Intelliflo client secret. Empty (default) skips writing the Key Vault secret.')
@secure()
param intellifloClientSecret string = ''

@description('Tags applied to every resource.')
param tags object = {
  project: 'switchpoint'
  environment: 'production'
  managedBy: 'bicep'
}

// ---------------------------------------------------------------------------------------------
// Naming
// ---------------------------------------------------------------------------------------------

var suffix = uniqueString(resourceGroup().id, appName)
var compactName = toLower(replace(appName, '-', ''))

var workspaceName = 'log-${appName}'
var appInsightsName = 'appi-${appName}'
var planName = 'plan-${appName}'
var sqlServerName = 'sql-${toLower(appName)}-${suffix}'
var databaseName = 'switchpoint'
// Key Vault names are capped at 24 characters: "kv-" + 7 + "-" + 13-character uniqueString.
var keyVaultName = 'kv-${take(compactName, 7)}-${suffix}'

// ---------------------------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------------------------

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    workspaceName: workspaceName
    appInsightsName: appInsightsName
    location: location
    tags: tags
  }
}

module sql 'modules/sql.bicep' = if (useSqlServer) {
  name: 'sql'
  params: {
    serverName: sqlServerName
    databaseName: databaseName
    location: location
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    useFreeLimit: useFreeLimit
    tags: tags
  }
}

// Built here (not in sql.bicep) so no module ever outputs a secret.
var sqlConnectionString = useSqlServer
  ? 'Server=tcp:${sql!.outputs.serverFqdn},1433;Initial Catalog=${sql!.outputs.databaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'
  : 'Data Source=/home/data/switchpoint.db'

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    keyVaultName: keyVaultName
    location: location
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    sqlConnectionString: sqlConnectionString
    jwtSigningKey: jwtSigningKey
    morningstarApiKey: morningstarApiKey
    intellifloClientSecret: intellifloClientSecret
    tags: tags
  }
}

module webApp 'modules/webapp.bicep' = {
  name: 'webapp'
  params: {
    appName: appName
    planName: planName
    location: location
    planSku: planSku
    imageRef: imageRef
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    keyVaultUri: keyVault.outputs.vaultUri
    sqlConnectionStringSecretUri: keyVault.outputs.sqlConnectionStringSecretUri
    jwtSigningKeySecretUri: keyVault.outputs.jwtSigningKeySecretUri
    authIssuer: authIssuer
    authAudience: authAudience
    seedDemo: seedDemo
    migrateOnStartup: migrateOnStartup
    useSqlServer: useSqlServer
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    tags: tags
  }
}

module keyVaultAccess 'modules/keyvault-access.bicep' = {
  name: 'keyvault-access'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    principalId: webApp.outputs.principalId
  }
}

// ---------------------------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------------------------

@description('Web app resource name (use with az webapp ...).')
output webAppName string = webApp.outputs.webAppName

@description('Public HTTPS URL of the API and SPA.')
output webAppUrl string = webApp.outputs.webAppUrl

@description('SQL logical server FQDN.')
output sqlServerFqdn string = useSqlServer ? sql!.outputs.serverFqdn : 'sqlite (/home/data/switchpoint.db)'

@description('Key Vault name.')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('Application Insights component name.')
output appInsightsName string = monitoring.outputs.appInsightsName
