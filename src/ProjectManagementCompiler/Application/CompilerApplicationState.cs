namespace ProjectManagementCompiler.Application;

public sealed class CompilerApplicationState
{
    private readonly object gate = new();
    private CompilationResult? current;

    public CompilationResult? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public void Set(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (gate)
        {
            current = result;
        }
    }
}
