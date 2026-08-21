using System.Data.Odbc;
using System.Globalization;

namespace AsistenciaSync.Backend;

public sealed record EmployeeOption(string Id, string Name);
public sealed record EmployeeSchedule(string EmployeeId, bool Monday, bool Tuesday, bool Wednesday, bool Thursday, bool Friday, bool Saturday, bool Sunday, TimeSpan Entry, TimeSpan Exit)
{
    public bool WorksOn(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Monday, DayOfWeek.Tuesday => Tuesday, DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday => Thursday, DayOfWeek.Friday => Friday, DayOfWeek.Saturday => Saturday,
        _ => Sunday
    };
}

public sealed record DailyIncident(long Id, string EmployeeId, DateTime Date, string Type, string Reason, bool JustifiesAbsence, bool JustifiesLateness);

public static class AttendanceConfigurationStore
{
    public static List<EmployeeOption> ReadEmployees(AppSettings settings)
    {
        using var cn = Open(settings); EnsureTables(cn);
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT EmpleadoId, Nombre FROM dbo.Empleados WHERE Activo = 1 ORDER BY EmpleadoId;";
        using var rd = cmd.ExecuteReader(); var result = new List<EmployeeOption>();
        while (rd.Read()) result.Add(new EmployeeOption(rd.GetString(0), rd.IsDBNull(1) ? rd.GetString(0) : rd.GetString(1)));
        return result;
    }

    public static Dictionary<string, EmployeeSchedule> ReadSchedules(AppSettings settings)
    {
        using var cn = Open(settings); EnsureTables(cn);
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT EmpleadoId,Lunes,Martes,Miercoles,Jueves,Viernes,Sabado,Domingo,HoraEntrada,HoraSalida FROM dbo.EmpleadoJornadas;";
        using var rd = cmd.ExecuteReader(); var result = new Dictionary<string, EmployeeSchedule>(StringComparer.OrdinalIgnoreCase);
        while (rd.Read()) result[rd.GetString(0)] = new EmployeeSchedule(rd.GetString(0), rd.GetBoolean(1), rd.GetBoolean(2), rd.GetBoolean(3), rd.GetBoolean(4), rd.GetBoolean(5), rd.GetBoolean(6), rd.GetBoolean(7), ParseTime(rd.GetString(8), new(8, 0, 0)), ParseTime(rd.GetString(9), new(17, 0, 0)));
        return result;
    }

    public static void SaveSchedule(AppSettings settings, EmployeeSchedule schedule)
    {
        using var cn = Open(settings); EnsureTables(cn);
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"UPDATE dbo.EmpleadoJornadas SET Lunes=?,Martes=?,Miercoles=?,Jueves=?,Viernes=?,Sabado=?,Domingo=?,HoraEntrada=?,HoraSalida=? WHERE EmpleadoId=?;
IF @@ROWCOUNT=0 INSERT INTO dbo.EmpleadoJornadas (EmpleadoId,Lunes,Martes,Miercoles,Jueves,Viernes,Sabado,Domingo,HoraEntrada,HoraSalida) VALUES (?,?,?,?,?,?,?,?,?,?);";
        AddScheduleParameters(cmd, schedule, false); AddScheduleParameters(cmd, schedule, true); cmd.ExecuteNonQuery();
    }

    public static List<DailyIncident> ReadIncidents(AppSettings settings, DateTime from, DateTime to)
    {
        using var cn = Open(settings); EnsureTables(cn);
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT Id,EmpleadoId,Fecha,Tipo,Motivo,JustificaAusencia,JustificaTardanza FROM dbo.IncidenciasAsistencia WHERE Fecha>=? AND Fecha<=? ORDER BY Fecha,EmpleadoId;";
        cmd.Parameters.Add("from", OdbcType.Date).Value = from.Date; cmd.Parameters.Add("to", OdbcType.Date).Value = to.Date;
        using var rd = cmd.ExecuteReader(); var result = new List<DailyIncident>();
        while (rd.Read()) result.Add(new DailyIncident(rd.GetInt64(0), rd.GetString(1), rd.GetDateTime(2), rd.GetString(3), rd.IsDBNull(4) ? "" : rd.GetString(4), rd.GetBoolean(5), rd.GetBoolean(6)));
        return result;
    }

