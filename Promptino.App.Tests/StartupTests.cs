using Promptino.App;

namespace Promptino.App.Tests;

public class StartupTests
{
    [Fact]
    public void BuildAvaloniaApp_ShouldCreateAppBuilder()
    {
        var builder = Program.BuildAvaloniaApp();

        builder.Should().NotBeNull();
    }
}
