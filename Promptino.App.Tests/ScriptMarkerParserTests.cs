using FluentAssertions;
using Promptino.Core.Scripts;
using System.Linq;

namespace Promptino.App.Tests;

public class ScriptMarkerParserTests
{
    [Fact]
    public void ParseAndRemoveMarkers_ExtractsMarkersCorrectly()
    {
        var script = "hello world [[marker:Intro]] this is [[marker]] a test [[marker: Outro ]]";
        
        var cleaned = ScriptMarkerParser.ParseAndRemoveMarkers(script, out var markers);
        
        cleaned.Should().Be("hello world  this is  a test ");
        
        markers.Should().HaveCount(3);
        markers[0].Label.Should().Be("Intro");
        markers[0].Order.Should().Be(1);
        
        markers[1].Label.Should().Be("Marker 2");
        markers[1].Order.Should().Be(2);
        
        markers[2].Label.Should().Be("Outro");
        markers[2].Order.Should().Be(3);
    }

    [Fact]
    public void ParseAndRemoveMarkers_CalculatesExactProgressRatio()
    {
        var script = "one two three [[marker:mid]] four five";
        var cleaned = ScriptMarkerParser.ParseAndRemoveMarkers(script, out var markers);
        
        // cleaned: "one two three  four five" -> 5 words
        // mid marker should be at word 3. Ratio = 3 / 5 = 0.6
        
        markers.Should().HaveCount(1);
        markers[0].ProgressRatio.Should().Be(0.6);
    }

    [Fact]
    public void ParseAndRemoveMarkers_HandlesEmptyOrNull()
    {
        var cleaned = ScriptMarkerParser.ParseAndRemoveMarkers("", out var markers);
        cleaned.Should().BeEmpty();
        markers.Should().BeEmpty();
    }
}
