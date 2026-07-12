using PoEnhance.DataImport;
using PoEnhance.DataTool;

internal static class Program
{
    public static int Main(string[] args)
    {
        var parsed = BuildPackageCommandLineParser.Parse(args);
        if (!parsed.IsValid)
        {
            foreach (var error in parsed.Errors)
            {
                Console.Error.WriteLine(error);
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine(BuildPackageCommandLineParser.GetUsage());
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
}
