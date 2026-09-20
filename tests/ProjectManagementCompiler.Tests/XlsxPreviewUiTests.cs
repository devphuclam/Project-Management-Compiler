namespace ProjectManagementCompiler.Tests;

internal static class XlsxPreviewUiTests
{
    public static void UiExposesExplicitReadOnlyPreviewMode()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot");
        var index = File.ReadAllText(Path.Combine(root, "index.html"));
        var app = File.ReadAllText(Path.Combine(root, "app.js"));
        var styles = File.ReadAllText(Path.Combine(root, "styles.css"));

        TestAssert.Contains("Import XLSX preview", index, "The source-intake UI must expose an XLSX preview picker.");
        TestAssert.Contains("XLSX Preview", index + app, "The UI must identify the active workbook preview mode.");
        TestAssert.Contains("Read-only", index + app, "The UI must identify the preview as read-only.");
        TestAssert.Contains("Non-authoritative", index + app, "The UI must identify the preview as non-authoritative.");
        TestAssert.Contains("Official source", index + app, "The UI must provide an explicit return to official source mode.");
        TestAssert.Contains("xlsx-preview", app, "The UI must call the dedicated preview endpoint.");
        TestAssert.Contains("preview", styles, "Preview mode must have a visible styling seam.");
    }

    public static void PreviewUiHidesProposalActionsAndDoesNotPersistWorkbook()
    {
        var app = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js"));
        TestAssert.Contains("readOnly", app, "Preview rendering must carry read-only semantics.");
        TestAssert.Contains("authoritative", app, "Preview rendering must carry authority semantics.");
        TestAssert.False(app.Contains("localStorage.setItem(\"xlsx-preview\"", StringComparison.Ordinal), "Preview workbooks must not be persisted across restarts.");
    }
}
