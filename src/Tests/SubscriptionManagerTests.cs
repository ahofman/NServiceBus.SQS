namespace NServiceBus.AmazonSQS.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.Runtime.SharedInterfaces;
    using Amazon.SimpleNotificationService.Model;
    using NUnit.Framework;
    using Settings;
    using Unicast.Messages;

    [TestFixture]
    public class SubscriptionManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            sqsClient = new MockSqsClient();
            snsClient = new MockSnsClient();
            settings = new SettingsHolder();
            messageMetadataRegistry = settings.SetupMessageMetadataRegistry();
            queueName = "fakeQueue";
            
            manager = new SubscriptionManager(sqsClient, snsClient, queueName, new QueueUrlCache(sqsClient), new TransportConfiguration(settings), messageMetadataRegistry);
        }

        [Test]
        public async Task Subscribe_object_should_ignore()
        {
            var eventType = typeof(object);
            
            await manager.Subscribe(eventType, null);
            
            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }
        
        [Test]
        public async Task Subscribe_again_should_ignore_because_cached()
        {
            // cache
            var eventType = typeof(Event);
            messageMetadataRegistry.GetMessageMetadata(eventType);
            
            await manager.Subscribe(eventType, null);

            var initialSubscribeRequests = new List<(string topicArn, ICoreAmazonSQS sqsClient, string sqsQueueUrl)>(snsClient.SubscribeQueueRequests);
            snsClient.SubscribeQueueRequests.Clear();

            await manager.Subscribe(eventType, null);
            
            Assert.IsNotEmpty(initialSubscribeRequests);
            Assert.IsEmpty(snsClient.SubscribeQueueRequests);
        }

        [Test]
        public async Task Subscribe_creates_topic_if_not_exists()
        {
            // cache
            var eventType = typeof(Event);
            messageMetadataRegistry.GetMessageMetadata(eventType);

            var responses = new Queue<Func<string, Topic>>();
            responses.Enqueue(t => null);
            responses.Enqueue(t => new Topic { TopicArn = t });
            snsClient.FindTopicAsyncResponse = topic => responses.Dequeue()(topic);
            
            await manager.Subscribe(eventType, null);
            
            CollectionAssert.AreEquivalent(new List<string> { "NServiceBus_AmazonSQS_Tests_SubscriptionManagerTests-Event" }, snsClient.CreateTopicRequests);
        }

        interface IEvent { }
        
        interface IMyEvent : IEvent { }
        class Event : IMyEvent { }

        MockSqsClient sqsClient;
        SubscriptionManager manager;
        MockSnsClient snsClient;
        MessageMetadataRegistry messageMetadataRegistry;
        SettingsHolder settings;
        string queueName;
    }
}