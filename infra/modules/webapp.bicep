// Linux App Service plan + Web App for Containers pulling the public API image from GHCR,
// with diagnostic settings streamed to Log Analytics.

@description('Web app name; also the azurewebsites.net host label, so it must be globally unique.')
@minLength(2)
@maxLength(60)
param appName string

@description('App Service plan name.')
param planName string

@description('Azure region for the plan and app.')
param location string

@description('App Service plan SKU. F1 is free (no Always On, 60 CPU-minutes/day); B1 is the cheapest paid tier; P0v3 is the smallest Premium v3 tier.')
@allowed(['F1', 'B1', 'P0v3'])
param planSku string = 'F1'

@description('Container image reference, e.g. ghcr.io/callan-jackson/switchpoint-api:latest.')
param imageRef string

@description('Container registry URL passed to App Service (public GHCR needs no credentials).')
param registryUrl string = 'https://ghcr.io'

@description('Port the container listens on.')
param containerPort int = 8080

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Key Vault URI (https://<name>.vault.azure.net/).')
param keyVaultUri string

@description('Key Vault secret URI for the SwitchPoint connection string.')
param sqlConnectionStringSecretUri string

@description('Key Vault secret URI for the JWT signing key.')
param jwtSigningKeySecretUri string

@description('JWT issuer written to Auth__Issuer.')
param authIssuer string

@description('JWT audience written to Auth__Audience.')
param authAudience string

@description('Whether the API seeds demo users and sample data on startup (Seed__Demo).')
param seedDemo bool = true

@description('Whether the API applies EF Core migrations on startup (Database__MigrateOnStartup).')
param migrateOnStartup bool = true

@description('Log Analytics workspace resource id for diagnostic settings.')
param logAnalyticsWorkspaceId string

@description('Tags applied to the plan and app.')
param tags object = {}

var skuTiers = {
  F1: 'Free'
  B1: 'Basic'
  P0v3: 'PremiumV3'
}

// Always On is not available on the Free tier; App Service rejects the property when set on F1.
var isFreeTier = planSku == 'F1'

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: planSku
    tier: skuTiers[planSku]
    capacity: 1
  }
  properties: {
    reserved: true
    zoneRedundant: false
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    reserved: true
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${imageRef}'
      alwaysOn: !isFreeTier
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/healthz'
      acrUseManagedIdentityCreds: false
      appSettings: [
        // --- container plumbing ---
        { name: 'WEBSITES_PORT', value: string(containerPort) }
        { name: 'DOCKER_REGISTRY_SERVER_URL', value: registryUrl }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'false' }
        { name: 'DOCKER_ENABLE_CI', value: 'false' }
        // --- ASP.NET Core ---
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        // --- SwitchPoint configuration (double underscore == ":" section separator) ---
        { name: 'KeyVault__Uri', value: keyVaultUri }
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'Database__MigrateOnStartup', value: string(migrateOnStartup) }
        { name: 'Auth__Issuer', value: authIssuer }
        { name: 'Auth__Audience', value: authAudience }
        { name: 'Seed__Demo', value: string(seedDemo) }
        // --- secrets resolved by App Service from Key Vault via the managed identity ---
        { name: 'ConnectionStrings__SwitchPoint', value: '@Microsoft.KeyVault(SecretUri=${sqlConnectionStringSecretUri})' }
        { name: 'Auth__SigningKey', value: '@Microsoft.KeyVault(SecretUri=${jwtSigningKeySecretUri})' }
      ]
    }
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: webApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

@description('Web app resource name.')
output webAppName string = webApp.name

@description('Default HTTPS URL of the web app.')
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'

@description('Object id of the web app system-assigned identity.')
output principalId string = webApp.identity.principalId

@description('App Service plan resource id.')
output planId string = plan.id
