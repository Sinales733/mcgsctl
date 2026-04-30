using System.Text.Json;

namespace McgsCtl.Tests;

public sealed class JsonlEvidenceTests
{
    [Fact]
    public void CompactJsonlParsesLineByLine()
    {
        var lines = new[]
        {
            "{\"timestamp\":\"2026-04-30T10:00:00+08:00\",\"pid\":1234,\"hwnd\":\"0x1\",\"className\":\"#32770\",\"title\":\"prompt\",\"body\":\"text\",\"buttons\":[\"OK\"],\"action\":\"click-ok\",\"state\":\"test\",\"screenshot\":\"dialog-screenshots/0001.png\"}",
            "{\"timestamp\":\"2026-04-30T10:00:01+08:00\",\"pid\":1234,\"hwnd\":\"0x2\",\"className\":\"#32768\",\"title\":\"\",\"body\":\"\",\"buttons\":[],\"action\":\"observe\",\"state\":\"test\",\"screenshot\":null}"
        };

        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }
    }

    [Fact]
    public void PrettyJsonIsNotValidLineByLineJsonl()
    {
        var prettyLines = new[]
        {
            "{",
            "  \"timestamp\": \"2026-04-30T10:00:00+08:00\",",
            "  \"action\": \"click-ok\"",
            "}"
        };

        Assert.Contains(prettyLines, line =>
        {
            try
            {
                using var _ = JsonDocument.Parse(line);
                return false;
            }
            catch (JsonException)
            {
                return true;
            }
        });
    }

    [Fact]
    public void EmptyJsonlIsAccepted()
    {
        var lines = Array.Empty<string>();
        foreach (var line in lines)
        {
            using var _ = JsonDocument.Parse(line);
        }
    }
}
