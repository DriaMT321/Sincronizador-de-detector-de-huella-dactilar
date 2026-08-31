namespace AsistenciaSync.Configuration;

public sealed class AppSettings
{
    public string DeviceIp { get; set; } = "192.168.0.201";
    public int DevicePort { get; set; } = 4370;
    public string CsvFolder { get; set; } = string.Empty;
    public DateTime ReportFrom { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime ReportTo { get; set; } = DateTime.Today;
    public bool SyncClock { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(DeviceIp) && !string.IsNullOrWhiteSpace(CsvFolder);
}
