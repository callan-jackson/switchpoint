// Log Analytics workspace + workspace-based Application Insights.
// Both sit inside the Azure Monitor free allowance (5 GB/month ingestion, 31 days retention)
// as long as the daily cap below is left in place.

@description('Log Analytics workspace name.')
param workspaceName string

@description('Application Insights component name.')
param appInsightsName string

@description('Azure region for both resources.')
param location string

@description('Retention in days for the workspace (30 is within the free allowance).')
@minValue(30)
@maxValue(730)
param retentionInDays int = 30

@description('Daily ingestion cap in GB. Guards against a runaway log loop costing money; -1 disables the cap.')
param dailyQuotaGb int = 1

@description('Tags applied to both resources.')
param tags object = {}

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    workspaceCapping: {
      dailyQuotaGb: dailyQuotaGb
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: workspace.id
    RetentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Resource id of the Log Analytics workspace (target for diagnostic settings).')
output workspaceId string = workspace.id

@description('Application Insights component name.')
output appInsightsName string = appInsights.name

@description('Application Insights connection string, consumed by the Azure Monitor OpenTelemetry distro.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString
