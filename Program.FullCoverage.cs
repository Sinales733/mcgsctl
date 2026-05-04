using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

internal static partial class Program
{
    private static int Mcgs(string[] args)
    {
        if (args.Length < 2)
            return Fail("Usage: mcgsctl mcgs inventory|tool-catalog|tool-probe|tool-sweep --project <candidate.mce> --out <dir>");

        return args[1].ToLowerInvariant() switch
        {
            "inventory" => McgsInventory(args),
            "tool-catalog" => McgsToolCatalog(args),
            "tool-probe" => McgsToolProbe(args),
            "tool-sweep" => McgsToolSweep(args),
            _ => Fail("Unknown mcgs command: " + args[1])
        };
    }

    private static int McgsInventory(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var catalogResult = BuildMcgsCatalog(project, Opt(args, "--toolbar-probe"), outDir);
        WriteMcgsCatalogOutputs(outDir, catalogResult);
        File.WriteAllText(Path.Combine(outDir, "inventory.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = catalogResult.UnknownCount == 0 ? "PASS" : "UNKNOWN",
            project,
            projectSha256 = Sha256(project),
            createdAt = DateTimeOffset.Now.ToString("O"),
            toolCatalog = "tool-catalog.json",
            functionCatalog = "function-catalog.json",
            catalogResult.ToolCount,
            catalogResult.FunctionCount,
            catalogResult.UnknownCount,
            catalogResult.BlockedReasons,
            nextProbe = catalogResult.UnknownCount == 0
                ? ""
                : "Run mcgs tool-probe/tool-sweep on candidate copies for catalog entries whose supportStatus is not implemented/probed."
        }, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("mcgs inventory: " + outDir);
        return catalogResult.UnknownCount == 0 ? 0 : 2;
    }

    private static int McgsToolCatalog(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var catalogResult = BuildMcgsCatalog(project, Opt(args, "--toolbar-probe"), outDir);
        WriteMcgsCatalogOutputs(outDir, catalogResult);
        Console.WriteLine("mcgs tool-catalog: " + outDir);
        return catalogResult.ToolCount > 0 ? 0 : 2;
    }

    private static int McgsToolProbe(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var toolId = Required(args, "--tool-id");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var catalogPath = Opt(args, "--tool-catalog");
        object? tool = null;
        if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(FullPath(catalogPath)))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FullPath(catalogPath), Encoding.UTF8));
            if (doc.RootElement.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tools.EnumerateArray())
                {
                    if ((JsonStringAny(item, "toolId", "ToolId") ?? "").Equals(toolId, StringComparison.OrdinalIgnoreCase))
                    {
                        tool = JsonSerializer.Deserialize<object>(item.GetRawText());
                        break;
                    }
                }
            }
        }

        var result = new
        {
            schemaVersion = 1,
            status = "UNKNOWN",
            project,
            projectSha256 = Sha256(project),
            toolId,
            tool,
            invoked = false,
            reason = "tool-probe first stage is catalog/readback only; direct invocation requires per-tool candidate-safe classifier.",
            safetyClass = "unknown-risk",
            nextProbe = "Classify this tool by command id, UI context, and expected side effect; then run on throwaway candidate with before/after hash evidence."
        };
        File.WriteAllText(Path.Combine(outDir, "tool-probe.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("mcgs tool-probe: " + outDir);
        return 2;
    }

    private static int McgsToolSweep(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var catalogPath = Opt(args, "--tool-catalog");
        McgsCatalogBuild catalog;
        if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(FullPath(catalogPath)))
        {
            catalog = ReadMcgsCatalog(project, FullPath(catalogPath));
        }
        else
        {
            catalog = BuildMcgsCatalog(project, Opt(args, "--toolbar-probe"), outDir);
            WriteMcgsCatalogOutputs(outDir, catalog);
        }

        var entries = catalog.Tools.Select(tool => new
        {
            tool.toolId,
            tool.displayName,
            tool.source,
            tool.commandId,
            status = tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase) ? "implemented" : "needs-probe",
            invoked = tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase),
            tool.safetyClass,
            invocationEvidence = tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase) ? tool.evidenceSource : "",
            nextProbe = tool.nextProbe
        }).ToArray();
        var result = new
        {
            schemaVersion = 1,
            status = entries.Any(e => e.status == "needs-probe") ? "UNKNOWN" : "PASS",
            project,
            projectSha256 = Sha256(project),
            createdAt = DateTimeOffset.Now.ToString("O"),
            toolCount = entries.Length,
            invokedCount = entries.Count(e => e.invoked),
            entries,
            blockedReasons = new[]
            {
                "Stage-1 tool-sweep does not invoke unknown-risk tools. Use mcgs tool-probe after candidate-safe classification."
            }
        };
        File.WriteAllText(Path.Combine(outDir, "tool-sweep.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("mcgs tool-sweep: " + outDir);
        return result.status == "PASS" ? 0 : 2;
    }

    private sealed class McgsCatalogBuild
    {
        public List<McgsToolEntry> Tools { get; } = new();
        public List<object> Functions { get; } = new();
        public List<string> BlockedReasons { get; } = new();
        public int ToolCount => Tools.Count;
        public int FunctionCount => Functions.Count;
        public int UnknownCount => Tools.Count(t => t.supportStatus is "blocked" or "needs-precondition" or "needs-probe");
    }

    private sealed class McgsToolEntry
    {
        public string toolId { get; set; } = "";
        public string displayName { get; set; } = "";
        public string source { get; set; } = "";
        public string uiPath { get; set; } = "";
        public int? commandId { get; set; }
        public bool enabled { get; set; }
        public bool hidden { get; set; }
        public string supportStatus { get; set; } = "needs-probe";
        public string safetyClass { get; set; } = "unknown-risk";
        public string invocationRoute { get; set; } = "";
        public string expectedEffect { get; set; } = "";
        public string evidenceSource { get; set; } = "";
        public string nextProbe { get; set; } = "";
    }

    private sealed record KnownMcgsCommand(
        string DisplayName,
        string SupportStatus,
        string SafetyClass,
        string InvocationRoute,
        string ExpectedEffect,
        string EvidenceSource,
        string NextProbe);

    private static KnownMcgsCommand? DescribeKnownToolbarCommand(int? commandId)
    {
        return commandId switch
        {
            32785 => new KnownMcgsCommand(
                "selected object property dialog",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 32785 after selecting a canvas object",
                "opens the selected object's property dialog; used by momentary/status/native readback workflows",
                "Program.cs OpenCanvasObjectPropertyDialog/ReopenVerify* readback evidence",
                ""),
            32786 => new KnownMcgsCommand(
                "project check",
                "implemented",
                "read-only",
                "WM_COMMAND 32786 on a same-SHA temporary project-check copy",
                "runs MCGS project check and writes project-check/check-result.json without mutating the candidate",
                "workflow run project.check temporary-copy validator",
                ""),
            32907 => new KnownMcgsCommand(
                "native static text drawing tool",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 32907 then canvas drag on a candidate copy",
                "creates a native static text/label object and verifies label text via property readback",
                "workflow run window.static-text.add",
                ""),
            32938 => new KnownMcgsCommand(
                "standard button drawing tool",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 32938 then canvas drag on a candidate copy",
                "creates a standard button object used by momentary/status-button workflows",
                "workflow run window.button.add-momentary and window.indicator.add",
                ""),
            32941 => new KnownMcgsCommand(
                "native animation display/lamp drawing tool",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 32941 then canvas drag on a candidate copy",
                "creates a native animation display component used as the native lamp workflow",
                "workflow run window.lamp.add-native",
                ""),
            33954 => new KnownMcgsCommand(
                "device configuration view",
                "implemented",
                "read-only",
                "WM_COMMAND 33954",
                "enters the device configuration view before Smart200 channel read/write workflows",
                "device.channel.map EnterDeviceConfiguration",
                ""),
            33955 => new KnownMcgsCommand(
                "user window list/view",
                "implemented",
                "read-only",
                "WM_COMMAND 33955",
                "enters the user window list before animation canvas workflows and readback",
                "window.button/window.indicator/window.layout workflows",
                ""),
            33957 => new KnownMcgsCommand(
                "realtime database view",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 33957",
                "enters the realtime database editor before adding Data objects on a candidate copy",
                "workflow run realtime-db.add",
                ""),
            57603 => new KnownMcgsCommand(
                "save project",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 57603",
                "saves the currently opened candidate project after controlled GUI writes",
                "mutating workflow save/readback evidence",
                ""),
            _ => null
        };
    }

    private static McgsCatalogBuild BuildMcgsCatalog(string project, string? toolbarProbePath, string outDir)
    {
        var build = new McgsCatalogBuild();
        var probePath = ResolveToolbarProbe(toolbarProbePath);
        if (probePath != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(probePath, Encoding.UTF8));
                if (doc.RootElement.TryGetProperty("toolbars", out var toolbars) && toolbars.ValueKind == JsonValueKind.Array)
                {
                    var toolbarIndex = 0;
                    foreach (var toolbar in toolbars.EnumerateArray())
                    {
                        toolbarIndex++;
                        var toolbarText = "";
                        if (toolbar.TryGetProperty("window", out var window) && window.ValueKind == JsonValueKind.Object)
                            toolbarText = JsonStringAny(window, "Text", "text") ?? "";
                        if (!toolbar.TryGetProperty("buttons", out var buttons) || buttons.ValueKind != JsonValueKind.Array)
                            continue;
                        foreach (var button in buttons.EnumerateArray())
                        {
                            var index = LayoutJsonIntAny(button, "Index", "index") ?? -1;
                            var commandId = LayoutJsonIntAny(button, "IdCommand", "idCommand");
                            var text = JsonStringAny(button, "Text", "text") ?? "";
                            var enabled = JsonBoolAny(button, "Enabled", "enabled") == true;
                            var hidden = JsonBoolAny(button, "Hidden", "hidden") == true;
                            var isSeparator = commandId.GetValueOrDefault() == 0;
                            var known = DescribeKnownToolbarCommand(commandId);
                            build.Tools.Add(new McgsToolEntry
                            {
                                toolId = isSeparator
                                    ? $"toolbar:{toolbarIndex}:{index}:separator"
                                    : $"toolbar:{toolbarIndex}:{index}:{commandId}",
                                displayName = string.IsNullOrWhiteSpace(text)
                                    ? (isSeparator ? "separator" : known?.DisplayName ?? $"command {commandId}")
                                    : text,
                                source = "toolbar",
                                uiPath = string.IsNullOrWhiteSpace(toolbarText) ? $"toolbar[{toolbarIndex}]/button[{index}]" : $"{toolbarText}/button[{index}]",
                                commandId = commandId,
                                enabled = enabled,
                                hidden = hidden,
                                supportStatus = isSeparator ? "implemented" : known?.SupportStatus ?? "needs-probe",
                                safetyClass = isSeparator ? "read-only" : known?.SafetyClass ?? "unknown-risk",
                                invocationRoute = isSeparator ? "" : known?.InvocationRoute ?? $"WM_COMMAND {commandId}",
                                expectedEffect = isSeparator ? "visual separator" : known?.ExpectedEffect ?? "unknown until candidate-safe probe records before/after evidence",
                                evidenceSource = known?.EvidenceSource ?? probePath,
                                nextProbe = isSeparator ? "" : known?.NextProbe ?? "Probe command on throwaway candidate after classifying menu/toolbar context and expected side effect."
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                build.BlockedReasons.Add("failed to read toolbar probe: " + ex.Message);
            }
        }
        else
        {
            build.BlockedReasons.Add("no toolbar-probe evidence was supplied or found; run canvas toolbar-probe on a candidate copy.");
        }

        foreach (var workflow in SupportedWorkflowFunctions())
            build.Functions.Add(workflow);

        build.Tools.AddRange(SupportedMcgsCtlTools(project));
        return build;
    }

    private static McgsCatalogBuild ReadMcgsCatalog(string project, string catalogPath)
    {
        var build = new McgsCatalogBuild();
        using var doc = JsonDocument.Parse(File.ReadAllText(catalogPath, Encoding.UTF8));
        if (doc.RootElement.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
        {
            foreach (var tool in tools.EnumerateArray())
            {
                build.Tools.Add(new McgsToolEntry
                {
                    toolId = JsonStringAny(tool, "toolId", "ToolId") ?? "",
                    displayName = JsonStringAny(tool, "displayName", "DisplayName") ?? "",
                    source = JsonStringAny(tool, "source", "Source") ?? "",
                    uiPath = JsonStringAny(tool, "uiPath", "UiPath") ?? "",
                    commandId = LayoutJsonIntAny(tool, "commandId", "CommandId"),
                    enabled = JsonBoolAny(tool, "enabled", "Enabled") == true,
                    hidden = JsonBoolAny(tool, "hidden", "Hidden") == true,
                    supportStatus = JsonStringAny(tool, "supportStatus", "SupportStatus") ?? "needs-probe",
                    safetyClass = JsonStringAny(tool, "safetyClass", "SafetyClass") ?? "unknown-risk",
                    invocationRoute = JsonStringAny(tool, "invocationRoute", "InvocationRoute") ?? "",
                    expectedEffect = JsonStringAny(tool, "expectedEffect", "ExpectedEffect") ?? "",
                    evidenceSource = JsonStringAny(tool, "evidenceSource", "EvidenceSource") ?? catalogPath,
                    nextProbe = JsonStringAny(tool, "nextProbe", "NextProbe") ?? ""
                });
            }
        }
        if (doc.RootElement.TryGetProperty("functions", out var functions) && functions.ValueKind == JsonValueKind.Array)
        {
            foreach (var function in functions.EnumerateArray())
                build.Functions.Add(JsonSerializer.Deserialize<object>(function.GetRawText()) ?? new { });
        }
        if (build.Functions.Count == 0)
        {
            foreach (var workflow in SupportedWorkflowFunctions())
                build.Functions.Add(workflow);
        }
        return build;
    }

    private static void WriteMcgsCatalogOutputs(string outDir, McgsCatalogBuild build)
    {
        File.WriteAllText(Path.Combine(outDir, "tool-catalog.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = build.ToolCount > 0 ? "PASS" : "UNKNOWN",
            createdAt = DateTimeOffset.Now.ToString("O"),
            toolCount = build.ToolCount,
            tools = build.Tools,
            blockedReasons = build.BlockedReasons
        }, JsonOptions()), Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDir, "function-catalog.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            status = "PASS",
            createdAt = DateTimeOffset.Now.ToString("O"),
            functionCount = build.FunctionCount,
            functions = build.Functions
        }, JsonOptions()), Encoding.UTF8);
    }

    private static string? ResolveToolbarProbe(string? toolbarProbePath)
    {
        if (!string.IsNullOrWhiteSpace(toolbarProbePath))
        {
            var full = FullPath(toolbarProbePath);
            return File.Exists(full) ? full : null;
        }
        var runs = FullPath(".mcgsctl-runs");
        if (!Directory.Exists(runs)) return null;
        return Directory.EnumerateFiles(runs, "toolbar-probe.json", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => info.FullName)
            .FirstOrDefault();
    }

    private static IEnumerable<object> SupportedWorkflowFunctions()
    {
        string[] workflows =
        {
            "realtime-db.add",
            "window.button.add-momentary",
            "window.indicator.add",
            "window.static-text.add",
            "window.lamp.add-native",
            "window.layout.apply",
            "device.channel.map",
            "script.edit",
            "project.check",
            "project.check-save",
            "safety.verify",
            "project.apply-candidate",
            "project.rollback"
        };
        foreach (var workflow in workflows)
        {
            yield return new
            {
                functionId = "workflow:" + workflow,
                name = workflow,
                purpose = WorkflowPurpose(workflow),
                invocationRoute = "mcgsctl workflow run " + workflow,
                safetyClass = workflow.Contains("apply", StringComparison.OrdinalIgnoreCase)
                    ? "formal-apply-required"
                    : workflow.Contains("rollback", StringComparison.OrdinalIgnoreCase)
                        ? "formal-apply-required"
                        : "candidate-safe-mutation",
                supportStatus = "implemented",
                evidenceSource = "mcgsctl workflow result schema and existing GUI/readback workflows",
                validation = "workflow result checks plus candidate summarize/validate where applicable"
            };
        }
    }

    private static IEnumerable<McgsToolEntry> SupportedMcgsCtlTools(string project)
    {
        yield return new McgsToolEntry
        {
            toolId = "mcgsctl:canvas:semantic-map-probe",
            displayName = "canvas semantic-map-probe",
            source = "mcgsctl",
            uiPath = "CLI/canvas",
            enabled = true,
            supportStatus = "implemented",
            safetyClass = "read-only",
            invocationRoute = "mcgsctl canvas semantic-map-probe --project <candidate.MCE> --out <dir>",
            expectedEffect = "read-only MCE export plus semantic map evidence",
            evidenceSource = project,
            nextProbe = ""
        };
        yield return new McgsToolEntry
        {
            toolId = "mcgsctl:canvas:property-map-probe",
            displayName = "canvas property-map-probe",
            source = "mcgsctl",
            uiPath = "CLI/canvas",
            enabled = true,
            supportStatus = "implemented",
            safetyClass = "read-only",
            invocationRoute = "mcgsctl canvas property-map-probe --project <candidate.MCE> --row-key <key> --out <dir>",
            expectedEffect = "expands semantic objects into property records with explicit unresolved next probes",
            evidenceSource = project,
            nextProbe = ""
        };
    }

    private static string WorkflowPurpose(string workflow) => workflow switch
    {
        "realtime-db.add" => "create or verify an HMI realtime database object through MCGS GUI",
        "window.button.add-momentary" => "create a momentary control button and verify press/release actions",
        "window.indicator.add" => "create a status-button indicator and verify readback",
        "window.static-text.add" => "create native static text through MCGS GUI",
        "window.lamp.add-native" => "create a native animation display/lamp component through MCGS GUI",
        "window.layout.apply" => "apply an HMI layout through supported GUI object creation workflows",
        "device.channel.map" => "map Smart200 channel rows and record structured smart200Channels evidence",
        "script.edit" => "edit script content and record token/data-object evidence",
        "project.check" => "run MCGS project check against a same-SHA temporary copy",
        "project.check-save" => "run project check and save through MCGS",
        "safety.verify" => "verify final candidate against safety spec and evidence",
        "project.apply-candidate" => "replace official file after approval and validator checks",
        "project.rollback" => "restore official file from rollback package",
        _ => "mcgsctl workflow"
    };

    private static int CanvasPropertyMapProbe(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var semanticPath = Opt(args, "--semantic-map");
        if (string.IsNullOrWhiteSpace(semanticPath))
        {
            var semanticOut = Path.Combine(outDir, "semantic-map-source");
            var semanticArgs = new List<string>
            {
                "canvas", "semantic-map-probe",
                "--project", project,
                "--out", semanticOut
            };
            foreach (var passthrough in new[] { "--row-key", "--workflow-results", "--layout-apply", "--canvas-width", "--canvas-height", "--window-index" })
            {
                var value = Opt(args, passthrough);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    semanticArgs.Add(passthrough);
                    semanticArgs.Add(value);
                }
            }
            CanvasSemanticMapProbe(semanticArgs.ToArray());
            semanticPath = Path.Combine(semanticOut, "semantic-map.json");
        }
        else
        {
            semanticPath = FullPath(semanticPath);
        }

        if (!File.Exists(semanticPath))
            return Fail("semantic map not found: " + semanticPath);

        using var doc = JsonDocument.Parse(File.ReadAllText(semanticPath, Encoding.UTF8));
        var root = doc.RootElement;
        var objects = root.TryGetProperty("Objects", out var upperObjects) ? upperObjects :
                      root.TryGetProperty("objects", out var lowerObjects) ? lowerObjects : default;
        if (objects.ValueKind != JsonValueKind.Array)
            return Fail("semantic map has no objects array: " + semanticPath);

        var propertyReadbacks = LoadPropertyDialogReadbacks(args);
        var records = new List<object>();
        var unresolved = new List<object>();
        var sequence = 0;
        foreach (var obj in objects.EnumerateArray())
        {
            sequence++;
            var id = JsonStringAny(obj, "Id", "id") ?? $"object-{sequence:D3}";
            propertyReadbacks.TryGetValue(id, out var readback);
            var record = BuildPropertyMapRecord(obj, sequence, unresolved, readback);
            records.Add(record);
        }

        var result = new
        {
            schemaVersion = 1,
            status = unresolved.Count == 0 ? "PASS" : "UNKNOWN",
            evidenceStatus = JsonStringAny(root, "Status", "status") == "PASS" ? "PASS" : "UNKNOWN",
            createdAt = DateTimeOffset.Now.ToString("O"),
            project,
            projectSha256 = Sha256(project),
            semanticMap = semanticPath,
            propertyReadbackCount = propertyReadbacks.Count,
            rowKey = JsonStringAny(root, "RowKey", "rowKey") ?? Opt(args, "--row-key") ?? "",
            objectCount = records.Count,
            unresolvedPropertyCount = unresolved.Count,
            objects = records,
            unresolvedProperties = unresolved,
            nextProbe = unresolved.Count == 0
                ? ""
                : "Run canvas property-readback or single-property MCE/clipboard diff for each unresolved property path."
        };
        File.WriteAllText(Path.Combine(outDir, "property-map-probe.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDir, "property-map.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("canvas property-map-probe: " + outDir);
        return unresolved.Count == 0 ? 0 : 2;
    }

    private static int CanvasPropertyReadback(string[] args)
    {
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        Process? process = null;
        var main = IntPtr.Zero;
        try
        {
            var semanticMap = FullPath(Required(args, "--semantic-map"));
            var objectId = Required(args, "--object-id");
            var expectedTitle = Opt(args, "--expected-title") ?? "";
            var preferCommand = !Has(args, "--prefer-double-click");
            using var semanticDoc = JsonDocument.Parse(File.ReadAllText(semanticMap, Encoding.UTF8));
            var target = FindSemanticMapObject(semanticDoc.RootElement, objectId);
            if (target.ValueKind == JsonValueKind.Undefined)
                throw new ArgumentException("Object was not found in semantic map: " + objectId);

            var rect = target.TryGetProperty("Rect", out var upperRect) ? upperRect :
                       target.TryGetProperty("rect", out var lowerRect) ? lowerRect : default;
            var x = LayoutJsonIntAny(rect, "X", "x") ?? 0;
            var y = LayoutJsonIntAny(rect, "Y", "y") ?? 0;
            var width = LayoutJsonIntAny(rect, "Width", "width") ?? 0;
            var height = LayoutJsonIntAny(rect, "Height", "height") ?? 0;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Semantic map object has no positive rectangle: " + objectId);

            using var session = OpenCanvasProbeSession(args, outDir, "property-readback", out var openedProcess, out main);
            process = openedProcess;
            var dialog = OpenCanvasObjectPropertyDialog(process.Id, session.Main, session.Canvas,
                x, y, width, height, expectedTitle, preferCommand);

            var tab = UiAutomation.EnumerateChildren(dialog)
                .FirstOrDefault(h => Native.GetClass(h).Equals("SysTabControl32", StringComparison.OrdinalIgnoreCase));
            var tabs = new List<object>();
            if (tab != IntPtr.Zero)
            {
                var tabItems = UiAutomation.TabItems(tab);
                foreach (var item in tabItems)
                {
                    UiAutomation.TabSelectIndex(tab, item.Index, mouse: true);
                    Thread.Sleep(250);
                    tabs.Add(new
                    {
                        item.Index,
                        item.Text,
                        controls = SnapshotDialogControls(dialog)
                    });
                }
            }
            else
            {
                tabs.Add(new
                {
                    Index = 0,
                    Text = "default",
                    controls = SnapshotDialogControls(dialog)
                });
            }
            if (tab != IntPtr.Zero)
            {
                UiAutomation.TabSelectIndex(tab, 0, mouse: true);
                Thread.Sleep(200);
            }
            var fontDialog = Has(args, "--probe-font")
                ? TryProbeFontDialog(process.Id, dialog, outDir)
                : null;
            var displayedText = JsonStringAny(target, "DisplayedText", "displayedText") ?? "";
            var selectionVerified = string.IsNullOrWhiteSpace(displayedText) ||
                DialogTabsContainText(tabs, displayedText);
            var status = tabs.Count > 0 && selectionVerified ? "PASS" : "UNKNOWN";

            var result = new
            {
                schemaVersion = 1,
                status,
                project = session.ProjectCopy,
                projectSha256 = session.ProjectSha256,
                createdAt = DateTimeOffset.Now.ToString("O"),
                semanticMap,
                objectId,
                semanticKind = JsonStringAny(target, "SemanticKind", "semanticKind") ?? "",
                displayedText,
                selectionVerified,
                rect = new { x, y, width, height },
                dialog = WindowInfo.FromHandle(dialog),
                tabs,
                fontDialog,
                blockedReasons = selectionVerified
                    ? Array.Empty<string>()
                    : new[] { "property dialog content did not contain requested displayedText; coordinate may have selected an overlapping object" },
                evidenceSource = "property-dialog-readback",
                nextProbe = tabs.Count > 0
                    ? "Parse property-readback tab/control text into property-map fields and add single-property diff samples for fields that remain ambiguous."
                    : "Property dialog opened but no controls/tabs were captured; rerun with screenshot and window-tree evidence."
            };
            File.WriteAllText(Path.Combine(outDir, "property-readback.json"),
                JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outDir, "property-dialog.tree.txt"), UiAutomation.WindowTreeLines(dialog), Encoding.UTF8);
            TryScreenshot(dialog, Path.Combine(outDir, "property-dialog.png"));
            UiAutomation.CloseWindow(dialog);
            Thread.Sleep(300);
            Console.WriteLine("canvas property-readback: " + outDir);
            return status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas property-readback failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: false); } catch { }
            }
        }
    }

    private static JsonElement FindSemanticMapObject(JsonElement root, string objectId)
    {
        var objects = root.TryGetProperty("Objects", out var upperObjects) ? upperObjects :
                      root.TryGetProperty("objects", out var lowerObjects) ? lowerObjects : default;
        if (objects.ValueKind == JsonValueKind.Array)
        {
            foreach (var obj in objects.EnumerateArray())
            {
                var id = JsonStringAny(obj, "Id", "id") ?? "";
                if (id.Equals(objectId, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }
        }
        return default;
    }

    private static object[] SnapshotDialogControls(IntPtr dialog)
    {
        var rootRect = UiAutomation.GetWindowRect(dialog);
        var controls = new List<object>();
        var sequence = 0;
        foreach (var hwnd in UiAutomation.EnumerateChildren(dialog))
        {
            sequence++;
            var cls = Native.GetClass(hwnd);
            var rect = UiAutomation.GetWindowRect(hwnd);
            object? items = null;
            var currentIndex = (int?)null;
            try
            {
                if (cls.Equals("ComboBox", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = UiAutomation.ComboCurrentIndex(hwnd);
                    items = UiAutomation.ComboItems(hwnd);
                }
                else if (cls.Contains("ListBox", StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = UiAutomation.ListBoxCurrentIndex(hwnd);
                    items = UiAutomation.ListBoxItems(hwnd);
                }
                else if (cls.Equals("SysTabControl32", StringComparison.OrdinalIgnoreCase))
                {
                    items = UiAutomation.TabItems(hwnd);
                }
                else if (cls.Equals("SysListView32", StringComparison.OrdinalIgnoreCase))
                {
                    items = UiAutomation.ListViewItems(hwnd);
                }
            }
            catch (Exception ex)
            {
                items = new { error = ex.Message };
            }

            var checkState = (int?)null;
            if (cls.Contains("Button", StringComparison.OrdinalIgnoreCase))
            {
                try { checkState = UiAutomation.ButtonGetCheck(hwnd); } catch { }
            }

            controls.Add(new
            {
                sequence,
                handle = FormatFullCoverageHandle(hwnd),
                className = cls,
                text = Native.GetText(hwnd),
                visible = Native.IsWindowVisible(hwnd),
                rect = new
                {
                    x = rect.Left - rootRect.Left,
                    y = rect.Top - rootRect.Top,
                    width = rect.Width,
                    height = rect.Height
                },
                screenRect = new { rect.Left, rect.Top, rect.Width, rect.Height },
                checkState,
                currentIndex,
                items
            });
        }
        return controls.ToArray();
    }

    private static object TryProbeFontDialog(int pid, IntPtr ownerDialog, string outDir)
    {
        var fontButton = UiAutomation.EnumerateChildren(ownerDialog)
            .FirstOrDefault(h =>
                Native.IsWindowVisible(h) &&
                Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase) &&
                CompactLabel(Native.GetText(h)).Equals(CompactLabel("字体"), StringComparison.OrdinalIgnoreCase));

        if (fontButton == IntPtr.Zero)
            return new
            {
                status = "notApplicable",
                reason = "property dialog has no visible font button on the current profile/page"
            };

        var before = UiAutomation.TopWindowsForPid(pid).ToHashSet();
        var buttonRect = UiAutomation.GetWindowRect(fontButton);
        UiAutomation.ClickPoint(fontButton, Math.Max(1, buttonRect.Width / 2), Math.Max(1, buttonRect.Height / 2),
            MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(400);

        var fontDialog = WaitForTopWindow(pid,
            h => h != ownerDialog &&
                 Native.GetClass(h) == "#32770" &&
                 Native.IsWindowVisible(h) &&
                 !before.Contains(h) &&
                 (CompactLabel(Native.GetText(h)).Contains(CompactLabel("字体"), StringComparison.OrdinalIgnoreCase) ||
                  CompactLabel(Native.GetText(h)).Contains("font", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(5));

        if (fontDialog == IntPtr.Zero)
            return new
            {
                status = "UNKNOWN",
                reason = "font button was present, but no font dialog appeared"
            };

        try
        {
            var controls = SnapshotDialogControls(fontDialog);
            var extracted = ExtractFontDialogSnapshot(controls);
            File.WriteAllLines(Path.Combine(outDir, "font-dialog.tree.txt"), UiAutomation.WindowTreeLines(fontDialog), Encoding.UTF8);
            TryScreenshot(fontDialog, Path.Combine(outDir, "font-dialog.png"));
            return new
            {
                status = "PASS",
                window = WindowInfo.FromHandle(fontDialog),
                controls,
                extracted
            };
        }
        finally
        {
            if (!ClickButtonByNormalizedText(fontDialog, mouse: true, "取消", "取消(&C)", "Cancel"))
                UiAutomation.CloseWindow(fontDialog);
            Thread.Sleep(250);
        }
    }

    private static object ExtractFontDialogSnapshot(object[] controls)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(controls, JsonOptions()));
        var array = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();
        var visibleCombos = array
            .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
            .Where(c => (JsonStringAny(c, "className", "ClassName") ?? "").Equals("ComboBox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => LayoutJsonIntAny(c, "sequence") ?? int.MaxValue)
            .Select(ControlCurrentText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return new
        {
            fontFamily = visibleCombos.ElementAtOrDefault(0) ?? TextValueAfterLabel(array, "字体", "字体名", "Font"),
            fontStyle = visibleCombos.ElementAtOrDefault(1) ?? TextValueAfterLabel(array, "字形", "字型", "样式", "Font style"),
            fontSize = visibleCombos.ElementAtOrDefault(2) ?? TextValueAfterLabel(array, "大小", "字号", "Size")
        };
    }

    private static bool DialogTabsContainText(IEnumerable<object> tabs, string text)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(tabs, JsonOptions()));
        return JsonElementContainsString(doc.RootElement, text);
    }

    private static bool JsonElementContainsString(JsonElement element, string text)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return (element.GetString() ?? "").Contains(text, StringComparison.Ordinal);
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (JsonElementContainsString(property.Value, text))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (JsonElementContainsString(item, text))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static string FormatFullCoverageHandle(IntPtr hwnd)
        => "0x" + hwnd.ToInt64().ToString("X");

    private sealed class PropertyDialogEvidence
    {
        public string ObjectId { get; init; } = "";
        public string DialogTitle { get; init; } = "";
        public string EvidencePath { get; init; } = "";
        public string? HorizontalAlignment { get; set; }
        public string? VerticalAlignment { get; set; }
        public string? BorderStyle { get; set; }
        public string? TextOrientation { get; set; }
        public bool? UsesBitmap { get; set; }
        public bool? UsesVector { get; set; }
        public string? ButtonType { get; set; }
        public string? TextEffect { get; set; }
        public bool? VisibilityUsesExpression { get; set; }
        public string? VisibilityExpression { get; set; }
        public string? VisibilityWhenNonZero { get; set; }
        public bool? DataObjectOperationEnabled { get; set; }
        public string? DataObjectOperation { get; set; }
        public string? DataObjectVariable { get; set; }
        public string? ScriptText { get; set; }
        public string? FontFamily { get; set; }
        public string? FontSize { get; set; }
        public string? FontStyle { get; set; }
        public string? NumericMin { get; set; }
        public string? NumericMax { get; set; }
        public string? NumericBase { get; set; }
        public bool? LeadingZero { get; set; }
        public bool? Rounding { get; set; }
        public bool? Password { get; set; }
        public bool? UnitEnabled { get; set; }
        public string? IntegerDigits { get; set; }
        public string? DecimalDigits { get; set; }
        public string? UnitText { get; set; }
        public bool? NaturalDecimalPlaces { get; set; }
    }

    private static Dictionary<string, PropertyDialogEvidence> LoadPropertyDialogReadbacks(string[] args)
    {
        var result = new Dictionary<string, PropertyDialogEvidence>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in PropertyReadbackPaths(args).OrderBy(File.GetLastWriteTimeUtc))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                if (!string.Equals(JsonStringAny(doc.RootElement, "status", "Status"), "PASS", StringComparison.OrdinalIgnoreCase))
                    continue;
                var evidence = ParsePropertyDialogEvidence(path, doc.RootElement);
                if (!string.IsNullOrWhiteSpace(evidence.ObjectId))
                    result[evidence.ObjectId] = evidence;
            }
            catch
            {
                // Bad readback evidence is ignored here; the originating command keeps failure.txt.
            }
        }
        return result;
    }

    private static IEnumerable<string> PropertyReadbackPaths(string[] args)
    {
        var single = Opt(args, "--property-readback");
        if (!string.IsNullOrWhiteSpace(single) && File.Exists(FullPath(single)))
            yield return FullPath(single);

        var dir = Opt(args, "--property-readback-dir");
        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(FullPath(dir)))
        {
            foreach (var path in Directory.EnumerateFiles(FullPath(dir), "property-readback.json", SearchOption.AllDirectories))
                yield return path;
        }
    }

    private static PropertyDialogEvidence ParsePropertyDialogEvidence(string path, JsonElement root)
    {
        var evidence = new PropertyDialogEvidence
        {
            ObjectId = JsonStringAny(root, "objectId", "ObjectId") ?? "",
            DialogTitle = root.TryGetProperty("dialog", out var dialog)
                ? JsonStringAny(dialog, "Text", "text") ?? ""
                : "",
            EvidencePath = path
        };

        if (!root.TryGetProperty("tabs", out var tabs) || tabs.ValueKind != JsonValueKind.Array)
            return evidence;

        if (root.TryGetProperty("fontDialog", out var fontDialog) &&
            fontDialog.ValueKind == JsonValueKind.Object &&
            string.Equals(JsonStringAny(fontDialog, "status", "Status"), "PASS", StringComparison.OrdinalIgnoreCase))
        {
            if (fontDialog.TryGetProperty("extracted", out var extracted) && extracted.ValueKind == JsonValueKind.Object)
            {
                evidence.FontFamily = JsonStringAny(extracted, "fontFamily", "FontFamily");
                evidence.FontSize = JsonStringAny(extracted, "fontSize", "FontSize");
                evidence.FontStyle = JsonStringAny(extracted, "fontStyle", "FontStyle");
            }

            if (fontDialog.TryGetProperty("controls", out var fontControls) && fontControls.ValueKind == JsonValueKind.Array)
            {
                var controls = fontControls.EnumerateArray().ToArray();
                evidence.FontFamily ??= TextValueAfterLabel(controls, "字体", "字体名", "Font");
                evidence.FontSize ??= TextValueAfterLabel(controls, "大小", "字号", "Size");
                evidence.FontStyle ??= TextValueAfterLabel(controls, "字形", "字型", "样式", "Font style");
            }
        }

        foreach (var tab in tabs.EnumerateArray())
        {
            var tabName = JsonStringAny(tab, "Text", "text") ?? "";
            var controls = tab.TryGetProperty("controls", out var controlArray) && controlArray.ValueKind == JsonValueKind.Array
                ? controlArray.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            controls = controls
                .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
                .ToArray();

            if (tabName.Contains("基本", StringComparison.OrdinalIgnoreCase))
            {
                evidence.HorizontalAlignment = CheckedByRelativeOrder(controls, "左对齐", "中对齐", "右对齐")
                                               ?? CheckedByRelativeOrder(controls, "靠左", "居中", "靠右");
                evidence.VerticalAlignment = CheckedByRelativeOrder(controls, "上对齐", "中对齐", "下对齐")
                                             ?? CheckedByRelativeOrder(controls, "靠上", "居中", "靠下");
                evidence.BorderStyle = CheckedByRelativeOrder(controls, "无边框", "普通边框", "三维边框");
                evidence.ButtonType = CheckedByRelativeOrder(controls, "3D按钮", "轻触按钮", "位图", "矢量图");
                evidence.TextEffect = CheckedByRelativeOrder(controls, "平面效果", "立体效果", "上凸");
            }
            else if (tabName.Contains("属性设置", StringComparison.OrdinalIgnoreCase))
            {
                evidence.UsesVector = IsChecked(controls, "矢量图");
                evidence.UsesBitmap = IsChecked(controls, "位图");
            }
            else if (tabName.Contains("扩展", StringComparison.OrdinalIgnoreCase))
            {
                evidence.HorizontalAlignment = CheckedByRelativeOrder(controls, "靠左", "居中", "靠右") ?? evidence.HorizontalAlignment;
                evidence.VerticalAlignment = CheckedByRelativeOrder(controls, "靠上", "居中", "靠下") ?? evidence.VerticalAlignment;
                evidence.TextOrientation = CheckedByRelativeOrder(controls, "横向", "纵向");
                evidence.UsesVector = IsChecked(controls, "矢量图") ?? evidence.UsesVector;
                evidence.UsesBitmap = IsChecked(controls, "位图") ?? evidence.UsesBitmap;
            }
            else if (tabName.Contains("操作", StringComparison.OrdinalIgnoreCase))
            {
                evidence.DataObjectOperationEnabled = IsChecked(controls, "数据对象值操作");
                var combo = controls.FirstOrDefault(c =>
                    (JsonStringAny(c, "className", "ClassName") ?? "").Equals("ComboBox", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(JsonStringAny(c, "text", "Text")));
                if (combo.ValueKind != JsonValueKind.Undefined)
                    evidence.DataObjectOperation = JsonStringAny(combo, "text", "Text");
                var edit = controls.FirstOrDefault(c =>
                    (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Edit", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(JsonStringAny(c, "text", "Text")));
                if (edit.ValueKind != JsonValueKind.Undefined)
                    evidence.DataObjectVariable = JsonStringAny(edit, "text", "Text");

                evidence.NumericMin = EditTextAfterLabel(controls, "最小值");
                evidence.NumericMax = EditTextAfterLabel(controls, "最大值");
                evidence.IntegerDigits = EditTextAfterLabel(controls, "整数位数");
                evidence.DecimalDigits = EditTextAfterLabel(controls, "小数位数");
                evidence.UnitText = EditTextAfterSequence(controls, LayoutJsonIntAny(controls.FirstOrDefault(c => (JsonStringAny(c, "text", "Text") ?? "").Equals("小数位数", StringComparison.Ordinal)), "sequence") ?? -1, skip: 1);
                evidence.NumericBase = CheckedByRelativeOrder(controls, "十进制", "十六进制", "二进制");
                evidence.LeadingZero = IsChecked(controls, "前导0");
                evidence.Rounding = IsChecked(controls, "四舍五入");
                evidence.Password = IsChecked(controls, "密码");
                evidence.UnitEnabled = IsChecked(controls, "使用单位");
                evidence.NaturalDecimalPlaces = IsChecked(controls, "自然小数位");
            }
            else if (tabName.Contains("脚本", StringComparison.OrdinalIgnoreCase))
            {
                var edit = controls.FirstOrDefault(c =>
                    (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Edit", StringComparison.OrdinalIgnoreCase));
                if (edit.ValueKind != JsonValueKind.Undefined)
                    evidence.ScriptText = JsonStringAny(edit, "text", "Text") ?? "";
            }
            else if (tabName.Contains("可见", StringComparison.OrdinalIgnoreCase))
            {
                evidence.VisibilityUsesExpression = IsChecked(controls, "表达式");
                var edit = controls.FirstOrDefault(c =>
                    (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Edit", StringComparison.OrdinalIgnoreCase));
                evidence.VisibilityExpression = edit.ValueKind == JsonValueKind.Undefined
                    ? ""
                    : JsonStringAny(edit, "text", "Text") ?? "";
                if (IsChecked(controls, "按钮可见") == true ||
                    IsChecked(controls, "对应图符可见") == true ||
                    IsChecked(controls, "输入框构件可见") == true)
                    evidence.VisibilityWhenNonZero = "visible";
                else if (IsChecked(controls, "按钮不可见") == true ||
                         IsChecked(controls, "对应图符不可见") == true ||
                         IsChecked(controls, "输入框构件不可见") == true)
                    evidence.VisibilityWhenNonZero = "hidden";
            }
        }

        return evidence;
    }

    private static string? CheckedByRelativeOrder(JsonElement[] controls, params string[] labels)
    {
        var matches = controls
            .Where(c => labels.Contains(JsonStringAny(c, "text", "Text") ?? "", StringComparer.Ordinal))
            .Where(c => JsonIntAny(c, "checkState", "CheckState") == 1)
            .OrderBy(c => LayoutJsonIntAny(c.TryGetProperty("rect", out var rect) ? rect : default, "x", "X") ?? 0)
            .ThenBy(c => LayoutJsonIntAny(c.TryGetProperty("rect", out var rect) ? rect : default, "y", "Y") ?? 0)
            .Select(c => JsonStringAny(c, "text", "Text") ?? "")
            .ToArray();
        return matches.FirstOrDefault(label => labels.Contains(label, StringComparer.Ordinal));
    }

    private static bool? IsChecked(JsonElement[] controls, string label)
    {
        var match = controls.FirstOrDefault(c => (JsonStringAny(c, "text", "Text") ?? "").Equals(label, StringComparison.Ordinal));
        return match.ValueKind == JsonValueKind.Undefined ? null : JsonIntAny(match, "checkState", "CheckState") == 1;
    }

    private static string? EditTextAfterLabel(JsonElement[] controls, string label)
    {
        var sequence = LayoutJsonIntAny(controls.FirstOrDefault(c => (JsonStringAny(c, "text", "Text") ?? "").Equals(label, StringComparison.Ordinal)), "sequence") ?? -1;
        return EditTextAfterSequence(controls, sequence, skip: 0);
    }

    private static string? TextValueAfterLabel(JsonElement[] controls, params string[] labels)
    {
        var labelControl = controls
            .Where(c => labels.Contains(JsonStringAny(c, "text", "Text") ?? "", StringComparer.OrdinalIgnoreCase))
            .OrderBy(c => LayoutJsonIntAny(c, "sequence") ?? int.MaxValue)
            .FirstOrDefault();
        var sequence = LayoutJsonIntAny(labelControl, "sequence") ?? -1;
        if (sequence < 0) return null;

        return controls
            .Where(c => (LayoutJsonIntAny(c, "sequence") ?? 0) > sequence)
            .Where(c =>
            {
                var cls = JsonStringAny(c, "className", "ClassName") ?? "";
                return cls.Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                       cls.Equals("ComboBox", StringComparison.OrdinalIgnoreCase) ||
                       cls.Contains("ListBox", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(c => LayoutJsonIntAny(c, "sequence") ?? 0)
            .Select(ControlCurrentText)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? ControlCurrentText(JsonElement control)
    {
        var text = JsonStringAny(control, "text", "Text");
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        var currentIndex = JsonIntAny(control, "currentIndex", "CurrentIndex");
        if (currentIndex is >= 0 && control.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            var array = items.EnumerateArray().ToArray();
            if (currentIndex.Value < array.Length)
            {
                var item = array[currentIndex.Value];
                if (item.ValueKind == JsonValueKind.String) return item.GetString();
                return JsonStringAny(item, "text", "Text");
            }
        }

        return null;
    }

    private static string? EditTextAfterSequence(JsonElement[] controls, int sequence, int skip)
    {
        if (sequence < 0) return null;
        return controls
            .Where(c => (LayoutJsonIntAny(c, "sequence") ?? 0) > sequence)
            .Where(c => (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Edit", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => LayoutJsonIntAny(c, "sequence") ?? 0)
            .Skip(skip)
            .Select(c => JsonStringAny(c, "text", "Text") ?? "")
            .FirstOrDefault();
    }

    private static int? JsonIntAny(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out var parsed))
                return parsed;
        }
        return null;
    }

    private static object BuildPropertyMapRecord(JsonElement obj, int sequence, List<object> unresolved, PropertyDialogEvidence? readback)
    {
        var id = JsonStringAny(obj, "Id", "id") ?? $"object-{sequence:D3}";
        var kind = JsonStringAny(obj, "SemanticKind", "semanticKind") ?? "unknown";
        var text = JsonStringAny(obj, "DisplayedText", "displayedText") ?? "";
        var variable = JsonStringAny(obj, "Variable", "variable") ?? "";
        var expression = JsonStringAny(obj, "Expression", "expression") ?? "";
        var press = JsonStringAny(obj, "PressOperation", "pressOperation") ?? "";
        var release = JsonStringAny(obj, "ReleaseOperation", "releaseOperation") ?? "";
        var scriptStatus = JsonStringAny(obj, "ScriptStatus", "scriptStatus") ?? "unknown";
        var scriptSummary = JsonStringAny(obj, "ScriptSummary", "scriptSummary") ?? "";
        var confidence = JsonDoubleAny(obj, "Confidence", "confidence") ?? 0;
        var rect = obj.TryGetProperty("Rect", out var upperRect) ? upperRect :
                   obj.TryGetProperty("rect", out var lowerRect) ? lowerRect : default;
        var evidenceSources = JsonStringArrayAny(obj, "EvidenceSources", "evidenceSources").ToArray();
        var evidenceRoot = obj.TryGetProperty("EvidenceChain", out var chain) ? chain :
                           obj.TryGetProperty("evidenceChain", out var lowerChain) ? lowerChain : obj;
        var fontFamily = ExtractFontFamily(evidenceRoot);
        var operations = new List<object>();
        if (!string.IsNullOrWhiteSpace(press)) operations.Add(new { eventName = "press", operation = press, source = "property-readback/workflow-result" });
        if (!string.IsNullOrWhiteSpace(release)) operations.Add(new { eventName = "release", operation = release, source = "property-readback/workflow-result" });
        var otherOperations = JsonStringArrayAny(obj, "OtherOperations", "otherOperations").ToArray();
        var navigationTarget = otherOperations
            .Select(value => value.StartsWith("open-window:", StringComparison.OrdinalIgnoreCase) ? value["open-window:".Length..] : "")
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        foreach (var other in otherOperations)
            operations.Add(new { eventName = "other", operation = other, source = "mce-script-anchor" });

        var properties = new Dictionary<string, object?>
        {
            ["rect"] = ValueProperty(RectObject(rect), "mce-rect-anchor", confidence),
            ["semanticKind"] = ValueProperty(kind, string.Join(",", evidenceSources), confidence),
            ["displayedText"] = string.IsNullOrWhiteSpace(text)
                ? NotApplicableProperty("object has no displayed text evidence in current semantic kind")
                : ValueProperty(text, "mce-text-anchor", confidence),
            ["variableBindings"] = string.IsNullOrWhiteSpace(variable)
                ? NotApplicableProperty("no direct variable binding evidence for this object")
                : ValueProperty(new[] { variable }, "workflow-result/mce-text-anchor", confidence),
            ["expressions"] = string.IsNullOrWhiteSpace(expression)
                ? NotApplicableProperty("no expression evidence for this object")
                : ValueProperty(new[] { expression }, "mce-text-anchor/workflow-result", confidence),
            ["operations"] = operations.Count == 0
                ? NotApplicableProperty("no operation/action evidence for this object")
                : ValueProperty(operations, "property-readback/mce-script-anchor", confidence),
            ["script"] = readback?.ScriptText != null
                ? ValueProperty(new
                {
                    status = string.IsNullOrWhiteSpace(readback.ScriptText) ? "empty" : "nonempty",
                    summary = readback.ScriptText.Length > 160 ? readback.ScriptText[..160] + "..." : readback.ScriptText
                }, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : scriptStatus.Equals("empty", StringComparison.OrdinalIgnoreCase)
                    ? ValueProperty(new { status = "empty", summary = "" }, "mce-anchor/workflow-readback", confidence)
                    : ValueProperty(new { status = scriptStatus, summary = scriptSummary }, "mce-anchor/workflow-readback", confidence),
            ["fontFamily"] = !string.IsNullOrWhiteSpace(readback?.FontFamily)
                ? ValueProperty(readback!.FontFamily, "property-dialog-font-readback:" + readback.EvidencePath, 0.9)
                : string.IsNullOrWhiteSpace(fontFamily)
                    ? UnresolvedProperty(id, "fontFamily", "Open property dialog text/style tab or run single-font diff.", unresolved)
                    : ValueProperty(fontFamily, "mce-text-anchor", 0.7),
            ["fontSize"] = !string.IsNullOrWhiteSpace(readback?.FontSize)
                ? ValueProperty(readback!.FontSize, "property-dialog-font-readback:" + readback.EvidencePath, 0.9)
                : UnresolvedProperty(id, "fontSize", "Run single-font-size diff or property-dialog text tab readback.", unresolved),
            ["fontStyle"] = !string.IsNullOrWhiteSpace(readback?.FontStyle)
                ? ValueProperty(readback!.FontStyle, "property-dialog-font-readback:" + readback.EvidencePath, 0.9)
                : UnresolvedProperty(id, "fontStyle", "Run property-dialog text tab readback for bold/italic/underline flags.", unresolved),
            ["alignment"] = readback?.HorizontalAlignment != null || readback?.VerticalAlignment != null
                ? ValueProperty(new
                {
                    horizontal = readback?.HorizontalAlignment ?? "unread",
                    vertical = readback?.VerticalAlignment ?? "unread"
                }, "property-dialog-readback:" + readback!.EvidencePath, 0.9)
                : UnresolvedProperty(id, "alignment", "Run property-dialog text/alignment tab readback or single-alignment diff.", unresolved),
            ["foregroundColor"] = UnresolvedProperty(id, "foregroundColor", "Run single-color diff or property-dialog color tab readback.", unresolved),
            ["backgroundColor"] = UnresolvedProperty(id, "backgroundColor", "Run single-color diff or property-dialog fill/background readback.", unresolved),
            ["borderColor"] = IsTextOnlyCanvasKind(kind)
                ? NotApplicableProperty("CDrawLabel/text-only object has no evidenced border property in the target row")
                : UnresolvedProperty(id, "borderColor", "Run property-dialog border tab readback or line-color diff.", unresolved),
            ["borderStyle"] = !string.IsNullOrWhiteSpace(readback?.BorderStyle)
                ? ValueProperty(readback!.BorderStyle, "property-dialog-readback:" + readback.EvidencePath, 0.9)
                : IsTextOnlyCanvasKind(kind)
                ? NotApplicableProperty("CDrawLabel/text-only object has no evidenced border style in the target row")
                : UnresolvedProperty(id, "borderStyle", "Run property-dialog border/line tab readback.", unresolved),
            ["fillStyle"] = readback?.ButtonType != null || readback?.TextEffect != null || readback?.TextOrientation != null || readback?.UsesBitmap != null || readback?.UsesVector != null
                ? ValueProperty(new
                {
                    buttonType = readback?.ButtonType ?? "",
                    textEffect = readback?.TextEffect ?? "",
                    textOrientation = readback?.TextOrientation ?? "",
                    usesBitmap = readback?.UsesBitmap,
                    usesVector = readback?.UsesVector
                }, "property-dialog-readback:" + readback!.EvidencePath, 0.85)
                : IsTextOnlyCanvasKind(kind)
                ? NotApplicableProperty("CDrawLabel/text-only object has no evidenced fill style in the target row")
                : UnresolvedProperty(id, "fillStyle", "Run property-dialog fill tab readback.", unresolved),
            ["visibility"] = readback?.VisibilityWhenNonZero != null
                ? ValueProperty(new
                {
                    status = readback.VisibilityUsesExpression == true ? "conditional" : "default",
                    expression = readback.VisibilityExpression ?? "",
                    whenExpressionNonZero = readback.VisibilityWhenNonZero
                }, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : expression.Contains("visibility", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { status = "conditional", expressionKnown = false, summary = expression }, "mce-visible-anchor", confidence)
                : UnresolvedProperty(id, "visibility", "Confirm default visibility or condition page with property-dialog readback.", unresolved),
            ["enableCondition"] = IsInteractiveKind(kind)
                ? UnresolvedProperty(id, "enableCondition", "Read operation/security tab for enable condition.", unresolved)
                : NotApplicableProperty("static/non-interactive object has no operation enable condition"),
            ["displayRules"] = readback?.VisibilityUsesExpression == true
                ? ValueProperty(new { kind = "visibility", expression = readback.VisibilityExpression ?? "", whenExpressionNonZero = readback.VisibilityWhenNonZero ?? "" }, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : expression.Contains("visibility", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { kind = "visibility", summary = expression }, "mce-visible-anchor", confidence)
                : NotApplicableProperty("no display/animation rule evidence"),
            ["inputFormat"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(readback?.NumericBase)
                ? ValueProperty(new
                {
                    numberBase = readback!.NumericBase,
                    leadingZero = readback.LeadingZero,
                    rounding = readback.Rounding,
                    password = readback.Password,
                    unitEnabled = readback.UnitEnabled,
                    naturalDecimalPlaces = readback.NaturalDecimalPlaces
                }, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "inputFormat", "Read numeric input/display format property page.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["unit"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(readback?.UnitText)
                ? ValueProperty(readback!.UnitText, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(expression)
                ? ValueProperty(expression, "mce-formula-anchor", confidence)
                : NotApplicableProperty("no unit evidence or object is not numeric input"),
            ["precision"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) &&
                             (!string.IsNullOrWhiteSpace(readback?.IntegerDigits) || !string.IsNullOrWhiteSpace(readback?.DecimalDigits))
                ? ValueProperty(new { integerDigits = readback?.IntegerDigits ?? "", decimalDigits = readback?.DecimalDigits ?? "" }, "property-dialog-readback:" + readback!.EvidencePath, 0.95)
                : kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "precision", "Read numeric input/display format property page or single-precision diff.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["range"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) &&
                         (!string.IsNullOrWhiteSpace(readback?.NumericMin) || !string.IsNullOrWhiteSpace(readback?.NumericMax))
                ? ValueProperty(new { min = readback?.NumericMin ?? "", max = readback?.NumericMax ?? "" }, "property-dialog-readback:" + readback!.EvidencePath, 0.95)
                : kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "range", "Read numeric range/up-down limit property fields.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["permissions"] = IsInteractiveKind(kind)
                ? UnresolvedProperty(id, "permissions", "Read operation/security tab for permission level.", unresolved)
                : NotApplicableProperty("static/non-interactive object"),
            ["navigationTarget"] = kind.Equals("navigation-button", StringComparison.OrdinalIgnoreCase)
                ? string.IsNullOrWhiteSpace(navigationTarget)
                    ? UnresolvedProperty(id, "navigationTarget", "Decode navigation script or read action tab.", unresolved)
                    : ValueProperty(navigationTarget, "mce-script-anchor/open-window", confidence)
                : NotApplicableProperty("not a navigation object"),
            ["animationRules"] = IsAnimationKind(kind) && readback?.VisibilityUsesExpression == true
                ? ValueProperty(new { kind = "visibility", expression = readback.VisibilityExpression ?? "", whenExpressionNonZero = readback.VisibilityWhenNonZero ?? "" }, "property-dialog-readback:" + readback.EvidencePath, 0.95)
                : IsAnimationKind(kind) && expression.Contains("visibility", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { kind = "visibility", expressionKnown = false, summary = expression }, "mce-visible-anchor", confidence)
                : IsAnimationKind(kind)
                ? UnresolvedProperty(id, "animationRules", "Read animation/display tab and MCE animation anchors.", unresolved)
                : NotApplicableProperty("no animation behavior evidenced for this kind"),
            ["alarmRules"] = IsAlarmKind(kind)
                ? UnresolvedProperty(id, "alarmRules", "Read alarm/state color mapping page.", unresolved)
                : NotApplicableProperty("not an alarm/status state object"),
            ["grouping"] = ValueProperty(new
            {
                parentRowKey = JsonStringAny(obj, "RowKey", "rowKey") ?? "",
                groupId = (string?)null,
                groupStatus = "no-explicit-group-marker-detected",
                zOrderEvidence = sequence
            }, "mce-row-object-stream", 0.55),
            ["zOrder"] = ValueProperty(sequence, "mce-control-order", 0.55)
        };

        var unresolvedCount = properties.Values.Count(value => IsUnresolvedProperty(value));
        return new
        {
            objectId = id,
            rowKey = JsonStringAny(obj, "RowKey", "rowKey") ?? "",
            semanticKind = kind,
            rect = RectObject(rect),
            displayedText = text,
            variable,
            expression,
            propertyStatus = unresolvedCount == 0 ? "complete" : "unresolved",
            unresolvedCount,
            confidence,
            evidenceSources,
            properties
        };
    }

    private static object RectObject(JsonElement rect)
        => new
        {
            x = LayoutJsonIntAny(rect, "X", "x") ?? 0,
            y = LayoutJsonIntAny(rect, "Y", "y") ?? 0,
            width = LayoutJsonIntAny(rect, "Width", "width") ?? 0,
            height = LayoutJsonIntAny(rect, "Height", "height") ?? 0
        };

    private static object ValueProperty(object? value, string evidence, double confidence)
        => new { status = "value", value, confidence, evidenceSource = evidence, nextProbe = "" };

    private static object NotApplicableProperty(string reason)
        => new { status = "notApplicable", value = (object?)null, confidence = 1.0, evidenceSource = "semantic-kind", reason, nextProbe = "" };

    private static object UnresolvedProperty(string objectId, string property, string nextProbe, List<object> unresolved)
    {
        var record = new
        {
            status = "unresolved",
            value = (object?)null,
            confidence = 0.0,
            evidenceSource = "",
            nextProbe
        };
        unresolved.Add(new { objectId, property, nextProbe });
        return record;
    }

    private static bool IsUnresolvedProperty(object? value)
        => value != null && JsonSerializer.Serialize(value, JsonOptions()).Contains("\"status\": \"unresolved\"", StringComparison.OrdinalIgnoreCase);

    private static bool IsInteractiveKind(string kind)
        => kind.Contains("button", StringComparison.OrdinalIgnoreCase) ||
           kind.Contains("input", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextOnlyCanvasKind(string kind)
        => kind.Equals("static-label", StringComparison.OrdinalIgnoreCase) ||
           kind.Equals("section-title", StringComparison.OrdinalIgnoreCase) ||
           kind.Equals("conditional-message", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnimationKind(string kind)
        => kind.Contains("status", StringComparison.OrdinalIgnoreCase) ||
           kind.Contains("lamp", StringComparison.OrdinalIgnoreCase) ||
           kind.Contains("conditional", StringComparison.OrdinalIgnoreCase);

    private static bool IsAlarmKind(string kind)
        => kind.Contains("alarm", StringComparison.OrdinalIgnoreCase) ||
           kind.Contains("fault", StringComparison.OrdinalIgnoreCase) ||
           kind.Contains("lamp", StringComparison.OrdinalIgnoreCase);

    private static string ExtractFontFamily(JsonElement evidenceRoot)
    {
        var evidenceText = string.Join("\n", CollectJsonStrings(evidenceRoot));
        foreach (var font in KnownFontFamilies())
        {
            if (evidenceText.Contains(font, StringComparison.OrdinalIgnoreCase))
                return font;
        }
        foreach (var font in new[] { "幼圆", "宋体", "黑体", "楷体", "仿宋", "Arial", "Tahoma", "Microsoft Sans Serif" })
        {
            if (evidenceText.Contains(font, StringComparison.OrdinalIgnoreCase))
                return font;
        }
        var match = Regex.Match(evidenceText, @"[\p{L}\s]{1,20}(?:体|圆)");
        return match.Success ? match.Value.Trim() : "";
    }

    private static IEnumerable<string> KnownFontFamilies()
    {
        yield return "\u5E7C\u5706"; // YouYuan
        yield return "\u5B8B\u4F53"; // SimSun
        yield return "\u9ED1\u4F53"; // SimHei
        yield return "\u6977\u4F53"; // KaiTi
        yield return "\u4EFF\u5B8B"; // FangSong
        yield return "Arial";
        yield return "Tahoma";
        yield return "Microsoft Sans Serif";
    }

    private static IEnumerable<string> CollectJsonStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString() ?? "";
                break;
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                foreach (var value in CollectJsonStrings(prop.Value))
                    yield return value;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                foreach (var value in CollectJsonStrings(item))
                    yield return value;
                break;
        }
    }

    private static double? JsonDoubleAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var value)) return value;
            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out value)) return value;
        }
        return null;
    }
}
