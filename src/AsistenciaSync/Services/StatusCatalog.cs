using System.Text.Json;
using AsistenciaSync.Configuration;
using AsistenciaSync.Models;

namespace AsistenciaSync.Services;

internal static class StatusCatalog
{
    static readonly StatusOption[] Defaults =
    {
        new() { Key = "Completo", Name = "Completo", Color = "#1B7E52" },
        new() { Key = "Ausente", Name = "Ausente", Color = "#BE3737" },
        new() { Key = "Incompleto", Name = "Incompleto", Color = "#C06A18" },
        new() { Key = "No laborable", Name = "No laborable", Color = "#6E7887" },
        new() { Key = "Festivo", Name = "Festivo", Color = "#6E7887" },
        new() { Key = "Tarde", Name = "Tarde", Color = "#BE7314" },
        new() { Key = "Salida anticipada", Name = "Salida anticipada", Color = "#BE7314" },
        new() { Key = "Tarde y salida anticipada", Name = "Tarde y salida anticipada", Color = "#BE7314" },
        new() { Key = "Ausencia justificada", Name = "Ausencia justificada", Color = "#416A9A" },
        new() { Key = "Salida justificada", Name = "Salida justificada", Color = "#416A9A" }
    };

    public static List<StatusOption> Load(AppSettings settings)
    {
        var path = Path.Combine(CsvStore.Folder(settings), "estados.json");
        try { if (File.Exists(path)) return JsonSerializer.Deserialize<List<StatusOption>>(File.ReadAllText(path)) ?? Defaults.Select(Clone).ToList(); } catch { }
        return Defaults.Select(Clone).ToList();
    }

    public static void Save(AppSettings settings, IEnumerable<StatusOption> statuses)
    {
        var path = Path.Combine(CsvStore.Folder(settings), "estados.json");
        File.WriteAllText(path, JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string Name(AppSettings settings, string key) => Load(settings).FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Name ?? key;
    static StatusOption Clone(StatusOption x) => new() { Key = x.Key, Name = x.Name, Color = x.Color };
}
