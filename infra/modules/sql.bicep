// Azure SQL logical server + a serverless General Purpose database that opts into the
// "Azure SQL Database free offer" (100,000 vCore-seconds and 32 GB per month, one database per
// subscription). When the free allowance is exhausted the database auto-pauses rather than
// billing, which is the safe default for a demo deployment.

@description('Globally unique SQL logical server name (lowercase letters, digits and hyphens).')
@minLength(1)
@maxLength(63)
param serverName string

@description('Database name.')
param databaseName string = 'switchpoint'

@description('Azure region for the server and database.')
param location string

@description('SQL administrator login name. Not used by the application at runtime except through the connection string secret.')
param administratorLogin string

@description('SQL administrator password.')
@secure()
param administratorLoginPassword string

@description('Opt the database into the Azure SQL free offer. Only one free database is allowed per subscription; set to false if the offer is already consumed.')
param useFreeLimit bool = true

@description('Minutes of inactivity before a serverless database pauses (min 60). -1 disables auto-pause.')
param autoPauseDelayMinutes int = 60

@description('Tags applied to the server and database.')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

// 0.0.0.0 - 0.0.0.0 is the documented sentinel for "allow Azure services and resources to access
// this server" (needed because the F1 web app has no fixed outbound address).
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    // 32 GB is the ceiling of the free offer.
    maxSizeBytes: 34359738368
    autoPauseDelay: autoPauseDelayMinutes
    minCapacity: json('0.5')
    useFreeLimit: useFreeLimit
    freeLimitExhaustionBehavior: useFreeLimit ? 'AutoPause' : null
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
  }
}

@description('Fully qualified domain name of the SQL logical server.')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('SQL logical server resource name.')
output serverName string = sqlServer.name

@description('Database name.')
output databaseName string = database.name
