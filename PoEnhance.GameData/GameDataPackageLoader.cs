using System.Text.Json;

namespace PoEnhance.GameData;

public static class GameDataPackageLoader
{
    private const int SupportedSchemaVersion = 1;

    public static async Task<GameDataPackageLoadResult> LoadFromFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var sourcePath = GetFullPathOrOriginal(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.FileNotFound,
                $"Game-data package file was not found: {path}",
                sourcePath);
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.ReadFailed,
                "Game-data package file could not be read.",
                sourcePath);
        }

        return LoadFromJson(json, sourcePath);
    }

    public static async Task<GameDataPackageLoadResult> LoadFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();

        string json;
        try
        {
            using var reader = new StreamReader(
                stream,
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsReadFailure(exception))
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.ReadFailed,
                "Game-data package stream could not be read.");
        }

        return LoadFromJson(json, sourcePath: null);
    }

    private static GameDataPackageLoadResult LoadFromJson(string json, string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.FileEmpty,
                "Game-data package content is empty.",
                sourcePath);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    GameDataPackageLoadDiagnosticCodes.SchemaUnsupported,
                    "Game-data package root must be a JSON object.",
                    sourcePath);
            }
        }
        catch (JsonException)
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.JsonInvalid,
                "Game-data package JSON is invalid.",
                sourcePath);
        }

        GameDataPackage? package;
        try
        {
            package = JsonSerializer.Deserialize<GameDataPackage>(
                json,
                GameDataPackageJson.CreateSerializerOptions());
        }
        catch (JsonException)
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.JsonInvalid,
                "Game-data package JSON is invalid.",
                sourcePath);
        }
        catch (NotSupportedException)
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.SchemaUnsupported,
                "Game-data package schema is unsupported.",
                sourcePath);
        }

        if (package is null)
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.SchemaUnsupported,
                "Game-data package schema is unsupported.",
                sourcePath);
        }

        if (package.Manifest.SchemaVersion != SupportedSchemaVersion)
        {
            return Failure(
                GameDataPackageLoadDiagnosticCodes.SchemaUnsupported,
                $"Game-data package schema version '{package.Manifest.SchemaVersion}' is unsupported.",
                sourcePath);
        }

        var validation = GameDataPackageValidator.Validate(package);
        if (!validation.IsValid)
        {
            return new GameDataPackageLoadResult
            {
                Diagnostics =
                [
                    new GameDataPackageLoadDiagnostic(
                        GameDataPackageLoadDiagnosticCodes.PackageInvalid,
                        "Game-data package failed validation."),
                ],
                ValidationErrors = validation.Errors,
                SourcePath = sourcePath,
            };
        }

        return new GameDataPackageLoadResult
        {
            Package = package,
            SourcePath = sourcePath,
        };
    }

    private static GameDataPackageLoadResult Failure(
        string code,
        string message,
        string? sourcePath = null)
    {
        return new GameDataPackageLoadResult
        {
            Diagnostics = [new GameDataPackageLoadDiagnostic(code, message)],
            SourcePath = sourcePath,
        };
    }

    private static string GetFullPathOrOriginal(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return path;
        }
    }

    private static bool IsReadFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or ObjectDisposedException;
    }
}
