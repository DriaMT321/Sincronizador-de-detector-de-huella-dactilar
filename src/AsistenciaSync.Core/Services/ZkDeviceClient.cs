using System.Net.Sockets;
using System.Text;

using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

public sealed class ZkDeviceClient : IDisposable
{
    const ushort CmdConnect = 1000, CmdExit = 1001, CmdDisable = 1003, CmdEnable = 1002;
    const ushort CmdPrepare = 1500, CmdData = 1501, CmdFree = 1502, CmdGetAttendance = 13, CmdPrepareBuffer = 1503, CmdUserTemp = 9, CmdUserWrite = 8, CmdDeleteUser = 18, CmdClearAttendance = 15, CmdRefresh = 1013;
    const ushort AckOk = 2000;
    readonly string host; readonly int port; TcpClient? tcp; NetworkStream? stream; ushort session, reply;
    public IReadOnlyDictionary<string, string> LastUsers { get; private set; } = new Dictionary<string, string>();
    public ZkDeviceClient(string host, int port) { this.host = host; this.port = port; }

    public List<AttendanceRecord> DownloadAttendance()
    {
        var userNames = DownloadUserNames(); LastUsers = userNames; stream?.Dispose(); tcp?.Dispose(); tcp = new TcpClient(); tcp.Connect(host, port); stream = tcp.GetStream(); var connect = Send(CmdConnect, Array.Empty<byte>());
        if (connect.Command != AckOk) throw new InvalidOperationException($"El dispositivo no aceptó la conexión (respuesta {connect.Command})."); session = connect.Session; Send(CmdDisable, Array.Empty<byte>());
        try
        {
            var prepared = Send(CmdGetAttendance, Array.Empty<byte>());
            // Algunos firmwares del iClock responden ACK_ERROR (2001) cuando no hay
            // marcaciones almacenadas. Eso debe significar "cero registros", no una
            // falla de conexión, especialmente después de limpiar el dispositivo.
            if (prepared.Command == 2001) return new List<AttendanceRecord>();
            if (prepared.Command != CmdPrepare && prepared.Command != AckOk) throw new InvalidOperationException($"No se pudieron solicitar las marcaciones (respuesta {prepared.Command}).");
            var size = prepared.Payload.Length >= 4 ? BitConverter.ToInt32(prepared.Payload, 0) : 0; var data = new List<byte>();
            while (data.Count < size) { var chunk = Receive(); if (chunk.Command != CmdData) break; data.AddRange(chunk.Payload); }
            Send(CmdFree, Array.Empty<byte>()); return ClassifyBySequence(Decode(data.ToArray(), userNames));
        }
        finally { try { Send(CmdEnable, Array.Empty<byte>()); } catch { } try { Send(CmdExit, Array.Empty<byte>()); } catch { } }
    }

    Dictionary<string, string> DownloadUserNames()
    {
        stream?.Dispose(); tcp?.Dispose(); tcp = new TcpClient(); tcp.Connect(host, port); stream = tcp.GetStream(); var connect = Send(CmdConnect, Array.Empty<byte>());
        if (connect.Command != AckOk) throw new InvalidOperationException($"No se pudo leer el catálogo de usuarios (respuesta {connect.Command})."); session = connect.Session;
        try { var request = new byte[11]; request[0] = 1; Buffer.BlockCopy(BitConverter.GetBytes((short)CmdUserTemp), 0, request, 1, 2); Buffer.BlockCopy(BitConverter.GetBytes(5), 0, request, 3, 4); return DecodeUsers(ReadBulk(Send(CmdPrepareBuffer, request))); }
        finally { try { Send(CmdExit, Array.Empty<byte>()); } catch { } }
    }

    byte[] ReadBulk(Packet prepared)
    {
        if (prepared.Command == CmdData) return prepared.Payload; if (prepared.Command != CmdPrepare || prepared.Payload.Length < 4) return Array.Empty<byte>(); var size = BitConverter.ToInt32(prepared.Payload, 0); var data = new List<byte>();
        while (data.Count < size) { var chunk = Receive(); if (chunk.Command != CmdData) break; data.AddRange(chunk.Payload); } return data.Take(size).ToArray();
    }

