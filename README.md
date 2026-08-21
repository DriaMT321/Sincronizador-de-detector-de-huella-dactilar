# Sincronizador iClock 680 → SQL Server

Aplicación Windows con botón **Sincronizar datos**.

Configuración inicial:

- Dispositivo: `192.168.0.201`
- Puerto: `4370`
- SQL Server: `DESKTOP-3Q2MHNB`
- Base de datos: `Asistencias`
- Autenticación: Windows

## Requisitos

1. Tener creada la base de datos `Asistencias`.
2. Tener instalado **ODBC Driver 17 for SQL Server**.
3. Ejecutar la aplicación con un usuario de Windows que tenga permiso de escritura en la base de datos.
4. Estar en la misma red que el reloj.

Las tablas también se crean automáticamente desde la aplicación al guardar datos o configuraciones. Si prefieres prepararlas manualmente, utiliza este script en SQL Server:

```sql
USE [Asistencias];
GO

IF OBJECT_ID(N'dbo.Empleados', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Empleados
    (
        EmpleadoId VARCHAR(30) NOT NULL CONSTRAINT PK_Empleados PRIMARY KEY,
        Nombre VARCHAR(150) NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT 1
    );
END;
GO

IF OBJECT_ID(N'dbo.Marcaciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Marcaciones
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Marcaciones PRIMARY KEY,
        EmpleadoId VARCHAR(30) NOT NULL,
        Nombre VARCHAR(150) NULL,
        FechaHora DATETIME2 NULL,
        Tipo VARCHAR(30) NULL,
        Origen VARCHAR(50) NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.EmpleadoJornadas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmpleadoJornadas
    (
        EmpleadoId VARCHAR(30) NOT NULL CONSTRAINT PK_EmpleadoJornadas PRIMARY KEY,
        Lunes BIT NOT NULL,
        Martes BIT NOT NULL,
        Miercoles BIT NOT NULL,
        Jueves BIT NOT NULL,
        Viernes BIT NOT NULL,
        Sabado BIT NOT NULL,
        Domingo BIT NOT NULL,
        HoraEntrada VARCHAR(5) NOT NULL,
        HoraSalida VARCHAR(5) NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.IncidenciasAsistencia', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IncidenciasAsistencia
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IncidenciasAsistencia PRIMARY KEY,
        EmpleadoId VARCHAR(30) NOT NULL,
        Fecha DATE NOT NULL,
        Tipo VARCHAR(40) NOT NULL,
        Motivo VARCHAR(500) NULL,
        JustificaAusencia BIT NOT NULL,
        JustificaTardanza BIT NOT NULL,
        CONSTRAINT UQ_Incidencia_Empleado_Fecha UNIQUE (EmpleadoId, Fecha)
    );
END;
GO
```

La aplicación no utiliza las columnas eliminadas `Punch`, `Estado`, `FechaImportacion` ni `HashRegistro`.

## Compilar

Desde esta carpeta:

```powershell
dotnet build -c Release
dotnet run -c Release
```
