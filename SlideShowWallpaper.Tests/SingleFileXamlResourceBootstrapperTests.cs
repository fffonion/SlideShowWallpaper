using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class SingleFileXamlResourceBootstrapperTests
{
    [Fact]
    public void Ensure_MissingTarget_CopiesResourceIndexBesideExecutable()
    {
        using var workspace = new TemporaryWorkspace();
        string sourcePath = workspace.WriteBundleResource([1, 2, 3]);

        SingleFileXamlResourceBootstrapper.Ensure(workspace.BundleDirectory, workspace.ProcessPath);

        Assert.Equal(File.ReadAllBytes(sourcePath), File.ReadAllBytes(workspace.TargetResourcePath));
    }

    [Fact]
    public void Ensure_StaleTarget_ReplacesResourceIndex()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteBundleResource([4, 5, 6]);
        File.WriteAllBytes(workspace.TargetResourcePath, [1, 2, 3]);

        SingleFileXamlResourceBootstrapper.Ensure(workspace.BundleDirectory, workspace.ProcessPath);

        Assert.Equal([4, 5, 6], File.ReadAllBytes(workspace.TargetResourcePath));
    }

    [Fact]
    public void Ensure_MatchingTarget_DoesNotRewriteResourceIndex()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteBundleResource([7, 8, 9]);
        File.WriteAllBytes(workspace.TargetResourcePath, [7, 8, 9]);
        DateTime originalWriteTimeUtc = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(workspace.TargetResourcePath, originalWriteTimeUtc);

        SingleFileXamlResourceBootstrapper.Ensure(workspace.BundleDirectory, workspace.ProcessPath);

        Assert.Equal(originalWriteTimeUtc, File.GetLastWriteTimeUtc(workspace.TargetResourcePath));
    }

    [Fact]
    public void Ensure_LooseBuild_DoesNotRewriteResourceIndex()
    {
        using var workspace = new TemporaryWorkspace();
        File.WriteAllBytes(workspace.TargetResourcePath, [7, 8, 9]);
        DateTime originalWriteTimeUtc = File.GetLastWriteTimeUtc(workspace.TargetResourcePath);

        SingleFileXamlResourceBootstrapper.Ensure(workspace.ProcessDirectory, workspace.ProcessPath);

        Assert.Equal(originalWriteTimeUtc, File.GetLastWriteTimeUtc(workspace.TargetResourcePath));
    }

    [Fact]
    public void Program_EnsuresResourceIndexBeforeWinUiStartup()
    {
        string source = File.ReadAllText(FindProgramPath());

        int bootstrapIndex = source.IndexOf("SingleFileXamlResourceBootstrapper.EnsureForCurrentProcess();", StringComparison.Ordinal);
        int winUiIndex = source.IndexOf("WinUiAppHost.Start();", StringComparison.Ordinal);

        Assert.True(bootstrapIndex >= 0);
        Assert.True(bootstrapIndex < winUiIndex);
    }

    private static string FindProgramPath()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string path = Path.Combine(directory, "Program.cs");
            if (File.Exists(path))
            {
                return path;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new FileNotFoundException("Program.cs was not found.");
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));

        public TemporaryWorkspace()
        {
            BundleDirectory = Path.Combine(_root, "bundle");
            ProcessDirectory = Path.Combine(_root, "app");
            Directory.CreateDirectory(BundleDirectory);
            Directory.CreateDirectory(ProcessDirectory);
        }

        public string BundleDirectory { get; }

        public string ProcessDirectory { get; }

        public string ProcessPath => Path.Combine(ProcessDirectory, "SlideShowWallpaper.exe");

        public string TargetResourcePath => Path.Combine(ProcessDirectory, "SlideShowWallpaper.pri");

        public string WriteBundleResource(byte[] contents)
        {
            string path = Path.Combine(BundleDirectory, "SlideShowWallpaper.pri");
            File.WriteAllBytes(path, contents);
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
