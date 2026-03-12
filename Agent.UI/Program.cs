using System.Text.Json.Serialization;
using Agent.Core.Extensions;
using Agent.Core.LLM;
using Agent.Core.Tasks.Proxy;
using Agent.UI.Components;
using Agent.UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddAgentCore(builder.Configuration);
builder.Services
    .AddSingleton<TaskLogService>()
    .AddSingleton<ProxyAgentService>(sp => new ProxyAgentService(
        sp.GetRequiredService<ILlmClient>(),
        sp.GetRequiredService<Agent.Core.Configuration.AgentOptions>(),
        sp.GetRequiredService<TaskLogService>().Log
    ));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/proxy", async (ProxyRequest req, ProxyAgentService proxy, CancellationToken ct) =>
{
    var response = await proxy.HandleAsync(req.SessionId, req.Msg, ct);
    return Results.Ok(new { msg = response });
});

app.Run();

internal record ProxyRequest(
    [property: JsonPropertyName("sessionID")]
    string SessionId,
    [property: JsonPropertyName("msg")] string Msg);
