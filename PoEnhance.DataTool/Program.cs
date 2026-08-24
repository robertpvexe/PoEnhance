using System.Text.Json;
using PoEnhance.DataImport;
using PoEnhance.DataTool;
using PoEnhance.DataTool.UniqueCorpusGate;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 &&
            string.Equals(args[0], UniqueCorpusGateCommandLineParser.CommandName, StringComparison.Ordinal))
        {
            return RunUniqueCorpusGate(args);
        }

        if (args.Length > 0 &&
            string.Equals(args[0], AugmentPackageSemanticsCommandLineParser.CommandName, StringComparison.Ordinal))
        {
            return RunAugmentPackageSemantics(args);
        }

        if (args.Length > 0 &&
            string.Equals(args[0], AugmentPackageBasePropertiesCommandLineParser.CommandName, StringComparison.Ordinal))
        {
            return RunAugmentPackageBaseProperties(args);
        }

        return RunBuildPackage(args);
    }

    private static int RunUniqueCorpusGate(string[] args)
    {
        var parsed = UniqueCorpusGateCommandLineParser.Parse(args);
        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(UniqueCorpusGateCommandLineParser.GetUsage());
            return 2;
        }

        try
        {
            var request = parsed.Request!;
            var options = new UniqueCorpusGateOptions
            {
                DeduplicateLatestCapturePerItem = request.DeduplicateLatestCapturePerItem,
                Strict = request.Strict,
                MaxUnclassifiedClusterComponents = request.MaxUnclassifiedClusterComponents,
                MaxSupportedCoverageDropPercent = request.MaxSupportedCoverageDropPercent,
                BaselineReportPath = request.BaselinePath,
            };
            var report = UniqueCorpusGateAnalyzer.AnalyzeDirectory(request.InputDirectory, options);
            if (!string.IsNullOrWhiteSpace(request.BaselinePath))
            {
                var baseline = UniqueCorpusGateReportPrinter.ReadJson(request.BaselinePath);
                var comparison = UniqueCorpusGateAnalyzer.Compare(report, baseline);
                report = CloneWithComparison(report, comparison);
            }

            if (request.Strict)
            {
                report = CloneWithStrict(
                    report,
                    UniqueCorpusGateAnalyzer.EvaluateStrictGate(report, options));
            }

            UniqueCorpusGateReportPrinter.Print(report, Console.Out);
            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                UniqueCorpusGateReportPrinter.WriteJson(report, request.OutputPath);
                Console.WriteLine();
                Console.WriteLine($"Wrote {request.OutputPath}");
            }

            return request.Strict && report.StrictGate?.Passed == false ? 1 : 0;
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or InvalidDataException or JsonException or IOException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected internal error: {exception.Message}");
            return 3;
        }
    }

    private static int RunBuildPackage(string[] args)
    {
        var parsed = BuildPackageCommandLineParser.Parse(args);
        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(GetUsage());
            return (int)GameDataPackageBuildExitCode.InvalidArguments;
        }

        try
        {
            var service = new RePoeGameDataPackageBuildService();
            var result = service.Build(parsed.Request!);
            BuildPackageReportPrinter.Print(result, Console.Out, parsed.VerboseDiagnostics);
            return (int)result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected internal error: {exception.Message}");
            return (int)GameDataPackageBuildExitCode.UnexpectedInternalError;
        }
    }

    private static int RunAugmentPackageSemantics(string[] args)
    {
        var parsed = AugmentPackageSemanticsCommandLineParser.Parse(args);
        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(GetUsage());
            return (int)GameDataPackageSemanticAugmentationExitCode.InvalidArguments;
        }

        try
        {
            var service = new GameDataPackageSemanticAugmentationService();
            var result = service.Augment(parsed.Request!);
            AugmentPackageSemanticsReportPrinter.Print(result, Console.Out);
            return (int)result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected internal error: {exception.Message}");
            return (int)GameDataPackageSemanticAugmentationExitCode.UnexpectedInternalError;
        }
    }

    private static int RunAugmentPackageBaseProperties(string[] args)
    {
        var parsed = AugmentPackageBasePropertiesCommandLineParser.Parse(args);
        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine(error);
            }
            Console.Error.WriteLine(AugmentPackageBasePropertiesCommandLineParser.GetUsage());
            return 2;
        }

        try
        {
            var result = new GameDataPackageWeaponPropertyAugmentationService().Augment(parsed.Request!);
            AugmentPackageBasePropertiesReportPrinter.Print(result, Console.Out);
            return result.IsSuccess ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Unexpected internal error: {exception.Message}");
            return 3;
        }
    }

    private static UniqueCorpusGateReport CloneWithComparison(
        UniqueCorpusGateReport report,
        UniqueCorpusGateComparison comparison)
    {
        return new UniqueCorpusGateReport
        {
            Schema = report.Schema,
            GeneratedAtUtc = report.GeneratedAtUtc,
            InputDirectory = report.InputDirectory,
            Identity = report.Identity,
            Outcomes = report.Outcomes,
            OutcomesByParsedKind = report.OutcomesByParsedKind,
            OutcomesByResolvedSourceKind = report.OutcomesByResolvedSourceKind,
            OutcomesBySourceFamily = report.OutcomesBySourceFamily,
            FailureStages = report.FailureStages,
            RootCauseClusters = report.RootCauseClusters,
            SignatureFamilies = report.SignatureFamilies,
            RankedBacklog = report.RankedBacklog,
            Comparison = comparison,
            StrictGate = report.StrictGate,
        };
    }

    private static UniqueCorpusGateReport CloneWithStrict(
        UniqueCorpusGateReport report,
        UniqueCorpusGateStrictResult strictGate)
    {
        return new UniqueCorpusGateReport
        {
            Schema = report.Schema,
            GeneratedAtUtc = report.GeneratedAtUtc,
            InputDirectory = report.InputDirectory,
            Identity = report.Identity,
            Outcomes = report.Outcomes,
            OutcomesByParsedKind = report.OutcomesByParsedKind,
            OutcomesByResolvedSourceKind = report.OutcomesByResolvedSourceKind,
            OutcomesBySourceFamily = report.OutcomesBySourceFamily,
            FailureStages = report.FailureStages,
            RootCauseClusters = report.RootCauseClusters,
            SignatureFamilies = report.SignatureFamilies,
            RankedBacklog = report.RankedBacklog,
            Comparison = report.Comparison,
            StrictGate = strictGate,
        };
    }

    private static string GetUsage()
    {
        return $"{UniqueCorpusGateCommandLineParser.GetUsage()}\n\n{BuildPackageCommandLineParser.GetUsage()}\n\n{AugmentPackageSemanticsCommandLineParser.GetUsage()}\n\n{AugmentPackageBasePropertiesCommandLineParser.GetUsage()}";
    }
}
