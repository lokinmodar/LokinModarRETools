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
        if (ImGui.Button("Reset Session"))
            controller.ResetSession();
        if (ImGui.Button("Export Session Note"))
            _ = controller.ExportSessionNoteAsync(exportRoot, CancellationToken.None);

        ImGui.Separator();
        ImGui.TextUnformatted("Anchors");
        foreach (var anchor in controller.State.Anchors)
            ImGui.TextUnformatted($"{anchor.AnchorId}: 0x{anchor.Address:X} ({anchor.Role})");

        ImGui.Separator();
        ImGui.TextUnformatted("Ranked candidates");
        foreach (var candidate in controller.State.Candidates)
        {
            var label = $"{candidate.CandidateId} 0x{candidate.Address:X} {candidate.Classification} {candidate.Confidence} [{candidate.Disposition}]";
            if (ImGui.Selectable(label, candidate.CandidateId == controller.State.SelectedCandidateId))
                controller.State.SetSelectedCandidate(candidate.CandidateId);
        }

        var selected = controller.State.Candidates.FirstOrDefault(candidate => candidate.CandidateId == controller.State.SelectedCandidateId);
        if (selected is null)
            return;

        ImGui.Separator();
        ImGui.TextUnformatted($"Selected: {selected.CandidateId}");
        ImGui.TextUnformatted($"Address: 0x{selected.Address:X}");
        controller.DrawSelectedCandidatePointer();
        if (ImGui.Button("Inspect Object"))
            controller.InspectSelectedCandidateObject();
        if (ImGui.Button("Inspect Region (0x100)"))
            controller.InspectSelectedCandidateRegion(0x100);
        if (ImGui.Button("Mark Discarded"))
            controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.Discarded);
        if (ImGui.Button("Mark Promising"))
            controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.Promising);
        if (ImGui.Button("Mark High Value For IDA"))
            controller.MarkSelectedCandidateDisposition(JournalCandidateDisposition.HighValueForIda);
    }
}
