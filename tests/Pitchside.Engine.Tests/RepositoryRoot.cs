namespace Pitchside.Engine.Tests;

/// <summary>Finds the repository root from the test output folder, so tests can read schemas and samples.</summary>
internal static class RepositoryRoot
{
    public static string Path { get; } = Find();

    public static string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Pitchside.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Pitchside.slnx not found above the test output folder.");
    }
}
