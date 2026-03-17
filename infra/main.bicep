@description('Base name for all resources')
param baseName string = 'd365-ops-copilot'

@description('Azure region for deployment')
param location string = 'swedencentral'

@description('Azure OpenAI model deployment name')
param gptDeploymentName string = 'd365-ops-gpt4o'

@description('Azure OpenAI embedding deployment name')
param embeddingDeploymentName string = 'd365-ops-embedding'

// Azure OpenAI Service
resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: 'oai-${baseName}'
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

resource gptDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: gptDeploymentName
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-11-20'
    }
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: openAi
  name: embeddingDeploymentName
  dependsOn: [gptDeployment]
  sku: {
    name: 'GlobalStandard'
    capacity: 10
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-large'
      version: '1'
    }
  }
}

// Azure AI Search
resource search 'Microsoft.Search/searchServices@2024-06-01-preview' = {
  name: 'srch-${baseName}'
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
  }
}

// Storage Account for Azure Functions
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: replace('st${baseName}', '-', '')
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

// App Service Plan (Consumption)
resource appPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'plan-${baseName}'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

// Azure Function App
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-${baseName}'
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appPlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value}' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'AZURE_OPENAI_ENDPOINT', value: openAi.properties.endpoint }
        { name: 'AZURE_OPENAI_DEPLOYMENT_NAME', value: gptDeploymentName }
        { name: 'AZURE_OPENAI_API_KEY', value: openAi.listKeys().key1 }
        { name: 'AZURE_SEARCH_ENDPOINT', value: 'https://${search.name}.search.windows.net' }
        { name: 'AZURE_SEARCH_API_KEY', value: search.listAdminKeys().primaryKey }
        { name: 'AZURE_SEARCH_INDEX_NAME', value: 'd365-ops-knowledge' }
      ]
    }
  }
}

// Outputs
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output openAiEndpoint string = openAi.properties.endpoint
output searchEndpoint string = 'https://${search.name}.search.windows.net'