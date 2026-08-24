# AsistenciaSync — iClock 680 → CSV

Aplicación Windows para sincronizar las marcaciones del reloj ZKTeco iClock 680 y almacenarlas en archivos CSV compatibles con Excel.

## Configuración inicial

- Dispositivo: `192.168.0.201`
- Puerto: `4370`
- Carpeta CSV predeterminada: `C:\Users\Pc\Downloads\nibol`
- No requiere SQL Server ni ODBC.

Desde **Configuración del sistema** se puede cambiar la IP, el puerto, la carpeta de destino y activar la sincronización de fecha y hora con el PC.

## Archivos generados

La aplicación crea estos archivos en la carpeta configurada:

- `marcaciones.csv`: registros del mes vigente, sin duplicados.
- `historial\AAAA-MM\marcaciones.csv`: registros archivados de cada mes cerrado.
- `empleados.csv`: usuarios y nombres descargados del dispositivo.
- `jornadas.csv`: días y horarios configurados por empleado. Permite jornada `Continua` (2 marcaciones) y `Discontinua` (4 marcaciones: ingreso, salida a descanso, regreso y salida final).
- `incidencias.csv`: enfermedades, permisos o inconvenientes justificados.
- Los botones **Descargar detalle** y **Descargar resumen** crean los CSV del reporte únicamente cuando se solicitan.

Los archivos usan separador `;` y codificación Unicode para abrirse correctamente en Excel en español.

## Funcionamiento

Al abrir la aplicación, si el dispositivo ya está configurado, se sincronizan automáticamente las marcaciones nuevas. La información se conserva en CSV y el reporte se visualiza únicamente al pulsar **Ver Reporte**. No se generan archivos de reporte automáticamente.

Al comenzar un nuevo mes, el archivo `marcaciones.csv` del mes anterior se mueve automáticamente a `historial\AAAA-MM\`. Los reportes consultan tanto el archivo vigente como todo el historial mensual.

El reporte se filtra automáticamente al mes vigente, desde el primer día del mes hasta la fecha actual. Toma en cuenta:

- La primera marcación del día como entrada y acepta otra marcación solo después de cinco minutos; las repeticiones dentro de esos cinco minutos se ignoran como errores.
- En jornada continua usa las dos primeras marcaciones válidas; en jornada discontinua usa hasta cuatro.
- Los días laborables y horarios configurados por empleado.
- Tardanzas, salidas anticipadas, horas faltantes y horas extra.
- Incidencias justificadas de ausencia o tardanza.

La ventana **Ver Reporte** permite filtrar por nombre, fechas y estado. Cada fila tiene un botón **Editar**; los cambios se aplican a la vista y se incluyen en el CSV cuando se descarga.

## Requisitos

1. Windows con .NET 8 Desktop Runtime.
2. El PC y el iClock 680 en la misma red.
3. Permiso de escritura en la carpeta CSV configurada.

## Estructura del proyecto

```text
AsistenciaSync/
├── src/
│   └── AsistenciaSync/
│       ├── Configuration/   # Ajustes y persistencia local
│       ├── Models/          # Registros y configuraciones de dominio
│       ├── Services/        # Reloj ZKTeco, CSV y reportes
│       ├── UI/              # Ventanas de WinForms
│       ├── Program.cs
│       └── AsistenciaSync.csproj
├── tests/
│   └── AsistenciaSync.Tests/
├── AsistenciaSync.sln
└── README.md
```

## Compilar

Desde la carpeta del proyecto:

```powershell
dotnet build AsistenciaSync.sln -c Release
dotnet run --project src\AsistenciaSync\AsistenciaSync.csproj -c Release
```

Para publicar el ejecutable:

```powershell
dotnet publish src\AsistenciaSync\AsistenciaSync.csproj -c Release -r win-x64 --self-contained false -o publish
```
