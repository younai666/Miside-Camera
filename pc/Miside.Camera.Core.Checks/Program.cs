using System.Text.Json;
using Miside.Camera.Core;

string Packet(int version = 2, long sequence = 1, long timestamp = 1, double q = 1, double zoom = 1) => JsonSerializer.Serialize(new {
    version, sequence, timestampNs = timestamp, source = "manual", position = new { x = 0, y = 0, z = 0 },
    rotation = new { x = 0, y = 0, z = 0, w = q }, rotationDegrees = new { pitch = 0, yaw = 0, roll = 0 }, zoom, recenter = 0, tracking = true
});
void Require(bool condition) { if (!condition) throw new Exception("Pose contract regression"); }
void Reject(string packet, long sequence = 0, long timestamp = 0) {
    try { PoseFrame.Parse(packet, sequence, timestamp); } catch (FormatException) { return; }
    throw new Exception("Invalid pose accepted");
}
Require(PoseFrame.Parse(Packet(), 0, 0).Rotation.W == 1);
Require(PoseFrame.Parse(Packet(version: 1), 0, 0).Rotation.W == 1);
Reject(Packet(version: 3));
Reject(Packet(sequence: 1), sequence: 1);
Reject(Packet(timestamp: 1), timestamp: 1);
Reject(Packet(q: 0.2));
Reject(Packet(zoom: 4));
Reject(Packet().Replace("\"x\":0", "\"x\":999"));
Console.WriteLine("PASS: v1/v2 parsing, monotonicity, quaternion and numeric bounds");
