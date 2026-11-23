using System.Text.Json;
using MissionControl.Contracts;

namespace Contracts.Tests;

public class CommandResultJsonTests
{
    private static readonly Guid Id = Guid.Parse("3f2a8c1e-0000-0000-0000-000000000001");

    public static TheoryData<CommandResult> Cases() =>
    [
        (CommandResult)new Acknowledged(Id, DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000)),
        (CommandResult)new Rejected(Id, "deltaV négatif"),
        (CommandResult)new TimedOut(Id, TimeSpan.FromSeconds(2)),
        (CommandResult)new ProbeLost(Id, "Pioneer-11"),
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Round_trips_each_case_without_discriminator(CommandResult result)
    {
        var json = JsonSerializer.Serialize(result, JsonSerializerOptions.Web);
        Assert.DoesNotContain("$type", json);

        var back = JsonSerializer.Deserialize<CommandResult>(json, JsonSerializerOptions.Web);
        Assert.Equal(result.Value, back.Value);
    }

    [Fact]
    public void Rejects_an_object_matching_no_case()
        => Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CommandResult>("""{"commandId":"3f2a8c1e-0000-0000-0000-000000000001"}""", JsonSerializerOptions.Web));
}
