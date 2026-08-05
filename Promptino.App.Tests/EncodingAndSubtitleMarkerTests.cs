using System.IO;
using System.Text;
using FluentAssertions;
using Promptino.App.Services;
using Promptino.Core.Scripts;
using Xunit;

namespace Promptino.App.Tests;

public class EncodingAndSubtitleMarkerTests
{
    static EncodingAndSubtitleMarkerTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void DetectAndReadText_DecodesWindows1252_ItalianAccentedCharacters()
    {
        var win1252 = Encoding.GetEncoding(1252);
        var originalText = "GIORNO 5: LA LIBERTÀ - PERCHÉ - PIÙ - COSÌ";
        var win1252Bytes = win1252.GetBytes(originalText);

        var decoded = LocalScriptFileReader.DetectAndReadText(win1252Bytes);

        decoded.Should().Be(originalText);
    }

    [Fact]
    public void ParseSubtitleMarkers_ExtractsSrtTimestampsAsMarkers()
    {
        var srtContent = @"1
00:00:05,000 --> 00:00:10,000
GIORNO 3: LA MALATTIA

2
00:00:15,000 --> 00:00:20,000
GIORNO 5: LA LIBERTÀ

3
00:00:25,000 --> 00:00:30,000
GIORNO 6: LA FINE";

        var markers = ScriptMarkerParser.ParseSubtitleMarkers(srtContent);

        markers.Should().HaveCount(3);
        markers[0].Label.Should().Be("00:00:05");
        markers[1].Label.Should().Be("00:00:15");
        markers[2].Label.Should().Be("00:00:25");

        markers[0].ProgressRatio.Should().BeLessThan(markers[1].ProgressRatio);
        markers[1].ProgressRatio.Should().BeLessThan(markers[2].ProgressRatio);
    }
}
