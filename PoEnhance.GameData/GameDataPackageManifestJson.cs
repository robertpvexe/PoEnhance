namespace PoEnhance.GameData;

public static class GameDataPackageManifestJson
{
    public static string Serialize(GameDataPackageManifest manifest)
    {
        return GameDataPackageJson.SerializeManifest(manifest);
    }

    public static GameDataPackageManifest? Deserialize(string json)
    {
        return GameDataPackageJson.DeserializeManifest(json);
    }

    public static System.Text.Json.JsonSerializerOptions CreateSerializerOptions()
    {
        return GameDataPackageJson.CreateSerializerOptions();
    }
}
