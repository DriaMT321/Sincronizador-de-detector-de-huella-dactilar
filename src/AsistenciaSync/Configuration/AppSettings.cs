namespace AsistenciaSync.Configuration;

public sealed class AppSettings
{
    public string DeviceIp { get; set; } = "192.168.0.201";
    public int DevicePort { get; set; } = 4370;
    public string CsvFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "nibol");
    public TimeSpan EntryTime { get; set; } = new(8, 0, 0);
    public TimeSpan ExitTime { get; set; } = new(17, 0, 0);
    public DateTime ReportFrom { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime ReportTo { get; set; } = DateTime.Today;
    public bool SyncClock { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(DeviceIp) && !string.IsNullOrWhiteSpace(CsvFolder);
}
