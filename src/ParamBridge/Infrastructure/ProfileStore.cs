using System.Text.Json;
using ParamBridge.Core.Models;

namespace ParamBridge.Infrastructure;

/// <summary>Saves profiles as one JSON file each in %AppData%\ParamBridge\Profiles.</summary>
public static class ProfileStore
{
    private static readonly char[] Forbidden = { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', '/', '*', '"' };
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string Folder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ParamBridge", "Profiles");

    public static string? Validate(string name, bool mustBeNew)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Profile name cannot be empty.";
        if (name.IndexOfAny(Forbidden) >= 0) return "Profile name contains characters that are not allowed.";
        if (mustBeNew && File.Exists(PathFor(name))) return "A profile with this name already exists.";
        return null;
    }

    public static IReadOnlyList<Profile> LoadAll()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<Profile>();
        var list = new List<Profile>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Json);
                if (p is not null) list.Add(p);
            }
            catch (Exception ex) { Log.Error($"Invalid profile file {file}", ex); }
        }
        return list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static void Save(Profile profile)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathFor(profile.Name), JsonSerializer.Serialize(profile, Json));
    }

    public static void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
    }

    public static Profile Import(string file) =>
        JsonSerializer.Deserialize<Profile>(File.ReadAllText(file), Json)
        ?? throw new InvalidDataException("The selected file is not a valid ParamBridge profile.");

    public static void Export(Profile profile, string file) =>
        File.WriteAllText(file, JsonSerializer.Serialize(profile, Json));

    private static string PathFor(string name) => Path.Combine(Folder, name + ".json");
}
