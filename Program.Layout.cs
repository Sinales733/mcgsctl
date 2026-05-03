using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private sealed class LayoutObjectPlan
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string? Variable { get; set; }
        public string? Expression { get; set; }
        public int WindowIndex { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Source { get; set; } = "";
        public string PlacementSource { get; set; } = "layout";
        public string? RenderAs { get; set; }
        public bool IsSyntheticText => Kind is "section-title" or "static-label";
        public string GuiKind
        {
            get
            {
                if (Kind is "momentary-button" or "status-button" or "native-static-text" or "native-lamp") return Kind;
                if (IsSyntheticText && string.Equals(RenderAs, "status-button", StringComparison.OrdinalIgnoreCase))
                    return "status-button";
                if (IsSyntheticText && !string.Equals(RenderAs, "preview-only", StringComparison.OrdinalIgnoreCase))
                    return "native-static-text";
                return Kind;
            }
        }
        public string EffectiveExpression => GuiKind is "status-button" or "native-lamp"
            ? (Expression ?? (IsSyntheticText ? "1" : ""))
            : "";
        public bool GuiSupported => GuiKind is "momentary-button" or "status-button" or "native-static-text" or "native-lamp";
    }

    private sealed class LayoutValidationResult
    {
        public int SchemaVersion { get; set; } = 1;
        public string Layout { get; set; } = "";
        public string Status { get; set; } = "UNKNOWN";
        public string Verdict { get; set; } = "blocked";
        public int WindowIndex { get; set; }
        public int CanvasWidth { get; set; }
        public int CanvasHeight { get; set; }
        public int ReadabilityScore { get; set; }
        public string PlacementSource { get; set; } = "layout";
        public LayoutPlacementPlan? PlacementPlan { get; set; }
        public List<ResultCheck> Checks { get; } = new();
        public List<string> BlockedReasons { get; } = new();
        public List<string> UnknownReasons { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<LayoutObjectPlan> Objects { get; } = new();
    }

    private sealed class LayoutPlacementPlan
    {
        public string Status { get; set; } = "NOT_REQUESTED";
        public string PlacementSource { get; set; } = "layout";
        public string? CanvasObjects { get; set; }
        public string ObjectProvider { get; set; } = "none";
        public bool ReliableGeometry { get; set; }
        public int Margin { get; set; }
        public object? PlannedBounds { get; set; }
        public object? Offset { get; set; }
        public List<CanvasOccupiedRect> OccupiedRectangles { get; } = new();
        public List<string> BlockedReasons { get; } = new();
        public List<string> UnknownReasons { get; } = new();
    }

    private sealed class LayoutStyle
    {
        public int Grid { get; set; } = 10;
        public int ButtonWidth { get; set; } = 90;
        public int ButtonHeight { get; set; } = 50;
        public int StatusWidth { get; set; } = 110;
        public int StatusHeight { get; set; } = 32;
        public int TitleHeight { get; set; } = 28;
        public int LabelHeight { get; set; } = 24;
    }

    private static int Layout(string[] args)
    {
        if (args.Length < 2)
            return Fail("Usage: mcgsctl layout validate|preview|readback --layout <layout.json> [--safety <safety-spec.json>]");

        return args[1].ToLowerInvariant() switch
        {
            "validate" => LayoutValidate(args),
            "preview" => LayoutPreview(args),
            "readback" => LayoutReadback(args),
            _ => Fail("Unknown layout command: " + args[1])
        };
    }

    private static int LayoutValidate(string[] args)
    {
        try
        {
            var layout = FullPath(RequiredLayoutPath(args));
            var safety = OptionalFullPath(Opt(args, "--safety"));
            var result = BuildLayoutValidation(layout, safety, OptionalFullPath(Opt(args, "--canvas-objects")), Opt(args, "--placement"));
            WriteJson(result);
            WriteLayoutValidationOutput(args, result, null);
            return result.Status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("layout validate failed: " + ex.Message);
            return 1;
        }
    }

    private static int LayoutPreview(string[] args)
    {
        try
        {
            var layout = FullPath(RequiredLayoutPath(args));
            var safety = OptionalFullPath(Opt(args, "--safety"));
            var outDir = FullPath(Required(args, "--out"));
            Directory.CreateDirectory(outDir);
            var result = BuildLayoutValidation(layout, safety, OptionalFullPath(Opt(args, "--canvas-objects")), Opt(args, "--placement"));
            WriteLayoutPreviewFiles(result, outDir);
            Console.WriteLine("layout preview: " + outDir);
            return result.Status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("layout preview failed: " + ex.Message);
            return 1;
        }
    }

    private static int LayoutReadback(string[] args)
    {
        try
        {
            var project = RequiredPath(args, "--project");
            var layout = FullPath(RequiredLayoutPath(args));
            var outDir = FullPath(Required(args, "--out"));
            Directory.CreateDirectory(outDir);
            var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
            var validation = BuildLayoutValidation(layout, safetyPath: null, OptionalFullPath(Opt(args, "--canvas-objects")), Opt(args, "--placement"));
            if (validation.Status != "PASS")
            {
                File.WriteAllText(Path.Combine(outDir, "layout-readback.json"),
                    JsonSerializer.Serialize(new
                    {
                        status = "FAIL",
                        validation.BlockedReasons,
                        validation.Warnings,
                        validation.Objects
                    }, ResultJsonOptions()), Encoding.UTF8);
                return 2;
            }

            var projectShaBefore = Sha256(project);
            var copyDir = Path.Combine(outDir, "readback-copy");
            Directory.CreateDirectory(copyDir);
            var readbackProject = Path.Combine(copyDir, "candidate-readback.MCE");
            File.Copy(project, readbackProject, overwrite: true);
            var readbackProjectShaBefore = Sha256(readbackProject);
            if (!readbackProjectShaBefore.Equals(projectShaBefore, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Layout readback copy SHA does not match candidate SHA.");

            var afterSnapshot = ExportMceSnapshot(readbackProject, Path.Combine(outDir, "mce-current"));
            var objectResults = new List<Dictionary<string, object?>>();
            foreach (var obj in validation.Objects.Where(o => o.GuiSupported))
            {
                var objectDir = Path.Combine(outDir, "objects", SafeFile(obj.Id));
                Directory.CreateDirectory(objectDir);
                if (obj.GuiKind == "momentary-button")
                {
                    var ok = ReopenVerifyMomentaryButton(readbackProject, editor, obj.Text, obj.Variable ?? "", obj.WindowIndex,
                        obj.X, obj.Y, obj.Width, obj.Height, objectDir, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)),
                        afterSnapshot, out var reopenVerified);
                    objectResults.Add(new Dictionary<string, object?>
                    {
                        ["id"] = obj.Id,
                        ["kind"] = obj.Kind,
                        ["status"] = ok && reopenVerified ? "PASS" : "UNKNOWN",
                        ["propertyReadback"] = ok ? "PASS" : "UNKNOWN",
                        ["reopenReadback"] = reopenVerified ? "PASS" : "UNKNOWN",
                        ["evidenceDir"] = objectDir
                    });
                }
                else if (obj.GuiKind == "status-button")
                {
                    var ok = ReopenVerifyStatusButtonIndicator(readbackProject, editor, obj.Text, obj.EffectiveExpression,
                        obj.WindowIndex, obj.X, obj.Y, obj.Width, obj.Height, objectDir,
                        TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
                    objectResults.Add(new Dictionary<string, object?>
                    {
                        ["id"] = obj.Id,
                        ["kind"] = obj.Kind,
                        ["status"] = ok ? "PASS" : "UNKNOWN",
                        ["propertyReadback"] = ok ? "PASS" : "UNKNOWN",
                        ["evidenceDir"] = objectDir
                    });
                }
                else if (obj.GuiKind == "native-static-text")
                {
                    var ok = ReopenVerifyNativeStaticText(readbackProject, editor, obj.Text,
                        obj.WindowIndex, obj.X, obj.Y, obj.Width, obj.Height, objectDir,
                        TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
                    objectResults.Add(new Dictionary<string, object?>
                    {
                        ["id"] = obj.Id,
                        ["kind"] = obj.Kind,
                        ["guiKind"] = obj.GuiKind,
                        ["status"] = ok ? "PASS" : "UNKNOWN",
                        ["propertyReadback"] = ok ? "PASS" : "UNKNOWN",
                        ["evidenceDir"] = objectDir
                    });
                }
                else if (obj.GuiKind == "native-lamp")
                {
                    var ok = ReopenVerifyNativeLamp(readbackProject, editor, obj.Text, obj.EffectiveExpression,
                        obj.WindowIndex, obj.X, obj.Y, obj.Width, obj.Height, objectDir,
                        TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
                    objectResults.Add(new Dictionary<string, object?>
                    {
                        ["id"] = obj.Id,
                        ["kind"] = obj.Kind,
                        ["guiKind"] = obj.GuiKind,
                        ["status"] = ok ? "PASS" : "UNKNOWN",
                        ["propertyReadback"] = ok ? "PASS" : "UNKNOWN",
                        ["evidenceDir"] = objectDir
                    });
                }
            }

            foreach (var obj in validation.Objects.Where(o => !o.GuiSupported))
            {
                objectResults.Add(new Dictionary<string, object?>
                {
                    ["id"] = obj.Id,
                    ["kind"] = obj.Kind,
                    ["status"] = "NOT_REQUESTED",
                    ["message"] = "preview-only object kind in this implementation"
                });
            }

            var projectShaAfter = Sha256(project);
            var readbackProjectShaAfter = Sha256(readbackProject);
            var candidateUnchanged = projectShaAfter.Equals(projectShaBefore, StringComparison.OrdinalIgnoreCase);
            var status = !candidateUnchanged
                ? "FAIL"
                : objectResults.Any(x => (string?)x["status"] == "UNKNOWN")
                    ? "UNKNOWN"
                    : "PASS";
            File.WriteAllText(Path.Combine(outDir, "layout-readback.json"),
                JsonSerializer.Serialize(new
                {
                    status,
                    project,
                    projectShaBefore,
                    projectShaAfter,
                    candidateUnchanged,
                    checkedTemporaryCopy = true,
                    checkedProject = readbackProject,
                    checkedProjectSha256Before = readbackProjectShaBefore,
                    checkedProjectSha256After = readbackProjectShaAfter,
                    layout,
                    objects = objectResults
                }, ResultJsonOptions()), Encoding.UTF8);
            Console.WriteLine("layout readback: " + Path.Combine(outDir, "layout-readback.json"));
            return status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("layout readback failed: " + ex.Message);
            return 1;
        }
    }

    private static int WorkflowLayoutApply(string[] args)
    {
        var outDir = FullPath(Opt(args, "--out") ?? Path.Combine(".mcgsctl-runs", "window.layout.apply-" + Timestamp()));
        Directory.CreateDirectory(outDir);
        try
        {
            var layout = FullPath(RequiredLayoutPath(args));
            var safety = OptionalFullPath(Opt(args, "--safety"));
            var validation = BuildLayoutValidation(layout, safety, OptionalFullPath(Opt(args, "--canvas-objects")), Opt(args, "--placement"));
            var previewDir = Path.Combine(outDir, "layout-preview");
            WriteLayoutPreviewFiles(validation, previewDir);
            if (validation.Status != "PASS")
            {
                File.WriteAllText(Path.Combine(outDir, "layout-apply-result.json"),
                    JsonSerializer.Serialize(new
                    {
                        status = validation.Status,
                        layout,
                        previewDir,
                        validation.BlockedReasons,
                        validation.UnknownReasons,
                        validation.PlacementPlan
                    }, ResultJsonOptions()), Encoding.UTF8);
                Console.Error.WriteLine("layout apply blocked by validation: " +
                                        string.Join("; ", validation.BlockedReasons.Concat(validation.UnknownReasons)));
                return 2;
            }

            var guiObjects = validation.Objects.Where(o => o.GuiSupported).ToArray();
            if (guiObjects.Length == 0)
                throw new InvalidOperationException("Layout contains no GUI-supported objects. Supported GUI kinds: momentary-button, status-button, native-static-text, native-lamp.");

            var source = Opt(args, "--source");
            var project = Opt(args, "--project");
            if (!string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(project))
                throw new ArgumentException("Use either --source or --project, not both.");
            if (string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(project))
                throw new ArgumentException("Missing --source or --project.");

            var workDir = Opt(args, "--workdir");
            if (!string.IsNullOrWhiteSpace(source) && string.IsNullOrWhiteSpace(workDir))
                workDir = Path.Combine(".mcgsctl-work", "layout-" + Timestamp());

            var childResults = new List<Dictionary<string, object?>>();
            var currentProject = string.IsNullOrWhiteSpace(project) ? null : FullPath(project);
            for (var i = 0; i < guiObjects.Length; i++)
            {
                var obj = guiObjects[i];
                var childOut = Path.Combine(outDir, "objects", $"{i + 1:D2}-{SafeFile(obj.Id)}");
                Directory.CreateDirectory(childOut);
                var childArgs = BuildLayoutChildWorkflowArgs(args, obj, childOut, source, workDir, currentProject);
                var exitCode = obj.GuiKind switch
                {
                    "momentary-button" => WorkflowAddMomentaryButton(childArgs),
                    "status-button" => WorkflowWindowIndicatorAdd(childArgs),
                    "native-static-text" => WorkflowNativeStaticTextAdd(childArgs),
                    "native-lamp" => WorkflowNativeLampAdd(childArgs),
                    _ => 2
                };
                if (exitCode == 0 && currentProject == null && !string.IsNullOrWhiteSpace(workDir))
                    currentProject = FullPath(Path.Combine(workDir, CandidateFileName));
                childResults.Add(new Dictionary<string, object?>
                {
                    ["id"] = obj.Id,
                    ["kind"] = obj.Kind,
                    ["guiKind"] = obj.GuiKind,
                    ["exitCode"] = exitCode,
                    ["outDir"] = childOut
                });
                if (exitCode != 0)
                {
                    File.WriteAllText(Path.Combine(outDir, "layout-apply-result.json"),
                        JsonSerializer.Serialize(new
                        {
                            status = "FAIL",
                            layout,
                            previewDir,
                            objects = childResults
                        }, ResultJsonOptions()), Encoding.UTF8);
                    return exitCode;
                }
                source = null;
            }

            File.WriteAllText(Path.Combine(outDir, "layout-apply-result.json"),
                JsonSerializer.Serialize(new
                {
                    status = "PASS",
                    layout,
                    layoutSha256 = Sha256(layout),
                    previewDir,
                    project = currentProject,
                    placementSource = validation.PlacementSource,
                    placementPlan = validation.PlacementPlan,
                    previewOnlyObjects = validation.Objects.Where(o => !o.GuiSupported).Select(o => new { o.Id, o.Kind, o.Text }).ToArray(),
                    syntheticTextObjects = validation.Objects.Where(o => o.IsSyntheticText && o.GuiSupported).Select(o => new { o.Id, o.Kind, o.Text, o.RenderAs, guiKind = o.GuiKind, expression = o.EffectiveExpression }).ToArray(),
                    objects = childResults
                }, ResultJsonOptions()), Encoding.UTF8);
            Console.WriteLine("layout apply evidence: " + outDir);
            if (!string.IsNullOrWhiteSpace(currentProject)) Console.WriteLine("candidate: " + currentProject);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("layout apply failed: " + ex.Message);
            return 1;
        }
    }

    private static string[] BuildLayoutChildWorkflowArgs(
        string[] parentArgs,
        LayoutObjectPlan obj,
        string outDir,
        string? source,
        string? workDir,
        string? project)
    {
        var args = new List<string>
        {
            "workflow",
            "run",
            obj.GuiKind switch
            {
                "momentary-button" => "window.button.add-momentary",
                "status-button" => "window.indicator.add",
                "native-static-text" => "window.static-text.add",
                "native-lamp" => "window.lamp.add-native",
                _ => throw new ArgumentException("Unsupported layout GUI kind: " + obj.GuiKind)
            }
        };
        if (!string.IsNullOrWhiteSpace(source))
        {
            args.Add("--source");
            args.Add(source);
            args.Add("--workdir");
            args.Add(workDir ?? Path.Combine(".mcgsctl-work", "layout-" + Timestamp()));
        }
        else
        {
            args.Add("--project");
            args.Add(project ?? throw new ArgumentException("Missing candidate project for layout child workflow."));
        }
        args.Add("--out");
        args.Add(outDir);
        args.Add("--text");
        args.Add(obj.Text);
        if (obj.GuiKind == "momentary-button")
        {
            args.Add("--variable");
            args.Add(obj.Variable ?? "");
        }
        else if (obj.GuiKind is "status-button" or "native-lamp")
        {
            args.Add("--expression");
            args.Add(obj.EffectiveExpression);
            if (obj.IsSyntheticText && obj.GuiKind == "status-button")
                args.Add("--synthetic-label");
        }
        args.Add("--window-index");
        args.Add(obj.WindowIndex.ToString());
        args.Add("--x");
        args.Add(obj.X.ToString());
        args.Add("--y");
        args.Add(obj.Y.ToString());
        args.Add("--width");
        args.Add(obj.Width.ToString());
        args.Add("--height");
        args.Add(obj.Height.ToString());
        foreach (var passthrough in new[] { "--editor", "--timeout" })
        {
            var value = Opt(parentArgs, passthrough);
            if (!string.IsNullOrWhiteSpace(value))
            {
                args.Add(passthrough);
                args.Add(value);
            }
        }
        return args.ToArray();
    }

    private static string RequiredLayoutPath(string[] args)
        => Opt(args, "--layout") ?? Opt(args, "--spec") ?? throw new ArgumentException("Missing --layout <layout.json>");

    private static string? OptionalFullPath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : FullPath(path);

    private static void WriteLayoutValidationOutput(string[] args, LayoutValidationResult result, string? defaultPath)
    {
        var outOpt = Opt(args, "--out") ?? defaultPath;
        if (string.IsNullOrWhiteSpace(outOpt)) return;
        var full = FullPath(outOpt);
        if (Directory.Exists(full) || Path.GetExtension(full).Length == 0)
        {
            Directory.CreateDirectory(full);
            full = Path.Combine(full, "validate.json");
        }
        File.WriteAllText(full, JsonSerializer.Serialize(result, ResultJsonOptions()), Encoding.UTF8);
    }

    private static LayoutValidationResult BuildLayoutValidation(
        string layoutPath,
        string? safetyPath,
        string? canvasObjectsPath = null,
        string? placementModeOverride = null)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(layoutPath, Encoding.UTF8));
        var root = doc.RootElement;
        var style = ReadLayoutStyle(root);
        var canvas = root.TryGetProperty("canvas", out var canvasProp) && canvasProp.ValueKind == JsonValueKind.Object
            ? canvasProp
            : default;
        var canvasWidth = LayoutJsonInt(canvas, "width") ?? 1024;
        var canvasHeight = LayoutJsonInt(canvas, "height") ?? 768;
        var windowIndex = LayoutJsonInt(root, "windowIndex") ?? 0;
        var result = new LayoutValidationResult
        {
            Layout = layoutPath,
            WindowIndex = windowIndex,
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight
        };

        result.Objects.AddRange(ReadDirectLayoutObjects(root, style, windowIndex));
        result.Objects.AddRange(ReadSectionLayoutObjects(root, style, windowIndex));
        var placementMode = placementModeOverride ?? ReadPlacementMode(root);
        if (placementMode.Equals("internal-occupancy", StringComparison.OrdinalIgnoreCase))
            ApplyInternalOccupancyPlacement(result, canvasObjectsPath, style);
        else if (!placementMode.Equals("explicit", StringComparison.OrdinalIgnoreCase) &&
                 !placementMode.Equals("layout", StringComparison.OrdinalIgnoreCase))
            result.BlockedReasons.Add("unsupported placement mode: " + placementMode);
        ValidateLayoutObjects(result, safetyPath);
        result.ReadabilityScore = ComputeReadabilityScore(result);
        result.Status = result.BlockedReasons.Count > 0 ? "FAIL" : result.UnknownReasons.Count > 0 ? "UNKNOWN" : "PASS";
        result.Verdict = result.Status == "PASS" ? "readable" : "blocked";
        result.Checks.Add(result.Status == "PASS"
            ? RequiredPass("layout-validate", "layout is geometrically and semantically valid")
            : result.Status == "UNKNOWN"
                ? RequiredUnknown("layout-validate", string.Join("; ", result.UnknownReasons))
                : RequiredFail("layout-validate", string.Join("; ", result.BlockedReasons)));
        return result;
    }

    private static string ReadPlacementMode(JsonElement root)
    {
        if (root.TryGetProperty("placement", out var placement) && placement.ValueKind == JsonValueKind.Object)
            return JsonString(placement, "mode") ?? "explicit";
        return "explicit";
    }

    private static void ApplyInternalOccupancyPlacement(LayoutValidationResult result, string? canvasObjectsPath, LayoutStyle style)
    {
        result.PlacementSource = "internal-occupancy";
        var plan = new LayoutPlacementPlan
        {
            Status = "UNKNOWN",
            PlacementSource = "internal-occupancy",
            CanvasObjects = canvasObjectsPath,
            Margin = Math.Max(4, style.Grid)
        };
        result.PlacementPlan = plan;

        if (string.IsNullOrWhiteSpace(canvasObjectsPath))
        {
            plan.UnknownReasons.Add("internal occupancy placement requires --canvas-objects <canvas-objects.json>");
            result.UnknownReasons.AddRange(plan.UnknownReasons);
            return;
        }
        if (!File.Exists(canvasObjectsPath))
        {
            plan.UnknownReasons.Add("canvas object map not found: " + canvasObjectsPath);
            result.UnknownReasons.AddRange(plan.UnknownReasons);
            return;
        }

        var map = LoadCanvasObjectMap(canvasObjectsPath);
        plan.ObjectProvider = map.ObjectProvider;
        plan.ReliableGeometry = map.ReliableGeometry;
        plan.OccupiedRectangles.AddRange(map.OccupiedRectangles);
        if (!map.ReliableGeometry || !map.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase))
        {
            var reason = "canvas object map is not reliable for internal occupancy placement";
            if (map.BlockedReasons.Count > 0) reason += ": " + string.Join("; ", map.BlockedReasons);
            plan.UnknownReasons.Add(reason);
            result.UnknownReasons.AddRange(plan.UnknownReasons);
            return;
        }

        var guiObjects = result.Objects.Where(o => o.GuiSupported).ToArray();
        if (guiObjects.Length == 0)
        {
            plan.Status = "NOT_REQUESTED";
            return;
        }

        var minX = guiObjects.Min(o => o.X);
        var minY = guiObjects.Min(o => o.Y);
        var maxX = guiObjects.Max(o => o.X + o.Width);
        var maxY = guiObjects.Max(o => o.Y + o.Height);
        var groupWidth = Math.Max(1, maxX - minX);
        var groupHeight = Math.Max(1, maxY - minY);
        var margin = plan.Margin;
        var inflated = map.OccupiedRectangles
            .Select(r => new CanvasOccupiedRect
            {
                Id = r.Id,
                Kind = r.Kind,
                Text = r.Text,
                X = Math.Max(0, r.X - margin),
                Y = Math.Max(0, r.Y - margin),
                Width = r.Width + margin * 2,
                Height = r.Height + margin * 2,
                Source = r.Source,
                Confidence = r.Confidence
            })
            .ToArray();
        var grid = Math.Max(1, style.Grid);
        for (var y = grid; y <= result.CanvasHeight - groupHeight - grid; y += grid)
        {
            for (var x = grid; x <= result.CanvasWidth - groupWidth - grid; x += grid)
            {
                var candidate = new CanvasOccupiedRect { X = x, Y = y, Width = groupWidth, Height = groupHeight };
                if (inflated.Any(r => RectsOverlap(candidate, r))) continue;
                var dx = x - minX;
                var dy = y - minY;
                foreach (var obj in guiObjects)
                {
                    obj.X += dx;
                    obj.Y += dy;
                    obj.PlacementSource = "internal-occupancy";
                }
                plan.Status = "PASS";
                plan.PlannedBounds = new { x, y, width = groupWidth, height = groupHeight };
                plan.Offset = new { x = dx, y = dy };
                return;
            }
        }

        plan.BlockedReasons.Add("no free rectangle large enough for layout after applying canvas occupancy");
        result.BlockedReasons.AddRange(plan.BlockedReasons);
    }

    private static bool RectsOverlap(CanvasOccupiedRect a, CanvasOccupiedRect b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X &&
           a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    private static CanvasObjectMap LoadCanvasObjectMap(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var root = doc.RootElement;
        var map = new CanvasObjectMap
        {
            Status = JsonStringAny(root, "Status", "status") ?? "UNKNOWN",
            ObjectProvider = JsonStringAny(root, "ObjectProvider", "objectProvider") ?? "none",
            ReliableGeometry = JsonBoolAny(root, "ReliableGeometry", "reliableGeometry") ?? false
        };
        foreach (var reason in JsonStringArrayAny(root, "BlockedReasons", "blockedReasons"))
            map.BlockedReasons.Add(reason);

        var rects = JsonArrayAny(root, "OccupiedRectangles", "occupiedRectangles");
        if (rects.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rects.EnumerateArray())
                if (TryReadCanvasOccupiedRect(item, out var rect)) map.OccupiedRectangles.Add(rect);
        }
        var objects = JsonArrayAny(root, "Objects", "objects");
        if (objects.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in objects.EnumerateArray())
            {
                if (item.TryGetProperty("Rect", out var rectEl) || item.TryGetProperty("rect", out rectEl))
                {
                    if (TryReadCanvasOccupiedRect(rectEl, out var rect))
                    {
                        rect.Id = JsonStringAny(item, "Id", "id") ?? rect.Id;
                        rect.Kind = JsonStringAny(item, "Kind", "kind") ?? rect.Kind;
                        rect.Text = JsonStringAny(item, "Text", "text") ?? rect.Text;
                        rect.Source = JsonStringAny(item, "Source", "source") ?? rect.Source;
                        rect.Confidence = JsonStringAny(item, "Confidence", "confidence") ?? rect.Confidence;
                        map.OccupiedRectangles.Add(rect);
                    }
                }
            }
        }
        return map;
    }

    private static bool TryReadCanvasOccupiedRect(JsonElement item, out CanvasOccupiedRect rect)
    {
        rect = new CanvasOccupiedRect
        {
            Id = JsonStringAny(item, "Id", "id") ?? "",
            Kind = JsonStringAny(item, "Kind", "kind") ?? "",
            Text = JsonStringAny(item, "Text", "text") ?? "",
            X = LayoutJsonIntAny(item, "X", "x") ?? 0,
            Y = LayoutJsonIntAny(item, "Y", "y") ?? 0,
            Width = LayoutJsonIntAny(item, "Width", "width") ?? 0,
            Height = LayoutJsonIntAny(item, "Height", "height") ?? 0,
            Source = JsonStringAny(item, "Source", "source") ?? "",
            Confidence = JsonStringAny(item, "Confidence", "confidence") ?? "unknown"
        };
        return rect.Width > 0 && rect.Height > 0;
    }

    private static LayoutStyle ReadLayoutStyle(JsonElement root)
    {
        var style = new LayoutStyle();
        if (root.TryGetProperty("canvas", out var canvas) && canvas.ValueKind == JsonValueKind.Object)
            style.Grid = LayoutJsonInt(canvas, "grid") ?? style.Grid;
        if (root.TryGetProperty("style", out var styleJson) && styleJson.ValueKind == JsonValueKind.Object)
        {
            style.ButtonWidth = LayoutJsonInt(styleJson, "buttonWidth") ?? style.ButtonWidth;
            style.ButtonHeight = LayoutJsonInt(styleJson, "buttonHeight") ?? style.ButtonHeight;
            style.StatusWidth = LayoutJsonInt(styleJson, "statusWidth") ?? style.StatusWidth;
            style.StatusHeight = LayoutJsonInt(styleJson, "statusHeight") ?? style.StatusHeight;
            style.TitleHeight = LayoutJsonInt(styleJson, "titleHeight") ?? style.TitleHeight;
            style.LabelHeight = LayoutJsonInt(styleJson, "labelHeight") ?? style.LabelHeight;
        }
        return style;
    }

    private static IEnumerable<LayoutObjectPlan> ReadDirectLayoutObjects(JsonElement root, LayoutStyle style, int defaultWindowIndex)
    {
        if (!root.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array) yield break;
        foreach (var obj in objects.EnumerateArray())
        {
            var kind = JsonString(obj, "kind") ?? "";
            yield return new LayoutObjectPlan
            {
                Id = JsonString(obj, "id") ?? "",
                Kind = kind,
                Text = JsonString(obj, "text") ?? "",
                Variable = JsonString(obj, "variable"),
                Expression = JsonString(obj, "expression"),
                RenderAs = JsonString(obj, "renderAs"),
                WindowIndex = LayoutJsonInt(obj, "windowIndex") ?? defaultWindowIndex,
                X = LayoutJsonInt(obj, "x") ?? 0,
                Y = LayoutJsonInt(obj, "y") ?? 0,
                Width = LayoutJsonInt(obj, "width") ?? DefaultLayoutWidth(kind, style),
                Height = LayoutJsonInt(obj, "height") ?? DefaultLayoutHeight(kind, style),
                Source = "objects"
            };
        }
    }

    private static IEnumerable<LayoutObjectPlan> ReadSectionLayoutObjects(JsonElement root, LayoutStyle style, int defaultWindowIndex)
    {
        if (!root.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Array) yield break;
        foreach (var section in sections.EnumerateArray())
        {
            var sectionId = JsonString(section, "id") ?? "section";
            var x = LayoutJsonInt(section, "x") ?? 0;
            var y = LayoutJsonInt(section, "y") ?? 0;
            var width = LayoutJsonInt(section, "width") ?? 300;
            var height = LayoutJsonInt(section, "height") ?? 220;
            var windowIndex = LayoutJsonInt(section, "windowIndex") ?? defaultWindowIndex;
            var title = JsonString(section, "title");
            if (!string.IsNullOrWhiteSpace(title))
            {
                yield return new LayoutObjectPlan
                {
                    Id = sectionId + "-title",
                    Kind = "section-title",
                    Text = title,
                    RenderAs = JsonString(section, "titleRenderAs") ?? JsonString(section, "renderAs"),
                    WindowIndex = windowIndex,
                    X = x + style.Grid,
                    Y = y + style.Grid,
                    Width = Math.Max(1, width - style.Grid * 2),
                    Height = style.TitleHeight,
                    Source = "sections/" + sectionId
                };
            }

            var controls = section.TryGetProperty("controls", out var controlsJson) && controlsJson.ValueKind == JsonValueKind.Array
                ? controlsJson.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            for (var i = 0; i < controls.Length; i++)
            {
                var control = controls[i];
                var kind = JsonString(control, "kind") ?? "momentary-button";
                var rect = RectForSectionItem(section, control, i, controls.Length, kind, style, isIndicator: false);
                yield return new LayoutObjectPlan
                {
                    Id = JsonString(control, "id") ?? $"{sectionId}-control-{i + 1}",
                    Kind = kind,
                    Text = JsonString(control, "text") ?? "",
                    Variable = JsonString(control, "variable"),
                    Expression = JsonString(control, "expression"),
                    RenderAs = JsonString(control, "renderAs"),
                    WindowIndex = windowIndex,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    Source = "sections/" + sectionId + "/controls"
                };
            }

            var indicators = section.TryGetProperty("indicators", out var indicatorsJson) && indicatorsJson.ValueKind == JsonValueKind.Array
                ? indicatorsJson.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            for (var i = 0; i < indicators.Length; i++)
            {
                var indicator = indicators[i];
                var rect = RectForSectionIndicator(section, indicator, i, controls.Length, style);
                yield return new LayoutObjectPlan
                {
                    Id = JsonString(indicator, "id") ?? $"{sectionId}-indicator-{i + 1}",
                    Kind = JsonString(indicator, "kind") ?? "status-button",
                    Text = JsonString(indicator, "text") ?? "",
                    Variable = JsonString(indicator, "variable"),
                    Expression = JsonString(indicator, "expression"),
                    RenderAs = JsonString(indicator, "renderAs"),
                    WindowIndex = windowIndex,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    Source = "sections/" + sectionId + "/indicators"
                };
            }
        }
    }

    private static LayoutObjectPlan RectForSectionItem(
        JsonElement section,
        JsonElement item,
        int index,
        int count,
        string kind,
        LayoutStyle style,
        bool isIndicator)
    {
        if (LayoutJsonInt(item, "x").HasValue && LayoutJsonInt(item, "y").HasValue)
        {
            return new LayoutObjectPlan
            {
                X = LayoutJsonInt(item, "x")!.Value,
                Y = LayoutJsonInt(item, "y")!.Value,
                Width = LayoutJsonInt(item, "width") ?? DefaultLayoutWidth(kind, style),
                Height = LayoutJsonInt(item, "height") ?? DefaultLayoutHeight(kind, style)
            };
        }

        var x = LayoutJsonInt(section, "x") ?? 0;
        var y = LayoutJsonInt(section, "y") ?? 0;
        var width = LayoutJsonInt(section, "width") ?? 300;
        var height = LayoutJsonInt(section, "height") ?? 220;
        var layout = JsonString(section, "layout") ?? "row";
        var itemWidth = isIndicator ? style.StatusWidth : DefaultLayoutWidth(kind, style);
        var itemHeight = isIndicator ? style.StatusHeight : DefaultLayoutHeight(kind, style);
        if (isIndicator)
        {
            return new LayoutObjectPlan
            {
                X = x + style.Grid + index * (itemWidth + style.Grid),
                Y = y + Math.Max(style.TitleHeight + style.Grid * 2, height - itemHeight - style.Grid),
                Width = itemWidth,
                Height = itemHeight
            };
        }

        if (layout.Equals("direction-pad", StringComparison.OrdinalIgnoreCase))
        {
            var position = (JsonString(item, "position") ?? "").ToLowerInvariant();
            var centerX = x + width / 2 - itemWidth / 2;
            var rowY = y + style.TitleHeight + style.Grid * 2 + itemHeight + style.Grid;
            return position switch
            {
                "up" => new LayoutObjectPlan { X = centerX, Y = y + style.TitleHeight + style.Grid * 2, Width = itemWidth, Height = itemHeight },
                "down" => new LayoutObjectPlan { X = centerX, Y = rowY + itemHeight + style.Grid, Width = itemWidth, Height = itemHeight },
                "left" => new LayoutObjectPlan { X = centerX - itemWidth - style.Grid, Y = rowY, Width = itemWidth, Height = itemHeight },
                "right" => new LayoutObjectPlan { X = centerX + itemWidth + style.Grid, Y = rowY, Width = itemWidth, Height = itemHeight },
                _ => new LayoutObjectPlan { X = centerX, Y = rowY, Width = itemWidth, Height = itemHeight }
            };
        }

        var maxPerRow = Math.Max(1, (width - style.Grid) / Math.Max(1, itemWidth + style.Grid));
        var col = index % maxPerRow;
        var row = index / maxPerRow;
        return new LayoutObjectPlan
        {
            X = x + style.Grid + col * (itemWidth + style.Grid),
            Y = y + style.TitleHeight + style.Grid * 2 + row * (itemHeight + style.Grid),
            Width = itemWidth,
            Height = itemHeight
        };
    }

    private static LayoutObjectPlan RectForSectionIndicator(
        JsonElement section,
        JsonElement item,
        int index,
        int controlCount,
        LayoutStyle style)
    {
        if (LayoutJsonInt(item, "x").HasValue && LayoutJsonInt(item, "y").HasValue)
        {
            var explicitKind = JsonString(item, "kind") ?? "status-button";
            return new LayoutObjectPlan
            {
                X = LayoutJsonInt(item, "x")!.Value,
                Y = LayoutJsonInt(item, "y")!.Value,
                Width = LayoutJsonInt(item, "width") ?? DefaultLayoutWidth(explicitKind, style),
                Height = LayoutJsonInt(item, "height") ?? DefaultLayoutHeight(explicitKind, style)
            };
        }

        var x = LayoutJsonInt(section, "x") ?? 0;
        var y = LayoutJsonInt(section, "y") ?? 0;
        var width = LayoutJsonInt(section, "width") ?? 300;
        var height = LayoutJsonInt(section, "height") ?? 220;
        var layout = JsonString(section, "layout") ?? "row";
        var kind = JsonString(item, "kind") ?? "status-button";
        var itemWidth = DefaultLayoutWidth(kind, style);
        var itemHeight = DefaultLayoutHeight(kind, style);
        var safeTop = y + style.TitleHeight + style.Grid * 2;

        if (layout.Equals("direction-pad", StringComparison.OrdinalIgnoreCase))
        {
            return new LayoutObjectPlan
            {
                X = x + Math.Max(style.Grid, width - itemWidth - style.Grid),
                Y = safeTop + index * (itemHeight + style.Grid),
                Width = itemWidth,
                Height = itemHeight
            };
        }

        var maxPerRow = Math.Max(1, (width - style.Grid) / Math.Max(1, style.ButtonWidth + style.Grid));
        var controlRows = Math.Max(1, (int)Math.Ceiling(Math.Max(1, controlCount) / (double)maxPerRow));
        var candidateY = safeTop + controlRows * (style.ButtonHeight + style.Grid);
        var bottomY = y + Math.Max(style.TitleHeight + style.Grid * 2, height - itemHeight - style.Grid);
        var indicatorY = Math.Min(candidateY, bottomY);
        var indicatorMaxPerRow = Math.Max(1, (width - style.Grid) / Math.Max(1, itemWidth + style.Grid));
        var col = index % indicatorMaxPerRow;
        var row = index / indicatorMaxPerRow;
        return new LayoutObjectPlan
        {
            X = x + style.Grid + col * (itemWidth + style.Grid),
            Y = indicatorY + row * (itemHeight + style.Grid),
            Width = itemWidth,
            Height = itemHeight
        };
    }

    private static int DefaultLayoutWidth(string kind, LayoutStyle style)
        => kind switch
        {
            "status-button" => style.StatusWidth,
            "native-lamp" => Math.Max(style.StatusWidth, 120),
            "native-static-text" => 160,
            "section-title" or "static-label" => 160,
            _ => style.ButtonWidth
        };

    private static int DefaultLayoutHeight(string kind, LayoutStyle style)
        => kind switch
        {
            "status-button" => style.StatusHeight,
            "native-lamp" => Math.Max(style.StatusHeight, 75),
            "native-static-text" => style.LabelHeight,
            "section-title" => style.TitleHeight,
            "static-label" => style.LabelHeight,
            _ => style.ButtonHeight
        };

    private static void ValidateLayoutObjects(LayoutValidationResult result, string? safetyPath)
    {
        if (result.Objects.Count == 0)
            result.BlockedReasons.Add("layout contains no objects");

        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "section-title", "static-label", "momentary-button", "status-button", "native-static-text", "native-lamp"
        };
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in result.Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.Id)) result.BlockedReasons.Add("object missing id");
            else if (!ids.Add(obj.Id)) result.BlockedReasons.Add("duplicate object id: " + obj.Id);
            if (!supported.Contains(obj.Kind)) result.BlockedReasons.Add($"{obj.Id}: unsupported kind {obj.Kind}");
            if (!string.IsNullOrWhiteSpace(obj.RenderAs) &&
                !obj.RenderAs.Equals("status-button", StringComparison.OrdinalIgnoreCase) &&
                !obj.RenderAs.Equals("native-static-text", StringComparison.OrdinalIgnoreCase) &&
                !obj.RenderAs.Equals("preview-only", StringComparison.OrdinalIgnoreCase))
                result.BlockedReasons.Add($"{obj.Id}: unsupported renderAs {obj.RenderAs}");
            if (!obj.IsSyntheticText && !string.IsNullOrWhiteSpace(obj.RenderAs))
                result.BlockedReasons.Add($"{obj.Id}: renderAs is only supported for section-title/static-label");
            if (string.IsNullOrWhiteSpace(obj.Text)) result.BlockedReasons.Add($"{obj.Id}: text is required");
            if (obj.Width <= 0 || obj.Height <= 0) result.BlockedReasons.Add($"{obj.Id}: width/height must be positive");
            if (obj.X < 0 || obj.Y < 0 || obj.X + obj.Width > result.CanvasWidth || obj.Y + obj.Height > result.CanvasHeight)
                result.BlockedReasons.Add($"{obj.Id}: rect is outside canvas");
            if (obj.Kind == "momentary-button")
            {
                if (string.IsNullOrWhiteSpace(obj.Variable)) result.BlockedReasons.Add($"{obj.Id}: variable is required");
                else if (!LooksLikeDataObjectName(obj.Variable)) result.BlockedReasons.Add($"{obj.Id}: variable name is malformed");
            }
            if (obj.Kind is "status-button" or "native-lamp")
            {
                if (string.IsNullOrWhiteSpace(obj.Expression)) result.BlockedReasons.Add($"{obj.Id}: expression is required");
            }
            if (obj.IsSyntheticText && obj.GuiKind == "status-button" && string.IsNullOrWhiteSpace(obj.EffectiveExpression))
                result.BlockedReasons.Add($"{obj.Id}: synthetic text expression is required");
        }

        var interactive = result.Objects.Where(o => o.GuiSupported).ToArray();
        for (var i = 0; i < interactive.Length; i++)
        {
            for (var j = i + 1; j < interactive.Length; j++)
            {
                if (RectsOverlap(interactive[i], interactive[j]))
                    result.BlockedReasons.Add($"objects overlap: {interactive[i].Id} and {interactive[j].Id}");
            }
        }

        if (safetyPath != null)
            ValidateLayoutAgainstSafety(result, safetyPath);

        if (result.Objects.Any(o => o.IsSyntheticText && o.GuiKind == "status-button"))
            result.Warnings.Add("section-title/static-label are rendered as verified status-button labels with constant visibility, not native static text");
        if (result.Objects.Any(o => o.GuiKind == "native-lamp"))
            result.Warnings.Add("native-lamp uses the MCGS animation display component and still requires human/site acceptance");
        if (result.Objects.Any(o => !o.GuiSupported))
            result.Warnings.Add("some layout objects are preview evidence only in current GUI apply implementation");
    }

    private static bool LooksLikeDataObjectName(string name)
        => !string.IsNullOrWhiteSpace(name) && !name.Any(char.IsWhiteSpace);

    private static bool RectsOverlap(LayoutObjectPlan a, LayoutObjectPlan b)
        => a.X < b.X + b.Width && a.X + a.Width > b.X &&
           a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    private static void ValidateLayoutAgainstSafety(LayoutValidationResult result, string safetyPath)
    {
        if (!File.Exists(safetyPath))
        {
            result.BlockedReasons.Add("safety spec not found: " + safetyPath);
            return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(safetyPath, Encoding.UTF8));
        var root = doc.RootElement;
        var addressPlan = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("plc", out var plc) &&
            plc.ValueKind == JsonValueKind.Object &&
            plc.TryGetProperty("addressPlan", out var plan) &&
            plan.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in plan.EnumerateArray())
            {
                var dataObject = JsonString(item, "dataObject");
                var address = JsonString(item, "address");
                if (!string.IsNullOrWhiteSpace(dataObject) && !string.IsNullOrWhiteSpace(address))
                    addressPlan[dataObject] = address;
            }
        }
        var dangerous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("dangerousOutputs", out var outputs) && outputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var output in outputs.EnumerateArray())
            {
                var value = output.GetString();
                if (!string.IsNullOrWhiteSpace(value)) dangerous.Add(value);
            }
        }

        foreach (var button in result.Objects.Where(o => o.Kind == "momentary-button"))
        {
            var variable = button.Variable ?? "";
            if (!addressPlan.TryGetValue(variable, out var address))
            {
                result.BlockedReasons.Add($"{button.Id}: variable {variable} is missing from safety addressPlan");
                continue;
            }
            if (dangerous.Contains(address) || address.StartsWith("Q", StringComparison.OrdinalIgnoreCase))
                result.BlockedReasons.Add($"{button.Id}: variable {variable} maps directly to dangerous output {address}");
        }
    }

    private static int ComputeReadabilityScore(LayoutValidationResult result)
    {
        var score = 100;
        score -= result.BlockedReasons.Count * 25;
        score -= result.Warnings.Count * 5;
        if (!result.Objects.Any(o => o.Kind == "section-title")) score -= 10;
        if (!result.Objects.Any(o => o.Kind == "status-button")) score -= 10;
        return Math.Max(0, Math.Min(100, score));
    }

    private static int? LayoutJsonInt(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null) return null;
        if (!element.TryGetProperty(name, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)) return value;
        return int.TryParse(JsonString(element, name), out var parsed) ? parsed : null;
    }

    private static int? LayoutJsonIntAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = LayoutJsonInt(element, name);
            if (value.HasValue) return value;
        }
        return null;
    }

    private static string? JsonStringAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var value = JsonString(element, name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static bool? JsonBoolAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
            if (bool.TryParse(JsonString(element, name), out var parsed)) return parsed;
        }
        return null;
    }

    private static JsonElement JsonArrayAny(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array) return prop;
        }
        return default;
    }

    private static IEnumerable<string> JsonStringArrayAny(JsonElement element, params string[] names)
    {
        var array = JsonArrayAny(element, names);
        if (array.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in array.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (!string.IsNullOrWhiteSpace(value)) yield return value!;
        }
    }

    private static void WriteLayoutPreviewFiles(LayoutValidationResult result, string outDir)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "validate.json"),
            JsonSerializer.Serialize(result, ResultJsonOptions()), Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDir, "preview.json"),
            JsonSerializer.Serialize(new
            {
                result.Status,
                result.Verdict,
                result.ReadabilityScore,
                result.CanvasWidth,
                result.CanvasHeight,
                result.Objects,
                result.PlacementSource,
                result.PlacementPlan,
                result.BlockedReasons,
                result.UnknownReasons,
                result.Warnings
            }, ResultJsonOptions()), Encoding.UTF8);
        if (result.PlacementPlan != null)
        {
            File.WriteAllText(Path.Combine(outDir, "layout-plan.json"),
                JsonSerializer.Serialize(result.PlacementPlan, ResultJsonOptions()), Encoding.UTF8);
        }
        var svg = BuildLayoutSvg(result);
        File.WriteAllText(Path.Combine(outDir, "preview.svg"), svg, Encoding.UTF8);
        if (result.PlacementPlan != null)
            File.WriteAllText(Path.Combine(outDir, "preview-overlay.svg"), svg, Encoding.UTF8);
        File.WriteAllText(Path.Combine(outDir, "preview.html"),
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>mcgsctl layout preview</title></head><body>" +
            svg +
            "<pre>" + EscapeHtml(JsonSerializer.Serialize(new { result.Status, result.BlockedReasons, result.UnknownReasons, result.Warnings, result.PlacementPlan }, ResultJsonOptions())) + "</pre>" +
            "</body></html>", Encoding.UTF8);
    }

    private static string BuildLayoutSvg(LayoutValidationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{result.CanvasWidth}" height="{result.CanvasHeight}" viewBox="0 0 {result.CanvasWidth} {result.CanvasHeight}">""");
        sb.AppendLine("""<rect x="0" y="0" width="100%" height="100%" fill="#eeeeee"/>""");
        if (result.PlacementPlan != null)
        {
            foreach (var occupied in result.PlacementPlan.OccupiedRectangles)
            {
                sb.AppendLine($"""<rect x="{occupied.X}" y="{occupied.Y}" width="{occupied.Width}" height="{occupied.Height}" fill="#c23b22" fill-opacity="0.18" stroke="#8a1f11" stroke-width="1" stroke-dasharray="4 3"/>""");
                if (!string.IsNullOrWhiteSpace(occupied.Id))
                    sb.AppendLine($"""<text x="{occupied.X + 4}" y="{occupied.Y + 14}" font-family="Arial" font-size="11" fill="#8a1f11">{EscapeXml(occupied.Id)}</text>""");
            }
        }
        foreach (var obj in result.Objects)
        {
            var fill = obj.Kind switch
            {
                "momentary-button" => "#f7f7f7",
                "status-button" => "#ffffff",
                "native-lamp" => "#eef7ee",
                "native-static-text" => "#eeeeee",
                "section-title" => "#dddddd",
                "static-label" => "#eeeeee",
                _ => "#ffdddd"
            };
            var stroke = obj.GuiSupported ? "#333333" : "#777777";
            sb.AppendLine($"""<rect x="{obj.X}" y="{obj.Y}" width="{obj.Width}" height="{obj.Height}" rx="2" ry="2" fill="{fill}" stroke="{stroke}" stroke-width="1"/>""");
            sb.AppendLine($"""<text x="{obj.X + 6}" y="{obj.Y + Math.Max(16, obj.Height / 2 + 5)}" font-family="SimSun, Arial" font-size="14" fill="#111111">{EscapeXml(obj.Text)}</text>""");
            var binding = obj.Variable ?? (!string.IsNullOrWhiteSpace(obj.EffectiveExpression) ? obj.EffectiveExpression : obj.Expression);
            if (!string.IsNullOrWhiteSpace(binding))
                sb.AppendLine($"""<title>{EscapeXml(obj.Id + " " + obj.Kind + " gui=" + obj.GuiKind + " " + binding + " placement=" + obj.PlacementSource)}</title>""");
        }
        if (result.BlockedReasons.Count > 0 || result.UnknownReasons.Count > 0)
        {
            var label = result.Status == "UNKNOWN" ? "UNKNOWN" : "BLOCKED";
            sb.AppendLine($"""<text x="10" y="22" font-family="Arial" font-size="16" fill="#b00020">{label}</text>""");
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string EscapeXml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string EscapeHtml(string value) => EscapeXml(value);
}
