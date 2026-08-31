namespace AsistenciaSync.Models;

public sealed record AttendanceRecord(string UserId, string Name, DateTime Timestamp, string Type, string Source, int Status, int Punch, string Fingerprint);
