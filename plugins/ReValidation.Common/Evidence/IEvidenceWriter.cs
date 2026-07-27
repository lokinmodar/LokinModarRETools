using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public interface IEvidenceWriter
{
    string Kind { get; }
    ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken);
}
