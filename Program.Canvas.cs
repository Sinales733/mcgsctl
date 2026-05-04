using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

internal static partial class Program
{
    private sealed class CanvasObjectMap
    {
        public int SchemaVersion { get; set; } = 1;
        public string Status { get; set; } = "UNKNOWN";
        public string ObjectProvider { get; set; } = "none";
        public bool ReliableGeometry { get; set; }
        public string CreatedAt { get; set; } = DateTimeOffset.Now.ToString("O");
        public string? Project { get; set; }
        public string? ProjectSha256 { get; set; }
        public int WindowIndex { get; set; }
        public object? CanvasWindow { get; set; }
        public List<string> ChannelsTried { get; } = new();
        public List<string> BlockedReasons { get; } = new();
        public List<CanvasOccupiedRect> OccupiedRectangles { get; } = new();
        public List<CanvasDetectedObject> Objects { get; } = new();
        public object? UiaProbe { get; set; }
        public object? MsaaProbe { get; set; }
        public object? WmGetObjectProbe { get; set; }
        public object? NativeObjectProbe { get; set; }
        public object? ClipboardProbe { get; set; }
        public object? MceGeometryProbe { get; set; }
    }

    private sealed class CanvasDetectedObject
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string Variable { get; set; } = "";
        public string Expression { get; set; } = "";
        public CanvasOccupiedRect Rect { get; set; } = new();
        public string Source { get; set; } = "";
        public string Confidence { get; set; } = "unknown";
    }

    private sealed class CanvasOccupiedRect
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string Variable { get; set; } = "";
        public string Expression { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Source { get; set; } = "";
        public string Confidence { get; set; } = "unknown";
    }

    private sealed class KnownCanvasObject
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string Variable { get; set; } = "";
        public string Expression { get; set; } = "";
        public CanvasOccupiedRect Rect { get; set; } = new();
        public string Workflow { get; set; } = "";
        public string ResultPath { get; set; } = "";
        public string Readback { get; set; } = "";
    }

    private sealed class MceObjectMapProbeResult
    {
        public int SchemaVersion { get; set; } = 1;
        public string Status { get; set; } = "UNKNOWN";
        public string EvidenceStatus { get; set; } = "UNKNOWN";
        public string CreatedAt { get; set; } = DateTimeOffset.Now.ToString("O");
        public string? Project { get; set; }
        public string? ProjectSha256 { get; set; }
        public string? WorkflowResults { get; set; }
        public string GeometryPath { get; set; } = "mce-export/blob_geometry.json";
        public List<string> BlockedReasons { get; } = new();
        public List<object> KnownObjects { get; } = new();
        public List<object> BlobEntries { get; } = new();
        public List<object> GenericOccupancy { get; } = new();
        public List<object> Matches { get; } = new();
        public List<object> RejectedHypotheses { get; } = new();
    }

    private sealed class MceObjectMapMatchResult
    {
        public bool Reliable { get; set; }
        public int Score { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public object Evidence { get; set; } = new();
    }

    private sealed class CanvasClipboardProbeResult
    {
        public int SchemaVersion { get; set; } = 1;
        public string Status { get; set; } = "UNKNOWN";
        public string CreatedAt { get; set; } = DateTimeOffset.Now.ToString("O");
        public string? Project { get; set; }
        public int WindowIndex { get; set; }
        public object? CanvasWindow { get; set; }
        public List<string> BlockedReasons { get; } = new();
        public string[] FormatsBefore { get; set; } = Array.Empty<string>();
        public List<object> FormatsAfter { get; } = new();
        public List<object> GeometryAnalyses { get; } = new();
        public CanvasObjectMap? CanvasObjects { get; set; }
        public bool ClipboardRestored { get; set; }
    }

    private sealed class ClipboardFormatDescription
    {
        public object Summary { get; set; } = new();
        public ClipboardGeometryAnalysis? GeometryAnalysis { get; set; }
    }

    private sealed class ClipboardGeometryAnalysis
    {
        public string Format { get; set; } = "";
        public int FormatId { get; set; }
        public int Length { get; set; }
        public string Sha256 { get; set; } = "";
        public string Confidence { get; set; } = "low";
        public bool ReliableGeometry { get; set; }
        public List<string> BlockedReasons { get; } = new();
        public List<object> StringSamples { get; } = new();
        public List<object> ClassRecords { get; } = new();
        public List<object> CandidateRectangles { get; } = new();
        public List<CanvasOccupiedRect> SelectedRectangles { get; } = new();
    }

    private sealed class ClipboardStringOccurrence
    {
        public int Offset { get; set; }
        public string Encoding { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private sealed class ClipboardRectCandidate
    {
        public int Offset { get; set; }
        public string OffsetHex => FormatHex(Offset);
        public string Encoding { get; set; } = "";
        public string Pattern { get; set; } = "";
        public int[] Raw { get; set; } = Array.Empty<int>();
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Confidence { get; set; } = "low";
    }

    private static int Canvas(string[] args)
    {
        if (args.Length < 2)
            return Fail("Usage: mcgsctl canvas inspect|context-menu-probe|clipboard-probe|toolbar-probe|mce-geometry-probe|mce-object-map-probe --project <candidate.mce> --out <dir> OR canvas mce-blob-diff-probe --before <a.mce> --after <b.mce> --out <dir>");

        return args[1].ToLowerInvariant() switch
        {
            "inspect" => CanvasInspect(args),
            "context-menu-probe" => CanvasContextMenuProbe(args),
            "clipboard-probe" => CanvasClipboardProbe(args),
            "toolbar-probe" => CanvasToolbarProbe(args),
            "mce-geometry-probe" => CanvasMceGeometryProbe(args),
            "mce-object-map-probe" => CanvasMceObjectMapProbe(args),
            "mce-blob-diff-probe" => CanvasMceBlobDiffProbe(args),
            _ => Fail("Unknown canvas command: " + args[1])
        };
    }

    private static int CanvasInspect(string[] args)
    {
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        Process? process = null;
        var main = IntPtr.Zero;
        try
        {
            using var session = OpenCanvasProbeSession(args, outDir, "inspect", out process, out main);
            var map = BuildCanvasObjectMap(session.ProjectCopy, session.ProjectSha256, session.WindowIndex, session.Main, session.Canvas);
            File.WriteAllText(Path.Combine(outDir, "canvas-inspect.json"),
                JsonSerializer.Serialize(map, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "canvas-objects.json"),
                JsonSerializer.Serialize(map, JsonOptions()), Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outDir, "window-tree.txt"), UiAutomation.WindowTreeLines(session.Main), Encoding.UTF8);
            TryScreenshot(session.Main, Path.Combine(outDir, "main-window.png"));
            Console.WriteLine("canvas inspect: " + outDir);
            return map.Status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas inspect failed: " + ex.Message);
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

    private static int CanvasContextMenuProbe(string[] args)
    {
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        Process? process = null;
        var main = IntPtr.Zero;
        try
        {
            using var session = OpenCanvasProbeSession(args, outDir, "context-menu-probe", out process, out main);
            var canvasRect = UiAutomation.GetWindowRect(session.Canvas);
            var x = ParseInt(args, "--x", Math.Max(10, Math.Min(canvasRect.Width - 10, canvasRect.Width / 2)));
            var y = ParseInt(args, "--y", Math.Max(10, Math.Min(canvasRect.Height - 10, canvasRect.Height / 2)));
            UiAutomation.OpenContextMenu(session.Canvas, x, y);
            Thread.Sleep(500);
            var menus = UiAutomation.GetPopupMenus(process!.Id).ToArray();
            SendKeys.SendWait("{ESC}");
            File.WriteAllText(Path.Combine(outDir, "context-menu.json"),
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    status = menus.Length > 0 ? "PASS" : "UNKNOWN",
                    project = session.ProjectCopy,
                    session.WindowIndex,
                    canvasWindow = WindowInfo.FromHandle(session.Canvas),
                    point = new { x, y },
                    menus,
                    blockedReasons = menus.Length > 0 ? Array.Empty<string>() : new[] { "no canvas context menu was observed" }
                }, JsonOptions()), Encoding.UTF8);
            Console.WriteLine("canvas context-menu-probe: " + outDir);
            return menus.Length > 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas context-menu-probe failed: " + ex.Message);
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

    private static int CanvasClipboardProbe(string[] args)
    {
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        Process? process = null;
        var main = IntPtr.Zero;
        IDataObject? before = null;
        try
        {
            using var session = OpenCanvasProbeSession(args, outDir, "clipboard-probe", out process, out main);
            var result = new CanvasClipboardProbeResult
            {
                Project = session.ProjectCopy,
                WindowIndex = session.WindowIndex,
                CanvasWindow = WindowInfo.FromHandle(session.Canvas)
            };

            before = Clipboard.GetDataObject();
            result.FormatsBefore = before?.GetFormats(false) ?? Array.Empty<string>();
            UiAutomation.ActivateForInput(session.Canvas);
            var canvasRect = UiAutomation.GetWindowRect(session.Canvas);
            var clipboardMap = NewCanvasObjectMap(session.ProjectCopy, session.ProjectSha256, session.WindowIndex, session.Canvas);
            clipboardMap.ChannelsTried.Add("clipboard");
            UiAutomation.ClickPoint(session.Canvas, Math.Max(5, canvasRect.Width / 2), Math.Max(5, canvasRect.Height / 2),
                MouseButton.Left, doubleClick: false, mouse: true);
            Thread.Sleep(150);
            if (Has(args, "--select-all")) SendKeys.SendWait("^a");
            Thread.Sleep(200);
            SendKeys.SendWait("^c");
            Thread.Sleep(600);

            var after = Clipboard.GetDataObject();
            if (after != null)
            {
                foreach (var format in after.GetFormats(false))
                {
                    var described = DescribeClipboardFormat(after, format, canvasRect);
                    result.FormatsAfter.Add(described.Summary);
                    if (described.GeometryAnalysis != null)
                    {
                        result.GeometryAnalyses.Add(described.GeometryAnalysis);
                        AddClipboardGeometryToMap(clipboardMap, described.GeometryAnalysis);
                    }
                }
            }
            result.Status = result.FormatsAfter.Count > 0 ? "PASS" : "UNKNOWN";
            if (result.FormatsAfter.Count == 0)
                result.BlockedReasons.Add("clipboard did not expose any formats after canvas copy");
            clipboardMap.ClipboardProbe = result.GeometryAnalyses;

            FinalizeCanvasMap(clipboardMap, fallbackProvider: "clipboard");
            if (!clipboardMap.ReliableGeometry)
                clipboardMap.BlockedReasons.Add("clipboard formats were observed but no high-confidence object geometry was decoded");
            result.CanvasObjects = clipboardMap;

            try
            {
                if (before != null) Clipboard.SetDataObject(before, copy: true);
                result.ClipboardRestored = true;
            }
            catch
            {
                result.ClipboardRestored = false;
            }

            File.WriteAllText(Path.Combine(outDir, "clipboard-probe.json"),
                JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "clipboard-geometry.json"),
                JsonSerializer.Serialize(result.GeometryAnalyses, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "canvas-objects.json"),
                JsonSerializer.Serialize(clipboardMap, JsonOptions()), Encoding.UTF8);
            Console.WriteLine("canvas clipboard-probe: " + outDir);
            return result.Status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas clipboard-probe failed: " + ex.Message);
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

    private static int CanvasToolbarProbe(string[] args)
    {
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        Process? process = null;
        var main = IntPtr.Zero;
        try
        {
            using var session = OpenCanvasProbeSession(args, outDir, "toolbar-probe", out process, out main);
            var toolbars = UiAutomation.EnumerateChildren(session.Main)
                .Where(h => Native.GetClass(h).Contains("ToolbarWindow32", StringComparison.OrdinalIgnoreCase))
                .Select(h =>
                {
                    object buttons;
                    try
                    {
                        buttons = UiAutomation.ToolbarButtons(h);
                    }
                    catch (Exception ex)
                    {
                        buttons = new { error = ex.Message };
                    }
                    return new
                    {
                        window = WindowInfo.FromHandle(h),
                        buttons
                    };
                })
                .ToArray();
            var result = new
            {
                schemaVersion = 1,
                status = toolbars.Length > 0 ? "PASS" : "UNKNOWN",
                createdAt = DateTimeOffset.Now.ToString("O"),
                project = session.ProjectCopy,
                windowIndex = session.WindowIndex,
                canvasWindow = WindowInfo.FromHandle(session.Canvas),
                toolbarCount = toolbars.Length,
                toolbars,
                blockedReasons = toolbars.Length > 0
                    ? Array.Empty<string>()
                    : new[] { "no ToolbarWindow32 descendants were found after entering animation configuration" }
            };
            File.WriteAllText(Path.Combine(outDir, "toolbar-probe.json"),
                JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
            File.WriteAllLines(Path.Combine(outDir, "window-tree.txt"), UiAutomation.WindowTreeLines(session.Main), Encoding.UTF8);
            TryScreenshot(session.Main, Path.Combine(outDir, "main-window.png"));
            Console.WriteLine("canvas toolbar-probe: " + outDir);
            return toolbars.Length > 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas toolbar-probe failed: " + ex.Message);
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

    private static int CanvasMceGeometryProbe(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        try
        {
            var exportDir = Path.Combine(outDir, "mce-export");
            MceExporter.Export(project, exportDir);
            var geometryPath = Path.Combine(exportDir, "blob_geometry.json");
            object mceGeometryProbe;
            if (File.Exists(geometryPath))
            {
                var entryCount = 0;
                var classOccurrenceCount = 0;
                var candidateRectangleCount = 0;
                using (var doc = JsonDocument.Parse(File.ReadAllText(geometryPath, Encoding.UTF8)))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in doc.RootElement.EnumerateArray())
                        {
                            entryCount++;
                            if (entry.TryGetProperty("classOccurrences", out var classes) &&
                                classes.ValueKind == JsonValueKind.Array)
                            {
                                classOccurrenceCount += classes.GetArrayLength();
                            }
                            if (entry.TryGetProperty("candidateRectangles", out var rectangles) &&
                                rectangles.ValueKind == JsonValueKind.Array)
                            {
                                candidateRectangleCount += rectangles.GetArrayLength();
                            }
                        }
                    }
                }
                mceGeometryProbe = new
                {
                    schemaVersion = 1,
                    status = "UNKNOWN",
                    evidenceStatus = "PASS",
                    path = "mce-export/blob_geometry.json",
                    entryCount,
                    classOccurrenceCount,
                    candidateRectangleCount,
                    blockedReasons = new[]
                    {
                        "read-only MCE geometry inference produced candidate evidence but no trusted object decoder exists yet"
                    }
                };
            }
            else
            {
                mceGeometryProbe = new
                {
                    schemaVersion = 1,
                    status = "UNKNOWN",
                    evidenceStatus = "UNKNOWN",
                    error = "blob_geometry.json was not produced by MCE export"
                };
            }
            var map = new CanvasObjectMap
            {
                Project = Path.GetFullPath(project),
                ProjectSha256 = Sha256(project),
                WindowIndex = OptInt(args, "--window-index") ?? 0,
                MceGeometryProbe = mceGeometryProbe
            };
            map.ChannelsTried.Add("mce-geometry");
            FinalizeCanvasMap(map, fallbackProvider: "mce-geometry");
            map.BlockedReasons.Add("read-only MCE geometry inference produced evidence but no high-confidence canvas object map");
            File.WriteAllText(Path.Combine(outDir, "mce-geometry-probe.json"),
                JsonSerializer.Serialize(map.MceGeometryProbe, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "canvas-objects.json"),
                JsonSerializer.Serialize(map, JsonOptions()), Encoding.UTF8);
            Console.WriteLine("canvas mce-geometry-probe: " + outDir);
            return 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas mce-geometry-probe failed: " + ex.Message);
            return 1;
        }
    }

    private static int CanvasMceObjectMapProbe(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        var workflowResultsDir = Opt(args, "--workflow-results") ?? InferWorkflowResultsDir(project);
        var windowIndex = OptInt(args, "--window-index") ?? 0;
        try
        {
            var exportDir = Path.Combine(outDir, "mce-export");
            MceExporter.Export(project, exportDir);
            var geometryPath = Path.Combine(exportDir, "blob_geometry.json");
            var projectSha = Sha256(project);
            var canvasWidth = OptInt(args, "--canvas-width") ?? 1024;
            var canvasHeight = OptInt(args, "--canvas-height") ?? 768;
            var rowKeyFilter = Opt(args, "--row-key");
            var map = new CanvasObjectMap
            {
                Project = Path.GetFullPath(project),
                ProjectSha256 = projectSha,
                WindowIndex = windowIndex
            };
            map.ChannelsTried.Add("mce-object-map");

            var probe = new MceObjectMapProbeResult
            {
                Project = Path.GetFullPath(project),
                ProjectSha256 = projectSha,
                WorkflowResults = workflowResultsDir == null ? null : Path.GetFullPath(workflowResultsDir)
            };

            var knownObjects = workflowResultsDir == null
                ? new List<KnownCanvasObject>()
                : LoadKnownCanvasObjects(workflowResultsDir);
            foreach (var known in knownObjects)
            {
                probe.KnownObjects.Add(new
                {
                    known.Id,
                    known.Kind,
                    known.Text,
                    known.Variable,
                    known.Expression,
                    rect = new { known.Rect.X, known.Rect.Y, known.Rect.Width, known.Rect.Height },
                    known.Workflow,
                    known.ResultPath,
                    known.Readback
                });
            }

            if (!File.Exists(geometryPath))
            {
                probe.BlockedReasons.Add("blob_geometry.json was not produced by MCE export");
            }
            else
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(geometryPath, Encoding.UTF8));
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in doc.RootElement.EnumerateArray())
                    {
                        var classCount = entry.TryGetProperty("classOccurrences", out var classes) && classes.ValueKind == JsonValueKind.Array
                            ? classes.GetArrayLength()
                            : 0;
                        var rectCount = entry.TryGetProperty("candidateRectangles", out var rects) && rects.ValueKind == JsonValueKind.Array
                            ? rects.GetArrayLength()
                            : 0;
                        var anchorCount = entry.TryGetProperty("textAnchors", out var anchors) && anchors.ValueKind == JsonValueKind.Array
                            ? anchors.GetArrayLength()
                            : 0;
                        probe.BlobEntries.Add(new
                        {
                            table = JsonString(entry, "table"),
                            column = JsonString(entry, "column"),
                            rowKey = JsonString(entry, "rowKey"),
                            bytes = JsonInt(entry, "bytes"),
                            sha256 = JsonString(entry, "sha256"),
                            classCount,
                            rectCount,
                            textAnchorCount = anchorCount
                        });
                    }

                    AddGenericMceGeometryOccupancy(doc.RootElement, map, probe, canvasWidth, canvasHeight, rowKeyFilter);

                    foreach (var known in knownObjects)
                    {
                        var match = FindMceObjectMapMatch(doc.RootElement, known);
                        if (match.Reliable)
                        {
                            var rect = new CanvasOccupiedRect
                            {
                                Id = known.Id,
                                Kind = known.Kind,
                                Text = known.Text,
                                Variable = known.Variable,
                                Expression = known.Expression,
                                X = match.X,
                                Y = match.Y,
                                Width = match.Width,
                                Height = match.Height,
                                Source = "mce-object-map",
                                Confidence = "high"
                            };
                            AddCanvasObject(map, new CanvasDetectedObject
                            {
                                Id = known.Id,
                                Kind = known.Kind,
                                Text = known.Text,
                                Variable = known.Variable,
                                Expression = known.Expression,
                                Rect = rect,
                                Source = "mce-object-map",
                                Confidence = "high"
                            });
                            probe.Matches.Add(match.Evidence);
                        }
                        else
                        {
                            probe.RejectedHypotheses.Add(match.Evidence);
                        }
                    }
                }
            }

            FinalizeCanvasMap(map, fallbackProvider: "mce-object-map");
            if (map.ReliableGeometry)
            {
                probe.Status = "PASS";
                probe.EvidenceStatus = "PASS";
                map.MceGeometryProbe = probe;
            }
            else
            {
                probe.Status = "UNKNOWN";
                probe.EvidenceStatus = File.Exists(geometryPath) ? "PASS" : "UNKNOWN";
                if (probe.BlockedReasons.Count == 0)
                    probe.BlockedReasons.Add("MCE object-map probe could not decode high-confidence occupied geometry");
                foreach (var reason in probe.BlockedReasons)
                    map.BlockedReasons.Add(reason);
                map.MceGeometryProbe = probe;
            }

            File.WriteAllText(Path.Combine(outDir, "mce-object-map-probe.json"),
                JsonSerializer.Serialize(probe, JsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(outDir, "canvas-objects.json"),
                JsonSerializer.Serialize(map, JsonOptions()), Encoding.UTF8);
            Console.WriteLine("canvas mce-object-map-probe: " + outDir);
            return map.ReliableGeometry ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas mce-object-map-probe failed: " + ex.Message);
            return 1;
        }
    }

    private static int CanvasMceBlobDiffProbe(string[] args)
    {
        var before = RequiredPath(args, "--before");
        var after = RequiredPath(args, "--after");
        var outDir = FullPath(Required(args, "--out"));
        Directory.CreateDirectory(outDir);
        try
        {
            MceExporter.BlobDiff(before, after, outDir);
            Console.WriteLine("canvas mce-blob-diff-probe: " + outDir);
            return File.Exists(Path.Combine(outDir, "blob_diff.json")) ? 0 : 2;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(outDir, "failure.txt"), ex.ToString(), Encoding.UTF8);
            Console.Error.WriteLine("canvas mce-blob-diff-probe failed: " + ex.Message);
            return 1;
        }
    }

    private static string? InferWorkflowResultsDir(string project)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(project));
        if (string.IsNullOrWhiteSpace(dir)) return null;
        var sibling = Path.Combine(dir, "workflow-results");
        return Directory.Exists(sibling) ? sibling : null;
    }

    private static List<KnownCanvasObject> LoadKnownCanvasObjects(string workflowResultsDir)
    {
        var known = new List<KnownCanvasObject>();
        if (!Directory.Exists(workflowResultsDir)) return known;

        var sequence = 0;
        foreach (var file in Directory.EnumerateFiles(workflowResultsDir, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (Path.GetFileName(file).Equals("index.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
                var root = doc.RootElement;
                if (!root.TryGetProperty("createdUiObjects", out var objects) || objects.ValueKind != JsonValueKind.Array)
                    continue;
                var workflow = JsonString(root, "workflow") ?? "";
                foreach (var item in objects.EnumerateArray())
                {
                    if (!item.TryGetProperty("rect", out var rect) || rect.ValueKind != JsonValueKind.Object)
                        continue;
                    var x = LayoutJsonIntAny(rect, "x", "X");
                    var y = LayoutJsonIntAny(rect, "y", "Y");
                    var width = LayoutJsonIntAny(rect, "width", "Width");
                    var height = LayoutJsonIntAny(rect, "height", "Height");
                    if (x == null || y == null || width == null || height == null || width <= 0 || height <= 0)
                        continue;

                    var kind = JsonStringAny(item, "kind", "Kind") ?? workflow;
                    var text = JsonStringAny(item, "text", "Text") ?? "";
                    var variable = JsonStringAny(item, "variable", "Variable") ?? "";
                    var expression = JsonStringAny(item, "expression", "Expression") ?? "";
                    var readback = JsonStringAny(item, "readback", "Readback") ?? "";
                    sequence++;
                    var id = JsonStringAny(item, "id", "Id");
                    if (string.IsNullOrWhiteSpace(id))
                        id = "known-" + sequence.ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
                    known.Add(new KnownCanvasObject
                    {
                        Id = id,
                        Kind = kind,
                        Text = text,
                        Variable = variable,
                        Expression = expression,
                        Rect = new CanvasOccupiedRect
                        {
                            Id = id,
                            Kind = kind,
                            Text = text,
                            Variable = variable,
                            Expression = expression,
                            X = x.Value,
                            Y = y.Value,
                            Width = width.Value,
                            Height = height.Value,
                            Source = "workflow-result",
                            Confidence = readback.Equals("PASS", StringComparison.OrdinalIgnoreCase) ? "high" : "medium"
                        },
                        Workflow = workflow,
                        ResultPath = file,
                        Readback = readback
                    });
                }
            }
            catch
            {
                // Probe evidence must be best-effort. Invalid old workflow evidence is reported by absence of anchors.
            }
        }
        return known;
    }

    private static void AddGenericMceGeometryOccupancy(
        JsonElement geometryRoot,
        CanvasObjectMap map,
        MceObjectMapProbeResult probe,
        int canvasWidth,
        int canvasHeight,
        string? rowKeyFilter)
    {
        if (geometryRoot.ValueKind != JsonValueKind.Array) return;

        var sequence = 0;
        var evidenceLimit = 160;
        foreach (var entry in geometryRoot.EnumerateArray())
        {
            if (!entry.TryGetProperty("candidateRectangles", out var rects) ||
                rects.ValueKind != JsonValueKind.Array)
                continue;

            var rowKey = JsonString(entry, "rowKey") ?? "";
            if (!string.IsNullOrWhiteSpace(rowKeyFilter) &&
                !rowKey.Equals(rowKeyFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var rectEl in rects.EnumerateArray())
            {
                if (!TryBuildGenericMceOccupiedRect(rectEl, canvasWidth, canvasHeight, out var rect))
                    continue;

                sequence++;
                rect.Id = string.IsNullOrWhiteSpace(rowKey)
                    ? "mce-occ-" + sequence.ToString("D4")
                    : "mce-occ-r" + rowKey + "-" + sequence.ToString("D4");
                rect.Kind = "unknown-mce-object";
                rect.Source = "mce-geometry-inferred";
                rect.Confidence = "high";

                AddCanvasObject(map, new CanvasDetectedObject
                {
                    Id = rect.Id,
                    Kind = rect.Kind,
                    Rect = rect,
                    Source = rect.Source,
                    Confidence = rect.Confidence
                });

                if (probe.GenericOccupancy.Count < evidenceLimit)
                {
                    probe.GenericOccupancy.Add(new
                    {
                        id = rect.Id,
                        rowKey,
                        offset = JsonString(rectEl, "offset"),
                        encoding = JsonString(rectEl, "encoding"),
                        pattern = JsonString(rectEl, "pattern"),
                        rect = new { rect.X, rect.Y, rect.Width, rect.Height },
                        source = rect.Source,
                        confidence = rect.Confidence
                    });
                }
            }
        }
    }

    private static bool TryBuildGenericMceOccupiedRect(JsonElement rectEl, int canvasWidth, int canvasHeight, out CanvasOccupiedRect rect)
    {
        rect = new CanvasOccupiedRect();
        if (!string.Equals(JsonString(rectEl, "encoding"), "int32", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(JsonString(rectEl, "pattern"), "ltrb", StringComparison.OrdinalIgnoreCase)) return false;

        var x = LayoutJsonIntAny(rectEl, "x", "X");
        var y = LayoutJsonIntAny(rectEl, "y", "Y");
        var width = LayoutJsonIntAny(rectEl, "width", "Width");
        var height = LayoutJsonIntAny(rectEl, "height", "Height");
        if (x == null || y == null || width == null || height == null) return false;

        if (x.Value <= 2 || y.Value <= 2) return false;
        if (width.Value < 30 || height.Value < 16) return false;
        if (width.Value > Math.Max(480, canvasWidth / 2) || height.Value > Math.Max(220, canvasHeight / 3)) return false;
        if ((long)width.Value * height.Value > 40000) return false;
        if (x.Value > canvasWidth + 20 || y.Value > canvasHeight + 20) return false;
        if (x.Value + width.Value > canvasWidth + 40 || y.Value + height.Value > canvasHeight + 40) return false;
        if (width.Value <= 25 && height.Value <= 25) return false;

        rect = new CanvasOccupiedRect
        {
            X = x.Value,
            Y = y.Value,
            Width = width.Value,
            Height = height.Value
        };
        return true;
    }

    private static MceObjectMapMatchResult FindMceObjectMapMatch(JsonElement geometryRoot, KnownCanvasObject known)
    {
        var best = new MceObjectMapMatchResult
        {
            Evidence = new
            {
                objectId = known.Id,
                kind = known.Kind,
                text = known.Text,
                expectedRect = new { known.Rect.X, known.Rect.Y, known.Rect.Width, known.Rect.Height },
                score = 0,
                reliable = false,
                rejectedReason = "no blob entry matched both text/class evidence and the known rectangle"
            }
        };

        if (geometryRoot.ValueKind != JsonValueKind.Array) return best;
        foreach (var entry in geometryRoot.EnumerateArray())
        {
            var tokenMatches = FindMceTokenAnchorMatches(entry, known);
            var classMatches = FindMceClassMatches(entry, known.Kind).ToArray();
            var rectMatches = FindMceRectMatches(entry, known.Rect).ToArray();
            var exactOrNearRect = rectMatches.FirstOrDefault(match => match.ExactOrNear);
            var bestRect = exactOrNearRect ?? rectMatches.FirstOrDefault();

            var score = 0;
            if (tokenMatches.Count > 0) score += 45;
            if (classMatches.Length > 0) score += 20;
            if (exactOrNearRect != null) score += 45;
            else if (bestRect != null && bestRect.DimensionNear) score += 15;

            var reliable = tokenMatches.Count > 0 && exactOrNearRect != null && score >= 90;
            var evidence = new
            {
                objectId = known.Id,
                kind = known.Kind,
                text = known.Text,
                variable = known.Variable,
                expression = known.Expression,
                expectedRect = new { known.Rect.X, known.Rect.Y, known.Rect.Width, known.Rect.Height },
                blobEntry = new
                {
                    table = JsonString(entry, "table"),
                    column = JsonString(entry, "column"),
                    rowKey = JsonString(entry, "rowKey"),
                    sha256 = JsonString(entry, "sha256")
                },
                score,
                reliable,
                tokenMatches,
                classMatches,
                rectMatches = rectMatches.Take(8).Select(match => new
                {
                    match.Offset,
                    match.Encoding,
                    match.Pattern,
                    match.X,
                    match.Y,
                    match.Width,
                    match.Height,
                    match.TotalDelta,
                    match.ExactOrNear,
                    match.DimensionNear
                }).ToArray(),
                rejectedReason = reliable ? null : "token/class evidence did not align with an exact or near decoded rectangle"
            };

            if (score > best.Score)
            {
                best = new MceObjectMapMatchResult
                {
                    Reliable = reliable,
                    Score = score,
                    X = bestRect?.X ?? known.Rect.X,
                    Y = bestRect?.Y ?? known.Rect.Y,
                    Width = bestRect?.Width ?? known.Rect.Width,
                    Height = bestRect?.Height ?? known.Rect.Height,
                    Evidence = evidence
                };
            }
        }
        return best;
    }

    private static List<object> FindMceTokenAnchorMatches(JsonElement entry, KnownCanvasObject known)
    {
        var tokens = new[] { known.Text, known.Variable, known.Expression }
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var matches = new List<object>();
        if (tokens.Length == 0 ||
            !entry.TryGetProperty("textAnchors", out var anchors) ||
            anchors.ValueKind != JsonValueKind.Array)
            return matches;

        foreach (var anchor in anchors.EnumerateArray())
        {
            var sample = JsonString(anchor, "sample") ?? "";
            var sha = JsonString(anchor, "sha256") ?? "";
            foreach (var token in tokens)
            {
                var tokenHash = Sha256Text(token);
                if ((!string.IsNullOrEmpty(sample) && sample.Contains(token, StringComparison.Ordinal)) ||
                    sha.Equals(tokenHash, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new
                    {
                        token,
                        offset = JsonString(anchor, "offset"),
                        encoding = JsonString(anchor, "encoding"),
                        byteLength = JsonInt(anchor, "byteLength"),
                        sha256 = sha,
                        sample = sample.Length > 120 ? sample[..120] : sample
                    });
                }
            }
        }
        return matches;
    }

    private static IEnumerable<string> FindMceClassMatches(JsonElement entry, string kind)
    {
        if (!entry.TryGetProperty("classOccurrences", out var classes) ||
            classes.ValueKind != JsonValueKind.Array)
            yield break;

        var compatible = CompatibleCanvasClasses(kind);
        foreach (var cls in classes.EnumerateArray())
        {
            var name = JsonString(cls, "className") ?? "";
            if (compatible.Any(match => name.Equals(match, StringComparison.OrdinalIgnoreCase)))
                yield return name;
        }
    }

    private sealed class MceRectMatch
    {
        public string Offset { get; set; } = "";
        public string Encoding { get; set; } = "";
        public string Pattern { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int TotalDelta { get; set; }
        public bool ExactOrNear { get; set; }
        public bool DimensionNear { get; set; }
    }

    private static IEnumerable<MceRectMatch> FindMceRectMatches(JsonElement entry, CanvasOccupiedRect expected)
    {
        if (!entry.TryGetProperty("candidateRectangles", out var rects) ||
            rects.ValueKind != JsonValueKind.Array)
            yield break;

        var matches = new List<MceRectMatch>();
        foreach (var rect in rects.EnumerateArray())
        {
            var x = LayoutJsonIntAny(rect, "x", "X");
            var y = LayoutJsonIntAny(rect, "y", "Y");
            var width = LayoutJsonIntAny(rect, "width", "Width");
            var height = LayoutJsonIntAny(rect, "height", "Height");
            if (x == null || y == null || width == null || height == null) continue;
            var dx = Math.Abs(x.Value - expected.X);
            var dy = Math.Abs(y.Value - expected.Y);
            var dw = Math.Abs(width.Value - expected.Width);
            var dh = Math.Abs(height.Value - expected.Height);
            var total = dx + dy + dw + dh;
            var exactOrNear = dx <= 3 && dy <= 3 && dw <= 3 && dh <= 3;
            var dimensionNear = dw <= 10 && dh <= 10;
            if (!exactOrNear && !dimensionNear && total > 120) continue;
            matches.Add(new MceRectMatch
            {
                Offset = JsonString(rect, "offset") ?? "",
                Encoding = JsonString(rect, "encoding") ?? "",
                Pattern = JsonString(rect, "pattern") ?? "",
                X = x.Value,
                Y = y.Value,
                Width = width.Value,
                Height = height.Value,
                TotalDelta = total,
                ExactOrNear = exactOrNear,
                DimensionNear = dimensionNear
            });
        }

        foreach (var match in matches.OrderByDescending(match => match.ExactOrNear).ThenBy(match => match.TotalDelta))
            yield return match;
    }

    private static string[] CompatibleCanvasClasses(string kind)
        => kind.ToLowerInvariant() switch
        {
            "native-static-text" or "section-title" or "static-label" => new[] { "CDrawLabel" },
            "native-lamp" => new[] { "CDrawMulDis", "CDrawButton" },
            "momentary-button" or "status-button" => new[] { "CDrawButton" },
            _ => new[] { "CDrawButton", "CDrawLabel", "CDrawMulDis", "CDrawEdit" }
        };

    private sealed class CanvasProbeSession : IDisposable
    {
        public required Process Process { get; init; }
        public required IntPtr Main { get; init; }
        public required IntPtr Canvas { get; init; }
        public required string ProjectCopy { get; init; }
        public required string ProjectSha256 { get; init; }
        public required int WindowIndex { get; init; }
        public void Dispose() { }
    }

    private static CanvasProbeSession OpenCanvasProbeSession(
        string[] args,
        string outDir,
        string label,
        out Process process,
        out IntPtr main)
    {
        var project = RequiredPath(args, "--project");
        var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
        var windowIndex = ParseInt(args, "--window-index", 0);
        var timeout = TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20));
        var copyDir = Path.Combine(outDir, label + "-copy");
        Directory.CreateDirectory(copyDir);
        var projectCopy = Path.Combine(copyDir, Path.GetFileNameWithoutExtension(project) + "-" + label + ".MCE");
        File.Copy(project, projectCopy, overwrite: true);
        var projectSha = Sha256(projectCopy);
        SetDialogEvidenceRoot(outDir);
        process = Process.Start(new ProcessStartInfo(editor, Quote(projectCopy))
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
        }) ?? throw new InvalidOperationException("Failed to start MCGS editor.");
        main = WaitForMainWindow(process.Id, timeout);
        HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
        main = UiAutomation.FindMainWindow(process.Id);
        if (main == IntPtr.Zero) throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");
        OpenAnimationConfiguration(process.Id, main, windowIndex, "canvas " + label);
        var canvas = FindCanvas(main);
        return new CanvasProbeSession
        {
            Process = process,
            Main = main,
            Canvas = canvas,
            ProjectCopy = projectCopy,
            ProjectSha256 = projectSha,
            WindowIndex = windowIndex
        };
    }

    private static CanvasObjectMap BuildCanvasObjectMap(string project, string projectSha, int windowIndex, IntPtr main, IntPtr canvas)
    {
        var map = NewCanvasObjectMap(project, projectSha, windowIndex, canvas);

        var uia = ProbeCanvasUia(canvas);
        map.UiaProbe = uia.Detail;
        map.ChannelsTried.Add("UIA");
        foreach (var obj in uia.Objects) AddCanvasObject(map, obj);

        var msaa = ProbeCanvasMsaa(canvas);
        map.MsaaProbe = msaa.Detail;
        map.ChannelsTried.Add("MSAA/IAccessible");
        foreach (var obj in msaa.Objects) AddCanvasObject(map, obj);

        map.WmGetObjectProbe = ProbeWmGetObject(canvas);
        map.ChannelsTried.Add("WM_GETOBJECT");
        map.NativeObjectProbe = ProbeNativeObjectModel(canvas);
        map.ChannelsTried.Add("OBJID_NATIVEOM");

        FinalizeCanvasMap(map, fallbackProvider: "none");
        if (!map.ReliableGeometry)
            map.BlockedReasons.Add("MCGS canvas did not expose child objects through UIA/MSAA/WM_GETOBJECT/OBJID_NATIVEOM in this probe");

        return map;
    }

    private static CanvasObjectMap NewCanvasObjectMap(string project, string projectSha, int windowIndex, IntPtr canvas)
        => new()
        {
            Project = project,
            ProjectSha256 = projectSha,
            WindowIndex = windowIndex,
            CanvasWindow = WindowInfo.FromHandle(canvas)
        };

    private static void FinalizeCanvasMap(CanvasObjectMap map, string fallbackProvider)
    {
        if (map.Objects.Count > 0 && map.Objects.All(o => o.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase)))
        {
            map.Status = "PASS";
            map.ReliableGeometry = true;
            map.ObjectProvider = map.Objects.Any(o => o.Source.StartsWith("uia", StringComparison.OrdinalIgnoreCase))
                ? "uia"
                : map.Objects.Any(o => o.Source.StartsWith("msaa", StringComparison.OrdinalIgnoreCase))
                    ? "msaa"
                    : map.Objects[0].Source;
            return;
        }

        map.Status = "UNKNOWN";
        map.ReliableGeometry = false;
        map.ObjectProvider = map.Objects.Count > 0 ? map.Objects[0].Source : fallbackProvider;
    }

    private static void AddCanvasObject(CanvasObjectMap map, CanvasDetectedObject obj)
    {
        if (obj.Rect.Width <= 0 || obj.Rect.Height <= 0) return;
        if (map.OccupiedRectangles.Any(existing =>
            existing.X == obj.Rect.X &&
            existing.Y == obj.Rect.Y &&
            existing.Width == obj.Rect.Width &&
            existing.Height == obj.Rect.Height))
            return;
        map.Objects.Add(obj);
        map.OccupiedRectangles.Add(obj.Rect);
    }

    private static (object Detail, List<CanvasDetectedObject> Objects) ProbeCanvasUia(IntPtr canvas)
    {
        var objects = new List<CanvasDetectedObject>();
        try
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(canvas);
            if (root == null)
                return (new { status = "UNKNOWN", error = "AutomationElement.FromHandle returned null" }, objects);

            var rootInfo = UiaElementInfo(root, canvas, includeRect: true);
            var children = new List<object>();
            var walker = System.Windows.Automation.TreeWalker.RawViewWalker;
            var stack = new Stack<(System.Windows.Automation.AutomationElement Element, int Depth)>();
            var first = Safe<System.Windows.Automation.AutomationElement?>(() => walker.GetFirstChild(root), null);
            if (first != null) stack.Push((first, 1));
            var index = 0;
            while (stack.Count > 0 && index < 100)
            {
                var (element, depth) = stack.Pop();
                var info = UiaElementInfo(element, canvas, includeRect: true);
                children.Add(info);
                if (TryBuildCanvasObjectFromUia(index, info, out var obj)) objects.Add(obj);
                index++;

                if (depth < 8)
                {
                    var child = Safe<System.Windows.Automation.AutomationElement?>(() => walker.GetFirstChild(element), null);
                    var siblings = new List<System.Windows.Automation.AutomationElement>();
                    while (child != null)
                    {
                        siblings.Add(child);
                        child = Safe<System.Windows.Automation.AutomationElement?>(() => walker.GetNextSibling(child), null);
                    }
                    for (var i = siblings.Count - 1; i >= 0; i--) stack.Push((siblings[i], depth + 1));
                }
            }

            return (new { status = "PASS", root = rootInfo, childCount = children.Count, children }, objects);
        }
        catch (Exception ex)
        {
            return (new { status = "UNKNOWN", error = ex.Message }, objects);
        }
    }

    private static object UiaElementInfo(System.Windows.Automation.AutomationElement element, IntPtr canvas, bool includeRect)
    {
        var canvasRect = UiAutomation.GetWindowRect(canvas);
        var current = element.Current;
        var bounds = Safe(() => current.BoundingRectangle, System.Windows.Rect.Empty);
        object? rect = null;
        if (includeRect)
        {
            rect = new
            {
                x = (int)Math.Round(bounds.Left - canvasRect.Left),
                y = (int)Math.Round(bounds.Top - canvasRect.Top),
                width = (int)Math.Round(bounds.Width),
                height = (int)Math.Round(bounds.Height)
            };
        }

        return new
        {
            name = Safe(() => current.Name, ""),
            automationId = Safe(() => current.AutomationId, ""),
            className = Safe(() => current.ClassName, ""),
            controlType = Safe(() => current.ControlType.ProgrammaticName, ""),
            localizedControlType = Safe(() => current.LocalizedControlType, ""),
            frameworkId = Safe(() => current.FrameworkId, ""),
            isEnabled = Safe(() => current.IsEnabled, false),
            isOffscreen = Safe(() => current.IsOffscreen, true),
            rect
        };
    }

    private static object? TryGetComProperty(object target, string property)
    {
        try
        {
            return target.GetType().InvokeMember(property,
                System.Reflection.BindingFlags.GetProperty,
                null,
                target,
                Array.Empty<object>());
        }
        catch
        {
            return null;
        }
    }

    private static object ReadComRect(object? rect, Rect canvasRect)
    {
        var left = ReadComDouble(rect, "left", "Left", "x", "X");
        var top = ReadComDouble(rect, "top", "Top", "y", "Y");
        var right = ReadComDouble(rect, "right", "Right");
        var bottom = ReadComDouble(rect, "bottom", "Bottom");
        var width = ReadComDouble(rect, "width", "Width");
        var height = ReadComDouble(rect, "height", "Height");
        if (width <= 0 && right > left) width = right - left;
        if (height <= 0 && bottom > top) height = bottom - top;
        return new
        {
            x = (int)Math.Round(left - canvasRect.Left),
            y = (int)Math.Round(top - canvasRect.Top),
            width = (int)Math.Round(width),
            height = (int)Math.Round(height)
        };
    }

    private static double ReadComDouble(object? target, params string[] names)
    {
        if (target == null) return 0;
        foreach (var name in names)
        {
            try
            {
                var value = target.GetType().InvokeMember(name,
                    System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.GetProperty,
                    null,
                    target,
                    Array.Empty<object>());
                if (value != null && double.TryParse(Convert.ToString(value), out var parsed)) return parsed;
            }
            catch
            {
            }
        }
        return 0;
    }

    private static bool TryBuildCanvasObjectFromUia(int index, object info, out CanvasDetectedObject obj)
    {
        obj = new CanvasDetectedObject();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(info, JsonOptions()));
        var root = doc.RootElement;
        if (!root.TryGetProperty("rect", out var rect) || rect.ValueKind != JsonValueKind.Object) return false;
        var width = LayoutJsonInt(rect, "width") ?? 0;
        var height = LayoutJsonInt(rect, "height") ?? 0;
        if (width <= 0 || height <= 0) return false;
        var name = JsonString(root, "name") ?? "";
        var className = JsonString(root, "className") ?? "";
        var controlType = JsonString(root, "controlType") ?? "";
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(className) && string.IsNullOrWhiteSpace(controlType))
            return false;
        obj = new CanvasDetectedObject
        {
            Id = "uia-" + index.ToString("D3"),
            Kind = string.IsNullOrWhiteSpace(controlType) ? "uia-element" : controlType,
            Text = name,
            Source = "uia",
            Confidence = "medium",
            Rect = new CanvasOccupiedRect
            {
                Id = "uia-" + index.ToString("D3"),
                Kind = string.IsNullOrWhiteSpace(controlType) ? "uia-element" : controlType,
                Text = name,
                X = LayoutJsonInt(rect, "x") ?? 0,
                Y = LayoutJsonInt(rect, "y") ?? 0,
                Width = width,
                Height = height,
                Source = "uia",
                Confidence = "medium"
            }
        };
        return true;
    }

    private static (object Detail, List<CanvasDetectedObject> Objects) ProbeCanvasMsaa(IntPtr canvas)
    {
        var objects = new List<CanvasDetectedObject>();
        try
        {
            var obj = AccessibleObjectFromWindow(canvas, unchecked((uint)-4));
            if (obj == null)
                return (new { status = "UNKNOWN", error = "AccessibleObjectFromWindow OBJID_CLIENT returned null" }, objects);
            var acc = (Accessibility.IAccessible)obj;
            var childCount = Safe(() => acc.accChildCount);
            var root = MsaaChildInfo(acc, 0, canvas, "msaa-root");
            var children = new List<object>();
            for (var i = 1; i <= Math.Min(childCount, 100); i++)
            {
                var child = MsaaChildInfo(acc, i, canvas, "msaa-" + i.ToString("D3"));
                children.Add(child);
                if (TryBuildCanvasObjectFromMsaa(i, child, out var detected)) objects.Add(detected);
            }
            return (new { status = "PASS", childCount, root, children }, objects);
        }
        catch (Exception ex)
        {
            return (new { status = "UNKNOWN", error = ex.Message }, objects);
        }
    }

    private static object MsaaChildInfo(Accessibility.IAccessible acc, int childId, IntPtr canvas, string id)
    {
        var child = (object)childId;
        var canvasRect = UiAutomation.GetWindowRect(canvas);
        var x = 0;
        var y = 0;
        var width = 0;
        var height = 0;
        var locationOk = false;
        try
        {
            acc.accLocation(out x, out y, out width, out height, child);
            locationOk = true;
        }
        catch
        {
        }
        return new
        {
            id,
            name = Safe(() => acc.get_accName(child)),
            value = Safe(() => acc.get_accValue(child)),
            role = Safe(() => Convert.ToString(acc.get_accRole(child)) ?? ""),
            state = Safe(() => Convert.ToString(acc.get_accState(child)) ?? ""),
            locationOk,
            rect = new
            {
                x = x - canvasRect.Left,
                y = y - canvasRect.Top,
                width,
                height
            }
        };
    }

    private static bool TryBuildCanvasObjectFromMsaa(int index, object info, out CanvasDetectedObject obj)
    {
        obj = new CanvasDetectedObject();
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(info, JsonOptions()));
        var root = doc.RootElement;
        if (!root.TryGetProperty("rect", out var rect) || rect.ValueKind != JsonValueKind.Object) return false;
        var width = LayoutJsonInt(rect, "width") ?? 0;
        var height = LayoutJsonInt(rect, "height") ?? 0;
        var name = JsonString(root, "name") ?? "";
        if (width <= 0 || height <= 0 || string.IsNullOrWhiteSpace(name)) return false;
        obj = new CanvasDetectedObject
        {
            Id = "msaa-" + index.ToString("D3"),
            Kind = JsonString(root, "role") ?? "msaa-object",
            Text = name,
            Source = "msaa",
            Confidence = "medium",
            Rect = new CanvasOccupiedRect
            {
                Id = "msaa-" + index.ToString("D3"),
                Kind = JsonString(root, "role") ?? "msaa-object",
                Text = name,
                X = LayoutJsonInt(rect, "x") ?? 0,
                Y = LayoutJsonInt(rect, "y") ?? 0,
                Width = width,
                Height = height,
                Source = "msaa",
                Confidence = "medium"
            }
        };
        return true;
    }

    private static object ProbeWmGetObject(IntPtr canvas)
    {
        var ids = new[] { ("OBJID_CLIENT", -4), ("UiaRootObjectId", -25), ("OBJID_NATIVEOM", -16) };
        var rows = new List<object>();
        foreach (var (name, id) in ids)
        {
            var ok = CanvasNative.SendMessageTimeout(canvas, Native.WM_GETOBJECT, IntPtr.Zero, new IntPtr(id),
                Native.SMTO_ABORTIFHUNG, 1500, out var result) != IntPtr.Zero;
            rows.Add(new { name, id, sendOk = ok, result = "0x" + result.ToInt64().ToString("X") });
        }
        return new { status = "PASS", rows };
    }

    private static object ProbeNativeObjectModel(IntPtr canvas)
    {
        try
        {
            var obj = AccessibleObjectFromWindow(canvas, unchecked((uint)-16));
            return obj == null
                ? new { status = "UNKNOWN", error = "OBJID_NATIVEOM returned null" }
                : new { status = "PASS", type = obj.GetType().FullName ?? obj.GetType().Name };
        }
        catch (Exception ex)
        {
            return new { status = "UNKNOWN", error = ex.Message };
        }
    }

    private static object? AccessibleObjectFromWindow(IntPtr hwnd, uint objectId)
    {
        var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
        var hr = CanvasNative.AccessibleObjectFromWindow(hwnd, objectId, ref iid, out var obj);
        return hr == 0 ? obj : null;
    }

    private static ClipboardFormatDescription DescribeClipboardFormat(IDataObject dataObject, string format, Rect canvasRect)
    {
        var formatId = Safe(() => DataFormats.GetFormat(format).Id, 0);
        try
        {
            var data = dataObject.GetData(format, autoConvert: false);
            var bytes = TryGetClipboardBytes(data);
            var geometry = bytes is { Length: > 0 }
                ? AnalyzeClipboardGeometry(format, formatId, bytes, canvasRect)
                : null;
            object summary = data switch
            {
                null => new { format, formatId, type = "null" } as object,
                string text => new
                {
                    format,
                    formatId,
                    type = "string",
                    length = text.Length,
                    sha256 = Sha256Bytes(Encoding.UTF8.GetBytes(text)),
                    sample = text.Length <= 120 && !LooksBinary(text) ? text : null
                } as object,
                byte[] raw => new { format, formatId, type = "byte[]", length = raw.Length, sha256 = Sha256Bytes(raw), geometryDecoded = geometry?.ReliableGeometry ?? false } as object,
                Stream stream => new { format, formatId, type = "stream", length = bytes?.Length ?? stream.Length, sha256 = bytes == null ? "" : Sha256Bytes(bytes), geometryDecoded = geometry?.ReliableGeometry ?? false } as object,
                _ => new { format, formatId, type = data?.GetType().FullName ?? data?.GetType().Name ?? "null" } as object
            };
            return new ClipboardFormatDescription { Summary = summary, GeometryAnalysis = geometry };
        }
        catch (Exception ex)
        {
            return new ClipboardFormatDescription { Summary = new { format, formatId, error = ex.Message } };
        }
    }

    private static byte[]? TryGetClipboardBytes(object? data)
    {
        switch (data)
        {
            case null:
                return null;
            case byte[] bytes:
                return bytes;
            case string text:
                return Encoding.UTF8.GetBytes(text);
            case MemoryStream memory:
                return memory.ToArray();
            case Stream stream:
            {
                var original = stream.CanSeek ? stream.Position : 0;
                try
                {
                    if (stream.CanSeek) stream.Position = 0;
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
                finally
                {
                    if (stream.CanSeek) stream.Position = original;
                }
            }
            default:
                return null;
        }
    }

    private static ClipboardGeometryAnalysis AnalyzeClipboardGeometry(string format, int formatId, byte[] bytes, Rect canvasRect)
    {
        var analysis = new ClipboardGeometryAnalysis
        {
            Format = format,
            FormatId = formatId,
            Length = bytes.Length,
            Sha256 = Sha256Bytes(bytes)
        };

        var strings = ExtractClipboardStringOccurrences(bytes).ToArray();
        var candidates = ExtractClipboardRectCandidates(bytes, canvasRect).ToArray();

        foreach (var sample in ClipboardStringSamples(strings).Take(40))
            analysis.StringSamples.Add(sample);

        foreach (var record in BuildClipboardClassRecords(strings, candidates).Take(80))
            analysis.ClassRecords.Add(record);

        foreach (var candidate in candidates.Take(200))
        {
            analysis.CandidateRectangles.Add(new
            {
                offset = candidate.OffsetHex,
                candidate.Encoding,
                candidate.Pattern,
                raw = candidate.Raw,
                x = candidate.X,
                y = candidate.Y,
                width = candidate.Width,
                height = candidate.Height,
                candidate.Confidence
            });
        }

        analysis.BlockedReasons.Add("MCGS_DRAW_OBJ clipboard format was captured, but geometry decoder is heuristic and not trusted for automatic placement yet");
        return analysis;
    }

    private static IEnumerable<object> ClipboardStringSamples(IEnumerable<ClipboardStringOccurrence> occurrences)
    {
        foreach (var sample in occurrences
                     .GroupBy(x => x.Text)
                     .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                     .Select(g => g.First()))
        {
            yield return new
            {
                offset = FormatHex(sample.Offset),
                encoding = sample.Encoding,
                length = sample.Text.Length,
                sha256 = Sha256Bytes(Encoding.UTF8.GetBytes(sample.Text)),
                text = sample.Text.Length <= 80 ? sample.Text : sample.Text[..80]
            };
        }
    }

    private static IEnumerable<ClipboardStringOccurrence> ExtractClipboardStringOccurrences(byte[] bytes)
        => ExtractAnsiOccurrences(bytes, 4).Concat(ExtractUtf16Occurrences(bytes, 3))
            .OrderBy(x => x.Offset);

    private static IEnumerable<ClipboardStringOccurrence> ExtractAnsiOccurrences(byte[] bytes, int minLength)
    {
        var i = 0;
        while (i < bytes.Length)
        {
            while (i < bytes.Length && !IsClipboardTextByte(bytes[i])) i++;
            var start = i;
            while (i < bytes.Length && IsClipboardTextByte(bytes[i])) i++;
            if (i - start >= minLength)
            {
                var text = Encoding.Default.GetString(bytes, start, i - start).Trim();
                if (text.Length >= minLength && text.Any(char.IsLetterOrDigit))
                {
                    yield return new ClipboardStringOccurrence { Offset = start, Encoding = "ansi", Text = text };
                }
            }
        }
    }

    private static IEnumerable<ClipboardStringOccurrence> ExtractUtf16Occurrences(byte[] bytes, int minLength)
    {
        for (var alignment = 0; alignment < 2; alignment++)
        {
            var i = alignment;
            while (i + 1 < bytes.Length)
            {
                while (i + 1 < bytes.Length && !IsClipboardTextChar(ReadCharLe(bytes, i))) i += 2;
                var start = i;
                while (i + 1 < bytes.Length && IsClipboardTextChar(ReadCharLe(bytes, i))) i += 2;
                var length = (i - start) / 2;
                if (length >= minLength)
                {
                    var text = Encoding.Unicode.GetString(bytes, start, i - start).Trim();
                    if (text.Length >= minLength && text.Any(ch => char.IsLetterOrDigit(ch) || IsCjkChar(ch)))
                    {
                        yield return new ClipboardStringOccurrence { Offset = start, Encoding = "utf16le", Text = text };
                    }
                }
            }
        }
    }

    private static IEnumerable<ClipboardRectCandidate> ExtractClipboardRectCandidates(byte[] bytes, Rect canvasRect)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var offset = 0; offset + 16 <= bytes.Length; offset += 4)
        {
            var a = BitConverter.ToInt32(bytes, offset);
            var b = BitConverter.ToInt32(bytes, offset + 4);
            var c = BitConverter.ToInt32(bytes, offset + 8);
            var d = BitConverter.ToInt32(bytes, offset + 12);
            foreach (var candidate in RectCandidatesFromTuple(offset, "int32", a, b, c, d, canvasRect))
            {
                var key = $"{candidate.X}:{candidate.Y}:{candidate.Width}:{candidate.Height}";
                if (seen.Add(key)) yield return candidate;
            }
        }

        for (var offset = 0; offset + 8 <= bytes.Length; offset += 2)
        {
            var a = BitConverter.ToInt16(bytes, offset);
            var b = BitConverter.ToInt16(bytes, offset + 2);
            var c = BitConverter.ToInt16(bytes, offset + 4);
            var d = BitConverter.ToInt16(bytes, offset + 6);
            foreach (var candidate in RectCandidatesFromTuple(offset, "int16", a, b, c, d, canvasRect))
            {
                var key = $"{candidate.X}:{candidate.Y}:{candidate.Width}:{candidate.Height}";
                if (seen.Add(key)) yield return candidate;
            }
        }
    }

    private static IEnumerable<ClipboardRectCandidate> RectCandidatesFromTuple(int offset, string encoding, int a, int b, int c, int d, Rect canvasRect)
    {
        if (TryBuildClipboardRect(a, b, c, d, canvasRect, "xywh", out var xywh))
            yield return new ClipboardRectCandidate { Offset = offset, Encoding = encoding, Pattern = "xywh", Raw = new[] { a, b, c, d }, X = xywh.X, Y = xywh.Y, Width = xywh.Width, Height = xywh.Height, Confidence = "low" };
        if (TryBuildClipboardRect(a, b, c - a, d - b, canvasRect, "ltrb", out var ltrb))
            yield return new ClipboardRectCandidate { Offset = offset, Encoding = encoding, Pattern = "ltrb", Raw = new[] { a, b, c, d }, X = ltrb.X, Y = ltrb.Y, Width = ltrb.Width, Height = ltrb.Height, Confidence = "low" };
    }

    private static IEnumerable<object> BuildClipboardClassRecords(IReadOnlyList<ClipboardStringOccurrence> strings, IReadOnlyList<ClipboardRectCandidate> candidates)
    {
        var classes = strings
            .Where(x => x.Text.StartsWith("CDraw", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Offset)
            .ToArray();
        for (var i = 0; i < classes.Length; i++)
        {
            var current = classes[i];
            var nextOffset = i + 1 < classes.Length ? classes[i + 1].Offset : int.MaxValue;
            var recordStrings = strings
                .Where(x => x.Offset > current.Offset && x.Offset < nextOffset && !x.Text.StartsWith("CDraw", StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .Select(x => new { offset = FormatHex(x.Offset), x.Encoding, text = x.Text.Length <= 80 ? x.Text : x.Text[..80] })
                .ToArray();
            var bestRect = candidates
                .Where(x => x.Pattern == "ltrb" && x.Offset > current.Offset && x.Offset < nextOffset && x.Width >= 20 && x.Height >= 20)
                .OrderByDescending(x => x.Width * x.Height)
                .FirstOrDefault();
            yield return new
            {
                className = current.Text,
                classOffset = FormatHex(current.Offset),
                nextClassOffset = nextOffset == int.MaxValue ? null : FormatHex(nextOffset),
                strings = recordStrings,
                rect = bestRect == null
                    ? null
                    : new
                    {
                        offset = bestRect.OffsetHex,
                        bestRect.Pattern,
                        bestRect.Raw,
                        x = bestRect.X,
                        y = bestRect.Y,
                        width = bestRect.Width,
                        height = bestRect.Height,
                        confidence = "medium"
                    }
            };
        }
    }

    private static bool TryBuildClipboardRect(int x, int y, int width, int height, Rect canvasRect, string pattern, out CanvasOccupiedRect rect)
    {
        rect = new CanvasOccupiedRect();
        if (width < 8 || height < 8) return false;
        if (width > canvasRect.Width + 80 || height > canvasRect.Height + 80) return false;
        if (x < -80 || y < -80 || x > canvasRect.Width + 80 || y > canvasRect.Height + 80) return false;
        if (x + width > canvasRect.Width + 100 || y + height > canvasRect.Height + 100) return false;
        if (width >= canvasRect.Width - 6 && height >= canvasRect.Height - 6) return false;
        rect = new CanvasOccupiedRect
        {
            Id = "clipboard-" + pattern + "-" + FormatHex(Math.Abs(HashCode.Combine(x, y, width, height))).Replace("0x", ""),
            Kind = "unknown",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Source = "clipboard-heuristic",
            Confidence = "low"
        };
        return true;
    }

    private static void AddClipboardGeometryToMap(CanvasObjectMap map, ClipboardGeometryAnalysis analysis)
    {
        foreach (var rect in analysis.SelectedRectangles)
        {
            AddCanvasObject(map, new CanvasDetectedObject
            {
                Id = rect.Id,
                Kind = rect.Kind,
                Text = rect.Text,
                Rect = rect,
                Source = "clipboard",
                Confidence = rect.Confidence
            });
        }
    }

    private static bool IsClipboardTextByte(byte value)
        => value is >= 0x20 and <= 0x7e || value >= 0x80;

    private static bool IsClipboardTextChar(char value)
        => value != '\0' && !char.IsControl(value) && !char.IsSurrogate(value);

    private static char ReadCharLe(byte[] bytes, int offset)
        => (char)(bytes[offset] | (bytes[offset + 1] << 8));

    private static bool IsCjkChar(char ch)
        => ch >= 0x3400 && ch <= 0x9FFF;

    private static string FormatHex(int value)
        => "0x" + value.ToString("X");

    private static bool LooksBinary(string text)
        => text.Any(ch => ch != '\r' && ch != '\n' && ch != '\t' && char.IsControl(ch));

    private static string Sha256Bytes(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static T Safe<T>(Func<T> action, T fallback = default!)
    {
        try { return action(); }
        catch { return fallback; }
    }

    private static class CanvasNative
    {
        [DllImport("oleacc.dll", PreserveSig = true)]
        public static extern int AccessibleObjectFromWindow(
            IntPtr hwnd,
            uint dwObjectId,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object? ppvObject);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
            uint flags, uint timeout, out IntPtr result);
    }
}