    static Dictionary<string, string> DecodeUsers(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); if (data.Length < 4) return result; var totalSize = BitConverter.ToInt32(data, 0); var body = data[4..]; var recordSize = totalSize % 72 == 0 ? 72 : 28;
        for (int i = 0; i + recordSize <= body.Length; i += recordSize) { string name, userId; if (recordSize == 72) { name = Text(body, i + 11, 24); userId = Text(body, i + 48, 24); } else { name = Text(body, i + 8, 8); userId = BitConverter.ToUInt32(body, i + 23).ToString(); } if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(name)) result[userId] = name; }
        return result;
    }

    public DateTime ReadDeviceTime()
    {
        stream?.Dispose(); tcp?.Dispose(); tcp = new TcpClient(); tcp.Connect(host, port); stream = tcp.GetStream(); var connect = Send(CmdConnect, Array.Empty<byte>()); if (connect.Command != AckOk) throw new InvalidOperationException($"El dispositivo no aceptó la conexión (respuesta {connect.Command})."); session = connect.Session; var response = Send(201, Array.Empty<byte>());
        try { return response.Payload.Length >= 4 ? DecodeTime(response.Payload, 0) : DateTime.MinValue; } finally { try { Send(CmdExit, Array.Empty<byte>()); } catch { } }
    }

    public void SyncClock(DateTime localTime)
    {
        stream?.Dispose(); tcp?.Dispose(); tcp = new TcpClient(); tcp.Connect(host, port); stream = tcp.GetStream(); var connect = Send(CmdConnect, Array.Empty<byte>()); if (connect.Command != AckOk) throw new InvalidOperationException($"No se pudo conectar para actualizar la hora (respuesta {connect.Command})."); session = connect.Session;
        try { var response = Send(202, BitConverter.GetBytes(EncodeTime(localTime))); if (response.Command != AckOk) throw new InvalidOperationException($"El dispositivo rechazó la actualización de hora (respuesta {response.Command})."); } finally { try { Send(CmdExit, Array.Empty<byte>()); } catch { } }
    }

    public void RenameUser(string employeeId, string name)
    {
        ConnectSession();
        try
        {
            var payload = FindUserRecord(employeeId) ?? throw new InvalidOperationException($"No se encontró el trabajador {employeeId} en el reloj.");
            Send(CmdDisable, Array.Empty<byte>()); Array.Clear(payload, 11, 24); WriteText(payload, 11, 24, name);
            var response = Send(CmdUserWrite, payload); if (response.Command != AckOk) throw new InvalidOperationException($"El reloj rechazó el cambio de nombre (respuesta {response.Command}).");
            Send(CmdRefresh, Array.Empty<byte>());
        }
        finally { try { Send(CmdEnable, Array.Empty<byte>()); } catch { } CloseSession(); }
    }

    public void CreateUser(string employeeId, string name)
    {
        if (!uint.TryParse(employeeId, out var numericId) || numericId > ushort.MaxValue) throw new InvalidOperationException("El ID debe ser numérico y menor que 65536.");
        ConnectSession();
        try
        {
            if (FindUserRecord(employeeId) is not null) throw new InvalidOperationException($"El trabajador {employeeId} ya existe en el reloj.");
            var payload = new byte[72]; Buffer.BlockCopy(BitConverter.GetBytes((ushort)numericId), 0, payload, 0, 2); payload[2] = 0; WriteText(payload, 11, 24, name); WriteText(payload, 48, 24, employeeId);
            Send(CmdDisable, Array.Empty<byte>()); var response = Send(CmdUserWrite, payload); if (response.Command != AckOk) throw new InvalidOperationException($"El reloj rechazó la creación del trabajador (respuesta {response.Command})."); Send(CmdRefresh, Array.Empty<byte>());
        }
        finally { try { Send(CmdEnable, Array.Empty<byte>()); } catch { } CloseSession(); }
    }

    public void DeleteUser(string employeeId)
    {
        ConnectSession();
        try { var record = FindUserRecord(employeeId) ?? throw new InvalidOperationException($"No se encontró el trabajador {employeeId} en el reloj."); var uid = BitConverter.ToUInt16(record, 0); Send(CmdDisable, Array.Empty<byte>()); var response = Send(CmdDeleteUser, BitConverter.GetBytes(uid)); if (response.Command != AckOk) throw new InvalidOperationException($"El reloj rechazó el borrado (respuesta {response.Command})."); Send(CmdRefresh, Array.Empty<byte>()); }
        finally { try { Send(CmdEnable, Array.Empty<byte>()); } catch { } CloseSession(); }
    }

    public void ClearAttendance()
    {
        ConnectSession();
        try { Send(CmdDisable, Array.Empty<byte>()); var response = Send(CmdClearAttendance, Array.Empty<byte>()); if (response.Command != AckOk) throw new InvalidOperationException($"El reloj rechazó el borrado de marcaciones (respuesta {response.Command})."); Send(CmdRefresh, Array.Empty<byte>()); }
        finally { try { Send(CmdEnable, Array.Empty<byte>()); } catch { } CloseSession(); }
    }

    void ConnectSession()
    {
        stream?.Dispose(); tcp?.Dispose(); tcp = new TcpClient(); tcp.Connect(host, port); stream = tcp.GetStream(); var connect = Send(CmdConnect, Array.Empty<byte>()); if (connect.Command != AckOk) throw new InvalidOperationException($"El dispositivo no aceptó la conexión (respuesta {connect.Command})."); session = connect.Session; reply = 0;
    }
    byte[]? FindUserRecord(string employeeId)
    {
        var request = new byte[11]; request[0] = 1; Buffer.BlockCopy(BitConverter.GetBytes((short)CmdUserTemp), 0, request, 1, 2); Buffer.BlockCopy(BitConverter.GetBytes(5), 0, request, 3, 4);
        var data = ReadBulk(Send(CmdPrepareBuffer, request)); if (data.Length < 4) return null; var totalSize = BitConverter.ToInt32(data, 0); var body = data[4..]; var recordSize = totalSize % 72 == 0 ? 72 : 28;
        for (var i = 0; i + recordSize <= body.Length; i += recordSize) if (Text(body, i + (recordSize == 72 ? 48 : 23), recordSize == 72 ? 24 : 4).Equals(employeeId, StringComparison.OrdinalIgnoreCase)) return body.Skip(i).Take(recordSize).ToArray();
        return null;
    }
    void CloseSession() { try { Send(CmdExit, Array.Empty<byte>()); } catch { } }
    static void WriteText(byte[] buffer, int offset, int length, string value) { var bytes = Encoding.ASCII.GetBytes(value ?? ""); Array.Copy(bytes, 0, buffer, offset, Math.Min(length, bytes.Length)); }

    List<AttendanceRecord> Decode(byte[] data, IReadOnlyDictionary<string, string> userNames)
    {
        var result = new List<AttendanceRecord>(); const int recordSize = 40; var recordsData = data.Length >= 4 ? data[4..] : Array.Empty<byte>();
        for (int i = 0; i + recordSize <= recordsData.Length; i += recordSize) { var userId = Text(recordsData, i + 2, 24); if (string.IsNullOrWhiteSpace(userId)) continue; var timestamp = DecodeTime(recordsData, i + 27); if (timestamp.Year < 2000) continue; var status = recordsData[i + 26]; var punch = recordsData[i + 31]; var type = status is 1 or 3 or 5 ? "Salida" : "Entrada"; var name = userNames.TryGetValue(userId, out var catalogName) ? catalogName : userId; result.Add(new AttendanceRecord(userId, name, timestamp, type, "iClock", status, punch, $"{userId}|{timestamp:O}|{status}|{punch}")); }
        return result;
    }

    static List<AttendanceRecord> ClassifyBySequence(List<AttendanceRecord> records) => records.GroupBy(r => new { r.UserId, Day = r.Timestamp.Date }).SelectMany(group => group.OrderBy(r => r.Timestamp).Select(record => record with { Type = record.Punch == 1 || record.Status is 1 or 3 or 5 ? "Salida" : "Entrada" })).OrderBy(r => r.Timestamp).ToList();
    static string Text(byte[] b, int start, int length) => Encoding.ASCII.GetString(b, start, length).Trim('\0', ' ');
    static DateTime DecodeTime(byte[] b, int p) { if (p + 4 > b.Length) return DateTime.MinValue; var value = BitConverter.ToUInt32(b, p); var second = (int)(value % 60); value /= 60; var minute = (int)(value % 60); value /= 60; var hour = (int)(value % 24); value /= 24; var day = (int)(value % 31) + 1; value /= 31; var month = (int)(value % 12) + 1; value /= 12; var year = (int)value + 2000; if (year > DateTime.Now.Year + 1 || year < 2000) year = DateTime.Now.Year; try { return new DateTime(year, month, day, hour, minute, second); } catch { return DateTime.MinValue; } }
    static uint EncodeTime(DateTime value) { var days = (value.Year % 100) * 12 * 31 + (value.Month - 1) * 31 + value.Day - 1; return (uint)(days * 86400 + (value.Hour * 60 + value.Minute) * 60 + value.Second); }
    Packet Send(ushort command, byte[] payload) { if (stream is null) throw new InvalidOperationException("No hay conexión con el dispositivo."); stream.Write(Build(command, payload, session, reply++)); return ReadPacket(); }
    Packet Receive() => ReadPacket();
    Packet ReadPacket() { if (stream is null) throw new InvalidOperationException("No hay conexión con el dispositivo."); var tcpHeader = ReadExact(8); if (BitConverter.ToUInt16(tcpHeader, 0) != 0x5050 || BitConverter.ToUInt16(tcpHeader, 2) != 0x7d82) throw new IOException("Respuesta TCP no válida del dispositivo."); var totalLength = BitConverter.ToInt32(tcpHeader, 4); if (totalLength < 8 || totalLength > 10_000_000) throw new IOException("Tamaño de respuesta no válido."); var body = ReadExact(totalLength); return new Packet(BitConverter.ToUInt16(body, 0), body.Length > 8 ? body[8..] : Array.Empty<byte>(), BitConverter.ToUInt16(body, 2), BitConverter.ToUInt16(body, 4), BitConverter.ToUInt16(body, 6)); }
    byte[] ReadExact(int count) { var buffer = new byte[count]; var offset = 0; while (offset < count) { var n = stream!.Read(buffer, offset, count - offset); if (n == 0) throw new IOException("El dispositivo cerró la conexión."); offset += n; } return buffer; }
    static byte[] Build(ushort command, byte[] payload, ushort session, ushort reply) { var body = new byte[8 + payload.Length]; Buffer.BlockCopy(BitConverter.GetBytes(command), 0, body, 0, 2); Buffer.BlockCopy(BitConverter.GetBytes(CalculateChecksum(command, payload, session, reply)), 0, body, 2, 2); Buffer.BlockCopy(BitConverter.GetBytes(session), 0, body, 4, 2); Buffer.BlockCopy(BitConverter.GetBytes(reply), 0, body, 6, 2); if (payload.Length > 0) Buffer.BlockCopy(payload, 0, body, 8, payload.Length); var packet = new byte[16 + payload.Length]; Buffer.BlockCopy(BitConverter.GetBytes((ushort)0x5050), 0, packet, 0, 2); Buffer.BlockCopy(BitConverter.GetBytes((ushort)0x7d82), 0, packet, 2, 2); Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, packet, 4, 4); Buffer.BlockCopy(body, 0, packet, 8, body.Length); return packet; }
    static ushort CalculateChecksum(ushort command, byte[] payload, ushort session, ushort reply) { var bytes = new byte[8 + payload.Length]; Buffer.BlockCopy(BitConverter.GetBytes(command), 0, bytes, 0, 2); Buffer.BlockCopy(BitConverter.GetBytes(session), 0, bytes, 4, 2); Buffer.BlockCopy(BitConverter.GetBytes(reply), 0, bytes, 6, 2); if (payload.Length > 0) Buffer.BlockCopy(payload, 0, bytes, 8, payload.Length); uint sum = 0; for (int i = 0; i + 1 < bytes.Length; i += 2) sum += BitConverter.ToUInt16(bytes, i); if ((bytes.Length & 1) != 0) sum += bytes[^1]; sum = (sum & 0xffff) + (sum >> 16); sum = (sum & 0xffff) + (sum >> 16); return (ushort)~sum; }
    public void Dispose() { stream?.Dispose(); tcp?.Dispose(); }
    readonly record struct Packet(ushort Command, byte[] Payload, ushort Checksum, ushort Session, ushort Reply);
}
