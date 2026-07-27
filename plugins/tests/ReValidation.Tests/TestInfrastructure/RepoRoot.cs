internal static class RepoRoot
{
    public static string Find()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "plugins")) &&
                File.Exists(Path.Combine(current.FullName, "README.md")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
