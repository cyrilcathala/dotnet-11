using System.Diagnostics;

namespace LaunchControl;

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
