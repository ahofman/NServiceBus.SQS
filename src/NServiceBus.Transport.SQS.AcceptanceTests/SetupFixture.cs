﻿namespace NServiceBus.Transport.SQS.AcceptanceTests
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Tests;

    [SetUpFixture]
    public class SetupFixture
    {
        /// <summary>
        /// The name prefix for the current run of the test suite.
        /// </summary>
        public static string NamePrefix { get; private set; }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Generate a new name prefix for acceptance tests
            // every time the tests are run.
            // This is to work around an SQS limitation that prevents
            // us from deleting then creating a queue with the
            // same name in a 60 second period.
            NamePrefix = $"AT{Regex.Replace(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), "[/+=]", "").ToUpperInvariant()}";
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            using (var sqsClient = SqsTransportExtensions.CreateSQSClient())
            using (var snsClient = SqsTransportExtensions.CreateSnsClient())
            {
                await Cleanup.DeleteAllResourcesWithPrefix(sqsClient, snsClient, NamePrefix).ConfigureAwait(false);
            }
        }
    }
}