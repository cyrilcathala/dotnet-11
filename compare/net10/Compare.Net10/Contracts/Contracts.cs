using System.Formats.Cbor;

namespace MissionControl.Contracts;

// Copie locale minimale des contrats partagés.
public enum ProbeKind { Orbiter, Lander, Rover }

public sealed record ProbeDto(string Name, ProbeKind Kind, DateTimeOffset CreatedAt);

public sealed record TelemetryFrame(
    long PacketId, string ProbeName, DateTimeOffset Timestamp,
    double AltitudeKm, double VelocityKmS, double BatteryPercent);

public sealed record SimulatorCommandRequest(Guid CommandId, string Type, double? Value);

public static class TelemetryCbor
{
    public static void Write(CborWriter writer, TelemetryFrame frame)
    {
        writer.WriteStartMap(6);
        writer.WriteTextString("id");
        writer.WriteInt64(frame.PacketId);
        writer.WriteTextString("probe");
        writer.WriteTextString(frame.ProbeName);
        writer.WriteTextString("ts");
        writer.WriteInt64(frame.Timestamp.ToUnixTimeMilliseconds());
        writer.WriteTextString("alt");
        writer.WriteDouble(frame.AltitudeKm);
        writer.WriteTextString("vel");
        writer.WriteDouble(frame.VelocityKmS);
        writer.WriteTextString("bat");
        writer.WriteDouble(frame.BatteryPercent);
        writer.WriteEndMap();
    }

    public static TelemetryFrame Read(CborReader reader)
    {
        long id = 0, ts = 0;
        string probe = "";
        double alt = 0, vel = 0, bat = 0;

        var entries = reader.ReadStartMap();
        for (var i = 0; i < entries; i++)
        {
            switch (reader.ReadTextString())
            {
                case "id": id = reader.ReadInt64(); break;
                case "probe": probe = reader.ReadTextString(); break;
                case "ts": ts = reader.ReadInt64(); break;
                case "alt": alt = reader.ReadDouble(); break;
                case "vel": vel = reader.ReadDouble(); break;
                case "bat": bat = reader.ReadDouble(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        return new TelemetryFrame(id, probe, DateTimeOffset.FromUnixTimeMilliseconds(ts), alt, vel, bat);
    }
}
