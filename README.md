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
- `jornadas.csv`: días laborables y tipo de jornada asignado a cada empleado.
- `tipos_jornada.csv`: jornadas configurables con uno o más tramos de ingreso/salida y un horario de almuerzo opcional.
- `incidencias.csv`: enfermedades, permisos o inconvenientes justificados. La columna `Tramo` indica si la justificación aplica a todo el día (vacío) o a un tramo concreto; los archivos de versiones anteriores se leen sin problema.
- Los botones **Descargar detalle** y **Descargar resumen** crean los CSV del reporte únicamente cuando se solicitan.

Los archivos usan separador `;` y codificación Unicode para abrirse correctamente en Excel en español.

## Funcionamiento

Al abrir la aplicación, si el dispositivo ya está configurado, se sincronizan automáticamente las marcaciones nuevas. La información se conserva en CSV y el reporte se visualiza únicamente al pulsar **Ver Reporte**. No se generan archivos de reporte automáticamente.

Al comenzar un nuevo mes, el archivo `marcaciones.csv` del mes anterior se mueve automáticamente a `historial\AAAA-MM\`. Los reportes consultan tanto el archivo vigente como todo el historial mensual.

El reporte se filtra automáticamente al mes vigente, desde el primer día del mes hasta la fecha actual. Toma en cuenta:

- La primera marcación del día como entrada y acepta otra marcación válida solo después de 30 minutos; las repeticiones dentro de esos 30 minutos se ignoran como accidentales.
- Cada tipo de jornada puede contener tantos tramos como sean necesarios. Cada tramo usa una marcación de ingreso y otra de salida.
- **Horas de mañana y de tarde por separado.** El corte es la hora de inicio del almuerzo; si la jornada no tiene almuerzo configurado, el corte son las 12:00. En jornada doble el tramo 1 es la mañana, el tramo 2 la tarde y el hueco entre ambos se muestra como **descanso**.
- El almuerzo puede configurarse dentro de un tramo (también en jornada continua o de un solo tramo). Sus marcaciones son opcionales, pero el reporte siempre muestra la fila de salida y regreso del almuerzo.
- Una salida del día actual permanece pendiente mientras su hora todavía no haya vencido; el tramo en curso no genera deuda provisional.
- Los días laborables y horarios configurados por empleado.
- En **Personalización y mantenimiento → Trabajadores y dispositivo** se pueden seleccionar individualmente los días laborales de cada trabajador, incluyendo combinaciones como lunes–viernes o lunes–sábado.
- Tardanzas, salidas anticipadas, horas faltantes y horas extra (fuera de horario).
- **Ausencias justificadas:** neutralizan el día. Las horas justificadas no suman ni restan al total; solo quedan en el registro como “ausencias justificadas (N veces)”.
- **Ausencias sin justificar:** restan del total de horas trabajadas.

La ventana **Hacer Reporte** permite filtrar por nombre y fechas del mes vigente. El **resumen general** presenta, en dos columnas (*Debería marcar* / *Marcado*): horas de mañana, horas de tarde, TOTAL, ausencias justificadas y sin justificar, total de horas trabajadas, fuera de horario y el TOTAL FINAL. Los botones **Descargar detalle**, **Descargar resumen** y **Descargar PDF** exportan lo mostrado.

La ventana **Justificaciones / Faltas** lista, por empleado, los días y tramos del periodo que están sin justificar y permite justificar cada uno (día completo o un tramo concreto), programar faltas para fechas futuras y quitar incidencias registradas.

## Requisitos

1. Windows con .NET 8 Desktop Runtime.
2. El PC y el iClock 680 en la misma red.
3. Permiso de escritura en la carpeta CSV configurada.

## Estructura del proyecto

```text
AsistenciaSync/
├── src/
│   ├── AsistenciaSync.Core/     # Lógica de dominio (net8.0, sin WinForms)
│   │   ├── Configuration/       # Ajustes y persistencia local
│   │   ├── Models/              # Registros y configuraciones de dominio
│   │   └── Services/            # Reloj ZKTeco, CSV y cálculo de reportes
│   └── AsistenciaSync/          # Aplicación WinForms (net8.0-windows)
│       ├── Assets/              # LOGO.ico / LOGO.png
│       ├── UI/                  # Ventanas de WinForms
│       └── Program.cs
├── tests/
│   └── AsistenciaSync.Tests/    # Pruebas xUnit sobre AsistenciaSync.Core
├── Directory.Build.props        # Ajustes de compilación compartidos
├── global.json                  # Versión del SDK de .NET
├── AsistenciaSync.sln
└── README.md
```

El cálculo de asistencia vive en `AsistenciaSync.Core` y no depende de WinForms, por lo que
`ReportService.Build(ReportInputs, generatedAt, downloadFolder)` puede ejercitarse con datos en
memoria y un "hoy" fijo desde las pruebas.

## Compilar

Desde la carpeta del proyecto:

```powershell
dotnet build AsistenciaSync.sln -c Release
dotnet run --project src\AsistenciaSync\AsistenciaSync.csproj -c Release
```

## Pruebas

```powershell
dotnet test AsistenciaSync.sln
```

Para publicar el ejecutable:

```powershell
dotnet publish src\AsistenciaSync\AsistenciaSync.csproj -c Release -r win-x64 --self-contained false -o publish
```
