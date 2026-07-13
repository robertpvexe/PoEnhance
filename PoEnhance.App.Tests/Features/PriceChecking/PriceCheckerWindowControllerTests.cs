using System.Reflection;
using System.Runtime.InteropServices;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerWindowControllerTests
{
    [Fact]
    public void ShowOrUpdate_SecondItemReusesSameWindowInstance()
    {
        using var fixture = ControllerFixture.Create();
        var firstItem = Item("First Loop", "Gold Ring");
        var secondItem = Item("Second Loop", "Two-Stone Ring");

        fixture.Controller.ShowOrUpdate(firstItem, null, []);
        fixture.Controller.ShowOrUpdate(secondItem, null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
    }

    [Fact]
    public void ShowOrUpdate_SecondItemReplacesDisplayedDraft()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal("Second Loop", window.CurrentState?.Draft.DisplayName);
        Assert.Equal("Two-Stone Ring", window.CurrentState?.Draft.ParsedBaseType);
    }

    [Fact]
    public void ShowOrUpdate_AfterWindowCloseCreatesFreshWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);

        firstWindow.Close();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.True(firstWindow.IsClosed);
        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsClosed);
        Assert.Equal("Second Loop", fixture.WindowFactory.CreatedWindows[1].CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void ShowOrUpdate_ReusesCoreDraftMappingAndValidationAdapters()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.Mapper.CallCount);
        Assert.Equal(2, fixture.Validator.CallCount);
    }

    [Fact]
    public void ShowOrUpdate_DoesNotInvokePriceCheckSearch()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);

        Assert.Equal(0, fixture.PriceCheckService.CallCount);
    }

    [Fact]
    public void ShowOrUpdate_UnavailablePathOfExileBoundsDoesNotThrowOrCreateWindow()
    {
        using var fixture = ControllerFixture.Create(boundsAvailable: false);

        var exception = Record.Exception(() =>
            fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []));

        Assert.Null(exception);
        Assert.Empty(fixture.WindowFactory.CreatedWindows);
    }

    [Fact]
    public void ShowOrUpdate_InitialNonActivatedShowDoesNotCloseWhenPathOfExileIsForeground()
    {
        using var fixture = ControllerFixture.Create();
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
        Assert.Empty(fixture.DeferredActionScheduler.PendingActions);
        Assert.Equal(1, window.ShowCount);
    }

    [Fact]
    public void PanelInteractionThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaisePanelInteraction();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void SearchInteractionThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaiseSearchRequested();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void PanelActivationThenDeactivationToPathOfExileClosesUnpinnedWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.RaisePanelActivated();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void PanelDeactivationToAnotherApplicationDoesNotCloseWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = false;

        window.RaisePanelInteraction();
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
    }

    [Fact]
    public void PinnedPanelRemainsOpenAfterDeactivationToPathOfExile()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.SetPinned(true);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.False(window.IsClosed);
        Assert.True(window.IsPinned);
    }

    [Fact]
    public void UnpinningRestoresAutoCloseAfterDeactivationToPathOfExile()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        window.SetPinned(true);
        window.SetPinned(false);
        window.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void CloseButtonPathClosesPinnedPanelAndNextUpdateReopensUnpinned()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.SetPinned(true);

        firstWindow.Close();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.True(firstWindow.IsClosed);
        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsPinned);
    }

    [Fact]
    public void EscapeClosePathClosesFocusedPinnedPanel()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetPinned(true);

        window.RaiseEscapeClose();

        Assert.True(window.IsClosed);
    }

    [Fact]
    public void AutoCloseClearsLiveWindowReferenceAndNextUpdateCreatesFreshWindow()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelInteraction();
        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.True(firstWindow.IsClosed);
        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.Equal("Second Loop", fixture.WindowFactory.CreatedWindows[1].CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void RecreatedWindowAfterAutoCloseStartsUnpinned()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.SetPinned(true);
        firstWindow.SetPinned(false);
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.False(fixture.WindowFactory.CreatedWindows[1].IsPinned);
    }

    [Fact]
    public void PinStateIsNotPersistedInPlacementStorage()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.SetPinned(true);
        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();

        var json = File.ReadAllText(fixture.PlacementStore.FilePath);
        Assert.DoesNotContain("pin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pinned", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistedCorrectionIsReusedAfterAutoCloseAndReopen()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var firstWindow = Assert.Single(fixture.WindowFactory.CreatedWindows);
        firstWindow.RaiseHorizontalDragDelta(-25);
        firstWindow.RaiseHorizontalDragCompleted();
        var adjustedLeft = firstWindow.CurrentPlacement?.Left;
        fixture.ForegroundWindowDetector.IsPathOfExileForeground = true;

        firstWindow.RaisePanelDeactivated();
        fixture.DeferredActionScheduler.RunPending();
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(2, fixture.WindowFactory.CreatedWindows.Count);
        Assert.Equal(adjustedLeft, fixture.WindowFactory.CreatedWindows[1].CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhilePinnedReusesWindowAndReplacesDraft()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.SetPinned(true);

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.True(window.IsPinned);
        Assert.Equal("Second Loop", window.CurrentState?.Draft.DisplayName);
    }

    [Fact]
    public void HorizontalDragCompletion_PersistsRelativeCorrection()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);

        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();

        var key = PriceCheckerPlacementKey.FromClientBounds(fixture.Bounds);
        var correction = fixture.PlacementStore.LoadHorizontalCorrection(key);
        Assert.Equal(-25, correction);
    }

    [Fact]
    public void ShowOrUpdate_AfterDragPreservesAdjustedXForSamePlacementKey()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();
        var adjustedLeft = window.CurrentPlacement?.Left;

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(adjustedLeft, window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_FailedStoreWriteStillPreservesCorrectionForSession()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        File.WriteAllText(fixture.PlacementStore.FilePath, "{not valid json");

        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();
        var adjustedLeft = window.CurrentPlacement?.Left;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(adjustedLeft, window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ResetPosition_ClearsCorrectionAndReturnsToAutomaticPosition()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-25);
        window.RaiseHorizontalDragCompleted();

        window.RaiseResetPositionRequested();

        var key = PriceCheckerPlacementKey.FromClientBounds(fixture.Bounds);
        Assert.Equal(0, fixture.PlacementStore.LoadHorizontalCorrection(key));
        Assert.Equal(
            fixture.Calculator.CalculateAutomaticLeft(fixture.Bounds),
            window.CurrentPlacement?.Left);

        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);
        Assert.Equal(
            fixture.Calculator.CalculateAutomaticLeft(fixture.Bounds),
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesLoadsCorrectionForNewKey()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };
        var secondKey = PriceCheckerPlacementKey.FromClientBounds(secondBounds);
        fixture.PlacementStore.SaveHorizontalCorrection(secondKey, -30);

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-60);
        window.RaiseHorizontalDragCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(
            fixture.Calculator.CalculatePlacement(secondBounds, -30).Left,
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_WhenPlacementKeyChangesUsesZeroWithoutSavedCorrection()
    {
        using var fixture = ControllerFixture.Create();
        var secondBounds = fixture.Bounds with { Width = 1200 };

        fixture.Controller.ShowOrUpdate(Item("First Loop", "Gold Ring"), null, []);
        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        window.RaiseHorizontalDragDelta(-60);
        window.RaiseHorizontalDragCompleted();

        fixture.BoundsProvider.Bounds = secondBounds;
        fixture.Controller.ShowOrUpdate(Item("Second Loop", "Two-Stone Ring"), null, []);

        Assert.Equal(
            fixture.Calculator.CalculateAutomaticLeft(secondBounds),
            window.CurrentPlacement?.Left);
    }

    [Fact]
    public void ShowOrUpdate_ExactBaseResolutionIsDisplayedAsExact()
    {
        using var fixture = ControllerFixture.Create();

        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(ItemBaseResolutionStatus.Exact, window.CurrentState?.Draft.Base.Status);
        Assert.Equal("Titan Plate", window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void ShowOrUpdate_ReplacesParserOnlyBaseStateWithExactBaseState()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(Item("Armoured Shell", "Titan Plate"), null, []);

        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Equal(ItemBaseResolutionStatus.Exact, window.CurrentState?.Draft.Base.Status);
        Assert.Equal("Titan Plate", window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void ShowOrUpdate_ReplacesExactBaseStateWithParserOnlyBaseState()
    {
        using var fixture = ControllerFixture.Create();
        fixture.Controller.ShowOrUpdate(
            Item("Armoured Shell", "Titan Plate"),
            ExactBase("base.titan-plate", "Titan Plate"),
            []);

        fixture.Controller.ShowOrUpdate(Item("Armoured Shell", "Titan Plate"), null, []);

        var window = Assert.Single(fixture.WindowFactory.CreatedWindows);
        Assert.Null(window.CurrentState?.Draft.Base.Status);
        Assert.Null(window.CurrentState?.Draft.Base.ResolvedBaseName);
    }

    [Fact]
    public void CoreAssembly_GainsNoWpfWin32FileStorageOrNetworkingDependency()
    {
        var referencedNames = typeof(TradeSearchDraft).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("PresentationCore", referencedNames);
        Assert.DoesNotContain("PresentationFramework", referencedNames);
        Assert.DoesNotContain("WindowsBase", referencedNames);
        Assert.DoesNotContain("System.Net.Http", referencedNames);
        Assert.DoesNotContain("System.IO.FileSystem", referencedNames);
    }

    [Fact]
    public void PriceCheckerUi_DoesNotInvokeTradeSearchOrFetchClients()
    {
        var priceCheckerTypes = typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain(priceCheckerTypes, type =>
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeSearchClient") ||
            Contains(type, "PathOfExileTradeFetchClient"));
    }

    [Fact]
    public void AppAssembly_DoesNotIntroduceGlobalMouseOrKeyboardHooks()
    {
        var importedFunctionNames = ImportedFunctionNames();

        Assert.DoesNotContain("SetWindowsHookExA", importedFunctionNames);
        Assert.DoesNotContain("SetWindowsHookExW", importedFunctionNames);
        Assert.DoesNotContain("SetWindowsHookEx", importedFunctionNames);
        Assert.DoesNotContain("CallNextHookEx", importedFunctionNames);
        Assert.DoesNotContain("UnhookWindowsHookEx", importedFunctionNames);
        Assert.DoesNotContain("RegisterRawInputDevices", importedFunctionNames);
        Assert.DoesNotContain("GetRawInputData", importedFunctionNames);
    }

    [Fact]
    public void AppAssembly_DoesNotIntroduceExclusiveFullscreenOverlayDependencies()
    {
        var referencedNames = typeof(PriceCheckerWindowController).Assembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var importedFunctionNames = ImportedFunctionNames();

        Assert.DoesNotContain("SharpDX", referencedNames);
        Assert.DoesNotContain("Vortice.Direct3D11", referencedNames);
        Assert.DoesNotContain("Vortice.DXGI", referencedNames);
        Assert.DoesNotContain("SlimDX", referencedNames);
        Assert.DoesNotContain("Silk.NET.Direct3D11", referencedNames);
        Assert.DoesNotContain("Direct3DCreate9", importedFunctionNames);
        Assert.DoesNotContain("D3D11CreateDevice", importedFunctionNames);
    }

    private static ParsedItem Item(string name, string baseType)
    {
        return new ItemTextParser().Parse($"""
Item Class: Rings
Rarity: Rare
{name}
{baseType}
--------
Item Level: 80
""");
    }

    private static HashSet<string> ImportedFunctionNames()
    {
        return typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(method => new
            {
                MethodName = method.Name,
                Attribute = method.GetCustomAttribute<DllImportAttribute>(),
            })
            .Where(import => import.Attribute is not null)
            .Select(import => import.Attribute?.EntryPoint ?? import.MethodName)
            .Where(entryPoint => !string.IsNullOrWhiteSpace(entryPoint))
            .Select(entryPoint => entryPoint!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(ConstructorTypes)
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static IEnumerable<Type> ConstructorTypes(ConstructorInfo constructor)
    {
        return constructor.GetParameters().Select(parameter => parameter.ParameterType);
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ItemBaseResolutionResult ExactBase(string id, string name)
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = new ItemBaseRecord { Id = id, Name = name },
            ResolvedBaseId = id,
            ResolvedBaseName = name,
        };
    }

    private sealed class ControllerFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;

        private ControllerFixture(
            TempDirectory tempDirectory,
            PriceCheckerWindowController controller,
            PathOfExileClientBounds bounds,
            FakeBoundsProvider boundsProvider,
            PriceCheckerPlacementCalculator calculator,
            PriceCheckerPlacementStore placementStore,
            FakeWindowFactory windowFactory,
            CountingMapper mapper,
            CountingValidator validator,
            FakePriceCheckService priceCheckService,
            FakeForegroundWindowDetector foregroundWindowDetector,
            FakeDeferredActionScheduler deferredActionScheduler)
        {
            this.tempDirectory = tempDirectory;
            Controller = controller;
            Bounds = bounds;
            BoundsProvider = boundsProvider;
            Calculator = calculator;
            PlacementStore = placementStore;
            WindowFactory = windowFactory;
            Mapper = mapper;
            Validator = validator;
            PriceCheckService = priceCheckService;
            ForegroundWindowDetector = foregroundWindowDetector;
            DeferredActionScheduler = deferredActionScheduler;
        }

        public PriceCheckerWindowController Controller { get; }

        public PathOfExileClientBounds Bounds { get; }

        public FakeBoundsProvider BoundsProvider { get; }

        public PriceCheckerPlacementCalculator Calculator { get; }

        public PriceCheckerPlacementStore PlacementStore { get; }

        public FakeWindowFactory WindowFactory { get; }

        public CountingMapper Mapper { get; }

        public CountingValidator Validator { get; }

        public FakePriceCheckService PriceCheckService { get; }

        public FakeForegroundWindowDetector ForegroundWindowDetector { get; }

        public FakeDeferredActionScheduler DeferredActionScheduler { get; }

        public static ControllerFixture Create(bool boundsAvailable = true)
        {
            var tempDirectory = TempDirectory.Create();
            var bounds = new PathOfExileClientBounds(
                Left: 100,
                Top: 50,
                Width: 1000,
                Height: 800,
                DisplayDeviceName: @"\\.\DISPLAY1",
                DpiScaleX: 1,
                DpiScaleY: 1);
            var boundsProvider = new FakeBoundsProvider(boundsAvailable, bounds);
            var calculator = new PriceCheckerPlacementCalculator(
                panelWidthRatio: 0.18,
                minimumPanelWidth: 280,
                maximumPanelWidth: 360,
                inventoryWidthToClientHeightRatio: 0.60,
                inventorySafetyGap: 12);
            var placementStore = new PriceCheckerPlacementStore(
                Path.Combine(tempDirectory.Path, "placement.json"));
            var windowFactory = new FakeWindowFactory();
            var mapper = new CountingMapper();
            var validator = new CountingValidator();
            var priceCheckService = new FakePriceCheckService();
            var foregroundWindowDetector = new FakeForegroundWindowDetector();
            var deferredActionScheduler = new FakeDeferredActionScheduler();
            var controller = new PriceCheckerWindowController(
                boundsProvider,
                calculator,
                placementStore,
                windowFactory,
                mapper,
                validator,
                foregroundWindowDetector,
                deferredActionScheduler,
                new PriceCheckerSearchController(priceCheckService));

            return new ControllerFixture(
                tempDirectory,
                controller,
                bounds,
                boundsProvider,
                calculator,
                placementStore,
                windowFactory,
                mapper,
                validator,
                priceCheckService,
                foregroundWindowDetector,
                deferredActionScheduler);
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }

    private sealed class FakeBoundsProvider : IPathOfExileClientBoundsProvider
    {
        private readonly bool isAvailable;

        public FakeBoundsProvider(bool isAvailable, PathOfExileClientBounds bounds)
        {
            this.isAvailable = isAvailable;
            Bounds = bounds;
        }

        public PathOfExileClientBounds Bounds { get; set; }

        public bool TryGetClientBounds(out PathOfExileClientBounds clientBounds)
        {
            clientBounds = Bounds;
            return isAvailable;
        }
    }

    private sealed class FakeForegroundWindowDetector : IPathOfExileForegroundWindowDetector
    {
        public bool IsPathOfExileForeground { get; set; }

        public bool IsPathOfExileForegroundWindow()
        {
            return IsPathOfExileForeground;
        }
    }

    private sealed class FakeDeferredActionScheduler : IPriceCheckerDeferredActionScheduler
    {
        public List<Action> PendingActions { get; } = [];

        public void Schedule(Action action)
        {
            PendingActions.Add(action);
        }

        public void RunPending()
        {
            var actions = PendingActions.ToArray();
            PendingActions.Clear();
            foreach (var action in actions)
            {
                action();
            }
        }
    }

    private sealed class FakeWindowFactory : IPriceCheckerWindowFactory
    {
        public List<FakeWindow> CreatedWindows { get; } = [];

        public IPriceCheckerWindow CreateWindow()
        {
            var window = new FakeWindow();
            CreatedWindows.Add(window);
            return window;
        }
    }

    private sealed class FakeWindow : IPriceCheckerWindow
    {
        public event EventHandler? Closed;

        public event EventHandler? PanelActivated;

        public event EventHandler? PanelDeactivated;

        public event EventHandler? PanelInteraction;

        public event EventHandler? SearchRequested;

        public event EventHandler<PriceCheckerLeagueChangedEventArgs>? LeagueChanged;

        public event EventHandler<bool>? PinStateChanged;

        public event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;

        public event EventHandler? HorizontalDragCompleted;

        public event EventHandler? ResetPositionRequested;

        public bool IsClosed { get; private set; }

        public bool IsPinned { get; private set; }

        public PriceCheckerWindowState? CurrentState { get; private set; }

        public PriceCheckerPlacement? CurrentPlacement { get; private set; }

        public PriceCheckerSearchViewState? CurrentSearchState { get; private set; }

        public int ShowCount { get; private set; }

        public void UpdateContent(PriceCheckerWindowState state)
        {
            CurrentState = state;
        }

        public void UpdateSearch(PriceCheckerSearchViewState state)
        {
            CurrentSearchState = state;
        }

        public void ApplyPlacement(PriceCheckerPlacement placement)
        {
            CurrentPlacement = placement;
        }

        public void ShowInactive()
        {
            ShowCount++;
        }

        public void RaiseHorizontalDragDelta(double horizontalChange)
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            HorizontalDragDelta?.Invoke(
                this,
                new PriceCheckerHorizontalDragEventArgs(horizontalChange));
        }

        public void RaiseHorizontalDragCompleted()
        {
            HorizontalDragCompleted?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseResetPositionRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            ResetPositionRequested?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelActivated()
        {
            PanelActivated?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelDeactivated()
        {
            PanelDeactivated?.Invoke(this, EventArgs.Empty);
        }

        public void RaisePanelInteraction()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseSearchRequested()
        {
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetLeague(string leagueIdentifier)
        {
            LeagueChanged?.Invoke(this, new PriceCheckerLeagueChangedEventArgs(leagueIdentifier));
        }

        public void SetPinned(bool isPinned)
        {
            IsPinned = isPinned;
            PanelInteraction?.Invoke(this, EventArgs.Empty);
            PinStateChanged?.Invoke(this, isPinned);
        }

        public void RaiseEscapeClose()
        {
            Close();
        }

        public void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class CountingMapper : ITradeSearchDraftMapper
    {
        public int CallCount { get; private set; }

        public TradeSearchDraftResult CreateDraft(
            ParsedItem parsedItem,
            ItemBaseResolutionResult? itemBaseResolution,
            IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions)
        {
            CallCount++;
            return TradeSearchDraftResult.Success(new TradeSearchDraft
            {
                ItemClass = parsedItem.ItemClass,
                Rarity = parsedItem.Rarity,
                DisplayName = parsedItem.DisplayName,
                ParsedBaseType = parsedItem.BaseType,
                Base = CreateBaseDraft(itemBaseResolution),
                ItemLevel = parsedItem.ItemLevel,
            });
        }

        private static TradeSearchBaseDraft CreateBaseDraft(
            ItemBaseResolutionResult? itemBaseResolution)
        {
            if (itemBaseResolution is null)
            {
                return new TradeSearchBaseDraft();
            }

            return new TradeSearchBaseDraft
            {
                Status = itemBaseResolution.Status,
                ResolvedBaseId = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                    ? null
                    : itemBaseResolution.ResolvedBaseId,
                ResolvedBaseName = itemBaseResolution.Status == ItemBaseResolutionStatus.Unknown
                    ? null
                    : itemBaseResolution.ResolvedBaseName,
            };
        }
    }

    private sealed class CountingValidator : ITradeSearchDraftValidator
    {
        public int CallCount { get; private set; }

        public TradeSearchValidationResult Validate(TradeSearchDraft draft)
        {
            CallCount++;
            return TradeSearchValidationResult.FromDiagnostics([]);
        }
    }

    private sealed class FakePriceCheckService : IPathOfExileTradePriceCheckService
    {
        public int CallCount { get; private set; }

        public Task<PathOfExileTradePriceCheckResult> CheckAsync(
            TradeSearchDraft? draft,
            TradeSearchValidationResult? validationResult,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PathOfExileTradePriceCheckResult
            {
                IsSuccess = true,
                Stage = PathOfExileTradePriceCheckStage.Completed,
                SearchQueryId = "query-1",
                ProviderTotal = 0,
            });
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-price-checker-controller-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
