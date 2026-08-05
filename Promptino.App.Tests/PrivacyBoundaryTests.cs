using System.Xml.Linq;

namespace Promptino.App.Tests;

public class PrivacyBoundaryTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string[] ProductionProjectFiles =
    [
        "Promptino.Core/Promptino.Core.csproj",
        "Promptino.App/Promptino.App.csproj",
        "Promptino.Platform/Promptino.Platform.csproj",
        "Promptino.Storage/Promptino.Storage.csproj"
    ];

    private static readonly string[] ForbiddenPackageNameFragments =
    [
        "ApplicationInsights",
        "OpenTelemetry",
        "Sentry",
        "Telemetry",
        "Analytics",
        "WebView",
        "CefSharp",
        "Chromium",
        "AspNetCore",
        "SignalR",
        "OpenAI",
        "Azure.AI",
        "Speech",
        "NAudio"
    ];

    private static readonly string[] ForbiddenSourceTerms =
    [
        "HttpClient",
        "WebApplication",
        "WebHost",
        "Kestrel",
        "TelemetryClient",
        "ApplicationInsights",
        "OpenTelemetry",
        "SentrySdk",
        "AnalyticsService",
        "WasapiCapture",
        "WaveInEvent",
        "SpeechRecognizer",
        "OpenAIClient",
        "Azure.AI"
    ];

    [Fact]
    public void ProductionProjects_DoNotReferenceTelemetryBrowserBackendOrAiPackages()
    {
        var packageNames = ProductionProjectFiles
            .Select(path => Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
            .SelectMany(ReadPackageReferences)
            .ToList();

        packageNames.Should().NotContain(package =>
            ForbiddenPackageNameFragments.Any(fragment => package.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ProductionSource_DoesNotContainNetworkTelemetryMicrophoneOrAiRuntimeCode()
    {
        var sourceFiles = ProductionProjectFiles
            .Select(path => Path.GetDirectoryName(Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))!)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var violations = sourceFiles
            .SelectMany(path =>
            {
                var lines = File.ReadAllLines(path);
                return ForbiddenSourceTerms
                    .SelectMany(term => lines
                        .Where(line => line.Contains(term, StringComparison.OrdinalIgnoreCase))
                        // Escape hatch: a line marked `// privacy-allow: <reason>` is intentionally allowed.
                        // Use sparingly — every exemption should have a documented reason in the comment.
                        .Where(line => !line.Contains("privacy-allow:", StringComparison.OrdinalIgnoreCase))
                        .Select(_ => $"{Path.GetRelativePath(RepositoryRoot, path)} contains {term}"));
            })
            .ToList();

        violations.Should().BeEmpty();
    }

    private static IEnumerable<string> ReadPackageReferences(string projectFile)
    {
        var document = XDocument.Load(projectFile);
        return document
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(package => !string.IsNullOrWhiteSpace(package))!;
    }
}
