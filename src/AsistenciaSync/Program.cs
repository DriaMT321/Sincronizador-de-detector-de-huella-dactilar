using AsistenciaSync.Services;
using AsistenciaSync.UI;

namespace AsistenciaSync;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--test-device", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var device = new ZkDeviceClient("192.168.0.201", 4370);
                var deviceTime = device.ReadDeviceTime();
                var records = device.DownloadAttendance();
                Console.WriteLine($"DEVICE_TIME={deviceTime:O}");
                Console.WriteLine($"DEVICE_OK records={records.Count}");
                foreach (var r in records.Take(20)) Console.WriteLine($"{r.UserId}|{r.Name}|{r.Timestamp:O}|{r.Type}|{r.Status}|{r.Punch}");
            }
            catch (Exception ex) { Console.Error.WriteLine("DEVICE_ERROR: " + ex.Message); Environment.ExitCode = 1; }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
