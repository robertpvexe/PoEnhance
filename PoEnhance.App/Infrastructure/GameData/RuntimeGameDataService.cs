using PoEnhance.GameData;
using Serilog;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class RuntimeGameDataService
{
    private readonly GameDataPackagePathResolver pathResolver;
    private readonly Func<string, CancellationToken, Task<GameDataPackageLoadResult>> loadPackageAsync;
    private readonly Func<GameDataPackage, GameDataCatalog> createCatalog;
    private readonly object sync = new();
    private Task<RuntimeGameDataStatus>? loadTask;

    public RuntimeGameDataService()
        : this(
            new GameDataPackagePathResolver(),
            GameDataPackageLoader.LoadFromFileAsync,
            GameDataCatalog.FromPackage)
    {
    }

    public RuntimeGameDataService(
        GameDataPackagePathResolver pathResolver,
        Func<string, CancellationToken, Task<GameDataPackageLoadResult>> loadPackageAsync,
        Func<GameDataPackage, GameDataCatalog> createCatalog)
    {
        this.pathResolver = pathResolver;
        this.loadPackageAsync = loadPackageAsync;
        this.createCatalog = createCatalog;
        Current = new RuntimeGameDataStatus();
    }

    public RuntimeGameDataStatus Current { get; private set; }

    public event EventHandler<RuntimeGameDataStatus>? StateChanged;

    public Task<RuntimeGameDataStatus> LoadAsync(
        IReadOnlyList<string> commandLineArgs,
        CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            loadTask ??= LoadCoreAsync(commandLineArgs, cancellationToken);
            return loadTask;
        }
    }

    private async Task<RuntimeGameDataStatus> LoadCoreAsync(
        IReadOnlyList<string> commandLineArgs,
        CancellationToken cancellationToken)
    {
        var pathResolution = pathResolver.Resolve(commandLineArgs);
        if (!pathResolution.IsConfigured)
        {
            return SetCurrent(new RuntimeGameDataStatus
            {
                State = RuntimeGameDataState.NotConfigured,
                FailureMessage =
                    "No game-data package was found beside PoEnhance.exe or in the development artifacts directory.",
            });
        }

        SetCurrent(new RuntimeGameDataStatus
        {
            State = RuntimeGameDataState.Loading,
            PackagePath = pathResolution.Path,
            PathSource = pathResolution.Source,
        });

        Log.Information(
            "Game-data package loading started from {PathSource}: {PackagePath}",
            pathResolution.Source,
            pathResolution.Path);

        GameDataPackageLoadResult loadResult;
        try
        {
            loadResult = await loadPackageAsync(pathResolution.Path!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Game-data package loading canceled");
            throw;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Unexpected game-data package load failure");
            return SetCurrent(new RuntimeGameDataStatus
            {
                State = RuntimeGameDataState.Failed,
                PackagePath = pathResolution.Path,
                PathSource = pathResolution.Source,
                FailureMessage = "Unexpected game-data package load failure.",
            });
        }

        if (!loadResult.IsSuccess || loadResult.Package is null)
        {
            Log.Warning(
                "Game-data package load failed. Diagnostic codes: {DiagnosticCodes}. Validation error count: {ValidationErrorCount}",
                string.Join(", ", loadResult.Diagnostics.Select(diagnostic => diagnostic.Code)),
                loadResult.ValidationErrors.Count);

            return SetCurrent(new RuntimeGameDataStatus
            {
                State = RuntimeGameDataState.Failed,
                PackagePath = loadResult.SourcePath ?? pathResolution.Path,
                PathSource = pathResolution.Source,
                Diagnostics = loadResult.Diagnostics,
                ValidationErrors = loadResult.ValidationErrors,
                FailureMessage = "Game-data package failed to load.",
            });
        }

        GameDataCatalog catalog;
        try
        {
            catalog = createCatalog(loadResult.Package);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Game-data catalog construction failed");
            return SetCurrent(new RuntimeGameDataStatus
            {
                State = RuntimeGameDataState.Failed,
                PackagePath = loadResult.SourcePath ?? pathResolution.Path,
                PathSource = pathResolution.Source,
                Package = loadResult.Package,
                DataVersion = loadResult.Package.Manifest.DataVersion,
                SourceVersion = FormatSourceVersions(loadResult.Package.Manifest.Sources),
                FailureMessage = "Game-data catalog construction failed.",
            });
        }

        var status = new RuntimeGameDataStatus
        {
            State = RuntimeGameDataState.Loaded,
            PackagePath = loadResult.SourcePath ?? pathResolution.Path,
            PathSource = pathResolution.Source,
            Package = loadResult.Package,
            Catalog = catalog,
            DataVersion = loadResult.Package.Manifest.DataVersion,
            SourceVersion = FormatSourceVersions(loadResult.Package.Manifest.Sources),
            ItemBaseCount = loadResult.Package.ItemBases.Count,
            ModifierCount = loadResult.Package.Modifiers.Count,
            StatCount = loadResult.Package.Stats.Count,
            StatTranslationCount = loadResult.Package.StatTranslations.Count,
        };

        Log.Information(
            "Game-data package loaded. DataVersion={DataVersion}, SourceVersion={SourceVersion}, ItemBases={ItemBaseCount}, Modifiers={ModifierCount}, Stats={StatCount}, StatTranslations={StatTranslationCount}",
            status.DataVersion,
            status.SourceVersion,
            status.ItemBaseCount,
            status.ModifierCount,
            status.StatCount,
            status.StatTranslationCount);

        return SetCurrent(status);
    }

    private RuntimeGameDataStatus SetCurrent(RuntimeGameDataStatus status)
    {
        Current = status;
        StateChanged?.Invoke(this, status);
        return status;
    }

    private static string? FormatSourceVersions(IReadOnlyList<GameDataPackageSource> sources)
    {
        var sourceVersions = sources
            .Select(source => source.SourceVersion?.Trim())
            .Where(sourceVersion => !string.IsNullOrWhiteSpace(sourceVersion))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return sourceVersions.Length == 0
            ? null
            : string.Join(", ", sourceVersions);
    }
}
