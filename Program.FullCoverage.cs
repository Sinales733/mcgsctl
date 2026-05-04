using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            invoked = false,
            tool.safetyClass,
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
            invokedCount = 0,
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
                            build.Tools.Add(new McgsToolEntry
                            {
                                toolId = isSeparator
                                    ? $"toolbar:{toolbarIndex}:{index}:separator"
                                    : $"toolbar:{toolbarIndex}:{index}:{commandId}",
                                displayName = string.IsNullOrWhiteSpace(text)
                                    ? (isSeparator ? "separator" : $"command {commandId}")
                                    : text,
                                source = "toolbar",
                                uiPath = string.IsNullOrWhiteSpace(toolbarText) ? $"toolbar[{toolbarIndex}]/button[{index}]" : $"{toolbarText}/button[{index}]",
                                commandId = commandId,
                                enabled = enabled,
                                hidden = hidden,
                                supportStatus = isSeparator ? "implemented" : "needs-probe",
                                safetyClass = isSeparator ? "read-only" : "unknown-risk",
                                invocationRoute = isSeparator ? "" : $"WM_COMMAND {commandId}",
                                expectedEffect = isSeparator ? "visual separator" : "unknown until candidate-safe probe records before/after evidence",
                                evidenceSource = probePath,
                                nextProbe = isSeparator ? "" : "Probe command on throwaway candidate after classifying menu/toolbar context and expected side effect."
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

        var records = new List<object>();
        var unresolved = new List<object>();
        var sequence = 0;
        foreach (var obj in objects.EnumerateArray())
        {
            sequence++;
            var record = BuildPropertyMapRecord(obj, sequence, unresolved);
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

    private static object BuildPropertyMapRecord(JsonElement obj, int sequence, List<object> unresolved)
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
        foreach (var other in JsonStringArrayAny(obj, "OtherOperations", "otherOperations"))
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
            ["script"] = scriptStatus.Equals("empty", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { status = "empty", summary = "" }, "mce-anchor/workflow-readback", confidence)
                : ValueProperty(new { status = scriptStatus, summary = scriptSummary }, "mce-anchor/workflow-readback", confidence),
            ["fontFamily"] = string.IsNullOrWhiteSpace(fontFamily)
                ? UnresolvedProperty(id, "fontFamily", "Open property dialog text/style tab or run single-font diff.", unresolved)
                : ValueProperty(fontFamily, "mce-text-anchor", 0.7),
            ["fontSize"] = UnresolvedProperty(id, "fontSize", "Run single-font-size diff or property-dialog text tab readback.", unresolved),
            ["fontStyle"] = UnresolvedProperty(id, "fontStyle", "Run property-dialog text tab readback for bold/italic/underline flags.", unresolved),
            ["alignment"] = UnresolvedProperty(id, "alignment", "Run property-dialog text/alignment tab readback or single-alignment diff.", unresolved),
            ["foregroundColor"] = UnresolvedProperty(id, "foregroundColor", "Run single-color diff or property-dialog color tab readback.", unresolved),
            ["backgroundColor"] = UnresolvedProperty(id, "backgroundColor", "Run single-color diff or property-dialog fill/background readback.", unresolved),
            ["borderColor"] = UnresolvedProperty(id, "borderColor", "Run property-dialog border tab readback or line-color diff.", unresolved),
            ["borderStyle"] = UnresolvedProperty(id, "borderStyle", "Run property-dialog border/line tab readback.", unresolved),
            ["fillStyle"] = UnresolvedProperty(id, "fillStyle", "Run property-dialog fill tab readback.", unresolved),
            ["visibility"] = expression.Contains("visibility", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { status = "conditional", expressionKnown = false, summary = expression }, "mce-visible-anchor", confidence)
                : UnresolvedProperty(id, "visibility", "Confirm default visibility or condition page with property-dialog readback.", unresolved),
            ["enableCondition"] = UnresolvedProperty(id, "enableCondition", "Read operation/security tab for enable condition.", unresolved),
            ["displayRules"] = expression.Contains("visibility", StringComparison.OrdinalIgnoreCase)
                ? ValueProperty(new { kind = "visibility", summary = expression }, "mce-visible-anchor", confidence)
                : NotApplicableProperty("no display/animation rule evidence"),
            ["inputFormat"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "inputFormat", "Read numeric input/display format property page.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["unit"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(expression)
                ? ValueProperty(expression, "mce-formula-anchor", confidence)
                : NotApplicableProperty("no unit evidence or object is not numeric input"),
            ["precision"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "precision", "Read numeric input/display format property page or single-precision diff.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["range"] = kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "range", "Read numeric range/up-down limit property fields.", unresolved)
                : NotApplicableProperty("not a numeric input/display object"),
            ["permissions"] = IsInteractiveKind(kind)
                ? UnresolvedProperty(id, "permissions", "Read operation/security tab for permission level.", unresolved)
                : NotApplicableProperty("static/non-interactive object"),
            ["navigationTarget"] = kind.Equals("navigation-button", StringComparison.OrdinalIgnoreCase)
                ? UnresolvedProperty(id, "navigationTarget", "Decode navigation script or read action tab.", unresolved)
                : NotApplicableProperty("not a navigation object"),
            ["animationRules"] = IsAnimationKind(kind)
                ? UnresolvedProperty(id, "animationRules", "Read animation/display tab and MCE animation anchors.", unresolved)
                : NotApplicableProperty("no animation behavior evidenced for this kind"),
            ["alarmRules"] = IsAlarmKind(kind)
                ? UnresolvedProperty(id, "alarmRules", "Read alarm/state color mapping page.", unresolved)
                : NotApplicableProperty("not an alarm/status state object"),
            ["grouping"] = UnresolvedProperty(id, "grouping", "Derive group/parent from MCE hierarchy or property dialog; do not rely only on geometry.", unresolved),
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
        foreach (var font in new[] { "幼圆", "宋体", "黑体", "楷体", "仿宋", "Arial", "Tahoma", "Microsoft Sans Serif" })
        {
            if (evidenceText.Contains(font, StringComparison.OrdinalIgnoreCase))
                return font;
        }
        var match = Regex.Match(evidenceText, @"[\p{L}\s]{1,20}(?:体|圆)");
        return match.Success ? match.Value.Trim() : "";
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
