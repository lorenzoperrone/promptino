using System.Xml.Linq;

namespace Promptino.App.Tests;

public class ArchitectureBoundaryTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string Abs(string relative) => Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void CoreProject_ShouldNotReferenceUiOrPlatformProjects()
    {
        var references = ReadProjectReferences("Promptino.Core/Promptino.Core.csproj");

        references.Should().BeEmpty("Promptino.Core must remain portable and independent from App/Platform/Storage implementation details");
    }

    [Fact]
    public void StorageProject_ShouldNotReferenceUiOrPlatformProjects()
    {
        var references = ReadProjectReferences("Promptino.Storage/Promptino.Storage.csproj");

        references.Should().BeEmpty("Promptino.Storage should remain a local-first persistence library without UI/platform coupling");
    }

    [Fact]
    public void PlatformProject_ShouldNotReferenceUiProject()
    {
        var references = ReadProjectReferences("Promptino.Platform/Promptino.Platform.csproj");

        references.Should().BeEmpty("platform abstractions and interop should not depend on the Avalonia app layer");
    }

    [Fact]
    public void AppProject_ShouldDependOnlyOnCorePlatformAndStorageProjects()
    {
        var references = ReadProjectReferences("Promptino.App/Promptino.App.csproj");

        references.Should().BeEquivalentTo([
            "..\\Promptino.Core\\Promptino.Core.csproj",
            "..\\Promptino.Platform\\Promptino.Platform.csproj",
            "..\\Promptino.Storage\\Promptino.Storage.csproj"
        ]);
    }

    [Fact]
    public void CoreSource_ShouldNotUseAvaloniaOrWindowsInterop()
    {
        var violations = FindSourceViolations(
            "Promptino.Core",
            "using Avalonia",
            "DllImport(",
            "user32",
            "kernel32",
            "Windows.");

        violations.Should().BeEmpty("Promptino.Core must stay free of UI and Windows-specific APIs");
    }

    [Fact]
    public void StorageSource_ShouldNotUseAvaloniaOrWindowsInterop()
    {
        var violations = FindSourceViolations(
            "Promptino.Storage",
            "using Avalonia",
            "DllImport(",
            "user32",
            "kernel32",
            "Windows.");

        violations.Should().BeEmpty("Promptino.Storage should stay local-first and avoid UI/platform API leakage");
    }

    [Fact]
    public void WindowsInterop_ShouldStayInsidePlatformProject()
    {
        var nonPlatformDirs = new[] { "Promptino.Core", "Promptino.Storage", "Promptino.App" };
        var violations = nonPlatformDirs
            .SelectMany(dir => FindSourceViolations(
                dir,
                "DllImport(",
                "user32",
                "kernel32",
                "RegisterHotKey",
                "SetWindowDisplayAffinity"))
            .ToList();

        violations.Should().BeEmpty("direct Win32 interop should remain isolated behind platform abstractions");
    }

    [Fact]
    public void Solution_ShouldContainOnlyPromptinoBaseProjects()
    {
        var solution = XDocument.Load(Abs("Promptino.slnx"));
        var projects = solution.Descendants("Project")
            .Select(p => p.Attribute("Path")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        projects.Should().BeEquivalentTo([
            "Promptino.App.Tests/Promptino.App.Tests.csproj",
            "Promptino.App/Promptino.App.csproj",
            "Promptino.Core/Promptino.Core.csproj",
            "Promptino.Platform/Promptino.Platform.csproj",
            "Promptino.Storage/Promptino.Storage.csproj"
        ]);
    }

    private static string[] ReadProjectReferences(string relativeProjectPath)
    {
        var document = XDocument.Load(Abs(relativeProjectPath));
        return document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()!;
    }

    private static IReadOnlyList<string> FindSourceViolations(string relativeDirectory, params string[] forbiddenTerms)
    {
        var directory = Abs(relativeDirectory);
        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenTerms
                    .Where(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(term => $"{Path.GetRelativePath(RepositoryRoot, path)} contains forbidden term '{term}'");
            })
            .ToList();
    }
}
