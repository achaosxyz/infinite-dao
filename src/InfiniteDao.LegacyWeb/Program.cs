using InfiniteDao.Agents.Providers;
using InfiniteDao.Core.Game;
using InfiniteDao.Application;

// dotnet run and the portable Windows build locate their own art/UI, independent of cwd.
var assets = new DirectoryInfo(AppContext.BaseDirectory);
while (assets is not null && !Directory.Exists(Path.Combine(assets.FullName, "wwwroot"))) assets = assets.Parent;
if (assets is null) throw new InvalidOperationException("Game assets (wwwroot) were not found.");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = assets.FullName });
builder.WebHost.UseUrls("http://127.0.0.1:5280");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8_000_000);
builder.Logging.ClearProviders();
var root = new DirectoryInfo(builder.Environment.ContentRootPath);
while (root is not null && !File.Exists(Path.Combine(root.FullName, "InfiniteDao.slnx")) && !Directory.Exists(Path.Combine(root.FullName, "config"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("The game config directory was not found.");
var handler = new HttpClientHandler { AllowAutoRedirect = false };
var session = new GameSession(root.FullName, new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan });
var app = builder.Build();
app.Use(async (context, next) =>
{
    // Bind and validate loopback host, reject cross-origin writes, never serve workspace/config files.
    var host = context.Request.Host.Host;
    if (host is not ("127.0.0.1" or "localhost")) { context.Response.StatusCode = 403; return; }
    if (context.Request.Method != "GET" && context.Request.Headers.TryGetValue("Origin", out var origin)
        && origin.ToString() != $"http://{context.Request.Host}") { context.Response.StatusCode = 403; return; }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; media-src 'self' blob:; frame-ancestors 'none'; base-uri 'none'";
    try { await next(context); }
    catch (GameRuleException e) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { error = e.Message }); }
    catch (ModelDecisionException e) { context.Response.StatusCode = 502; await context.Response.WriteAsJsonAsync(new { error = e.Message, modelFailure = true }); }
    catch (Exception) { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { error = "操作未完成，世界未被替换。请重新读取状态后再试。" }); }
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/catalog", () => Results.Json(new { sects = WorldFactory.Templates(), realms = WorldFactory.RealmNames, policies = WorldFactory.Policies, policyNames = WorldFactory.PolicyNames, model = session.ModelStatus() }));
app.MapGet("/api/state", async () =>
{
    await session.Gate.WaitAsync(); try { return Results.Json(new { state = session.State, model = session.ModelStatus() }); } finally { session.Gate.Release(); }
});
app.MapGet("/api/saves", async () => await session.Saves.ListAsync());
app.MapPost("/api/new", async (NewGameRequest input) =>
{
    await session.Gate.WaitAsync();
    try
    {
        if (input.Mode == "llm") _ = session.ReadConfiguration();
        var state = WorldFactory.Create(input.SectId, input.Seed, input.Mode, input.Difficulty);
        await session.Commit(state); return Results.Json(state);
    }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/command", async (CommandRequest input) =>
{
    await session.Gate.WaitAsync();
    try
    {
        var state = session.Require(input.Revision).Copy();
        if (input.Type == "mode" && input.Option == "llm") _ = session.ReadConfiguration();
        GameEngine.Command(state, new GameCommand(input.Type, input.Target, input.Option));
        await session.Commit(state); return Results.Json(state);
    }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/advance", async (RevisionRequest input) =>
{
    await session.Gate.WaitAsync();
    try { await session.Advance(input.Revision, CancellationToken.None); return Results.Json(session.State); }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/save/{slot}", async (string slot, RevisionRequest input) =>
{
    await session.Gate.WaitAsync();
    try { await session.Saves.SaveAsync(session.Require(input.Revision), slot); return Results.Json(new { saved = true }); }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/load/{slot}", async (string slot) =>
{
    await session.Gate.WaitAsync();
    try { var state = await session.Saves.LoadAsync(slot); await session.Commit(state); return Results.Json(state); }
    finally { session.Gate.Release(); }
});
app.MapGet("/api/export", async () =>
{
    await session.Gate.WaitAsync();
    try { return session.State is null ? Results.NotFound() : Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(session.State, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)), "application/json", "无限修仙-宗门存档.json"); }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/import", async (HttpRequest request) =>
{
    await session.Gate.WaitAsync();
    try { using var reader = new StreamReader(request.Body); var state = session.Saves.Parse(await reader.ReadToEndAsync()); await session.Commit(state); return Results.Json(state); }
    finally { session.Gate.Release(); }
});
app.MapPost("/api/model/renew", async () =>
{
    await session.Gate.WaitAsync();
    try { session.RenewBudget(); return Results.Json(session.ModelStatus()); }
    finally { session.Gate.Release(); }
});
Console.WriteLine("无限修仙 · 宗门纪 | http://127.0.0.1:5280 | Ctrl+C 退出；游戏自动保存于 artifacts/game/saves");
if (builder.Configuration.GetValue<bool>("open-browser"))
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://127.0.0.1:5280") { UseShellExecute = true }); }
        catch { Console.WriteLine("请在浏览器打开 http://127.0.0.1:5280"); }
    });
app.Run();

public sealed record NewGameRequest(string SectId, int Seed, string Mode = "llm", string Difficulty = "normal");
public sealed record CommandRequest(long Revision, string Type, string Target = "", string Option = "");
public sealed record RevisionRequest(long Revision);
