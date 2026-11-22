using MissionControl.Contracts;

namespace TelemetryReceiver;

/// <summary>Deux trames sont égales si elles ont le même PacketId.</summary>
public static class PacketIdComparer
{
    public static readonly IEqualityComparer<TelemetryFrame> Instance =
        EqualityComparer<TelemetryFrame>.Create(f => f!.PacketId);
}
