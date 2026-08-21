using System.Data.Odbc;

namespace AsistenciaSync.Backend;

internal static class SqlStore
{
    public static void SaveEmployees(string server, string database, IReadOnlyDictionary<string, string> users, bool useSqlAuthentication, string sqlUser, string sqlPassword)
    {
        if (users.Count == 0) return; using var cn = Open(server, database, useSqlAuthentication, sqlUser, sqlPassword); EnsureEmployees(cn);
        foreach (var user in users) { using var cmd = cn.CreateCommand(); cmd.CommandText = "UPDATE dbo.Empleados SET Nombre = ?, Activo = 1 WHERE EmpleadoId = ?; IF @@ROWCOUNT = 0 INSERT INTO dbo.Empleados (EmpleadoId, Nombre) VALUES (?, ?);"; cmd.Parameters.Add("p1", OdbcType.VarChar, 150).Value = user.Value; cmd.Parameters.Add("p2", OdbcType.VarChar, 30).Value = user.Key; cmd.Parameters.Add("p3", OdbcType.VarChar, 30).Value = user.Key; cmd.Parameters.Add("p4", OdbcType.VarChar, 150).Value = user.Value; cmd.ExecuteNonQuery(); }
    }

    public static void TestConnection(string server, string database, bool useSqlAuthentication, string sqlUser, string sqlPassword) { using var cn = Open(server, database, useSqlAuthentication, sqlUser, sqlPassword); }

    public static int Save(string server, string database, IReadOnlyCollection<AttendanceRecord> records, bool useSqlAuthentication, string sqlUser, string sqlPassword)
    {
        if (records.Count == 0) return 0; using var cn = Open(server, database, useSqlAuthentication, sqlUser, sqlPassword); EnsureSchema(cn);
        foreach (var user in records.Where(r => !string.IsNullOrWhiteSpace(r.Name) && r.Name != r.UserId).GroupBy(r => r.UserId).Select(g => g.First())) { using var updateName = cn.CreateCommand(); updateName.CommandText = "UPDATE dbo.Marcaciones SET Nombre = ? WHERE EmpleadoId = ? AND Origen = 'iClock';"; updateName.Parameters.Add("p1", OdbcType.VarChar, 150).Value = user.Name; updateName.Parameters.Add("p2", OdbcType.VarChar, 30).Value = user.UserId; updateName.ExecuteNonQuery(); }
        var inserted = 0; foreach (var r in records) { using var cmd = cn.CreateCommand(); cmd.CommandText = "INSERT INTO dbo.Marcaciones (EmpleadoId, Nombre, FechaHora, Tipo, Origen) SELECT ?, ?, ?, ?, ? WHERE NOT EXISTS (SELECT 1 FROM dbo.Marcaciones WHERE EmpleadoId = ? AND FechaHora = ? AND Origen = ?);"; cmd.Parameters.Add("p1", OdbcType.VarChar, 30).Value = r.UserId; cmd.Parameters.Add("p2", OdbcType.VarChar, 150).Value = r.Name; cmd.Parameters.Add("p3", OdbcType.DateTime).Value = r.Timestamp; cmd.Parameters.Add("p4", OdbcType.VarChar, 30).Value = r.Type; cmd.Parameters.Add("p5", OdbcType.VarChar, 50).Value = r.Source; cmd.Parameters.Add("p6", OdbcType.VarChar, 30).Value = r.UserId; cmd.Parameters.Add("p7", OdbcType.DateTime).Value = r.Timestamp; cmd.Parameters.Add("p8", OdbcType.VarChar, 50).Value = r.Source; inserted += cmd.ExecuteNonQuery(); }
        return inserted;
    }

    static OdbcConnection Open(string server, string database, bool sqlAuth, string user, string password)
    { var cs = sqlAuth ? $"Driver={{ODBC Driver 17 for SQL Server}};Server={server};Database={database};Uid={user};Pwd={password};Trusted_Connection=No;TrustServerCertificate=Yes;" : $"Driver={{ODBC Driver 17 for SQL Server}};Server={server};Database={database};Trusted_Connection=Yes;TrustServerCertificate=Yes;"; var cn = new OdbcConnection(cs); cn.Open(); return cn; }
    static void EnsureEmployees(OdbcConnection cn) { using var cmd = cn.CreateCommand(); cmd.CommandText = "IF OBJECT_ID('dbo.Empleados','U') IS NULL CREATE TABLE dbo.Empleados (EmpleadoId VARCHAR(30) NOT NULL PRIMARY KEY, Nombre VARCHAR(150) NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT 1);"; cmd.ExecuteNonQuery(); }
    static void EnsureSchema(OdbcConnection cn) { using var cmd = cn.CreateCommand(); cmd.CommandText = @"IF OBJECT_ID('dbo.Marcaciones','U') IS NULL BEGIN CREATE TABLE dbo.Marcaciones (Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Marcaciones PRIMARY KEY, EmpleadoId VARCHAR(30) NOT NULL, Nombre VARCHAR(150) NULL, FechaHora DATETIME2 NULL, Tipo VARCHAR(30) NULL, Origen VARCHAR(50) NULL); END"; cmd.ExecuteNonQuery(); }
}