    public static void SaveIncident(AppSettings settings, string employeeId, DateTime date, string type, string reason, bool absence, bool lateness)
    {
        using var cn = Open(settings); EnsureTables(cn);
        using var cmd = cn.CreateCommand(); cmd.CommandText = @"UPDATE dbo.IncidenciasAsistencia SET Tipo=?,Motivo=?,JustificaAusencia=?,JustificaTardanza=? WHERE EmpleadoId=? AND Fecha=?;
IF @@ROWCOUNT=0 INSERT INTO dbo.IncidenciasAsistencia (EmpleadoId,Fecha,Tipo,Motivo,JustificaAusencia,JustificaTardanza) VALUES (?,?,?,?,?,?);";
        cmd.Parameters.Add("p1", OdbcType.VarChar, 40).Value = type; cmd.Parameters.Add("p2", OdbcType.VarChar, 500).Value = reason;
        cmd.Parameters.Add("p3", OdbcType.Bit).Value = absence; cmd.Parameters.Add("p4", OdbcType.Bit).Value = lateness;
        cmd.Parameters.Add("p5", OdbcType.VarChar, 30).Value = employeeId; cmd.Parameters.Add("p6", OdbcType.Date).Value = date.Date;
        cmd.Parameters.Add("p7", OdbcType.VarChar, 30).Value = employeeId; cmd.Parameters.Add("p8", OdbcType.Date).Value = date.Date;
        cmd.Parameters.Add("p9", OdbcType.VarChar, 40).Value = type; cmd.Parameters.Add("p10", OdbcType.VarChar, 500).Value = reason;
        cmd.Parameters.Add("p11", OdbcType.Bit).Value = absence; cmd.Parameters.Add("p12", OdbcType.Bit).Value = lateness; cmd.ExecuteNonQuery();
    }

    static void AddScheduleParameters(OdbcCommand cmd, EmployeeSchedule s, bool insert)
    {
        var values = new object[] { s.Monday, s.Tuesday, s.Wednesday, s.Thursday, s.Friday, s.Saturday, s.Sunday, s.Entry.ToString(@"hh\:mm"), s.Exit.ToString(@"hh\:mm") };
        if (insert) cmd.Parameters.Add(new OdbcParameter { Value = s.EmployeeId });
        foreach (var value in values) cmd.Parameters.Add(new OdbcParameter { Value = value });
        if (!insert) cmd.Parameters.Add(new OdbcParameter { Value = s.EmployeeId });
    }

    static OdbcConnection Open(AppSettings s)
    {
        var cs = s.SqlAuthentication ? $"Driver={{ODBC Driver 17 for SQL Server}};Server={s.Server};Database={s.Database};Uid={s.SqlUser};Pwd={s.SqlPassword};Trusted_Connection=No;TrustServerCertificate=Yes;" : $"Driver={{ODBC Driver 17 for SQL Server}};Server={s.Server};Database={s.Database};Trusted_Connection=Yes;TrustServerCertificate=Yes;";
        var cn = new OdbcConnection(cs); cn.Open(); return cn;
    }

    static void EnsureTables(OdbcConnection cn)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = @"
IF OBJECT_ID('dbo.EmpleadoJornadas','U') IS NULL CREATE TABLE dbo.EmpleadoJornadas (EmpleadoId VARCHAR(30) NOT NULL PRIMARY KEY,Lunes BIT NOT NULL,Martes BIT NOT NULL,Miercoles BIT NOT NULL,Jueves BIT NOT NULL,Viernes BIT NOT NULL,Sabado BIT NOT NULL,Domingo BIT NOT NULL,HoraEntrada VARCHAR(5) NOT NULL,HoraSalida VARCHAR(5) NOT NULL);
IF OBJECT_ID('dbo.IncidenciasAsistencia','U') IS NULL CREATE TABLE dbo.IncidenciasAsistencia (Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,EmpleadoId VARCHAR(30) NOT NULL,Fecha DATE NOT NULL,Tipo VARCHAR(40) NOT NULL,Motivo VARCHAR(500) NULL,JustificaAusencia BIT NOT NULL,JustificaTardanza BIT NOT NULL, CONSTRAINT UQ_Incidencia_Empleado_Fecha UNIQUE (EmpleadoId,Fecha));"; cmd.ExecuteNonQuery();
    }

    static TimeSpan ParseTime(string value, TimeSpan fallback) => TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}
