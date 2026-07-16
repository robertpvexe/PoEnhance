namespace PoEnhance.GameData;

public static class GameDataPackageManifestValidator
{
    public static GameDataValidationResult Validate(GameDataPackageManifest? manifest)
    {
        var errors = new List<GameDataValidationError>();

        if (manifest is null)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestRequired,
                "manifest",
                "Manifest is required."));
            return new GameDataValidationResult(errors);
        }

        if (manifest.SchemaVersion < 1)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestSchemaVersionInvalid,
                "manifest.schemaVersion",
                "SchemaVersion must be 1 or greater."));
        }

        if (string.IsNullOrWhiteSpace(manifest.DataVersion))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestDataVersionRequired,
                "manifest.dataVersion",
                "DataVersion is required."));
        }

        if (!IsUtc(manifest.CreatedAtUtc))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestCreatedAtUtcNotUtc,
                "manifest.createdAtUtc",
                "CreatedAtUtc must be a UTC timestamp."));
        }

        ValidateReviewedItemPropertySemantics(manifest.ReviewedItemPropertySemantics, errors);

        if (manifest.Sources is null || manifest.Sources.Count == 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestSourcesRequired,
                "manifest.sources",
                "At least one source entry is required."));
            return new GameDataValidationResult(errors);
        }

        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < manifest.Sources.Count; index++)
        {
            var source = manifest.Sources[index];
            if (source is null)
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ManifestSourceRequired,
                    $"manifest.sources[{index}]",
                    $"Sources[{index}] is required."));
                continue;
            }

            var sourceId = source.SourceId?.Trim();
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ManifestSourceIdRequired,
                    $"manifest.sources[{index}].sourceId",
                    $"Sources[{index}].SourceId is required."));
            }
            else if (!sourceIds.Add(sourceId))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ManifestSourceIdDuplicate,
                    $"manifest.sources[{index}].sourceId",
                    $"SourceId '{sourceId}' is duplicated."));
            }

            if (!IsUtc(source.RetrievedAtUtc))
            {
                errors.Add(Error(
                    GameDataValidationErrorCodes.ManifestSourceRetrievedAtUtcNotUtc,
                    $"manifest.sources[{index}].retrievedAtUtc",
                    $"Sources[{index}].RetrievedAtUtc must be a UTC timestamp."));
            }
        }

        return new GameDataValidationResult(errors);
    }

    private static void ValidateReviewedItemPropertySemantics(
        GameDataPackageReviewedItemPropertySemanticInput? input,
        List<GameDataValidationError> errors)
    {
        if (input is null)
        {
            return;
        }

        const string path = "manifest.reviewedItemPropertySemantics";
        if (string.IsNullOrWhiteSpace(input.SourceId))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsSourceIdRequired,
                $"{path}.sourceId",
                "Reviewed item-property semantic SourceId is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Label))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsLabelRequired,
                $"{path}.label",
                "Reviewed item-property semantic Label is required."));
        }

        if (string.IsNullOrWhiteSpace(input.DisplayPath) || Path.IsPathRooted(input.DisplayPath))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsDisplayPathRequired,
                $"{path}.displayPath",
                "Reviewed item-property semantic DisplayPath must be a non-rooted display path."));
        }

        if (input.SizeBytes <= 0)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsSizeInvalid,
                $"{path}.sizeBytes",
                "Reviewed item-property semantic SizeBytes must be greater than zero."));
        }

        if (!IsSha256(input.Sha256))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsSha256Invalid,
                $"{path}.sha256",
                "Reviewed item-property semantic Sha256 must contain 64 hexadecimal characters."));
        }

        if (input.SchemaVersion < 1)
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsSchemaVersionInvalid,
                $"{path}.schemaVersion",
                "Reviewed item-property semantic SchemaVersion must be 1 or greater."));
        }

        if (string.IsNullOrWhiteSpace(input.ReviewVersion))
        {
            errors.Add(Error(
                GameDataValidationErrorCodes.ManifestReviewedItemPropertySemanticsReviewVersionRequired,
                $"{path}.reviewVersion",
                "Reviewed item-property semantic ReviewVersion is required."));
        }
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private static bool IsUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero;
    }

    private static GameDataValidationError Error(string code, string path, string message)
    {
        return new GameDataValidationError(code, path, message);
    }
}
