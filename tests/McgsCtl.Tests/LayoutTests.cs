using System.Text;
using System.Text.Json.Nodes;

namespace McgsCtl.Tests;

public sealed class LayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mcgsctl-layout-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ValidLayoutPassesAndPreviewWritesArtifacts()
    {
        var layout = WriteLayout("valid", ValidLayout());
        var safety = WriteSafety("valid", "MCGSCTL_SW", "V603.0");
        var validate = TestCli.Run("layout", "validate", "--layout", layout, "--safety", safety);
        var previewDir = Path.Combine(_root, "preview");
        var preview = TestCli.Run("layout", "preview", "--layout", layout, "--safety", safety, "--out", previewDir);

        Assert.Equal(0, validate.ExitCode);
        Assert.Contains("\"Status\": \"PASS\"", validate.Stdout);
        Assert.Equal(0, preview.ExitCode);
        Assert.True(File.Exists(Path.Combine(previewDir, "preview.svg")));
        Assert.True(File.Exists(Path.Combine(previewDir, "preview.html")));
        Assert.True(File.Exists(Path.Combine(previewDir, "preview.json")));
        Assert.True(File.Exists(Path.Combine(previewDir, "validate.json")));
    }

    [Fact]
    public void DuplicateIdIsBlocked()
    {
        var json = ValidLayout();
        var objects = json["objects"]!.AsArray();
        objects.Add(new JsonObject
        {
            ["id"] = "jog-up",
            ["kind"] = "status-button",
            ["text"] = "DUP",
            ["expression"] = "MCGSCTL_SW",
            ["x"] = 300,
            ["y"] = 300,
            ["width"] = 100,
            ["height"] = 40
        });
        var layout = WriteLayout("duplicate", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("duplicate object id: jog-up", result.Stdout);
    }

    [Fact]
    public void OverlapIsBlocked()
    {
        var json = ValidLayout();
        json["objects"]![1]!["x"] = 120;
        json["objects"]![1]!["y"] = 120;
        var layout = WriteLayout("overlap", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("objects overlap", result.Stdout);
    }

    [Fact]
    public void OutOfCanvasIsBlocked()
    {
        var json = ValidLayout();
        json["objects"]![0]!["x"] = 1000;
        var layout = WriteLayout("out-of-canvas", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("rect is outside canvas", result.Stdout);
    }

    [Fact]
    public void MissingMomentaryVariableIsBlocked()
    {
        var json = ValidLayout();
        json["objects"]![0]!.AsObject().Remove("variable");
        var layout = WriteLayout("missing-variable", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("variable is required", result.Stdout);
    }

    [Fact]
    public void SafetySpecMismatchIsBlocked()
    {
        var layout = WriteLayout("safety-mismatch", ValidLayout());
        var safety = WriteSafety("safety-mismatch", "OTHER_VAR", "V603.0");

        var result = TestCli.Run("layout", "validate", "--layout", layout, "--safety", safety);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("missing from safety addressPlan", result.Stdout);
    }

    [Fact]
    public void DangerousQMappingIsBlocked()
    {
        var layout = WriteLayout("dangerous-q", ValidLayout());
        var safety = WriteSafety("dangerous-q", "MCGSCTL_SW", "Q0.0", dangerous: true);

        var result = TestCli.Run("layout", "validate", "--layout", layout, "--safety", safety);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("maps directly to dangerous output", result.Stdout);
    }

    private string WriteLayout(string name, JsonObject json)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name + ".layout.json");
        File.WriteAllText(path, json.ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);
        return path;
    }

    private string WriteSafety(string name, string dataObject, string address, bool dangerous = false)
    {
        var path = Path.Combine(_root, name + ".safety.json");
        var json = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["project"] = "TEST",
            ["plcStaticVerification"] = new JsonObject
            {
                ["requiresAwl"] = false,
                ["unknownBlocksApply"] = true
            },
            ["plc"] = new JsonObject
            {
                ["type"] = "Other",
                ["addressPlan"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = address,
                        ["dataObject"] = dataObject,
                        ["direction"] = "hmi_to_plc",
                        ["kind"] = "momentary"
                    }
                }
            },
            ["dangerousOutputs"] = dangerous ? new JsonArray(address) : new JsonArray()
        };
        File.WriteAllText(path, json.ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);
        return path;
    }

    private static JsonObject ValidLayout()
        => new()
        {
            ["schemaVersion"] = 1,
            ["windowIndex"] = 0,
            ["canvas"] = new JsonObject
            {
                ["width"] = 640,
                ["height"] = 480,
                ["grid"] = 10
            },
            ["objects"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "jog-up",
                    ["kind"] = "momentary-button",
                    ["text"] = "UP",
                    ["variable"] = "MCGSCTL_SW",
                    ["x"] = 120,
                    ["y"] = 120,
                    ["width"] = 90,
                    ["height"] = 50
                },
                new JsonObject
                {
                    ["id"] = "ready",
                    ["kind"] = "status-button",
                    ["text"] = "READY",
                    ["expression"] = "MCGSCTL_SW",
                    ["x"] = 240,
                    ["y"] = 120,
                    ["width"] = 110,
                    ["height"] = 32
                }
            }
        };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
