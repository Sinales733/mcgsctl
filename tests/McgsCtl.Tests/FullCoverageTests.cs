using System.Text;
using System.Text.Json.Nodes;

namespace McgsCtl.Tests;

public sealed class FullCoverageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mcgsctl-full-coverage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ToolCatalogReadsToolbarProbeEvidence()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "绘图工具条" },
              "buttons": [
                { "Index": 0, "IdCommand": 32938, "Enabled": true, "Hidden": false, "Text": "按钮" },
                { "Index": 1, "IdCommand": 0, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var catalog = File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8);
        Assert.Contains("\"toolId\": \"toolbar:1:0:32938\"", catalog);
        Assert.Contains("\"supportStatus\": \"needs-probe\"", catalog);
        Assert.Contains("\"toolId\": \"mcgsctl:canvas:property-map-probe\"", catalog);
        Assert.True(File.Exists(Path.Combine(outDir, "function-catalog.json")));
    }

    [Fact]
    public void PropertyMapMakesMissingFieldsExplicit()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var semantic = Path.Combine(_root, "semantic-map.json");
        File.WriteAllText(semantic, BuildSemanticMap().ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);
        var outDir = Path.Combine(_root, "property-map");

        var result = TestCli.Run("canvas", "property-map-probe", "--project", project, "--semantic-map", semantic, "--row-key", "2", "--out", outDir);

        Assert.Equal(2, result.ExitCode);
        var map = File.ReadAllText(Path.Combine(outDir, "property-map.json"), Encoding.UTF8);
        var root = JsonNode.Parse(map)!.AsObject();
        var first = root["objects"]!.AsArray()[0]!.AsObject();
        var properties = first["properties"]!.AsObject();
        Assert.Contains("\"status\": \"UNKNOWN\"", map);
        Assert.Contains("\"objectCount\": 1", map);
        Assert.Equal("value", properties["fontFamily"]!["status"]!.GetValue<string>());
        Assert.Equal("幼圆", properties["fontFamily"]!["value"]!.GetValue<string>());
        Assert.Equal("unresolved", properties["fontSize"]!["status"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(properties["fontSize"]!["nextProbe"]!.GetValue<string>()));
    }

    private static JsonObject BuildSemanticMap()
        => new()
        {
            ["schemaVersion"] = 1,
            ["status"] = "PASS",
            ["rowKey"] = "2",
            ["objects"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "mce-sem-r2-0001",
                    ["rowKey"] = "2",
                    ["semanticKind"] = "section-title",
                    ["displayedText"] = "阈值设置",
                    ["variable"] = "",
                    ["expression"] = "",
                    ["scriptStatus"] = "empty",
                    ["scriptSummary"] = "",
                    ["confidence"] = 0.82,
                    ["rect"] = new JsonObject
                    {
                        ["x"] = 61,
                        ["y"] = 7,
                        ["width"] = 284,
                        ["height"] = 73
                    },
                    ["evidenceSources"] = new JsonArray("mce-rect-anchor", "mce-text-anchor"),
                    ["evidenceChain"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["source"] = "mce-control-anchor",
                            ["payload"] = new JsonArray
                            {
                                new JsonObject { ["sample"] = "H#/(1/:阈值设置/)" },
                                new JsonObject { ["sample"] = "1幼圆" }
                            }
                        }
                    }
                }
            }
        };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temp cleanup is best-effort.
        }
    }
}
