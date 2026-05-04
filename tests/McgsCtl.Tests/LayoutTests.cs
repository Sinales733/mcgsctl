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

    [Fact]
    public void SectionTitleUsesNativeStaticTextByDefault()
    {
        var layout = WriteLayout("synthetic-title", SectionLayout());
        var previewDir = Path.Combine(_root, "synthetic-preview");

        var result = TestCli.Run("layout", "preview", "--layout", layout, "--out", previewDir);

        Assert.Equal(0, result.ExitCode);
        var previewJson = File.ReadAllText(Path.Combine(previewDir, "preview.json"), Encoding.UTF8);
        Assert.Contains("\"kind\": \"section-title\"", previewJson);
        Assert.Contains("\"guiKind\": \"native-static-text\"", previewJson);
        Assert.DoesNotContain("constant visibility", previewJson);
    }

    [Fact]
    public void StaticLabelCanStillRenderAsStatusButtonFallback()
    {
        var json = ValidLayout();
        json["objects"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "fallback-label",
            ["kind"] = "static-label",
            ["text"] = "Fallback",
            ["renderAs"] = "status-button",
            ["x"] = 120,
            ["y"] = 220,
            ["width"] = 120,
            ["height"] = 24
        });
        var layout = WriteLayout("fallback-label", json);
        var previewDir = Path.Combine(_root, "fallback-preview");

        var result = TestCli.Run("layout", "preview", "--layout", layout, "--out", previewDir);

        Assert.Equal(0, result.ExitCode);
        var previewJson = File.ReadAllText(Path.Combine(previewDir, "preview.json"), Encoding.UTF8);
        Assert.Contains("\"id\": \"fallback-label\"", previewJson);
        Assert.Contains("\"guiKind\": \"status-button\"", previewJson);
        Assert.Contains("constant visibility", previewJson);
    }

    [Fact]
    public void NativeLampLayoutPassesValidation()
    {
        var json = ValidLayout();
        json["objects"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "native-lamp",
            ["kind"] = "native-lamp",
            ["text"] = "LAMP",
            ["expression"] = "MCGSCTL_SW",
            ["x"] = 120,
            ["y"] = 220,
            ["width"] = 120,
            ["height"] = 75
        });
        var layout = WriteLayout("native-lamp", json);
        var previewDir = Path.Combine(_root, "native-lamp-preview");

        var result = TestCli.Run("layout", "preview", "--layout", layout, "--out", previewDir);

        Assert.Equal(0, result.ExitCode);
        var previewJson = File.ReadAllText(Path.Combine(previewDir, "preview.json"), Encoding.UTF8);
        Assert.Contains("\"kind\": \"native-lamp\"", previewJson);
        Assert.Contains("\"guiKind\": \"native-lamp\"", previewJson);
        Assert.Contains("animation display component", previewJson);
    }

    [Fact]
    public void PreviewOnlyStaticLabelDoesNotBecomeGuiSupported()
    {
        var json = ValidLayout();
        json["objects"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "note",
            ["kind"] = "static-label",
            ["text"] = "Preview only",
            ["renderAs"] = "preview-only",
            ["x"] = 120,
            ["y"] = 220,
            ["width"] = 120,
            ["height"] = 24
        });
        var layout = WriteLayout("preview-only-label", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("preview evidence only", result.Stdout);
    }

    [Fact]
    public void UnsupportedRenderAsIsBlocked()
    {
        var json = ValidLayout();
        json["objects"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "bad-label",
            ["kind"] = "static-label",
            ["text"] = "Bad",
            ["renderAs"] = "native-text",
            ["x"] = 120,
            ["y"] = 220,
            ["width"] = 120,
            ["height"] = 24
        });
        var layout = WriteLayout("bad-render-as", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("unsupported renderAs native-text", result.Stdout);
    }

    [Fact]
    public void InternalOccupancyWithoutCanvasObjectsIsUnknown()
    {
        var json = ValidLayout();
        json["placement"] = new JsonObject { ["mode"] = "internal-occupancy" };
        var layout = WriteLayout("internal-no-map", json);

        var result = TestCli.Run("layout", "validate", "--layout", layout);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("\"Status\": \"UNKNOWN\"", result.Stdout);
        Assert.Contains("internal occupancy placement requires", result.Stdout);
    }

    [Fact]
    public void InternalOccupancyWithReliableCanvasMapWritesPlanAndOverlay()
    {
        var json = ValidLayout();
        json["placement"] = new JsonObject
        {
            ["mode"] = "internal-occupancy",
            ["margin"] = 20
        };
        var layout = WriteLayout("internal-map", json);
        var map = WriteCanvasObjects("internal-map", reliable: true);
        var previewDir = Path.Combine(_root, "internal-map-preview");

        var result = TestCli.Run("layout", "preview", "--layout", layout, "--canvas-objects", map, "--out", previewDir);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(previewDir, "layout-plan.json")));
        Assert.True(File.Exists(Path.Combine(previewDir, "preview-overlay.svg")));
        var previewJson = File.ReadAllText(Path.Combine(previewDir, "preview.json"), Encoding.UTF8);
        Assert.Contains("\"placementSource\": \"internal-occupancy\"", previewJson);
        Assert.Contains("\"status\": \"PASS\"", previewJson);
        Assert.DoesNotContain("\"x\": 120", previewJson);
    }

    [Fact]
    public void InternalOccupancyAcceptsObjectRectMap()
    {
        var json = ValidLayout();
        json["placement"] = new JsonObject
        {
            ["mode"] = "internal-occupancy",
            ["margin"] = 20
        };
        var layout = WriteLayout("internal-object-rect-map", json);
        var map = WriteCanvasObjectRectMap("internal-object-rect-map");
        var previewDir = Path.Combine(_root, "internal-object-rect-map-preview");

        var result = TestCli.Run("layout", "preview", "--layout", layout, "--canvas-objects", map, "--out", previewDir);

        Assert.Equal(0, result.ExitCode);
        var planJson = File.ReadAllText(Path.Combine(previewDir, "layout-plan.json"), Encoding.UTF8);
        Assert.Contains("\"objectProvider\": \"mce-geometry-inferred\"", planJson);
        Assert.Contains("\"id\": \"known-mce-object\"", planJson);
        Assert.Contains("\"margin\": 20", planJson);
        Assert.Contains("\"placementSource\": \"internal-occupancy\"", File.ReadAllText(Path.Combine(previewDir, "preview.json"), Encoding.UTF8));
    }

    [Fact]
    public void InternalOccupancyWithUnreliableCanvasMapIsUnknown()
    {
        var json = ValidLayout();
        json["placement"] = new JsonObject { ["mode"] = "internal-occupancy" };
        var layout = WriteLayout("internal-unreliable", json);
        var map = WriteCanvasObjects("internal-unreliable", reliable: false);

        var result = TestCli.Run("layout", "validate", "--layout", layout, "--canvas-objects", map);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("\"Status\": \"UNKNOWN\"", result.Stdout);
        Assert.Contains("canvas object map is not reliable", result.Stdout);
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

    private string WriteCanvasObjects(string name, bool reliable)
    {
        var path = Path.Combine(_root, name + ".canvas-objects.json");
        var json = new JsonObject
        {
            ["SchemaVersion"] = 1,
            ["Status"] = reliable ? "PASS" : "UNKNOWN",
            ["ObjectProvider"] = reliable ? "uia" : "none",
            ["ReliableGeometry"] = reliable,
            ["BlockedReasons"] = reliable ? new JsonArray() : new JsonArray("no object provider"),
            ["OccupiedRectangles"] = new JsonArray
            {
                new JsonObject
                {
                    ["Id"] = "existing-panel",
                    ["Kind"] = "existing",
                    ["Text"] = "Existing",
                    ["X"] = 0,
                    ["Y"] = 0,
                    ["Width"] = 430,
                    ["Height"] = 210,
                    ["Source"] = "test",
                    ["Confidence"] = "high"
                }
            }
        };
        File.WriteAllText(path, json.ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);
        return path;
    }

    private string WriteCanvasObjectRectMap(string name)
    {
        var path = Path.Combine(_root, name + ".canvas-objects.json");
        var json = new JsonObject
        {
            ["SchemaVersion"] = 1,
            ["Status"] = "PASS",
            ["ObjectProvider"] = "mce-geometry-inferred",
            ["ReliableGeometry"] = true,
            ["BlockedReasons"] = new JsonArray(),
            ["Objects"] = new JsonArray
            {
                new JsonObject
                {
                    ["Id"] = "known-mce-object",
                    ["Kind"] = "native-static-text",
                    ["Text"] = "Known",
                    ["Source"] = "mce-geometry-inferred",
                    ["Confidence"] = "high",
                    ["Rect"] = new JsonObject
                    {
                        ["Id"] = "known-mce-object",
                        ["Kind"] = "native-static-text",
                        ["Text"] = "Known",
                        ["X"] = 0,
                        ["Y"] = 0,
                        ["Width"] = 430,
                        ["Height"] = 210,
                        ["Source"] = "mce-geometry-inferred",
                        ["Confidence"] = "high"
                    }
                }
            }
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

    private static JsonObject SectionLayout()
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
            ["sections"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "ptz",
                    ["title"] = "PTZ",
                    ["x"] = 80,
                    ["y"] = 80,
                    ["width"] = 360,
                    ["height"] = 220,
                    ["layout"] = "direction-pad",
                    ["controls"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "jog-up",
                            ["kind"] = "momentary-button",
                            ["text"] = "UP",
                            ["variable"] = "MCGSCTL_SW",
                            ["position"] = "up"
                        }
                    },
                    ["indicators"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "ready",
                            ["kind"] = "status-button",
                            ["text"] = "READY",
                            ["expression"] = "MCGSCTL_SW"
                        }
                    }
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
