// The API's Container App (D-36, D-44). Deployed after infra/main.bicep, after the signing key exists in Key Vault
// and after the migrations have run, so the new revision starts against the schema it expects.

targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Outputs of infra/main.bicep.')
param containerAppName string
param containerAppsEnvironmentId string
param identityId string
param identityClientId string
param keyVaultUri string
param sqlServerFqdn string
param databaseName string
param storageBlobEndpoint string
param documentsContainerName string

@description('The only origin CORS admits: the Static Web App.')
param allowedOrigin string

@description('Image to run, e.g. ghcr.io/owner/claims-api:<commit sha>.')
param image string

@description('0 = scale to zero when idle (D-36). Set 1 before the live review so there is no cold start.')
@minValue(0)
@maxValue(1)
param minReplicas int = 0

// No password: Microsoft.Data.SqlClient gets an Entra token for the managed identity named by User Id (its client id).
// Hangfire uses the same connection string, and Hangfire.SqlServer picks Microsoft.Data.SqlClient when it is present.
var connectionString = 'Server=tcp:${sqlServerFqdn},1433;Database=${databaseName};Authentication=Active Directory Managed Identity;User Id=${identityClientId};Encrypt=True;TrustServerCertificate=False;Connect Timeout=60'

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      // A Key Vault reference: Container Apps resolves it with the managed identity; the value never enters the template.
      secrets: [
        {
          name: 'auth-signing-key'
          keyVaultUrl: '${keyVaultUri}secrets/auth-signing-key'
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            // Container Apps terminates TLS; trust its X-Forwarded-Proto/-For so the API sees https (Location headers, Secure cookie).
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
            // DefaultAzureCredential (Blob Storage) picks this user-assigned identity.
            { name: 'AZURE_CLIENT_ID', value: identityClientId }
            { name: 'ConnectionStrings__ClaimsDb', value: connectionString }
            { name: 'Auth__SigningKey', secretRef: 'auth-signing-key' }
            // D-16: the role switcher's dev-token endpoint stays on in the demo deployment.
            { name: 'Auth__DevTokensEnabled', value: 'true' }
            { name: 'Cors__AllowedOrigins__0', value: allowedOrigin }
            { name: 'Storage__Provider', value: 'AzureBlob' }
            { name: 'Storage__AzureBlob__ServiceUri', value: storageBlobEndpoint }
            { name: 'Storage__AzureBlob__ContainerName', value: documentsContainerName }
          ]
          probes: [
            // D-38 item 11: liveness has no dependencies, so a resuming database never restarts the container.
            {
              type: 'Startup'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 30
              failureThreshold: 3
            }
            // Readiness includes the database; a serverless database resuming from pause takes up to about a minute.
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 12
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        // One replica, so one Hangfire server (D-36).
        maxReplicas: 1
        rules: [
          {
            name: 'http'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output latestRevisionName string = api.properties.latestRevisionName
