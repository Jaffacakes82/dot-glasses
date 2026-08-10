using DotGlasses.App;
using DotGlasses.App.Auth;
using DotGlasses.App.Logging;
using DotGlasses.App.ReferenceData;
using DotGlasses.App.Sync;
using DotGlasses.Contracts.Sync;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;

// Singleton throughout, not Scoped: a Blazor WASM app is one long-lived client session with a
// single implicit root scope (no per-request scoping like a server app), and BatchingLoggerProvider
// (registered as a singleton ILoggerProvider, resolved eagerly by the logging infrastructure)
// needs to depend on ISyncQueueStore — mixing Scoped in here trips DI's
// singleton-can't-consume-scoped validation at startup.
builder.Services.AddSingleton<AuthTokenStore>();
builder.Services.AddTransient<AuthorizationMessageHandler>();
builder.Services
    .AddHttpClient("Api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api"));

builder.Services.AddSingleton<ISyncQueueStore, IndexedDbOutboxStore>();
builder.Services.AddSingleton<ISyncService, SyncService>();
builder.Services.AddSingleton<ConnectivitySyncTrigger>();

builder.Services.AddSingleton<IReferenceDataClient, ReferenceDataClient>();
builder.Services.AddSingleton<IUserLocationClient, UserLocationClient>();

builder.Services.AddSingleton<IClientSessionContext, ClientSessionContext>();
builder.Services.AddSingleton<BatchingLoggerProvider>();
builder.Services.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<BatchingLoggerProvider>());

var host = builder.Build();

// Rehydrate a persisted token before the first render, so a returning technician lands straight
// on Home rather than being bounced through the login page — see AuthTokenStore.
var tokenStore = host.Services.GetRequiredService<AuthTokenStore>();
await tokenStore.InitializeAsync();

var connectivityTrigger = host.Services.GetRequiredService<ConnectivitySyncTrigger>();
await connectivityTrigger.StartAsync();

await host.RunAsync();
