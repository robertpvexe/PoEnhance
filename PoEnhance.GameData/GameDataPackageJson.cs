using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoEnhance.GameData;

public static class GameDataPackageJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static string Serialize(GameDataPackage package)
    {
        return JsonSerializer.Serialize(package, SerializerOptions);
    }

    public static GameDataPackage? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<GameDataPackage>(json, SerializerOptions);
    }

    public static string SerializeManifest(GameDataPackageManifest manifest)
    {
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    public static GameDataPackageManifest? DeserializeManifest(string json)
    {
        return JsonSerializer.Deserialize<GameDataPackageManifest>(json, SerializerOptions);
    }

    public static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions(SerializerOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter<ModifierGenerationType>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<ItemPropertyTarget>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<ItemPropertyOperation>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<ItemPropertyApplicability>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<ItemPropertySemanticEvidenceMethod>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));

        return options;
    }
}
