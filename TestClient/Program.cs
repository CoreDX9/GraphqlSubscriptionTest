// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using TestClient;

Console.WriteLine("Hello, World!");

IServiceCollection services = new ServiceCollection();

services.AddSubscriptionTestClient()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri("https://localhost:7147/graphql/");
    })
    .ConfigureWebSocketClient(client =>
    {
        client.Uri = new Uri("wss://localhost:7147/graphql/");
    });

await using var rootProvider = services.BuildServiceProvider();
await using var asyncScope = rootProvider.CreateAsyncScope();
var provider = asyncScope.ServiceProvider;

var client = provider.GetRequiredService<ISubscriptionTestClient>();
var subscription = client.OnMessage.Watch();

subscription.Subscribe(
    x => Console.WriteLine(x?.Data?.OnMessage.Message),
    // It should be shown after click "Complete Subscription" button.
    // But this will never happen.
    ex => Console.WriteLine($"Exception happened: {ex.Message}"),
    // It should be shown after click "Complete Subscription" button.
    // But this will never happen.
    () => Console.WriteLine("The subscription stream was completed by server!"));

Console.ReadKey();

//var asyncStream = subscription.ToAsyncEnumerable();
//await foreach (var item in asyncStream)
//{
//    Console.WriteLine(item?.Data?.OnMessage.Message);
//}

//// It should be shown after click "Complete Subscription" button.
//// But this will never happen.
//Console.WriteLine("The subscription stream was completed by server!");