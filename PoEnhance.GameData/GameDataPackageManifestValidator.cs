namespace PoEnhance.GameData;

public static class GameDataPackageManifestValidator
{
    public static GameDataPackageManifestValidationResult Validate(GameDataPackageManifest? manifest)
    {
        var errors = new List<string>();

        if (manifest is null)
        {
            errors.Add("Manifest is required.");
            return new GameDataPackageManifestValidationResult(errors);
        }

        if (manifest.SchemaVersion < 1)
        {
            errors.Add("SchemaVersion must be 1 or greater.");
        }

        if (string.IsNullOrWhiteSpace(manifest.DataVersion))
        {
            errors.Add("DataVersion is required.");
        }

        if (!IsUtc(manifest.CreatedAtUtc))
        {
            errors.Add("CreatedAtUtc must be a UTC timestamp.");
        }

        if (manifest.Sources is null || manifest.Sources.Count == 0)
        {
            errors.Add("At least one source entry is required.");
            return new GameDataPackageManifestValidationResult(errors);
        }

        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < manifest.Sources.Count; index++)
        {
            var source = manifest.Sources[index];
            if (source is null)
            {
                errors.Add($"Sources[{index}] is required.");
                continue;
            }

            var sourceId = source.SourceId?.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                errors.Add($"Sources[{index}].SourceId is required.");
            }
            else if (!sourceIds.Add(sourceId))
            {
                errors.Add($"SourceId '{sourceId}' is duplicated.");
            }

            if (!IsUtc(source.RetrievedAtUtc))
            {
                errors.Add($"Sources[{index}].RetrievedAtUtc must be a UTC timestamp.");
            }
        }

        return new GameDataPackageManifestValidationResult(errors);
    }

    private static bool IsUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero;
    }
}
