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
        Assert.Equal("blocked", FindTool(tools, 57600)["supportStatus"]!.GetValue<string>());
        Assert.Equal("formal-apply-required", FindTool(tools, 57601)["safetyClass"]!.GetValue<string>());
        Assert.Equal("blocked", FindTool(tools, 57607)["supportStatus"]!.GetValue<string>());
        Assert.Equal("implemented", FindTool(tools, 57634)["supportStatus"]!.GetValue<string>());
        Assert.Equal("read-only", FindTool(tools, 57634)["safetyClass"]!.GetValue<string>());
        Assert.Equal("candidate-safe-mutation", FindTool(tools, 57635)["safetyClass"]!.GetValue<string>());
        Assert.Equal("needs-precondition", FindTool(tools, 57669)["supportStatus"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCatalogClassifiesUnidentifiedAnimationCommand34026AsReadOnlyEditorState()
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
        Assert.Equal("needs-precondition", tool["supportStatus"]!.GetValue<string>());
        Assert.Equal("read-only", tool["safetyClass"]!.GetValue<string>());
        Assert.Contains("editor-state command", tool["expectedEffect"]!.GetValue<string>());
        Assert.Contains("normalized-diff evidence", tool["evidenceSource"]!.GetValue<string>());
        Assert.Contains("message map", tool["nextProbe"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCatalogClassifiesKnownAnimationStyleCommandWithoutResourceText()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-34060.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "动画组态工具条" },
              "buttons": [
                { "Index": 33, "IdCommand": 34060, "Enabled": false, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog-34060");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8))!.AsObject();
        var tool = root["tools"]!.AsArray().Select(n => n!.AsObject())
            .First(t => t["commandId"]?.GetValue<int>() == 34060);
        Assert.Equal("needs-precondition", tool["supportStatus"]!.GetValue<string>());
        Assert.Equal("candidate-safe-mutation", tool["safetyClass"]!.GetValue<string>());
        Assert.Contains("selected animation canvas object", tool["expectedEffect"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCatalogBlocksTableEditorCommandsUntilTableFixtureExists()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-table.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "表格编辑工具条" },
              "buttons": [
                { "Index": 2, "IdCommand": 33112, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog-table");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8))!.AsObject();
        var tool = root["tools"]!.AsArray().Select(n => n!.AsObject())
            .First(t => t["commandId"]?.GetValue<int>() == 33112);
        Assert.Equal("needs-precondition", tool["supportStatus"]!.GetValue<string>());
        Assert.Equal("candidate-safe-mutation", tool["safetyClass"]!.GetValue<string>());
        Assert.Contains("canvas table object fixture", tool["nextProbe"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCatalogClassifiesToolboxTableDrawingToolsAsCandidateSafeFixtureBuilders()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var toolbarProbe = Path.Combine(_root, "toolbar-probe-toolbox-table.json");
        File.WriteAllText(toolbarProbe, """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolbars": [
            {
              "window": { "Text": "Toolbox" },
              "buttons": [
                { "Index": 24, "IdCommand": 32946, "Enabled": true, "Hidden": false, "Text": "" },
                { "Index": 25, "IdCommand": 32947, "Enabled": true, "Hidden": false, "Text": "" }
              ]
            }
          ]
        }
        """, Encoding.UTF8);
        var outDir = Path.Combine(_root, "catalog-toolbox-table");

        var result = TestCli.Run("mcgs", "tool-catalog", "--project", project, "--toolbar-probe", toolbarProbe, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "tool-catalog.json"), Encoding.UTF8))!.AsObject();
        var tools = root["tools"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        var freeTable = FindTool(tools, 32946);
        var historyTable = FindTool(tools, 32947);
        Assert.Equal("candidate-safe-mutation", freeTable["safetyClass"]!.GetValue<string>());
        Assert.Equal("needs-precondition", freeTable["supportStatus"]!.GetValue<string>());
        Assert.Contains("free table", freeTable["displayName"]!.GetValue<string>());
        Assert.Contains("animation-draw-table", freeTable["nextProbe"]!.GetValue<string>());
        Assert.Equal("candidate-safe-mutation", historyTable["safetyClass"]!.GetValue<string>());
        Assert.Contains("historical table", historyTable["displayName"]!.GetValue<string>());
        Assert.Contains("animation-draw-table", historyTable["nextProbe"]!.GetValue<string>());
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
    public void ToolSweepDoesNotTreatHashOnlyCandidateSafeProbeAsProbed()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-hash-only");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:4:14:32805",
              "displayName": "add pull-down menu",
              "source": "toolbar",
              "uiPath": "菜单组态工具条/button[14]",
              "commandId": 32805,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32805",
              "expectedEffect": "adds a pull-down menu in menu configuration",
              "evidenceSource": "test catalog",
              "nextProbe": "open menu editor and require a functional normalized MCE diff"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "hash-only");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:4:14:32805",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationFunctionalDiff": false
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-hash-only");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(2, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("UNKNOWN", sweep["status"]!.GetValue<string>());
        Assert.Equal("needs-precondition", entry["status"]!.GetValue<string>());
        Assert.Equal("PASS", entry["probeStatus"]!.GetValue<string>());
        Assert.False(entry["invoked"]!.GetValue<bool>());
    }

    [Fact]
    public void ToolSweepCanUseEquivalentCommandProbeForDuplicateToolbarTools()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-equivalent-command");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:7:57635",
              "displayName": "MFC edit cut",
              "source": "toolbar",
              "uiPath": "动画组态工具条/button[7]",
              "commandId": 57635,
              "enabled": false,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 57635",
              "expectedEffect": "cuts selected objects",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable object"
            },
            {
              "toolId": "toolbar:7:7:57635",
              "displayName": "MFC edit cut",
              "source": "toolbar",
              "uiPath": "workbench toolbar/button[7]",
              "commandId": 57635,
              "enabled": false,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 57635",
              "expectedEffect": "cuts selected objects",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable object"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "cut-animation");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:7:57635",
          "commandId": 57635,
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationFunctionalDiff": true
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-equivalent-command");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entries = sweep["entries"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        Assert.All(entries, entry => Assert.Equal("probed", entry["status"]!.GetValue<string>()));
        Assert.Equal("exact-tool", entries[0]["probeEvidenceKind"]!.GetValue<string>());
        Assert.Equal("equivalent-command", entries[1]["probeEvidenceKind"]!.GetValue<string>());
        Assert.Equal("toolbar:5:7:57635", entries[1]["equivalentProbeToolId"]!.GetValue<string>());
        Assert.NotEqual("", entries[1]["equivalentProbePath"]!.GetValue<string>());
    }

    [Fact]
    public void ToolSweepClosesEditorStyleContextProbeWithObjectNonMutationSidecar()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-style-context");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:20:32830",
              "displayName": "animation object font dialog",
              "source": "toolbar",
              "uiPath": "animation toolbar/button[20]",
              "commandId": 32830,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32830",
              "expectedEffect": "changes selected object or editor default font style",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable object and prove whether object properties changed"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "style-context");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "toolId": "toolbar:5:20:32830",
          "commandId": 32830,
          "context": "animation-single-object",
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationNotFunctional": true,
            "projectCopyHashChanged": true
          }
        }
        """, Encoding.UTF8);
        File.WriteAllText(Path.Combine(probeDir, "style-object-nonmutation-closure.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "commandId": 32830,
          "objectPropertiesUnchanged": true,
          "comparedPropertyPaths": [
            "fontFamily",
            "fontSize",
            "fontStyle"
          ]
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-style-context");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["readOnlyClosedLoopPassCount"]!.GetValue<int>());
        Assert.Equal(0, sweep["needsProbeClosureCount"]!.GetValue<int>());
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("probed", entry["status"]!.GetValue<string>());
        Assert.Equal("UNKNOWN", entry["probeStatus"]!.GetValue<string>());
        Assert.Equal("readOnlyClosedLoopPass", entry["closureStatus"]!.GetValue<string>());
        Assert.Empty(entry["missingEvidence"]!.AsArray());

        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("readOnlyClosedLoopPass", record["closureStatus"]!.GetValue<string>());
        Assert.Contains("editor-default style/context drift", record["actualEffect"]!.GetValue<string>());
        Assert.Contains("style-object-nonmutation-closure",
            string.Join("\n", record["readbackEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
    }

    [Fact]
    public void ToolSweepClosesExactPropertyDialogProbeWithoutCoveringOtherContexts()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-property-dialog");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:25:32785",
              "displayName": "animation selected object property dialog",
              "source": "toolbar",
              "uiPath": "animation toolbar/button[25]",
              "commandId": 32785,
              "enabled": true,
              "hidden": false,
              "supportStatus": "implemented",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32785",
              "expectedEffect": "opens selected animation object property dialog",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable animation object"
            },
            {
              "toolId": "toolbar:3:19:32785",
              "displayName": "device selected object property dialog",
              "source": "toolbar",
              "uiPath": "device toolbar/button[19]",
              "commandId": 32785,
              "enabled": true,
              "hidden": false,
              "supportStatus": "implemented",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32785",
              "expectedEffect": "opens selected device editor property dialog",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable device-tree object"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "property-dialog-animation");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "toolId": "toolbar:5:25:32785",
          "commandId": 32785,
          "context": "animation-single-object",
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": true,
          "evidence": {
            "candidateSafeMutation": false,
            "projectCopyHashChanged": false
          }
        }
        """, Encoding.UTF8);
        File.WriteAllText(Path.Combine(probeDir, "property-dialog-closure.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:25:32785",
          "commandId": 32785,
          "selectionVerified": true,
          "tabCount": 4,
          "controlCount": 42,
          "propertyReadback": "property-readback.json"
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-property-dialog");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("UNKNOWN", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["readOnlyClosedLoopPassCount"]!.GetValue<int>());
        Assert.Equal(1, sweep["notClosedLoopCount"]!.GetValue<int>());
        var entries = sweep["entries"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        Assert.Equal("readOnlyClosedLoopPass", entries[0]["closureStatus"]!.GetValue<string>());
        Assert.Equal("notClosedLoop", entries[1]["closureStatus"]!.GetValue<string>());
        Assert.Equal("", entries[1]["equivalentProbePath"]!.GetValue<string>());

        var records = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray().Select(n => n!.AsObject()).ToArray();
        Assert.Contains("property-dialog-closure",
            string.Join("\n", records[0]["readbackEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
        Assert.Contains("disposable copy", records[0]["actualEffect"]!.GetValue<string>());
    }

    [Fact]
    public void ToolSweepClosesExactTablessPropertyDialogOnlyWithEditorContextDiffEvidence()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-tabless-property-dialog");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:3:19:32785",
              "displayName": "device selected object property dialog",
              "source": "toolbar",
              "uiPath": "device toolbar/button[19]",
              "commandId": 32785,
              "enabled": true,
              "hidden": false,
              "supportStatus": "implemented",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32785",
              "expectedEffect": "opens selected device editor property dialog",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable device object"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "tabless-property-dialog");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "toolId": "toolbar:3:19:32785",
          "commandId": 32785,
          "context": "device-editor",
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": true,
          "evidence": {
            "candidateSafeMutation": true,
            "projectCopyHashChanged": true,
            "candidateSafeMutationFunctionalDiff": false
          }
        }
        """, Encoding.UTF8);
        File.WriteAllText(Path.Combine(probeDir, "property-dialog-closure.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:3:19:32785",
          "commandId": 32785,
          "selectionVerified": true,
          "tabCount": 0,
          "controlCount": 39,
          "tablessDialog": true,
          "windowTitle": "设备编辑窗口",
          "editorContextOnly": true,
          "normalizedDiff": "normalized-diff/mce-normalized-diff.json"
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-tabless-property-dialog");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["readOnlyClosedLoopPassCount"]!.GetValue<int>());
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("readOnlyClosedLoopPass", record["closureStatus"]!.GetValue<string>());
        Assert.Contains("property-dialog-closure",
            string.Join("\n", record["readbackEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
    }

    [Fact]
    public void ToolSweepWritesClosureRecordsAndDoesNotTreatProbePassAsUsability()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-closure");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:7:57635",
              "displayName": "MFC edit cut",
              "source": "toolbar",
              "uiPath": "animation toolbar/button[7]",
              "commandId": 57635,
              "enabled": false,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 57635",
              "expectedEffect": "cuts selected objects",
              "evidenceSource": "test catalog",
              "nextProbe": "select a disposable object"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "cut-functional-diff");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:7:57635",
          "commandId": 57635,
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationFunctionalDiff": true
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-closure");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", sweep["status"]!.GetValue<string>());
        Assert.Equal("UNKNOWN", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["notClosedLoopCount"]!.GetValue<int>());
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("probed", entry["status"]!.GetValue<string>());
        Assert.Equal("notClosedLoop", entry["closureStatus"]!.GetValue<string>());
        Assert.Contains("expected effect readback", string.Join("\n", entry["missingEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));

        var closurePath = Path.Combine(sweepDir, "tool-closure-records.json");
        Assert.True(File.Exists(closurePath));
        var closure = JsonNode.Parse(File.ReadAllText(closurePath, Encoding.UTF8))!.AsObject();
        Assert.Equal("UNKNOWN", closure["status"]!.GetValue<string>());
        var record = closure["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("notClosedLoop", record["closureStatus"]!.GetValue<string>());
        Assert.Equal("drawing-edit", record["category"]!.GetValue<string>());
        Assert.Contains("internal canvas object/property evidence",
            string.Join("\n", record["missingEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
    }

    [Fact]
    public void ToolSweepClosesDrawingCreateWithInternalClosureEvidence()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-line-closed");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:9:1:32901",
              "displayName": "line drawing tool",
              "source": "toolbar",
              "uiPath": "tools/button[1]",
              "commandId": 32901,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32901",
              "expectedEffect": "draws a line object",
              "evidenceSource": "test catalog",
              "nextProbe": "draw line on throwaway candidate"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "line-closed");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:9:1:32901",
          "commandId": 32901,
          "context": "animation-draw-object",
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationFunctionalDiff": true,
            "drawingCreateClosurePass": true
          },
          "drawingCreateClosure": {
            "status": "PASS",
            "placementSource": "internal-occupancy"
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-line-closed");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["closedLoopPassCount"]!.GetValue<int>());
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("closedLoopPass", entry["closureStatus"]!.GetValue<string>());
        Assert.Empty(entry["missingEvidence"]!.AsArray());

        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("closedLoopPass", record["closureStatus"]!.GetValue<string>());
        Assert.Equal("drawing-create", record["category"]!.GetValue<string>());
        Assert.Contains("drawing-create closure evidence PASS",
            string.Join("\n", record["afterEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
        Assert.Contains("drawing-create-closure",
            string.Join("\n", record["internalCanvasEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
    }

    [Fact]
    public void ToolSweepDoesNotCloseDrawingCreateWithoutInternalClosureEvidence()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-line-open");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:9:1:32901",
              "displayName": "line drawing tool",
              "source": "toolbar",
              "uiPath": "tools/button[1]",
              "commandId": 32901,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 32901",
              "expectedEffect": "draws a line object",
              "evidenceSource": "test catalog",
              "nextProbe": "draw line on throwaway candidate"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "line-open");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:9:1:32901",
          "commandId": 32901,
          "context": "animation-draw-object",
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": true,
            "candidateSafeMutationFunctionalDiff": true
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-line-open");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("UNKNOWN", sweep["closureStatus"]!.GetValue<string>());
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("notClosedLoop", record["closureStatus"]!.GetValue<string>());
        Assert.Contains("internal canvas object/property evidence",
            string.Join("\n", record["missingEvidence"]!.AsArray().Select(n => n!.GetValue<string>())));
    }

    [Fact]
    public void ToolSweepClosureKeepsSafetyBlockedToolsOutOfNeedsProbeQueue()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-blocked-safety");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:1:0:57600",
              "displayName": "MFC file new",
              "source": "toolbar",
              "uiPath": "standard toolbar/button[0]",
              "commandId": 57600,
              "enabled": true,
              "hidden": false,
              "supportStatus": "blocked",
              "safetyClass": "formal-apply-required",
              "invocationRoute": "WM_COMMAND 57600",
              "expectedEffect": "standard MFC File/New command",
              "evidenceSource": "test catalog",
              "nextProbe": "Do not invoke in unattended sweeps."
            }
          ]
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-blocked-safety");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", sweep["closureStatus"]!.GetValue<string>());
        Assert.Equal(1, sweep["blockedBySafetyCount"]!.GetValue<int>());
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-closure-records.json"), Encoding.UTF8))!
            .AsObject()["records"]!.AsArray()[0]!.AsObject();
        Assert.Equal("blockedBySafety", record["closureStatus"]!.GetValue<string>());
        Assert.Empty(record["missingEvidence"]!.AsArray());
    }

    [Fact]
    public void ToolSweepTreatsReversibleUndoReturnAsCandidateSafeEvidence()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-reversible-undo");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:11:57643",
              "displayName": "MFC edit undo",
              "source": "toolbar",
              "uiPath": "动画组态工具条/button[11]",
              "commandId": 57643,
              "enabled": false,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 57643",
              "expectedEffect": "undoes a controlled disposable edit",
              "evidenceSource": "test catalog",
              "nextProbe": "cut then undo"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "undo-return");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:11:57643",
          "commandId": 57643,
          "safetyClass": "candidate-safe-mutation",
          "newWindowObserved": false,
          "evidence": {
            "candidateSafeMutation": false,
            "candidateSafeMutationFunctionalDiff": false,
            "candidateSafeMutationReversibleReturn": true,
            "projectCopyHashChanged": false
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-reversible-undo");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("PASS", sweep["status"]!.GetValue<string>());
        Assert.Equal("probed", entry["status"]!.GetValue<string>());
        Assert.True(entry["invoked"]!.GetValue<bool>());
    }

    [Fact]
    public void ToolSweepUsesNewestProbeForSameToolId()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-newest-probe");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:11:57643",
              "displayName": "MFC edit undo",
              "source": "toolbar",
              "uiPath": "动画组态工具条/button[11]",
              "commandId": 57643,
              "enabled": false,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "candidate-safe-mutation",
              "invocationRoute": "WM_COMMAND 57643",
              "expectedEffect": "undoes a controlled disposable edit",
              "evidenceSource": "test catalog",
              "nextProbe": "cut then undo"
            }
          ]
        }
        """, Encoding.UTF8);
        var oldProbeDir = Path.Combine(_root, "probe-root", "old-pass");
        Directory.CreateDirectory(oldProbeDir);
        File.WriteAllText(Path.Combine(oldProbeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:11:57643",
          "commandId": 57643,
          "safetyClass": "candidate-safe-mutation",
          "evidence": {
            "candidateSafeMutationFunctionalDiff": true
          }
        }
        """, Encoding.UTF8);
        File.SetLastWriteTimeUtc(Path.Combine(oldProbeDir, "tool-probe.json"), DateTime.UtcNow.AddMinutes(-5));
        var newProbeDir = Path.Combine(_root, "probe-root", "new-unknown");
        Directory.CreateDirectory(newProbeDir);
        File.WriteAllText(Path.Combine(newProbeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "toolId": "toolbar:5:11:57643",
          "commandId": 57643,
          "safetyClass": "candidate-safe-mutation",
          "evidence": {
            "candidateSafeMutationFunctionalDiff": false,
            "candidateSafeMutationReversibleReturn": false
          }
        }
        """, Encoding.UTF8);
        File.SetLastWriteTimeUtc(Path.Combine(newProbeDir, "tool-probe.json"), DateTime.UtcNow);

        var sweepDir = Path.Combine(_root, "sweep-newest-probe");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(2, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("UNKNOWN", entry["probeStatus"]!.GetValue<string>());
        Assert.Equal("needs-precondition", entry["status"]!.GetValue<string>());
        Assert.False(entry["invoked"]!.GetValue<bool>());
    }

    [Fact]
    public void MceNormalizedDiffTreatsBlobGeometryOffsetOnlyChangesAsEquivalent()
    {
        Directory.CreateDirectory(_root);
        var baseline = Path.Combine(_root, "export-baseline");
        var candidate = Path.Combine(_root, "export-candidate");
        Directory.CreateDirectory(baseline);
        Directory.CreateDirectory(candidate);
        WriteMinimalExport(baseline, blobGeometryExtra: "\"offset\": 100, \"sha256\": \"baseline-private\", \"bytes\": 32");
        WriteMinimalExport(candidate, blobGeometryExtra: "\"offset\": 2048, \"sha256\": \"candidate-private\", \"bytes\": 48");

        var outDir = Path.Combine(_root, "normalized-diff");
        var result = TestCli.Run("mce", "normalized-diff", "--baseline", baseline, "--candidate", candidate, "--out", outDir);

        Assert.Equal(0, result.ExitCode);
        var diff = JsonNode.Parse(File.ReadAllText(Path.Combine(outDir, "mce-normalized-diff.json"), Encoding.UTF8))!.AsObject();
        Assert.Equal("PASS", diff["status"]!.GetValue<string>());
        Assert.True(diff["equivalent"]!.GetValue<bool>());
        Assert.Equal(0, diff["changedFileCount"]!.GetValue<int>());
        var geometry = diff["files"]!.AsArray().Select(n => n!.AsObject())
            .First(f => f["file"]!.GetValue<string>() == "blob_geometry.json");
        Assert.True(geometry["equivalent"]!.GetValue<bool>());
        Assert.Empty(geometry["entryDiffs"]!.AsArray());
    }

    [Fact]
    public void ToolSweepDoesNotTreatUnknownRiskHashDriftAsProbed()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-unknown-drift");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:7:26:34027",
              "displayName": "select current editing language",
              "source": "toolbar",
              "uiPath": "workbench toolbar/button[26]",
              "commandId": 34027,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "unknown-risk",
              "invocationRoute": "WM_COMMAND 34027",
              "expectedEffect": "opens a current-language dialog",
              "evidenceSource": "test catalog",
              "nextProbe": "capture dialog fields and prove hash drift is editor-context-only"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "unknown-drift");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:7:26:34027",
          "newWindowObserved": false,
          "evidence": {
            "projectCopyHashChanged": true
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-unknown-drift");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(2, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("UNKNOWN", sweep["status"]!.GetValue<string>());
        Assert.Equal("needs-precondition", entry["status"]!.GetValue<string>());
        Assert.Equal("PASS", entry["probeStatus"]!.GetValue<string>());
        Assert.False(entry["invoked"]!.GetValue<bool>());
    }

    [Fact]
    public void ToolSweepDoesNotTreatReadOnlyHashDriftAsProbed()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-readonly-drift");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:5:23:34024",
              "displayName": "toggle grid display",
              "source": "toolbar",
              "uiPath": "animation toolbar/button[23]",
              "commandId": 34024,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "read-only",
              "invocationRoute": "WM_COMMAND 34024",
              "expectedEffect": "toggles animation grid display",
              "evidenceSource": "test catalog",
              "nextProbe": "verify project SHA unchanged or prove drift is editor-context-only"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "readonly-drift");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:5:23:34024",
          "commandId": 34024,
          "safetyClass": "read-only",
          "evidence": {
            "projectCopyHashChanged": true
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-readonly-drift");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(2, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("UNKNOWN", sweep["status"]!.GetValue<string>());
        Assert.Equal("needs-precondition", entry["status"]!.GetValue<string>());
        Assert.Equal("PASS", entry["probeStatus"]!.GetValue<string>());
        Assert.False(entry["invoked"]!.GetValue<bool>());
        Assert.Equal("needsProbe", entry["closureStatus"]!.GetValue<string>());
    }

    [Fact]
    public void ToolSweepAcceptsReadOnlyNormalizedEquivalentHashDriftAsProbed()
    {
        Directory.CreateDirectory(_root);
        var project = Path.Combine(_root, "candidate.MCE");
        File.WriteAllBytes(project, Encoding.ASCII.GetBytes("dummy candidate"));
        var catalogDir = Path.Combine(_root, "catalog-readonly-equivalent");
        Directory.CreateDirectory(catalogDir);
        File.WriteAllText(Path.Combine(catalogDir, "tool-catalog.json"), """
        {
          "schemaVersion": 1,
          "status": "UNKNOWN",
          "tools": [
            {
              "toolId": "toolbar:7:15:32782",
              "displayName": "workbench small-icon view",
              "source": "toolbar",
              "uiPath": "workbench toolbar/button[15]",
              "commandId": 32782,
              "enabled": true,
              "hidden": false,
              "supportStatus": "needs-precondition",
              "safetyClass": "read-only",
              "invocationRoute": "WM_COMMAND 32782",
              "expectedEffect": "switches view mode",
              "evidenceSource": "test catalog",
              "nextProbe": "verify project SHA unchanged or prove drift is normalized-equivalent"
            }
          ]
        }
        """, Encoding.UTF8);
        var probeDir = Path.Combine(_root, "probe-root", "readonly-equivalent");
        Directory.CreateDirectory(probeDir);
        File.WriteAllText(Path.Combine(probeDir, "tool-probe.json"), """
        {
          "schemaVersion": 1,
          "status": "PASS",
          "toolId": "toolbar:7:15:32782",
          "commandId": 32782,
          "safetyClass": "read-only",
          "evidence": {
            "projectCopyHashChanged": true,
            "readOnlyHashDriftClass": "normalized-equivalent",
            "normalizedDiff": {
              "Equivalent": true,
              "ChangedFileCount": 0
            }
          }
        }
        """, Encoding.UTF8);

        var sweepDir = Path.Combine(_root, "sweep-readonly-equivalent");
        var result = TestCli.Run("mcgs", "tool-sweep", "--project", project,
            "--tool-catalog", Path.Combine(catalogDir, "tool-catalog.json"),
            "--probe-root", Path.Combine(_root, "probe-root"),
            "--out", sweepDir);

        Assert.Equal(0, result.ExitCode);
        var sweep = JsonNode.Parse(File.ReadAllText(Path.Combine(sweepDir, "tool-sweep.json"), Encoding.UTF8))!.AsObject();
        var entry = sweep["entries"]!.AsArray()[0]!.AsObject();
        Assert.Equal("PASS", sweep["status"]!.GetValue<string>());
        Assert.Equal("probed", entry["status"]!.GetValue<string>());
        Assert.True(entry["invoked"]!.GetValue<bool>());
        Assert.Equal("notClosedLoop", entry["closureStatus"]!.GetValue<string>());
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

    private static void WriteMinimalExport(string dir, string blobGeometryExtra)
    {
        File.WriteAllText(Path.Combine(dir, "summary.json"), """{"schemaVersion":1,"project":"ignored.MCE"}""", Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "schema.json"), """{"schemaVersion":1}""", Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "data.json"), "[]", Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "blob_strings.json"), "[]", Encoding.UTF8);
        File.WriteAllText(Path.Combine(dir, "blob_geometry.json"), $$"""
        [
          {
            "table": "WndUser",
            "column": "lbObjects",
            "rowKey": "2",
            "rowLabelSha256": "row-label",
            {{blobGeometryExtra}},
            "classOccurrences": [
              { "className": "CDrawButton", "offset": 10 }
            ],
            "candidateRectangles": [
              { "encoding": "le32", "pattern": "xywh", "x": 610, "y": 320, "width": 90, "height": 42, "offset": 12 }
            ],
            "textAnchors": [
              { "encoding": "gb2312", "byteLength": 12, "charLength": 6, "sha256": "text-anchor", "offset": 40 }
            ]
          }
        ]
        """, Encoding.UTF8);
    }

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
