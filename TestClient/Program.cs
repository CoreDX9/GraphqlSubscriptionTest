// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake.Transport.WebSockets.Messages;
using StrawberryShake.Transport.WebSockets;
using TestClient;
using StrawberryShake.Transport.WebSockets.Protocols;
using System.Reflection;
using StrawberryShake;

IServiceCollection services = new ServiceCollection();

//services.AddSingleton<SubscriptionSocketStateMonitor>();
services.AddSubscriptionTestClient()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://localhost:7147/graphql/");
    })
    .ConfigureWebSocketClient((sp, client) =>
    {
        client.Uri = new Uri("wss://localhost:7147/graphql/");
    });

await using var rootProvider = services.BuildServiceProvider();
await using var asyncScope = rootProvider.CreateAsyncScope();
var provider = asyncScope.ServiceProvider;

var client = provider.GetRequiredService<ISubscriptionTestClient>();

//var monitor = provider.GetRequiredService<SubscriptionSocketStateMonitor>();
//monitor.Start(TimeSpan.FromSeconds(15));

int i = 0;
while (true)
{
    try
    {
        i++;
        Console.WriteLine($"{i} : Connect");
        var subscription = client.OnMessage.Watch();

        var stream = subscription.ToAsyncEnumerable();
        await foreach (var item in stream)
        {
            Console.WriteLine($"{i}[msg] : {item.Data?.OnMessage.Message}");
        }

        // If subscription completed normally by server, stream can finish automatically.
        Console.WriteLine($"{i} : The subscription stream was completed !");
        if (!Retry(i)) break;
    }
    catch (Exception ex)
    {
        if (!Retry(i, ex)) break;
    }
}

Console.WriteLine($"Press any key to exit...");
Console.ReadKey();

static bool Retry(int index, Exception? ex = null)
{
    if (ex is not null) Console.WriteLine($"{index} : Exception happened: {ex.Message}");

    Console.Write("Try reconnect? Enter is Yes :");
    var key = Console.ReadKey();
    Console.WriteLine();

    return key.Key is ConsoleKey.Enter;
}

//var subscription = client.OnMessage.Watch();
//subscription.Subscribe(
//    OnActions.OnNext,
//    // error callback
//    // It should be shown after click "Complete Subscription" button. or complete callback
//    // But this will never happen.
//    OnActions.OnError,
//    // complete callback
//    // It should be shown after click "Complete Subscription" button. or error callback
//    // But this will never happen.
//    OnActions.OnCompleted);

//Console.WriteLine($"Press any key to exit...");
//Console.ReadKey();

//public static class OnActions
//{
//    public static void OnNext(IOperationResult<IOnMessageResult> result) => Console.WriteLine(result?.Data?.OnMessage.Message);
//    public static void OnError(Exception ex) => Console.WriteLine($"Exception happened: {ex.Message}");
//    public static void OnCompleted() => Console.WriteLine("The subscription stream was completed!");
//}

// If you want Monitor work well, please look at StrawberryShake.Core.OperationExecutor.Observable.cs line 133 !!
public class SubscriptionSocketStateMonitor
{
    private const BindingFlags _bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;

    private readonly System.Timers.Timer _timer;
    private readonly ISessionPool _sessionPool;
    private readonly Type _sessionPoolType;
    private readonly FieldInfo _sessionsField;

    private readonly FieldInfo _socketOperationsDictionaryField = typeof(Session).GetField("_operations", _bindingFlags)!;
    private readonly FieldInfo _socketOperationManagerField = typeof(SocketOperation).GetField("_manager", _bindingFlags)!;
    private readonly FieldInfo _socketProtocolField = typeof(Session)!.GetField("_socketProtocol", _bindingFlags)!;
    private readonly FieldInfo _protocolReceiverField = typeof(GraphQLWebSocketProtocol).GetField("_receiver", _bindingFlags)!;
    private readonly MethodInfo _notifyMethod = typeof(SocketProtocolBase).GetMethod("Notify", _bindingFlags)!;

    private Type? _sessionInfoType;
    private PropertyInfo? _sessionProperty;
    private Type? _receiverType;
    private FieldInfo? _receiverClientField;
    private FieldInfo? _receiverCancellationTokenSourceField;

    public SubscriptionSocketStateMonitor(ISessionPool sessionPool)
    {
        _timer = new();
        _timer.Elapsed += (s, e) => NotifySocketClosed();
        _timer.AutoReset = true;
        _timer.Interval = 1_500;
        _sessionPool = sessionPool;
        _sessionPoolType = _sessionPool.GetType();
        _sessionsField = _sessionPoolType.GetField("_sessions", _bindingFlags)!;
    }

    private void NotifySocketClosed()
    {
        var sessionInfos = (_sessionsField!.GetValue(_sessionPool) as System.Collections.IDictionary)!.Values;

        foreach (var sessionInfo in sessionInfos)
        {
            _sessionInfoType ??= sessionInfo.GetType();
            _sessionProperty ??= _sessionInfoType.GetProperty("Session")!;
            var session = _sessionProperty.GetValue(sessionInfo) as Session;
            var socketOperations = _socketOperationsDictionaryField
                .GetValue(session) as System.Collections.Concurrent.ConcurrentDictionary<string, SocketOperation>;

            foreach (var operation in socketOperations!)
            {
                var operationsession = _socketOperationManagerField.GetValue(operation.Value) as Session;
                var protocol = _socketProtocolField.GetValue(operationsession) as GraphQLWebSocketProtocol;

                var receiver = _protocolReceiverField.GetValue(protocol)!;

                _receiverType ??= receiver.GetType();
                _receiverClientField ??= _receiverType.GetField("_client", _bindingFlags)!;
                var client = _receiverClientField.GetValue(receiver) as ISocketClient;

                if (client!.IsClosed is false) continue;

                _receiverCancellationTokenSourceField ??= _receiverType.GetField("_cts", _bindingFlags)!;

                var cts = _receiverCancellationTokenSourceField.GetValue(receiver) as CancellationTokenSource;

                // If websocket close because server app crashed or network error, StrawberryShake can't receive CompleteOperationMessage.
                // We have to trigger OnCompleted event callback manually.
                _notifyMethod.Invoke(protocol, new object[] { operation.Value.Id, CompleteOperationMessage.Default, cts!.Token });
            }
        }
    }

    public void Start(TimeSpan? interval = null)
    {
        _timer.Stop();
        if (interval is not null) _timer.Interval = interval.Value.TotalMilliseconds;
        _timer.Start();
    }

    public void Stop(bool hasNotOpenedSessionOnly = false)
    {
        if (hasNotOpenedSessionOnly)
        {
            var sessionInfos = (_sessionsField!.GetValue(_sessionPool) as System.Collections.IDictionary)!.Values;

            if (sessionInfos.Count > 0) return;
        }

        _timer.Stop();
    }
}