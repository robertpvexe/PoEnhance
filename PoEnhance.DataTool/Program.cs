using PoEnhance.DataImport;
using PoEnhance.DataTool;

internal static class Program
{
    public static int Main(string[] args)
    {
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

    private static string GetUsage()
    {
        return $"{BuildPackageCommandLineParser.GetUsage()}\n\n{AugmentPackageSemanticsCommandLineParser.GetUsage()}\n\n{AugmentPackageBasePropertiesCommandLineParser.GetUsage()}";
    }
}
