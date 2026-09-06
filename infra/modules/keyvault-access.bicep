// Grants a managed identity the "Key Vault Secrets User" data-plane role on an existing vault.
// Kept separate from keyvault.bicep so the vault can be created before the web app exists and the
// assignment after it (the web app's app settings reference vault secret URIs, the assignment
// needs the web app's principal id).

@description('Name of the existing Key Vault in this resource group.')
param keyVaultName string

@description('Object id of the principal (system-assigned identity) that should read secrets.')
param principalId string

@description('Principal type for the role assignment.')
@allowed(['ServicePrincipal', 'User', 'Group'])
param principalType string = 'ServicePrincipal'

// Built-in role: Key Vault Secrets User
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: principalId
    principalType: principalType
  }
}

@description('Role assignment resource id.')
output roleAssignmentId string = secretsUser.id
