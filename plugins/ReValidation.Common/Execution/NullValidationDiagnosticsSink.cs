using ReValidation.Common.Abstractions;

namespace ReValidation.Common.Execution;

internal sealed class NullValidationDiagnosticsSink : IValidationDiagnosticsSink
{
    public static IValidationDiagnosticsSink Instance { get; } = new NullValidationDiagnosticsSink();

    public void Debug(string message)
    {
    }
}
