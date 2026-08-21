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
