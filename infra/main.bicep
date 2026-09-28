// Platform for the Claims Module demo deployment (brief §3.8, D-36, D-44).
//
// Everything except the API's Container App, which infra/api.bicep deploys once the image exists, the signing key is in
// Key Vault and the migrations have run. Resource-group scope; idempotent, so the deploy workflow runs it every time.
//
//   Container Apps environment (Consumption) + Log Analytics   hosts the API (scale to zero, D-36)
//   User-assigned managed identity                              the API's only credential: SQL, Blob Storage, Key Vault
//   Azure SQL logical server + serverless free-offer database   Entra-only authentication, no SQL password anywhere
//   Storage account + claim-documents container                 shared-key access off; SAS URLs are user-delegation SAS
//   Key Vault (RBAC)                                            the JWT signing key
//   Static Web App (Free)                                       the Angular SPA

targetScope = 'resourceGroup'

@description('Region for everything except the Static Web App.')
param location string = resourceGroup().location

@description('Static Web Apps (Free) exists in a few regions only: westus2, centralus, eastus2, westeurope, eastasia.')
@allowed([
  'westus2'
  'centralus'
  'eastus2'
  'westeurope'
  'eastasia'
])
param staticWebAppLocation string = 'westeurope'

@description('Short prefix for resource names.')
@maxLength(8)
param appName string = 'claims'

@description('Application (client) id of the deployment service principal. It becomes the SQL server\'s Entra admin, so the pipeline can run migrations and create the API\'s database user.')
param deployerClientId string

@description('Object id of the same service principal: it gets Key Vault Secrets Officer to create the signing key.')
param deployerObjectId string

@description('Display name recorded as the SQL Entra admin login.')
param deployerDisplayName string = 'github-claims-deploy'

var suffix = uniqueString(resourceGroup().id)
var names = {
  identity: 'id-${appName}-api'
  logAnalytics: 'log-${appName}-${suffix}'
  containerAppsEnvironment: 'cae-${appName}'
  containerApp: 'ca-${appName}-api'
  keyVault: 'kv-${appName}-${suffix}'
  storage: 'st${appName}${suffix}'
  sqlServer: 'sql-${appName}-${suffix}'
  database: 'claims'
  staticWebApp: 'swa-${appName}-${suffix}'
}

// Built-in role definition ids, checked with `az role definition list --name "<role name>" --query "[0].name"` (a wrong
// GUID compiles fine and fails only at deployment: RoleDefinitionDoesNotExist).
var roles = {
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageBlobDelegator: 'db58b8e5-c6ad-4a2a-8342-4190687cbf4a'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
}

// ---------------------------------------------------------------------------------------------------------------------
// Identity
// ---------------------------------------------------------------------------------------------------------------------

// User-assigned rather than system-assigned: it exists before the Container App, so its role assignments are in place
// when the app is created and its first revision can already resolve the Key Vault secret reference.
resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: names.identity
  location: location
}

// ---------------------------------------------------------------------------------------------------------------------
// Logs and the Container Apps environment
// ---------------------------------------------------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.logAnalytics
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // A cost ceiling for a demo: ingestion stops for the day after 0.5 GB.
    workspaceCapping: {
      dailyQuotaGb: json('0.5')
    }
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: names.containerAppsEnvironment
  location: location
  properties: {
    // Consumption only (no workload profiles): billed per use, and replicas can scale to zero (D-36).
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Key Vault
// ---------------------------------------------------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.keyVault
  location: location
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    // Azure RBAC instead of access policies: the same role-assignment model as Storage.
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

// The API reads secrets through the Container Apps Key Vault reference (D-38 item 9); it never lists or writes them.
resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, apiIdentity.id, roles.keyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// The pipeline creates the signing key on the first deployment (a random value no template or log ever holds).
resource keyVaultSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, deployerObjectId, roles.keyVaultSecretsOfficer)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsOfficer)
    principalId: deployerObjectId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Blob Storage (FRS §13, BR-D-01..03)
// ---------------------------------------------------------------------------------------------------------------------

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: names.storage
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // No account keys in use: the API authenticates with its managed identity and signs user-delegation SAS (D-42).
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    // A deleted document blob is recoverable for 7 days.
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'claim-documents'
  properties: {
    publicAccess: 'None'
  }
}

// Read and write blobs, in this container only.
resource blobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: documentsContainer
  name: guid(documentsContainer.id, apiIdentity.id, roles.storageBlobDataContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Get a user delegation key, which signs the 1-hour download SAS (BR-D-02). That is an account-level operation, so
// the container-scoped Contributor above does not cover it; the Delegator role grants that one action and no data access.
resource blobDelegator 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storage
  name: guid(storage.id, apiIdentity.id, roles.storageBlobDelegator)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDelegator)
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Azure SQL (application tables + Hangfire storage, D-36)
// ---------------------------------------------------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: names.sqlServer
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    // Entra-only: SQL logins are disabled, so there is no password to store, rotate or leak. The deployment service
    // principal is the admin; the API connects as its managed identity, a plain database user (infra/sql/grant-api-identity.sql).
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      principalType: 'Application'
      login: deployerDisplayName
      sid: deployerClientId
      tenantId: tenant().tenantId
    }
  }
}

// "Allow Azure services": the special 0.0.0.0 rule admits traffic from Azure datacenters, which is how a Container App
// without a VNet reaches the server. Authentication still requires an Entra token for a user of this database. The
// production answer is a VNet-integrated environment with a private endpoint (REVIEW-PREP).
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: names.database
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    // Free offer (100,000 vCore-seconds and 32 GB a month). When the allowance runs out the database keeps running and
    // bills the excess, so it can never go offline during the review (D-36).
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'BillOverUsage'
    minCapacity: json('0.5')
    // The shortest delay Azure allows. The database pauses 15 minutes after the API scales to zero.
    autoPauseDelay: 15
    maxSizeBytes: 34359738368
    zoneRedundant: false
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Static Web App (the Angular SPA)
// ---------------------------------------------------------------------------------------------------------------------

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: names.staticWebApp
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // Deployed by the workflow with the site's deployment token, not by a GitHub integration that commits a workflow file.
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
  }
}

// ---------------------------------------------------------------------------------------------------------------------
// Outputs: the inputs of infra/api.bicep, the migration step and the SPA build.
// ---------------------------------------------------------------------------------------------------------------------

output identityId string = apiIdentity.id
output identityName string = apiIdentity.name
output identityClientId string = apiIdentity.properties.clientId
output containerAppsEnvironmentId string = containerAppsEnvironment.id
output containerAppName string = names.containerApp
// Known before the app exists, so the SPA can be built in parallel with the API deployment.
output apiUrl string = 'https://${names.containerApp}.${containerAppsEnvironment.properties.defaultDomain}'
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output storageBlobEndpoint string = storage.properties.primaryEndpoints.blob
output documentsContainerName string = documentsContainer.name
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
