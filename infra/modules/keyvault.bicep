// Key Vault (RBAC model) holding every secret the API needs at runtime.
//
// Secret names use "--" where the configuration key has ":" because Key Vault names only allow
// letters, digits and hyphens; Azure.Extensions.AspNetCore.Configuration.Secrets maps
// "Morningstar--ApiKey" to "Morningstar:ApiKey" automatically.

@description('Globally unique Key Vault name (3-24 characters, letters, digits and hyphens).')
@minLength(3)
@maxLength(24)
param keyVaultName string

@description('Azure region for the vault.')
param location string

@description('Full ADO.NET connection string for the SwitchPoint database.')
@secure()
param sqlConnectionString string

@description('HMAC signing key for JWT bearer tokens issued by the API.')
@secure()
param jwtSigningKey string

@description('Morningstar Direct Web Services API key. Leave empty to skip writing the secret.')
@secure()
param morningstarApiKey string = ''

@description('Intelliflo Office OAuth client secret. Leave empty to skip writing the secret.')
@secure()
param intellifloClientSecret string = ''

@description('Log Analytics workspace resource id. When set, vault audit events are streamed there via a diagnostic setting.')
param logAnalyticsWorkspaceId string = ''

@description('Tags applied to the vault.')
param tags object = {}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // enablePurgeProtection is deliberately omitted: the property can only ever be set to true
    // (ARM rejects an explicit false) and leaving purge protection off lets a demo environment be
    // torn down and recreated under the same name after `az keyvault purge`.
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  tags: tags
  properties: {
    value: sqlConnectionString
    contentType: 'text/plain'
  }
}

resource jwtSigningKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'JwtSigningKey'
  tags: tags
  properties: {
    value: jwtSigningKey
    contentType: 'text/plain'
  }
}

resource morningstarApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(morningstarApiKey)) {
  parent: keyVault
  name: 'Morningstar--ApiKey'
  tags: tags
  properties: {
    value: morningstarApiKey
    contentType: 'text/plain'
  }
}

resource intellifloClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(intellifloClientSecret)) {
  parent: keyVault
  name: 'Intelliflo--ClientSecret'
  tags: tags
  properties: {
    value: intellifloClientSecret
    contentType: 'text/plain'
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'to-log-analytics'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      { categoryGroup: 'audit', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

@description('Key Vault resource name.')
output keyVaultName string = keyVault.name

@description('Key Vault resource id.')
output keyVaultId string = keyVault.id

@description('Vault URI, e.g. https://<name>.vault.azure.net/ (trailing slash included).')
output vaultUri string = keyVault.properties.vaultUri

@description('Versionless URI of the SqlConnectionString secret, for App Service Key Vault references.')
output sqlConnectionStringSecretUri string = sqlConnectionStringSecret.properties.secretUri

@description('Versionless URI of the JwtSigningKey secret, for App Service Key Vault references.')
output jwtSigningKeySecretUri string = jwtSigningKeySecret.properties.secretUri
