namespace PoEnhance.DataImport;

public enum GameDataPackageSemanticAugmentationExitCode
{
    Success = 0,
    InvalidArguments = 1,
    MissingInputFile = 2,
    InputPackageInvalid = 3,
    SemanticImportFailure = 4,
    FinalPackageValidationFailure = 5,
    OutputWriteFailure = 6,
    UnexpectedInternalError = 10,
}
