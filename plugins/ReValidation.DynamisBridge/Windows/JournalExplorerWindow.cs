using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ReValidation.DynamisBridge.Models;
using ReValidation.DynamisBridge.UI;

namespace ReValidation.DynamisBridge.Windows;

public sealed class JournalExplorerWindow : Window
{
    private readonly JournalExplorerController controller;
    private readonly string exportRoot;

    public JournalExplorerWindow(JournalExplorerController controller, string exportRoot)
        : base("ReValidation: Dynamis Bridge")
    {
        this.controller = controller;
        this.exportRoot = exportRoot;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"Status: {controller.State.StatusText}");
        if (!string.IsNullOrEmpty(controller.State.StatusDetailText))
            ImGui.TextUnformatted(controller.State.StatusDetailText);
        if (ImGui.Button("Arm Journal Session"))
            _ = controller.ArmJournalSessionAsync(CancellationToken.None);
        if (ImGui.Button("Inspect Object"))
            controller.InspectSelectedCandidateObject();
        if (ImGui.Button("Mark High Value For IDA"))
            controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.HighValueForIda);
        if (ImGui.Button("Export Session Note"))
            _ = controller.ExportSessionNoteAsync(exportRoot, CancellationToken.None);

        foreach (var candidate in controller.State.Candidates)
        {
            if (ImGui.Selectable($"{candidate.CandidateId} {candidate.Classification} {candidate.Confidence}", candidate.CandidateId == controller.State.SelectedCandidateId))
                controller.State.SetSelectedCandidate(candidate.CandidateId);
        }
    }
}
