using System.Text.Json;
using Microsoft.Extensions.Options;
using MissionControl.Api;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<CommandDispatcherOptions>(builder.Configuration.GetSection("Commands"));
builder.Services.AddSingleton<CommandDispatcher>();
builder.Services.AddSingleton<ICommandHistory, InMemoryCommandHistory>();
builder.Services.AddHttpClient("probesimulator", c => c.BaseAddress = new Uri("https+http://probesimulator"));

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapPost("/commands", async (ProbeCommand command, CommandDispatcher dispatcher, ICommandHistory history,
    TimeProvider time, CancellationToken ct) =>
{
    CommandResult result = await dispatcher.DispatchAsync(command, ct);
    history.Append(new CommandHistoryEntry(command.Id, command.ProbeName, ProbeCommand.ToRequest(command).Type,
        CommandResult.Describe(result), time.GetUtcNow()));
    return result;
});

app.MapGet("/commands/history", async (HttpResponse response, ICommandHistory history,
    IOptions<JsonOptions> json, CancellationToken ct) =>
{
    response.ContentType = "application/jsonl";
    await JsonSerializer.SerializeAsyncEnumerable(response.Body, history.ReadAllAsync(ct), topLevelValues: true,
        json.Value.SerializerOptions, ct);
});

app.Run();

public partial class Program;
