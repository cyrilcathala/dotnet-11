using System.Collections.Concurrent;

namespace MissionControl.Api;

public sealed record CommandHistoryEntry(Guid CommandId, string ProbeName, string Command, string Outcome, DateTimeOffset At);

public interface ICommandHistory
{
    void Append(CommandHistoryEntry e);
    IAsyncEnumerable<CommandHistoryEntry> ReadAllAsync(CancellationToken ct);
}

public sealed class InMemoryCommandHistory : ICommandHistory
{
    private readonly ConcurrentQueue<CommandHistoryEntry> _entries = new();

    public void Append(CommandHistoryEntry e) => _entries.Enqueue(e);

    public IAsyncEnumerable<CommandHistoryEntry> ReadAllAsync(CancellationToken ct) => _entries.ToArray().ToAsyncEnumerable();
}
