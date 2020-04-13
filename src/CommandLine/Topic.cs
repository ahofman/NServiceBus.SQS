﻿namespace NServiceBus.Transport.SQS.CommandLine
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Amazon.SimpleNotificationService;
    using Amazon.SimpleNotificationService.Model;
    using Amazon.SQS;
    using McMaster.Extensions.CommandLineUtils;

    static class Topic
    {
        public static async Task<string> Get(IAmazonSimpleNotificationService sns, CommandArgument eventType)
        {
            var topicName = GetSanitizedTopicName(eventType.Value);
            var findTopicResponse = await sns.FindTopicAsync(topicName).ConfigureAwait(false);
            return findTopicResponse.TopicArn;
        }

        public static async Task<string> Create(IAmazonSimpleNotificationService sns, CommandArgument eventType)
        {
            var topicName = GetSanitizedTopicName(eventType.Value);
            await Console.Out.WriteLineAsync($"Creating SNS Topic with name '{topicName}'.");
            var createTopicResponse = await sns.CreateTopicAsync(topicName).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Created SNS Topic with name '{topicName}'.");
            return createTopicResponse.TopicArn;
        }

        public static async Task<string> Subscribe(IAmazonSQS sqs, IAmazonSimpleNotificationService sns, string topicArn, string queueUrl)
        {
            await Console.Out.WriteLineAsync($"Subscribing queue with url '{queueUrl}' to topic with ARN '{topicArn}'.");
            var createdSubscription = await sns.SubscribeQueueAsync(topicArn, sqs, queueUrl).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Queue with url '{queueUrl}' subscribed to topic with ARN '{topicArn}'.");
            await Console.Out.WriteLineAsync($"Setting raw delivery for subscription with arn '{createdSubscription}'.");
            await sns.SetSubscriptionAttributesAsync(new SetSubscriptionAttributesRequest
            {
                SubscriptionArn = createdSubscription,
                AttributeName = "RawMessageDelivery",
                AttributeValue = "true"
            }).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Raw delivery for subscription with arn '{createdSubscription}' set.");
            return createdSubscription;
        }

        public static async Task Unsubscribe(IAmazonSimpleNotificationService sns, string topicArn, string queueArn)
        {
            await Console.Out.WriteLineAsync($"Unsubscribing queue with ARN '{queueArn}' from topic with ARN '{topicArn}'.");

            var subscriptionArn = await sns.FindMatchingSubscription(topicArn, queueArn);

            var unsubscribeResponse = await sns.UnsubscribeAsync(subscriptionArn).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"Queue with ARN '{queueArn}' unsubscribed from topic with ARN '{topicArn}'.");       
        }

        public static async Task<string> FindMatchingSubscription(this IAmazonSimpleNotificationService sns, string topicArn, string queueArn)
        {
            ListSubscriptionsByTopicResponse upToAHundredSubscriptions = null;

            do
            {
                upToAHundredSubscriptions = await sns.ListSubscriptionsByTopicAsync(topicArn, upToAHundredSubscriptions?.NextToken)
                    .ConfigureAwait(false);

                foreach (var upToAHundredSubscription in upToAHundredSubscriptions.Subscriptions)
                {
                    if (upToAHundredSubscription.Endpoint == queueArn)
                    {
                        return upToAHundredSubscription.SubscriptionArn;
                    }
                }
            } while (upToAHundredSubscriptions.NextToken != null && upToAHundredSubscriptions.Subscriptions.Count > 0);

            return null;
        }

        public static string GetSanitizedTopicName(string topicName)
        {
            var topicNameBuilder = new StringBuilder(topicName);
            // SNS topic names can only have alphanumeric characters, hyphens and underscores.
            // Any other characters will be replaced with a hyphen.
            for (var i = 0; i < topicNameBuilder.Length; ++i)
            {
                var c = topicNameBuilder[i];
                if (!char.IsLetterOrDigit(c)
                    && c != '-'
                    && c != '_')
                {
                    topicNameBuilder[i] = '-';
                }
            }

            return topicNameBuilder.ToString();
        }
    }

}