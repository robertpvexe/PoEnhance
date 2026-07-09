using System.Text.Json;

namespace PoEnhance.GameData;

public static class GameDataPackageManifestJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string Serialize(GameDataPackageManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    public static GameDataPackageManifest? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GameDataPackageManifest>(json, SerializerOptions);
    }

    public static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions(SerializerOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
    }
}
