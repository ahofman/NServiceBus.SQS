namespace NServiceBus.Transport.SQS.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Amazon.Auth.AccessControlPolicy;
    using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;

#pragma warning disable 618
    static class PolicyExtensions
    {
        internal static void AddSQSPermission(this Policy policy, Statement addStatement)
        {
            policy.Statements.Add(addStatement);
        }

        internal static bool ContainsPermission(this Policy policy, Statement statement)
        {
            if (policy.Statements == null)
            {
                return false;
            }

            return policy.Statements.Any(stmt => stmt.Effect == statement.Effect &&
                                                       stmt.StatementContainsResources(statement.Resources) &&
                                                       stmt.StatementContainsActions(statement.Actions) &&
                                                       stmt.StatementContainsConditions(statement.Conditions) &&
                                                       stmt.StatementContainsPrincipals(statement.Principals));
        }

        internal static bool CoveredByPermission(this Statement statement, Statement permission) =>
            statement.Effect == permission.Effect &&
            statement.StatementContainsResources(permission.Resources) &&
            statement.StatementContainsActions(permission.Actions) &&

            statement.StatementCoveredByConditions(permission.Conditions) &&

            statement.StatementContainsPrincipals(permission.Principals);

        internal static bool CoveredByWildcard(this Statement statement, Statement permission) =>
            statement.Effect == permission.Effect &&
            statement.StatementContainsResources(permission.Resources) &&
            statement.StatementContainsActions(permission.Actions) &&

            statement.StatementCoveredByWildcardConditions() &&

            statement.StatementContainsPrincipals(permission.Principals);

        private static bool StatementContainsResources(this Statement statement, IEnumerable<Resource> resources) =>
            resources.All(resource => statement.Resources.FirstOrDefault(x => string.Equals(x.Id, resource.Id)) != null);

        private static bool StatementContainsActions(
            this Statement statement,
            IEnumerable<ActionIdentifier> actions) =>
            actions.All(action => statement.Actions.FirstOrDefault(x => string.Equals(x.ActionName, action.ActionName)) != null);

        private static bool StatementContainsPrincipals(
            this Statement statement,
            IEnumerable<Principal> principals) =>
            principals.All(principal => statement.Principals.FirstOrDefault(x => string.Equals(x.Id, principal.Id) && string.Equals(x.Provider, principal.Provider)) != null);

        private static bool StatementContainsConditions(
            this Statement statement,
            IEnumerable<Condition> conditions) =>
            conditions.All(condition => statement.Conditions.FirstOrDefault(x => string.Equals(x.Type, condition.Type) &&
                string.Equals(x.ConditionKey, condition.ConditionKey) &&
                x.Values.OrderBy(v => v, OrdinalComparer).SequenceEqual(condition.Values.OrderBy(v => v, OrdinalComparer), OrdinalComparer)) != null);

        private static bool StatementCoveredByConditions(
            this Statement statement,
            IList<Condition> conditions) =>
            statement.Conditions.Any(condition => conditions.Any(x => string.Equals(x.Type, condition.Type) && string.Equals(x.ConditionKey, condition.ConditionKey) && condition.Values.All(v => x.Values.Contains(v, OrdinalComparer))));

        private static bool StatementCoveredByWildcardConditions(
            this Statement statement) =>
            statement.Conditions.Any(condition => condition.Values.All(v => v.Contains("*")));

        internal static Statement CreateSQSPermissionStatement(string sqsQueueArn, string topicArn)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Conditions.Add(ConditionFactory.NewSourceArnCondition(topicArn));
            statement.Principals.Add(new Principal("*"));
            return statement;
        }

        internal static Statement CreateSQSPermissionStatement(string sqsQueueArn, IEnumerable<string> topicArnMatchPatterns)
        {
            var statement = new Statement(Statement.StatementEffect.Allow);
            statement.Actions.Add(SQSActionIdentifiers.SendMessage);
            statement.Resources.Add(new Resource(sqsQueueArn));
            statement.Principals.Add(new Principal("*"));
            var queuePermissionCondition = new Condition(ConditionFactory.ArnComparisonType.ArnLike.ToString(), "aws:SourceArn", topicArnMatchPatterns.OrderBy(t => t, OrdinalComparer).ToArray());
            statement.Conditions.Add(queuePermissionCondition);
            return statement;
        }

        // conditions are case sensitive
        // https://stackoverflow.com/a/47769284/290290
        private static readonly StringComparer OrdinalComparer = StringComparer.Ordinal;
    }
#pragma warning restore 618
}