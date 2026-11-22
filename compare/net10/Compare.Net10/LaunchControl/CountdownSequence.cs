using System.Diagnostics;

namespace LaunchControl;

// Chaîne d'appels async : le Task.Yield() libère le thread, donc la reprise part de la file du pool.
// Seuls les appels synchrones qui suivent la dernière reprise figurent sur la pile live.
public sealed class CountdownSequence
{
    public async Task<string> CaptureStackAsync(CancellationToken ct)
    {
        await Task.Yield();
        return await IgnitionSequenceAsync(ct);
    }

    private static async Task<string> IgnitionSequenceAsync(CancellationToken ct)
    {
        await Task.Yield();
        return await StageSeparationAsync(ct);
    }

    private static async Task<string> StageSeparationAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return await OrbitInsertionAsync(ct);
    }

    private static async Task<string> OrbitInsertionAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new StackTrace(fNeedFileInfo: true).ToString();
    }
}
