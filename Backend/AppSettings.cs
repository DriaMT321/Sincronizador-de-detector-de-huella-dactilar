namespace AsistenciaSync.Backend;

public sealed class AppSettings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public string DeviceIp { get; set; } = "192.168.0.201";
    public int DevicePort { get; set; } = 4370;
    public bool SqlAuthentication { get; set; }
    public string SqlUser { get; set; } = "";
    public string SqlPassword { get; set; } = "";
    public TimeSpan EntryTime { get; set; } = new(8, 0, 0);
    public TimeSpan ExitTime { get; set; } = new(17, 0, 0);
    public DateTime ReportFrom { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime ReportTo { get; set; } = DateTime.Today;
    public bool SyncClock { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Database) && !string.IsNullOrWhiteSpace(DeviceIp);
}
