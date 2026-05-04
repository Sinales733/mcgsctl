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
        Assert.Contains("\"supportStatus\": \"implemented\"", catalog);
        Assert.Contains("\"expectedEffect\": \"creates a standard button object used by momentary/status-button workflows\"", catalog);
        Assert.Contains("\"toolId\": \"mcgsctl:canvas:property-map-probe\"", catalog);
        Assert.True(File.Exists(Path.Combine(outDir, "function-catalog.json")));
        var functions = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "function-catalog.json"), Encoding.UTF8))!.AsObject();
        Assert.True(functions["functionCount"]!.GetValue<int>() >= 17);
        Assert.Equal(4, functions["toolBackedFunctionCount"]!.GetValue<int>());
        var toolFunction = functions["functions"]!.AsArray().Select(n => n!.AsObject())
            .First(f => f["functionId"]!.GetValue<string>() == "tool:toolbar:1:0:32938");
        Assert.Equal("candidate-safe-mutation", toolFunction["safetyClass"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(toolFunction["inputs"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(toolFunction["outputs"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(toolFunction["sideEffects"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(toolFunction["validationReadbackMethod"]!.GetValue<string>()));
    }

    [Fact]
    public void ToolCatalogClassifiesMfcStandardCommandsConservatively()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-mfc.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "标准工具条" },
              "buttons": [
                { "Index": 0, "IdCommand": 57600, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 1, "IdCommand": 57601, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 2, "IdCommand": 57607, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 3, "IdCommand": 57634, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 4, "IdCommand": 57635, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 5, "IdCommand": 57669, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog-mfc");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8))!.AsObject();
        var tools = root["tools"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        Assert.Equal("needs-precondition", FindTool(tools, 57600)["supportStatus"]!.GetValue<string>());
        Assert.Equal("formal-apply-required", FindTool(tools, 57601)["safetyClass"]!.GetValue<string>());
        Assert.Equal("blocked", FindTool(tools, 57607)["supportStatus"]!.GetValue<string>());
        Assert.Equal("implemented", FindTool(tools, 57634)["supportStatus"]!.GetValue<string>());
        Assert.Equal("read-only", FindTool(tools, 57634)["safetyClass"]!.GetValue<string>());
        Assert.Equal("candidate-safe-mutation", FindTool(tools, 57635)["safetyClass"]!.GetValue<string>());
        Assert.Equal("needs-precondition", FindTool(tools, 57669)["supportStatus"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCatalogBlocksUnidentifiedAnimationCommand34026()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-34026.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "动画组态工具条" },
              "buttons": [
                { "Index": 30, "IdCommand": 34026, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog-34026");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8))!.AsObject();
        var tool = root["tools"]!.AsArray().Select(n => n!.AsObject())
            .First(t => t["commandId"]?.GetValue<int>() == 34026);
        Assert.Equal("blocked", tool["supportStatus"]!.GetValue<string>());
        Assert.Equal("unknown-risk", tool["safetyClass"]!.GetValue<string>());
        Assert.Contains("static handler analysis", tool["nextProbe"]!.GetValue<string>());
    }

    [Fact]
    public void ToolSweepRemainsUnknownWhilePreconditionsRemain()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-precondition.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "鏍囧噯宸ュ叿鏉? },
              "buttons": [
                { "Index": 0, "IdCommand": 57609, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 1, "IdCommand": 57634, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "Standard toolbar" },
              "buttons": [
                { "Index": 0, "IdCommand": 57609, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 1, "IdCommand": 57634, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var catalogDir = Path.Combine(_root, "catalog-precondition");
        var catalogResult = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", catalogDir);
        Assert.Equal(0, catalogResult.ExitCode);

        var sweepDir = Path.Combine(_root, "sweep-precondition");
        var sweepResult = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"), "--out", sweepDir);

        Assert.Equal(2, sweepResult.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("UNKNOWN", sweep["status"]!.GetValue<string>());
        Assert.True(sweep["needsPreconditionCount"]!.GetValue<int>() > 0);
        Assert.Contains("unmet preconditions", sweep["blockedReasons"]!.AsArray()[0]!.GetValue<string>());
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

    [Fact]
    public void PropertyMapUsesLatestFontDialogReadback()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var semantic = Path.Combine(_root, "semantic-map.json");
        File.WriteAllText(semantic, BuildSemanticMap().ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);

        var readbackRoot = Path.Combine(_root, "readbacks");
        var oldDir = Path.Combine(readbackRoot, "old");
        var newDir = Path.Combine(readbackRoot, "new");
        Directory.CreateDirectory(oldDir);
        Directory.CreateDirectory(newDir);
        File.WriteAllText(Path.Combine(oldDir, "property-readback.json"), BuildPropertyReadback("宋体", "常规", "五号"), Encoding.UTF8);
        Thread.Sleep(20);
        File.WriteAllText(Path.Combine(newDir, "property-readback.json"), BuildPropertyReadback("幼圆", "加粗", "小四"), Encoding.UTF8);

        var outDir = Path.Combine(_root, "property-map-font");
        var result = TestCli.Run("canvas", "property-map-probe", "--project", project, "--semantic-map", semantic,
            "--row-key", "2", "--property-readback-dir", readbackRoot, "--out", outDir);

        Assert.Equal(2, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "property-map.json"), Encoding.UTF8))!.AsObject();
        var properties = root["objects"]!.AsArray()[0]!.AsObject()["properties"]!.AsObject();
        Assert.Equal("value", properties["fontFamily"]!["status"]!.GetValue<string>());
        Assert.Equal("幼圆", properties["fontFamily"]!["value"]!.GetValue<string>());
        Assert.Equal("小四", properties["fontSize"]!["value"]!.GetValue<string>());
        Assert.Equal("加粗", properties["fontStyle"]!["value"]!.GetValue<string>());
    }

    [Fact]
    public void PropertyMapUsesPropertyDialogDefaultsForNumericInput()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var semantic = Path.Combine(_root, "semantic-map-numeric.json");
        File.WriteAllText(semantic, BuildNumericSemanticMap().ToJsonString(new() { WriteIndented = true }), Encoding.UTF8);

        var readbackRoot = Path.Combine(_root, "numeric-readback");
        Directory.CreateDirectory(readbackRoot);
        File.WriteAllText(Path.Combine(readbackRoot, "property-readback.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "objectId": "mce-sem-r2-0000",
          "semanticKind": "numeric-input",
          "displayedText": "启喷设定",
          "tabs": [
            {
              "Text": "基本属性",
              "controls": [
                { "sequence": 1, "className": "Button", "text": "三维边框", "checkState": 1, "visible": true, "rect": { "x": 1, "y": 1, "width": 10, "height": 10 } },
                { "sequence": 2, "className": "Static", "text": "背景颜色", "visible": true, "rect": { "x": 1, "y": 20, "width": 10, "height": 10 } },
                { "sequence": 3, "className": "Button", "text": "Button3", "checkState": 0, "visible": true, "rect": { "x": 1, "y": 40, "width": 10, "height": 10 } },
                { "sequence": 4, "className": "Static", "text": "字符颜色", "visible": true, "rect": { "x": 1, "y": 60, "width": 10, "height": 10 } },
                { "sequence": 5, "className": "Button", "text": "Button5", "checkState": 0, "visible": true, "rect": { "x": 1, "y": 80, "width": 10, "height": 10 } }
              ]
            },
            {
              "Text": "操作属性",
              "controls": [
                { "sequence": 1, "className": "Static", "text": "最小值", "visible": true, "rect": { "x": 1, "y": 1, "width": 10, "height": 10 } },
                { "sequence": 2, "className": "Edit", "text": "0", "visible": true, "rect": { "x": 1, "y": 20, "width": 10, "height": 10 } },
                { "sequence": 3, "className": "Static", "text": "最大值", "visible": true, "rect": { "x": 1, "y": 40, "width": 10, "height": 10 } },
                { "sequence": 4, "className": "Edit", "text": "4", "visible": true, "rect": { "x": 1, "y": 60, "width": 10, "height": 10 } },
                { "sequence": 5, "className": "Button", "text": "十进制", "checkState": 0, "visible": true, "rect": { "x": 1, "y": 80, "width": 10, "height": 10 } },
                { "sequence": 6, "className": "Button", "text": "四舍五入", "checkState": 1, "visible": true, "rect": { "x": 1, "y": 100, "width": 10, "height": 10 } },
                { "sequence": 7, "className": "Button", "text": "使用单位", "checkState": 1, "visible": true, "rect": { "x": 1, "y": 120, "width": 10, "height": 10 } },
                { "sequence": 8, "className": "Static", "text": "整数位数", "visible": true, "rect": { "x": 1, "y": 140, "width": 10, "height": 10 } },
                { "sequence": 9, "className": "Edit", "text": "0", "visible": true, "rect": { "x": 1, "y": 160, "width": 10, "height": 10 } },
                { "sequence": 10, "className": "Static", "text": "小数位数", "visible": true, "rect": { "x": 1, "y": 180, "width": 10, "height": 10 } },
                { "sequence": 11, "className": "Edit", "text": "3", "visible": true, "rect": { "x": 1, "y": 200, "width": 10, "height": 10 } },
                { "sequence": 12, "className": "Edit", "text": "Mpa", "visible": true, "rect": { "x": 1, "y": 220, "width": 10, "height": 10 } },
                { "sequence": 13, "className": "Static", "text": "显示效果-例:", "visible": true, "rect": { "x": 1, "y": 240, "width": 10, "height": 10 } },
                { "sequence": 14, "className": "Static", "text": "80.345678", "visible": true, "rect": { "x": 1, "y": 260, "width": 10, "height": 10 } },
                { "sequence": 15, "className": "Edit", "text": "80.346Mpa", "visible": true, "rect": { "x": 1, "y": 280, "width": 10, "height": 10 } }
              ]
            },
            {
              "Text": "可见度属性",
              "controls": [
                { "sequence": 1, "className": "Button", "text": "表达式", "checkState": 0, "visible": true, "rect": { "x": 1, "y": 1, "width": 10, "height": 10 } },
                { "sequence": 2, "className": "Edit", "text": "", "visible": true, "rect": { "x": 1, "y": 20, "width": 10, "height": 10 } },
                { "sequence": 3, "className": "Button", "text": "输入框构件可见", "checkState": 1, "visible": true, "rect": { "x": 1, "y": 40, "width": 10, "height": 10 } },
                { "sequence": 4, "className": "Button", "text": "权限(&A)", "checkState": 0, "visible": true, "rect": { "x": 1, "y": 60, "width": 10, "height": 10 } }
              ]
            }
          ],
          "fontDialog": {
            "status": "notApplicable",
            "reason": "property dialog has no visible font button on the current profile/page"
          }
        }
        """, Encoding.UTF8);

        var outDir = Path.Combine(_root, "property-map-numeric");
        var result = TestCli.Run("canvas", "property-map-probe", "--project", project, "--semantic-map", semantic,
            "--row-key", "2", "--property-readback-dir", readbackRoot, "--out", outDir);

        Assert.Equal(2, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "property-map.json"), Encoding.UTF8))!.AsObject();
        var properties = root["objects"]!.AsArray()[0]!.AsObject()["properties"]!.AsObject();
        Assert.Equal("notApplicable", properties["fontFamily"]!["status"]!.GetValue<string>());
        Assert.Equal("value", properties["inputFormat"]!["status"]!.GetValue<string>());
        Assert.Equal("decimal-inferred-from-display-example", properties["inputFormat"]!["value"]!["numberBase"]!.GetValue<string>());
        Assert.Equal("value", properties["foregroundColor"]!["status"]!.GetValue<string>());
        Assert.Equal("value", properties["enableCondition"]!["status"]!.GetValue<string>());
        Assert.Equal("unresolved", properties["permissions"]!["status"]!.GetValue<string>());
    }

    private static string BuildPropertyReadback(string family, string style, string size)
        => $$"""
        {
          "schemaVersion": 1,
          "status": "PASS",
          "objectId": "mce-sem-r2-0001",
          "tabs": [
            {
              "Text": "属性设置",
              "controls": [
                { "sequence": 1, "className": "Static", "text": "文本内容输入", "visible": true, "rect": { "x": 1, "y": 1, "width": 10, "height": 10 } },
                { "sequence": 2, "className": "Edit", "text": "阈值设置", "visible": true, "rect": { "x": 1, "y": 20, "width": 10, "height": 10 } }
              ]
            }
          ],
          "fontDialog": {
            "status": "PASS",
            "extracted": {
              "fontFamily": "{{family}}",
              "fontStyle": "{{style}}",
              "fontSize": "{{size}}"
            }
          }
        }
        """;

    private static JsonObject FindTool(JsonObject[] tools, int commandId)
        => tools.First(t => t["commandId"]?.GetValue<int>() == commandId);

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

    private static JsonObject BuildNumericSemanticMap()
        => new()
        {
            ["schemaVersion"] = 1,
            ["status"] = "PASS",
            ["rowKey"] = "2",
            ["objects"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "mce-sem-r2-0000",
                    ["rowKey"] = "2",
                    ["semanticKind"] = "numeric-input",
                    ["displayedText"] = "启喷设定",
                    ["variable"] = "启喷设定",
                    ["expression"] = "Mpa",
                    ["scriptStatus"] = "empty",
                    ["scriptSummary"] = "",
                    ["confidence"] = 0.82,
                    ["rect"] = new JsonObject
                    {
                        ["x"] = 180,
                        ["y"] = 80,
                        ["width"] = 141,
                        ["height"] = 42
                    },
                    ["evidenceSources"] = new JsonArray("mce-rect-anchor", "mce-text-anchor")
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
