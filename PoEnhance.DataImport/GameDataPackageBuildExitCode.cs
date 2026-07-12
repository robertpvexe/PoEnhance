namespace PoEnhance.DataImport;

public enum GameDataPackageBuildExitCode
{
    Success = 0,
    InvalidArguments = 1,
    MissingInputFile = 2,
    SourceImportFailure = 3,
    PackageValidationFailure = 4,
    OutputWriteFailure = 5,
    UnexpectedInternalError = 10,
}
