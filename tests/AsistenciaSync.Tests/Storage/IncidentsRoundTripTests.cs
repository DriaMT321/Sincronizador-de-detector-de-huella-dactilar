using System.Text;
using AsistenciaSync.Configuration;
using AsistenciaSync.Services;

namespace AsistenciaSync.Tests.Storage;

public sealed class IncidentsRoundTripTests : IDisposable
{
    readonly string folder;
    readonly AppSettings settings;

    public IncidentsRoundTripTests()
    {
        folder = Path.Combine(Path.GetTempPath(), "asistenciasync-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        settings = new AppSettings { CsvFolder = folder };
    }

    static readonly DateTime Day = new(2026, 3, 10);

    [Fact]
    public void Guarda_y_recupera_una_incidencia_por_tramo()
    {
        CsvStore.SaveIncident(settings, "7", Day, "Permiso", "cita médica", absence: false, lateness: false, permission: true, permissionHours: 2, segment: 1);

        var incident = Assert.Single(CsvStore.ReadIncidents(settings, Day.AddDays(-1), Day.AddDays(1)));
        Assert.Equal("7", incident.EmployeeId);
        Assert.Equal(1, incident.Segment);
        Assert.Equal(120, incident.PermissionMinutes);
        Assert.True(incident.JustifiesPermission);
    }

    [Fact]
    public void Dia_completo_y_tramo_conviven_como_incidencias_distintas()
    {
        CsvStore.SaveIncident(settings, "7", Day, "Enfermedad", "", absence: true, lateness: false, permission: false);
        CsvStore.SaveIncident(settings, "7", Day, "Permiso", "", absence: false, lateness: false, permission: true, segment: 2);

        var incidents = CsvStore.ReadIncidents(settings, Day.AddDays(-1), Day.AddDays(1));
        Assert.Equal(2, incidents.Count);
        Assert.Contains(incidents, x => x.Segment is null && x.JustifiesAbsence);
        Assert.Contains(incidents, x => x.Segment == 2 && x.JustifiesPermission);
    }

    [Fact]
    public void Lee_formato_anterior_sin_columna_tramo_como_dia_completo()
    {
        var path = Path.Combine(folder, "incidencias.csv");
        var content = "sep=;\r\n" +
                      "\"ID\";\"ID empleado\";\"Fecha\";\"Tipo\";\"Motivo\";\"Justifica ausencia\";\"Justifica tardanza\";\"Justifica permiso\";\"Horas permiso\"\r\n" +
                      "\"1\";\"7\";\"2026-03-10\";\"Enfermedad\";\"reposo\";\"1\";\"0\";\"0\";\"0\"\r\n";
        File.WriteAllText(path, content, new UnicodeEncoding(false, true));

        var incident = Assert.Single(CsvStore.ReadIncidents(settings, Day.AddDays(-1), Day.AddDays(1)));
        Assert.Null(incident.Segment);
        Assert.True(incident.JustifiesAbsence);
    }

    [Fact]
    public void DeleteIncident_elimina_solo_el_tramo_indicado()
    {
        CsvStore.SaveIncident(settings, "7", Day, "Enfermedad", "", absence: true, lateness: false, permission: false);
        CsvStore.SaveIncident(settings, "7", Day, "Permiso", "", absence: false, lateness: false, permission: true, segment: 2);

        CsvStore.DeleteIncident(settings, "7", Day, 2);

        var incident = Assert.Single(CsvStore.ReadIncidents(settings, Day.AddDays(-1), Day.AddDays(1)));
        Assert.Null(incident.Segment);
    }

    public void Dispose()
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
    }
}
