namespace ProjectManagementCompiler.Application;

public static class WorkbookExportSelection
{
    public static CompilationResult? ResolveOfficial(CompilerApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CurrentOfficialResult is not null)
        {
            return state.CurrentOfficialResult;
        }

        var current = state.Current;
        return current?.Project.ImportMetadata is null ? current : null;
    }

    public static CompilationResult? ResolvePreview(CompilerApplicationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ActivePreviewResult;
    }
}
