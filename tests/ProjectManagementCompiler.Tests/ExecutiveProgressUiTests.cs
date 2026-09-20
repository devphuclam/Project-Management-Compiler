namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressUiTests
{
    public static void UiExposesSeparateExecutiveAndTechnicalExportActions()
    {
        var index = Read("index.html");
        var app = Read("app.js");

        TestAssert.Contains("id=\"export-executive-button\"", index, "The header must expose a dedicated executive progress export control.");
        TestAssert.Contains("Xuất báo cáo tiến độ", index, "The primary management export must use the approved Vietnamese wording.");
        TestAssert.Contains("id=\"export-xlsx-button\"", index, "The technical workbook export control must remain available.");
        TestAssert.Contains("Xuất dữ liệu CARIO + Gantt", index, "The technical export must explain that it is CARIO plus Gantt data.");
        TestAssert.Contains("/api/exports/executive-progress.xlsx", app, "The executive control must bind to the executive endpoint.");
        TestAssert.Contains("/api/exports/cario.xlsx", app, "The technical control must remain bound to the CARIO endpoint.");
    }

    public static void UiKeepsExecutiveExportOfficialOnlyAndPreviewSeparate()
    {
        var app = Read("app.js");
        var index = Read("index.html");

        TestAssert.Contains("setExportAvailability", app, "The UI must derive export availability from the loaded authority state.");
        TestAssert.Contains("OFFICIAL_COMMIT", app, "The UI must recognize official manifest availability explicitly.");
        TestAssert.Contains("disabled", index, "The executive action must have an unavailable state before an official snapshot is loaded.");
        TestAssert.Contains("xlsxPreview", app, "The UI must preserve the separate XLSX preview state.");
        TestAssert.False(app.Contains("/api/exports/cario-preview.xlsx", StringComparison.Ordinal), "The executive action must not redirect to the technical preview export.");
    }

    public static void UiKeepsOfficialExportAvailableWhilePreviewIsActive()
    {
        var app = Read("app.js");

        TestAssert.Contains("officialManifest", app, "The UI must retain the last official manifest separately from a candidate preview.");
        TestAssert.Contains("state.officialManifest", app, "Executive export availability must be based on the retained official snapshot, not the active preview classification.");
        TestAssert.Contains("response.classification === \"OFFICIAL_COMMIT\"", app, "Only a successful official import may replace the retained official manifest.");
    }

    public static void UiExportActionsRemainResponsiveAndAccessible()
    {
        var styles = Read("styles.css");
        var index = Read("index.html");

        TestAssert.Contains("header-actions", styles, "Export actions must use the existing responsive header action layout.");
        TestAssert.Contains("flex-wrap: wrap", styles, "Header actions must wrap on narrow screens.");
        TestAssert.Contains("aria-describedby", index, "The executive export control must expose a concise availability explanation.");
        TestAssert.Contains("type=\"button\"", index, "Export controls must be explicit buttons rather than implicit form submissions.");
    }

    private static string Read(string fileName) =>
        File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", fileName));
}
