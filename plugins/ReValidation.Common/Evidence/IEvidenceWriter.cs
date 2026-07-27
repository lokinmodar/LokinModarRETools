using ReValidation.Common.Models;

namespace ReValidation.Common.Evidence;

public interface IEvidenceWriter
{
    ValueTask<EvidenceWriteResult> WriteAsync(ScenarioRunReport report, ScenarioExecutionContext context, CancellationToken cancellationToken);
}
