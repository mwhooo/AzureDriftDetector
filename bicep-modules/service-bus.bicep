// Type definitions
@export()
type ServiceBusSku = 'Basic' | 'Standard' | 'Premium'

@export()
type TlsVersion = '1.0' | '1.1' | '1.2'

@export()
type PublicNetworkAccess = 'Enabled' | 'Disabled'

@export()
type QueueConfig = {
  @description('Queue name')
  name: string
  
  @description('Maximum delivery count')
  maxDeliveryCount: int?
  
  @description('Lock duration (ISO 8601 duration format)')
  lockDuration: string?
  
  @description('Requires duplicate detection')
  requiresDuplicateDetection: bool?
  
  @description('Requires session')
  requiresSession: bool?
  
  @description('Dead lettering on message expiration')
  deadLetteringOnMessageExpiration: bool?
  
  @description('Auto delete on idle (ISO 8601 duration format)')
  autoDeleteOnIdle: string?
  
  @description('Default message time to live (ISO 8601 duration format)')
  defaultMessageTimeToLive: string?
  
  @description('Duplicate detection history time window (ISO 8601 duration format)')
  duplicateDetectionHistoryTimeWindow: string?
  
  @description('Maximum message size in kilobytes')
  maxMessageSizeInKilobytes: int?
}

@export()
type SubscriptionConfig = {
  @description('Subscription name')
  name: string
  
  @description('Maximum delivery count')
  maxDeliveryCount: int?
  
  @description('Lock duration (ISO 8601 duration format)')
  lockDuration: string?
}

@export()
type TopicConfig = {
  @description('Topic name')
  name: string
  
  @description('Topic subscriptions')
  subscriptions: SubscriptionConfig[]
}

@export()
type ServiceBusConfig = {
  @description('Service Bus namespace name')
  name: string
  
  @description('Location for the Service Bus namespace')
  location: string?
  
  @description('Service Bus SKU')
  skuName: ServiceBusSku?
  
  @description('Disable local authentication')
  disableLocalAuth: bool?
  
  @description('Public network access')
  publicNetworkAccess: PublicNetworkAccess?
  
  @description('Minimum TLS version')
  minimumTlsVersion: TlsVersion?
  
  @description('Queues configuration')
  queues: QueueConfig[]?
  
  @description('Topics configuration')
  topics: TopicConfig[]?
  
  @description('Tags for the Service Bus namespace')
  tags: object?
}

// Parameters
@description('Service Bus configuration')
param serviceBusConfig ServiceBusConfig

// Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusConfig.name
  location: serviceBusConfig.?location ?? resourceGroup().location
  tags: serviceBusConfig.?tags ?? {}
  sku: {
    name: serviceBusConfig.?skuName ?? 'Basic'
    tier: serviceBusConfig.?skuName ?? 'Basic'
  }
  properties: {
    disableLocalAuth: serviceBusConfig.?disableLocalAuth ?? false
    publicNetworkAccess: serviceBusConfig.?publicNetworkAccess ?? 'Enabled'
    minimumTlsVersion: serviceBusConfig.?minimumTlsVersion ?? '1.2'
    zoneRedundant: false
  }
}

// Queues
resource queues 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for queue in (serviceBusConfig.?queues ?? []): {
  parent: serviceBusNamespace
  name: queue.name
  properties: union(
    // Base properties supported by all tiers
    {
      maxDeliveryCount: queue.?maxDeliveryCount ?? 10
      lockDuration: queue.?lockDuration ?? 'PT1M'
      requiresDuplicateDetection: queue.?requiresDuplicateDetection ?? false
      requiresSession: queue.?requiresSession ?? false
      deadLetteringOnMessageExpiration: queue.?deadLetteringOnMessageExpiration ?? false
      maxSizeInMegabytes: 1024
      enableBatchedOperations: true
      enablePartitioning: false
    },
    // Advanced properties only for Standard and Premium tiers
    (serviceBusConfig.?skuName ?? 'Basic') != 'Basic' ? {
      autoDeleteOnIdle: queue.?autoDeleteOnIdle ?? 'P10675199DT2H48M5.4775807S'
      defaultMessageTimeToLive: queue.?defaultMessageTimeToLive ?? 'P14D'
      duplicateDetectionHistoryTimeWindow: queue.?duplicateDetectionHistoryTimeWindow ?? 'PT10M'
      maxMessageSizeInKilobytes: queue.?maxMessageSizeInKilobytes ?? 256
    } : {}
  )
}]

// Topics
resource topics 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = [for topic in (serviceBusConfig.?topics ?? []): {
  parent: serviceBusNamespace
  name: topic.name
  properties: {
    maxSizeInMegabytes: 1024
    enableBatchedOperations: true
    enablePartitioning: false
  }
}]

// Topic Subscriptions
resource subscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = [for (topic, topicIndex) in (serviceBusConfig.?topics ?? []): {
  parent: topics[topicIndex]
  name: topic.subscriptions[0].name
  properties: {
    maxDeliveryCount: topic.subscriptions[0].?maxDeliveryCount ?? 10
    lockDuration: topic.subscriptions[0].?lockDuration ?? 'PT1M'
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
  }
}]

// Additional subscriptions for topics with multiple subscriptions
resource additionalSubscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = [for (topic, topicIndex) in (serviceBusConfig.?topics ?? []): if (length(topic.subscriptions) > 1) {
  parent: topics[topicIndex]
  name: topic.subscriptions[1].name
  properties: {
    maxDeliveryCount: topic.subscriptions[1].?maxDeliveryCount ?? 10
    lockDuration: topic.subscriptions[1].?lockDuration ?? 'PT1M'
    deadLetteringOnMessageExpiration: true
    enableBatchedOperations: true
  }
}]

// Outputs
@description('Service Bus namespace resource ID')
output serviceBusNamespaceId string = serviceBusNamespace.id

@description('Service Bus namespace name')
output serviceBusNamespaceName string = serviceBusNamespace.name

@description('Service Bus endpoint')
output serviceBusEndpoint string = serviceBusNamespace.properties.serviceBusEndpoint

@description('Queue names')
output queueNames array = [for (queue, i) in (serviceBusConfig.?queues ?? []): queues[i].name]

@description('Topic names')
output topicNames array = [for (topic, i) in (serviceBusConfig.?topics ?? []): topics[i].name]
