using HotChocolate.Execution;
using HotChocolate.Subscriptions;

[ExtendObjectType(OperationTypeNames.Subscription)]
public class TestSubscriptions
{
    public const string TopicName = "test_topoc";

    [Subscribe(With = nameof(SubscribeToOnMessageAsync))]
    public Task<TestMessage> OnMessageAsync(
        [EventMessage] string message,
        CancellationToken cancellationToken) =>
            Task.FromResult(new TestMessage { Message = message });

    public async ValueTask<ISourceStream<string>> SubscribeToOnMessageAsync(
        [Service] ITopicEventReceiver eventReceiver,
        [Service] ILogger<TestSubscriptions> logger,
        CancellationToken cancellationToken)
    {
        var stream = await eventReceiver.SubscribeAsync<string, string>(TopicName, cancellationToken);

        cancellationToken.Register(() => logger.LogWarning("The subscription of topic {0} has closed!", TopicName));

        return stream;
    }
}