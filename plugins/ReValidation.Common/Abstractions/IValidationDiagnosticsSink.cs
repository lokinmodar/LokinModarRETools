namespace ReValidation.Common.Abstractions;

public interface IValidationDiagnosticsSink
{
    void Debug(string message);
}
