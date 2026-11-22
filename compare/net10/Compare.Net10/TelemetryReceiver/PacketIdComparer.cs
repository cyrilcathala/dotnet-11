using MissionControl.Contracts;

namespace TelemetryReceiver;

/// <summary>Comparateur écrit à la main : deux trames sont égales si elles ont le même PacketId.</summary>
public sealed class PacketIdComparer : IEqualityComparer<TelemetryFrame>
{
    public bool Equals(TelemetryFrame? x, TelemetryFrame? y) => x?.PacketId == y?.PacketId;

    public int GetHashCode(TelemetryFrame obj) => obj.PacketId.GetHashCode();
}
