using System.Formats.Cbor;

namespace MissionControl.Contracts;

/// <summary>Encodage CBOR d'une trame : map à 6 clés texte (id, probe, ts, alt, vel, bat).</summary>
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

    public static byte[] Encode(TelemetryFrame frame)
    {
        var writer = new CborWriter();
        Write(writer, frame);
        return writer.Encode();
    }
}
