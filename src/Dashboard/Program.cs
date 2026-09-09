using Dashboard.Components;
using Dashboard.Hubs;
using Dashboard.Services;
using Dashboard.Sinks;
using FabBridgeEngine.Core.Ingestion;
using FabBridgeEngine.Core.Interfaces;
using FabBridgeEngine.Core.Simulation;
using FabBridgeEngine.Core.Translation;
using FabBridgeEngine.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor + SignalR ────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSignalR();   // for our custom EquipmentHub

// ── FabBridge pipeline registrations ─────────────────────────────────────────
// The bounded channel is a single shared instance; expose it as both producer and consumer.
builder.Services.AddSingleton<SecsEventChannel>(_ => new SecsEventChannel(capacity: 10_000));
builder.Services.AddSingleton<IEventProducer>(sp => sp.GetRequiredService<SecsEventChannel>());
builder.Services.AddSingleton<IEventConsumer>(sp => sp.GetRequiredService<SecsEventChannel>());

builder.Services.AddSingleton<ICeidTranslator>(_ => CeidTranslator.CreateDefault());

// Two sinks, registered as IStateChangeSink so the worker receives both via IEnumerable<>.
var connectionString = builder.Configuration.GetConnectionString("FabBridge")
    ?? Environment.GetEnvironmentVariable("FABBRIDGE_SQL")
    ?? "Server=localhost,1433;Database=FabBridge;User Id=sa;Password=FabBridge!2026;TrustServerCertificate=True;";
builder.Services.AddSingleton<IStateChangeSink, SignalRStateChangeSink>();
builder.Services.AddSingleton<IStateChangeSink>(_ => new SqlTelemetrySink(connectionString));

builder.Services.AddSingleton<TranslationWorker>();
builder.Services.AddSingleton<IEquipmentSource>(sp =>
    new SimulatedEquipment(sp.GetRequiredService<IEventProducer>()));

builder.Services.AddHostedService<BridgePipelineService>();

var app = builder.Build();

// ── HTTP pipeline ────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<EquipmentHub>(EquipmentHub.Route);   // /hubs/equipment

app.Run();
