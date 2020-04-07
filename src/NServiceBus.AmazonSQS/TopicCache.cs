namespace NServiceBus.AmazonSQS
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Unicast.Messages;

    class TopicCache
    {
        public TopicCache(IAmazonSimpleNotificationService snsClient, MessageMetadataRegistry messageMetadataRegistry, TransportConfiguration configuration)
        {
            this.configuration = configuration;
            this.messageMetadataRegistry = messageMetadataRegistry;
            this.snsClient = snsClient;
            CustomEventToTopicsMappings = configuration.CustomEventToTopicsMappings ?? new EventToTopicsMappings();
            CustomEventToEventsMappings = configuration.CustomEventToEventsMappings ?? new EventToEventsMappings();
        }

        public EventToEventsMappings CustomEventToEventsMappings { get; }

        public EventToTopicsMappings CustomEventToTopicsMappings { get; }

        public Task<Topic> GetTopic(MessageMetadata metadata)
        {
            return GetAndCacheTopicIfFound(metadata);
        }

        public Task<Topic> GetTopic(Type eventType)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(eventType);
            return GetAndCacheTopicIfFound(metadata);
        }

        public Task<Topic> GetTopic(string messageTypeIdentifier)
        {
            var metadata = messageMetadataRegistry.GetMessageMetadata(messageTypeIdentifier);
            return GetAndCacheTopicIfFound(metadata);
        }

        public async Task<Topic> CreateIfNotExistent(MessageMetadata metadata, Action<string, Topic> creationAction = null)
        {
            var existingTopic = await GetTopic(metadata).ConfigureAwait(false);
            if (existingTopic != null)
            {
                return existingTopic;
            }

            var topicName = GetTopicName(metadata);
            var response = await snsClient.CreateTopicAsync(topicName).ConfigureAwait(false);
            // avoid querying again
            var topic = new Topic { TopicArn = response.TopicArn };
            creationAction?.Invoke(topicName, topic);
            return topicCache.GetOrAdd(metadata.MessageType, topic);
        }

        public string GetTopicName(MessageMetadata metadata)
        {
            if (topicNameCache.TryGetValue(metadata.MessageType, out var topicName))
            {
                return topicName;
            }

            return topicNameCache.GetOrAdd(metadata.MessageType, configuration.TopicNameGenerator(metadata));
        }

        async Task<Topic> GetAndCacheTopicIfFound(MessageMetadata metadata)
        {
            if (topicCache.TryGetValue(metadata.MessageType, out var topic))
            {
                return topic;
            }

            var topicName = GetTopicName(metadata);
            var foundTopic = await snsClient.FindTopicAsync(topicName).ConfigureAwait(false);
            return foundTopic != null ? topicCache.GetOrAdd(metadata.MessageType, foundTopic) : null;
        }

        IAmazonSimpleNotificationService snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        TransportConfiguration configuration;
        ConcurrentDictionary<Type, Topic> topicCache = new ConcurrentDictionary<Type, Topic>();
        ConcurrentDictionary<Type, string> topicNameCache = new ConcurrentDictionary<Type, string>();
    }
}