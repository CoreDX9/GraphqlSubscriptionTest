using HotChocolate.Resolvers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

#region GraphQL Server

builder.Services.AddGraphQLServer()
    .AddQueryType(d => d
        .Name(OperationTypeNames.Query)
        .Field("version")
        .Resolve(typeof(IResolverContext).Assembly.GetName().Version?.ToString()))
    .AddTypeExtension<TestMessageType>()
    .AddSubscriptionType(d => d.Name(OperationTypeNames.Subscription))
        .AddTypeExtension<TestSubscriptions>()
    .AddInMemorySubscriptions();

#endregion

var app = builder.Build();

app.UseWebSockets();

app.MapRazorPages();

app.MapGraphQL();

app.Run();
