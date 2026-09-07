// Parameters for a manual deployment. Secrets are read from the environment so nothing sensitive
// lives in the repo:
//
//   export SQL_ADMIN_PASSWORD='...'   # 8-128 chars, three of upper/lower/digit/symbol
//   export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
//   export AZURE_WEBAPP_NAME='switchpoint-api-<something-unique>'
//   az deployment group create -g switchpoint-rg -f infra/main.bicep -p infra/main.bicepparam
//
// The GitHub deploy workflow does not use this file; it passes parameters on the command line.

using './main.bicep'

param appName = readEnvironmentVariable('AZURE_WEBAPP_NAME', 'switchpoint-api')
param planSku = 'F1'
param imageRef = 'ghcr.io/callan-jackson/switchpoint-api:latest'
param sqlAdminLogin = readEnvironmentVariable('SQL_ADMIN_LOGIN', 'switchpointadmin')
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param jwtSigningKey = readEnvironmentVariable('JWT_SIGNING_KEY', '')
param useSqlServer = true
param useFreeLimit = true
param seedDemo = true
param migrateOnStartup = true
param morningstarApiKey = readEnvironmentVariable('MORNINGSTAR_API_KEY', '')
param intellifloClientSecret = readEnvironmentVariable('INTELLIFLO_CLIENT_SECRET', '')
