using AsistenciaSync.Configuration;
using AsistenciaSync.Models;
using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Storage;

public sealed class CsvStoreRoundTripTests : IDisposable
{
    readonly string folder;
    readonly AppSettings settings;

    public CsvStoreRoundTripTests()
    {
        folder = Path.Combine(Path.GetTempPath(), "asistenciasync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        settings = new AppSettings { CsvFolder = folder };
    }

    static AttendanceRecord Record(DateTime timestamp) =>
        new("1", "Ana", timestamp, "in", "device", 0, 0, "");

    [Fact]
    public void Save_persiste_las_marcaciones_y_ReadPunches_las_recupera()
    {
        var today = DateTime.Today;
        var records = new[] { Record(today.AddHours(8)), Record(today.AddHours(17)) };

        var inserted = CsvStore.Save(settings, records);
        var punches = CsvStore.ReadPunches(settings, today.AddDays(-1), today.AddDays(1));

        Assert.Equal(2, inserted);
        Assert.Equal(2, punches.Count);
    }

    [Fact]
    public void Save_no_duplica_marcaciones_ya_almacenadas()
    {
        var today = DateTime.Today;
        var records = new[] { Record(today.AddHours(8)), Record(today.AddHours(17)) };

        CsvStore.Save(settings, records);
        var insertedSecondTime = CsvStore.Save(settings, records);
        var punches = CsvStore.ReadPunches(settings, today.AddDays(-1), today.AddDays(1));

        Assert.Equal(0, insertedSecondTime);
        Assert.Equal(2, punches.Count);
    }

    [Fact]
    public void Las_marcaciones_de_meses_anteriores_se_archivan_en_historial()
    {
        var today = DateTime.Today;
        var lastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1).AddDays(2).AddHours(8);
        var records = new[] { Record(today.AddHours(8)), Record(lastMonth) };

        // La rotación ocurre al inicio de cada Save; el segundo Save archiva el mes anterior.
        CsvStore.Save(settings, records);
        CsvStore.Save(settings, Array.Empty<AttendanceRecord>());

        var archive = Path.Combine(folder, "historial", lastMonth.ToString("yyyy-MM"), "marcaciones.csv");
        Assert.True(File.Exists(archive), $"Se esperaba el archivo de historial en {archive}");

        var allPunches = CsvStore.ReadPunches(settings, lastMonth.AddMonths(-1), today.AddDays(1));
        Assert.Equal(2, allPunches.Count);
    }

    public void Dispose()
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
    }
}
