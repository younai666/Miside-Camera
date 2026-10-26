using System.Numerics;
using System.Text.Json;

namespace Miside.Camera.Core;

/// <summary>Right-handed camera coordinates: X right, Y up, Z backward; quaternion XYZW.</summary>
public sealed record PoseFrame(long Sequence, long TimestampNs, string Source, Vector3 Position,
    Quaternion Rotation, double Zoom, long Recenter, bool Tracking)
{
    public static PoseFrame Parse(string json, long previousSequence, long previousTimestamp)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        return Parse(document.RootElement, previousSequence, previousTimestamp);
    }

    internal static PoseFrame Parse(JsonElement root, long previousSequence, long previousTimestamp)
    {
        int version = root.GetProperty("version").GetInt32();
        string? source = root.GetProperty("source").GetString();
        if (version is not (1 or 2) || source is not ("manual" or "arcore") || (version == 1 && source != "manual"))
            throw new FormatException("Unsupported version or pose source.");
        long sequence = root.GetProperty("sequence").GetInt64();
        long timestamp = root.GetProperty("timestampNs").GetInt64();
        if (sequence <= previousSequence || timestamp <= previousTimestamp)
            throw new FormatException("Sequence and monotonic timestamp must increase within a connection.");
        var p = root.GetProperty("position");
        double limit = source == "manual" ? 5 : 100;
        var position = new Vector3(Number(p, "x", -limit, limit), Number(p, "y", -limit, limit), Number(p, "z", -limit, limit));
        Quaternion rotation;
        long recenter = 0;
        bool tracking = true;
        if (version == 1)
        {
            var r = root.GetProperty("rotationDegrees");
            float pitch = Number(r, "pitch", -180, 180) * MathF.PI / 180;
            float yaw = Number(r, "yaw", -180, 180) * MathF.PI / 180;
            float roll = Number(r, "roll", -180, 180) * MathF.PI / 180;
            rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, roll) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch);
        }
        else
        {
            var r = root.GetProperty("rotation");
            rotation = new Quaternion(Number(r, "x", -1, 1), Number(r, "y", -1, 1), Number(r, "z", -1, 1), Number(r, "w", -1, 1));
            if (MathF.Abs(rotation.LengthSquared() - 1) > 0.02f) throw new FormatException("Quaternion must be normalized.");
            rotation = Quaternion.Normalize(rotation);
            recenter = root.GetProperty("recenter").GetInt64();
            if (recenter < 0) throw new FormatException("Recenter generation must be nonnegative.");
            tracking = root.GetProperty("tracking").GetBoolean();
        }
        return new PoseFrame(sequence, timestamp, source, position, rotation, Number(root, "zoom", 1, 3), recenter, tracking);
    }

    private static float Number(JsonElement value, string key, double min, double max)
    {
        double number = value.GetProperty(key).GetDouble();
        if (!double.IsFinite(number) || number < min || number > max) throw new FormatException($"{key} must be finite and in [{min}, {max}].");
        return (float)number;
    }
}

public sealed record CameraStatus(string Phase, long AppliedSequence = 0, string Camera = "", string Scene = "",
    float FieldOfView = 0, float X = 0, float Y = 0, float Z = 0, string Detail = "", int CameraId = 0,
    float Qx = 0, float Qy = 0, float Qz = 0, float Qw = 1, long Recenter = 0,
    float Pitch = 0, float Yaw = 0, float Roll = 0);

/// <summary>Called on the host's render thread. It never owns a socket or a video encoder.</summary>
public interface ICameraAdapter : IDisposable
{
    CameraStatus Status { get; }
    void Apply(PoseFrame pose, long connection);
    void Release(string reason);
}
