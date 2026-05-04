using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        McgsToolEntry? tool = null;
        if (!string.IsNullOrWhiteSpace(catalogPath) && File.Exists(FullPath(catalogPath)))
        {
            tool = ReadMcgsCatalog(project, FullPath(catalogPath)).Tools
                .FirstOrDefault(t => t.toolId.Equals(toolId, StringComparison.OrdinalIgnoreCase));
        }

        var invokeReadonly = Has(args, "--invoke-readonly");
        var invokeCandidateSafe = Has(args, "--invoke-candidate-safe");
        var allowUnknownRisk = Has(args, "--allow-unknown-risk");
        var saveAfterInvoke = Has(args, "--save-after-invoke");
        var baselineAfterPrecondition = Has(args, "--baseline-after-precondition");
        if (saveAfterInvoke && !invokeCandidateSafe)
            return Fail("--save-after-invoke is only allowed with --invoke-candidate-safe on a disposable project copy.");
        if (baselineAfterPrecondition && !invokeCandidateSafe)
            return Fail("--baseline-after-precondition is only allowed with --invoke-candidate-safe on a disposable project copy.");
        var safetyClass = tool?.safetyClass ?? "unknown-risk";
        var canInvoke = tool != null &&
                        tool.commandId.HasValue &&
                        ((invokeReadonly && safetyClass.Equals("read-only", StringComparison.OrdinalIgnoreCase)) ||
                         (invokeCandidateSafe && safetyClass.Equals("candidate-safe-mutation", StringComparison.OrdinalIgnoreCase)) ||
                         (allowUnknownRisk && safetyClass.Equals("unknown-risk", StringComparison.OrdinalIgnoreCase)));

        if ((!invokeReadonly && !invokeCandidateSafe && !allowUnknownRisk) || !canInvoke)
        {
            var result = new
            {
                schemaVersion = 1,
                status = tool == null ? "UNKNOWN" : tool.supportStatus == "needs-probe" ? "UNKNOWN" : "PASS",
                project,
                projectSha256 = Sha256(project),
                toolId,
                tool,
                invoked = false,
                reason = tool == null
                    ? "tool-id was not found in the supplied catalog"
                    : invokeReadonly || invokeCandidateSafe || allowUnknownRisk
                        ? "tool was not invoked because its safety class does not match the requested guarded invocation mode"
                        : "catalog/readback probe only; pass --invoke-readonly or --invoke-candidate-safe for guarded disposable-copy invocation",
                safetyClass,
                nextProbe = tool?.nextProbe ?? "Classify this tool by command id, UI context, and expected side effect; then run on throwaway candidate with before/after hash evidence."
            };
            File.WriteAllText(Path.Combine(outDir, "tool-probe.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
            Console.WriteLine("mcgs tool-probe: " + outDir);
            return result.status == "PASS" ? 0 : 2;
        }

        Process? process = null;
        IntPtr main = IntPtr.Zero;
        CanvasProbeSession? session = null;
        var commandId = tool!.commandId!.Value;
        var invocationError = "";
        var invoked = false;
        var projectCopy = "";
        var projectCopySha256Before = "";
        var projectCopySha256Initial = "";
        var forcedProcessKill = false;
        var forcedProcessKillError = "";
        var commandObserved = false;
        var newWindowObserved = false;
        var commandObservedWindows = Array.Empty<string>();
        object? postCommandAction = null;
        var requestedContext = (Opt(args, "--context") ?? "").ToLowerInvariant();
        var baselineAfterPreconditionExport = "";
        var baselineAfterPreconditionError = "";
        BaselineReferenceResult? baselineAfterPreconditionReference = null;
        try
        {
            var context = requestedContext;
            if (requestedContext is "animation-select-all" or "animation-clipboard-seed" or
                "animation-after-cut" or "animation-after-cut-undo" or
                "animation-after-paste" or "animation-after-paste-undo" or
                "animation-single-object" or "animation-single-after-cut" or
                "animation-single-after-cut-undo" or "animation-single-after-paste" or
                "animation-single-after-paste-undo" or "animation-draw-object" or
                "animation-draw-table")
                context = "animation";
            if (context == "animation" || tool.uiPath.Contains("动画组态", StringComparison.OrdinalIgnoreCase))
            {
                session = OpenCanvasProbeSession(args, outDir, "tool-probe", out process, out main);
                projectCopy = session.ProjectCopy;
                projectCopySha256Before = session.ProjectSha256;
                projectCopySha256Initial = projectCopySha256Before;
                ApplyAnimationToolProbePrecondition(args, requestedContext, outDir, process.Id, session);
            }
            else
            {
                var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
                var copyDir = Path.Combine(outDir, "tool-probe-copy");
                Directory.CreateDirectory(copyDir);
                projectCopy = Path.Combine(copyDir, Path.GetFileNameWithoutExtension(project) + "-tool-probe.MCE");
                File.Copy(project, projectCopy, overwrite: true);
                projectCopySha256Before = Sha256(projectCopy);
                projectCopySha256Initial = projectCopySha256Before;
                process = Process.Start(new ProcessStartInfo(editor, Quote(projectCopy))
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
                }) ?? throw new InvalidOperationException("Failed to start MCGS editor.");
                main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
                SetDialogEvidenceRoot(outDir);
                HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
                main = UiAutomation.FindMainWindow(process.Id);
                if (main == IntPtr.Zero)
                    throw new TimeoutException("MCGS main window disappeared while handling startup dialogs.");
                ApplyWorkbenchToolProbePrecondition(args, outDir, requestedContext, process.Id, main);
            }

            if (baselineAfterPrecondition)
            {
                try
                {
                    UiAutomation.SendCommand(main, 57603, send: true);
                    Thread.Sleep(800);
                    projectCopySha256Before = Sha256(projectCopy);
                    baselineAfterPreconditionExport = Path.Combine(outDir, "baseline-after-precondition-export");
                    MceExporter.Export(projectCopy, baselineAfterPreconditionExport);
                }
                catch (Exception ex)
                {
                    baselineAfterPreconditionError = ex.Message;
                }
            }

            var beforeWindows = UiAutomation.TopWindowsForPid(process!.Id).Select(WindowInfo.FromHandle).ToArray();
            CaptureProcessWindows(process!.Id, Path.Combine(outDir, "before-invoke"));
            try
            {
                UiAutomation.SendCommand(main, (uint)commandId, send: true, hiword: 0);
                invoked = true;
                if (session != null)
                    postCommandAction = ApplyAnimationToolProbePostCommandAction(args, requestedContext, outDir, process!.Id, session);
            }
            catch (Exception ex)
            {
                invocationError = ex.Message;
            }
            Thread.Sleep(800);
            CaptureProcessWindows(process.Id, Path.Combine(outDir, "after-invoke"));
            var beforeHandles = beforeWindows.Select(w => w.Handle).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var afterWindows = UiAutomation.TopWindowsForPid(process.Id).Select(WindowInfo.FromHandle).ToArray();
            var newWindows = afterWindows.Where(w => !beforeHandles.Contains(w.Handle)).ToArray();
            newWindowObserved = newWindows.Length > 0;
            commandObserved = invoked || newWindowObserved;
            commandObservedWindows = newWindows.Select(w => $"{w.Handle} {w.ClassName} {w.Text}").ToArray();
        }
        finally
        {
            if (process != null && !process.HasExited && main != IntPtr.Zero)
            {
                try { CloseEditorProcess(process.Id, main, saveIntent: saveAfterInvoke); } catch { }
                try { process.WaitForExit(5000); } catch { }
                if (!process.HasExited)
                {
                    try
                    {
                        forcedProcessKill = true;
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        forcedProcessKillError = ex.Message;
                    }
                }
            }
        }

        if (baselineAfterPrecondition && string.IsNullOrWhiteSpace(baselineAfterPreconditionExport))
        {
            baselineAfterPreconditionReference = TryCreateToolProbePreconditionReferenceBaseline(args, project, outDir, requestedContext, tool);
            if (baselineAfterPreconditionReference.Success)
            {
                baselineAfterPreconditionExport = baselineAfterPreconditionReference.ExportDir;
                projectCopySha256Before = baselineAfterPreconditionReference.ProjectSha256;
                if (!string.IsNullOrWhiteSpace(baselineAfterPreconditionError))
                    baselineAfterPreconditionError += " ; ";
                baselineAfterPreconditionError += "same-precondition reference baseline was created after the primary in-session export failed";
            }
            else if (string.IsNullOrWhiteSpace(baselineAfterPreconditionError))
            {
                baselineAfterPreconditionError = baselineAfterPreconditionReference.Error;
            }
            else
            {
                baselineAfterPreconditionError += " ; reference baseline failed: " + baselineAfterPreconditionReference.Error;
            }
        }

        var projectCopySha256After = TrySha256(projectCopy, out var projectCopySha256AfterError);
        var projectCopyHashChanged = !string.IsNullOrWhiteSpace(projectCopySha256Before) &&
                                     !string.IsNullOrWhiteSpace(projectCopySha256After) &&
                                     !string.Equals(projectCopySha256Before, projectCopySha256After, StringComparison.OrdinalIgnoreCase);
        var readOnlyHashDrift = tool.safetyClass.Equals("read-only", StringComparison.OrdinalIgnoreCase) && projectCopyHashChanged;
        object? normalizedDiffEvidence = null;
        var normalizedDiffError = "";
        var normalizedDiffEquivalent = false;
        var normalizedDiffEditorContextOnly = false;
        var readOnlyHashDriftClass = readOnlyHashDrift ? "unclassified" : "";
        var candidateSafeMutationFunctionalDiff = false;
        var candidateSafeMutationReversibleReturn = false;
        var baselineExport = Opt(args, "--baseline-export");
        var effectiveBaselineExport = !string.IsNullOrWhiteSpace(baselineAfterPreconditionExport)
            ? baselineAfterPreconditionExport
            : baselineExport;
        var candidateSafeCommand = tool.safetyClass.Equals("candidate-safe-mutation", StringComparison.OrdinalIgnoreCase);
        var candidateSafeMutation = candidateSafeCommand && projectCopyHashChanged;
        var unknownRiskHashDrift = tool.safetyClass.Equals("unknown-risk", StringComparison.OrdinalIgnoreCase) && projectCopyHashChanged;
        if ((readOnlyHashDrift || candidateSafeMutation || unknownRiskHashDrift) && !string.IsNullOrWhiteSpace(effectiveBaselineExport))
        {
            try
            {
                var normalizedRoot = Path.Combine(outDir, "normalized-diff");
                var candidateExport = Path.Combine(normalizedRoot, "candidate-export");
                MceExporter.Export(projectCopy, candidateExport);
                var diff = WriteMceNormalizedDiff(FullPath(effectiveBaselineExport!), candidateExport, normalizedRoot);
                normalizedDiffEquivalent = diff.Equivalent;
                normalizedDiffEditorContextOnly = diff.EditorContextOnly;
                readOnlyHashDriftClass = diff.Equivalent
                    ? "normalized-equivalent"
                    : diff.EditorContextOnly ? "editor-context-only" : "normalized-functional-diff";
                candidateSafeMutationFunctionalDiff = candidateSafeMutation &&
                                                      !diff.Equivalent &&
                                                      !diff.EditorContextOnly &&
                                                      diff.ChangedFileCount > 0;
                normalizedDiffEvidence = new
                {
                    baselineExport = FullPath(effectiveBaselineExport!),
                    baselineExportSource = !string.IsNullOrWhiteSpace(baselineAfterPreconditionExport)
                        ? "after-precondition"
                        : "argument",
                    candidateExport,
                    diffPath = Path.Combine(normalizedRoot, "mce-normalized-diff.json"),
                    diff.Status,
                    diff.Equivalent,
                    diff.ChangedFileCount,
                    diff.EditorContextOnly
                };
                candidateSafeMutationReversibleReturn =
                    candidateSafeCommand &&
                    IsCandidateSafeReversibleReturnProbe(commandId, requestedContext) &&
                    (diff.Equivalent || diff.EditorContextOnly);
            }
            catch (Exception ex)
            {
                normalizedDiffError = ex.Message;
            }
        }
        if (candidateSafeCommand &&
            IsCandidateSafeReversibleReturnProbe(commandId, requestedContext) &&
            (!projectCopyHashChanged || normalizedDiffEquivalent || normalizedDiffEditorContextOnly))
        {
            candidateSafeMutationReversibleReturn = true;
        }
        var candidateSafeMutationUnexpectedFunctionalDiff =
            candidateSafeCommand &&
            IsCandidateSafeReversibleReturnProbe(commandId, requestedContext) &&
            candidateSafeMutationFunctionalDiff &&
            !candidateSafeMutationReversibleReturn;
        var readOnlyHashDriftExplained = readOnlyHashDrift &&
                                         (readOnlyHashDriftClass.Equals("editor-context-only", StringComparison.OrdinalIgnoreCase) ||
                                          readOnlyHashDriftClass.Equals("normalized-equivalent", StringComparison.OrdinalIgnoreCase));
        var unknownRiskHashDriftExplained = unknownRiskHashDrift &&
                                            (readOnlyHashDriftClass.Equals("editor-context-only", StringComparison.OrdinalIgnoreCase) ||
                                             readOnlyHashDriftClass.Equals("normalized-equivalent", StringComparison.OrdinalIgnoreCase));
        var unknownRiskHashDriftUnclassified = unknownRiskHashDrift && normalizedDiffEvidence == null;
        var unknownRiskHashDriftFunctional = unknownRiskHashDrift &&
                                             normalizedDiffEvidence != null &&
                                             !unknownRiskHashDriftExplained;
        var candidateSafeMutationUnclassified = candidateSafeMutation && normalizedDiffEvidence == null;
        var candidateSafeMutationNotFunctional = candidateSafeMutation &&
                                                 normalizedDiffEvidence != null &&
                                                 !candidateSafeMutationFunctionalDiff &&
                                                 !candidateSafeMutationReversibleReturn;
        var candidateSafePreconditionUnmet = candidateSafeCommand &&
                                             !candidateSafeMutation &&
                                             !newWindowObserved &&
                                             !candidateSafeMutationReversibleReturn;
        var invocationBlockingError = (!string.IsNullOrWhiteSpace(invocationError) && !commandObserved);
        var invokedStatus = commandObserved && !invocationBlockingError &&
                            (!readOnlyHashDrift || readOnlyHashDriftExplained) &&
                            !candidateSafeMutationUnexpectedFunctionalDiff &&
                            !candidateSafeMutationUnclassified &&
                            !candidateSafeMutationNotFunctional &&
                            !candidateSafePreconditionUnmet &&
                            !unknownRiskHashDriftUnclassified &&
                            !unknownRiskHashDriftFunctional
            ? "PASS"
            : "UNKNOWN";
        var invokedResult = new
        {
            schemaVersion = 1,
            status = invokedStatus,
            project,
            projectSha256 = Sha256(project),
            toolId,
            tool,
            invoked,
            commandId,
            context = Opt(args, "--context") ?? "",
            invocationError,
            invocationBlockingError,
            commandObserved,
            newWindowObserved,
            commandObservedWindows,
            evidence = new
            {
                before = "before-invoke",
                after = "after-invoke",
                projectCopy,
                projectCopySha256Initial,
                projectCopySha256Before,
                projectCopySha256After,
                projectCopySha256AfterError,
                baselineAfterPrecondition,
                baselineAfterPreconditionExport,
                baselineAfterPreconditionError,
                baselineAfterPreconditionReference,
                projectCopyHashChanged,
                saveAfterInvoke,
                readOnlyHashDrift,
                readOnlyHashDriftClass,
                candidateSafeMutation,
                candidateSafeMutationFunctionalDiff,
                candidateSafeMutationReversibleReturn,
                candidateSafeMutationUnexpectedFunctionalDiff,
                candidateSafeMutationUnclassified,
                candidateSafeMutationNotFunctional,
                candidateSafePreconditionUnmet,
                unknownRiskHashDrift,
                unknownRiskHashDriftExplained,
                unknownRiskHashDriftUnclassified,
                unknownRiskHashDriftFunctional,
                normalizedDiff = normalizedDiffEvidence,
                normalizedDiffError,
                forcedProcessKill,
                forcedProcessKillError
            },
            postCommandAction,
            safetyClass = tool.safetyClass,
            nextProbe = candidateSafePreconditionUnmet
                ? "Candidate-safe command produced no modal/dialog evidence, reversible-return evidence, or normalized candidate diff; open the required editor selection/context before treating this tool as probed."
                : candidateSafeMutationUnexpectedFunctionalDiff
                ? "Undo probe produced a functional normalized diff instead of returning to the pre-edit baseline; rerun with a controlled paste/undo context and compare normalized MCE evidence."
                : unknownRiskHashDriftUnclassified
                ? "Unknown-risk tool changed the disposable copy hash; rerun with --baseline-export so normalized MCE diff can prove whether the drift is editor-context-only."
                : unknownRiskHashDriftFunctional
                ? "Unknown-risk tool produced a functional normalized MCE diff; keep it unresolved until a dedicated reversible candidate-safe probe and readback evidence explain the side effect."
                : candidateSafeMutationUnclassified
                ? "Candidate-safe tool changed the disposable copy hash; rerun with --baseline-export so normalized MCE diff records the candidate mutation."
                : candidateSafeMutationNotFunctional
                ? "Candidate-safe tool changed only editor-context or normalized-equivalent evidence; rerun in the exact editor precondition and require a functional normalized MCE diff before treating it as probed."
                : readOnlyHashDrift && !readOnlyHashDriftExplained
                ? "Read-only tool probe changed the disposable copy hash; compare against an open-close baseline or implement normalized MCE diff before marking the invocation understood."
                : readOnlyHashDriftExplained
                    ? ""
                    : invoked ? "" : tool.nextProbe
        };
        File.WriteAllText(Path.Combine(outDir, "tool-probe.json"), JsonSerializer.Serialize(invokedResult, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("mcgs tool-probe: " + outDir);
        return invokedResult.status == "PASS" ? 0 : 2;
    }

    private static BaselineReferenceResult TryCreateToolProbePreconditionReferenceBaseline(string[] args, string project, string outDir, string requestedContext, McgsToolEntry tool)
    {
        if (requestedContext.StartsWith("animation-", StringComparison.OrdinalIgnoreCase) ||
            requestedContext.Equals("animation", StringComparison.OrdinalIgnoreCase) ||
            tool.uiPath.Contains("\u52A8\u753B\u7EC4\u6001", StringComparison.OrdinalIgnoreCase))
        {
            return new BaselineReferenceResult(false, "", "", "", "after-precondition reference baseline is not implemented for animation canvas contexts");
        }

        Process? process = null;
        var main = IntPtr.Zero;
        var copyDir = Path.Combine(outDir, "baseline-after-precondition-copy");
        var exportDir = Path.Combine(outDir, "baseline-after-precondition-export");
        var projectCopy = "";
        try
        {
            Directory.CreateDirectory(copyDir);
            projectCopy = Path.Combine(copyDir, Path.GetFileNameWithoutExtension(project) + "-baseline-precondition.MCE");
            File.Copy(project, projectCopy, overwrite: true);
            var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
            process = Process.Start(new ProcessStartInfo(editor, Quote(projectCopy))
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(editor) ?? Environment.CurrentDirectory
            }) ?? throw new InvalidOperationException("Failed to start MCGS editor for after-precondition reference baseline.");
            main = WaitForMainWindow(process.Id, TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20)));
            SetDialogEvidenceRoot(outDir);
            HandleStartupDialogs(process.Id, TimeSpan.FromSeconds(10));
            main = UiAutomation.FindMainWindow(process.Id);
            if (main == IntPtr.Zero)
                throw new TimeoutException("MCGS main window disappeared while creating after-precondition reference baseline.");

            ApplyWorkbenchToolProbePrecondition(args, Path.Combine(outDir, "baseline-after-precondition-reference"), requestedContext, process.Id, main);
            UiAutomation.SendCommand(main, 57603, send: true);
            Thread.Sleep(1000);
            CloseEditorProcess(process.Id, main, saveIntent: true);
            process.WaitForExit(7000);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                throw new InvalidOperationException("MCGS editor did not exit cleanly after reference baseline creation.");
            }

            var sha = Sha256(projectCopy);
            MceExporter.Export(projectCopy, exportDir);
            return new BaselineReferenceResult(true, projectCopy, sha, exportDir, "");
        }
        catch (Exception ex)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch { }
            return new BaselineReferenceResult(false, projectCopy, "", exportDir, ex.Message);
        }
    }

    private sealed record BaselineReferenceResult(bool Success, string ProjectCopy, string ProjectSha256, string ExportDir, string Error);

    private static void ApplyAnimationToolProbePrecondition(string[] args, string context, string outDir, int pid, CanvasProbeSession session)
    {
        if (string.IsNullOrWhiteSpace(context) || context == "animation")
            return;
        if (context is not ("animation-select-all" or "animation-clipboard-seed" or
            "animation-after-cut" or "animation-after-cut-undo" or
            "animation-after-paste" or "animation-after-paste-undo" or
            "animation-single-object" or "animation-single-after-cut" or
            "animation-single-after-cut-undo" or "animation-single-after-paste" or
            "animation-single-after-paste-undo"))
            return;

        var contextDir = Path.Combine(outDir, "precondition-" + context);
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before"));
        var rect = UiAutomation.GetWindowRect(session.Canvas);
        var selectedAll = true;
        object? selectedObject = null;
        if (context.StartsWith("animation-single-", StringComparison.OrdinalIgnoreCase) &&
            TryReadAnimationSelection(args, out var sx, out var sy, out selectedObject))
        {
            UiAutomation.ClickPoint(session.Canvas, Math.Max(5, sx), Math.Max(5, sy),
                MouseButton.Left, doubleClick: false, mouse: true);
            selectedAll = false;
        }
        else
        {
            UiAutomation.ClickPoint(session.Canvas, Math.Max(5, rect.Width / 2), Math.Max(5, rect.Height / 2),
                MouseButton.Left, doubleClick: false, mouse: true);
        }
        Thread.Sleep(150);
        if (selectedAll)
        {
            SendKeys.SendWait("^a");
            Thread.Sleep(250);
        }
        var clipboardSeeded = false;
        var cutApplied = false;
        var pasteApplied = false;
        var undoApplied = false;
        var clipboardFormatsAfterSeed = new List<object>();
        if (context is "animation-clipboard-seed" or "animation-after-paste" or "animation-after-paste-undo" or
            "animation-single-after-paste" or "animation-single-after-paste-undo")
        {
            UiAutomation.SendCommand(session.Main, 57634, send: true);
            clipboardSeeded = true;
            Thread.Sleep(500);
            var data = Clipboard.GetDataObject();
            if (data != null)
            {
                foreach (var format in data.GetFormats(false))
                    clipboardFormatsAfterSeed.Add(DescribeClipboardFormat(data, format, rect).Summary);
            }
        }
        if (context is "animation-after-cut" or "animation-after-cut-undo" or
            "animation-single-after-cut" or "animation-single-after-cut-undo")
        {
            UiAutomation.SendCommand(session.Main, 57635, send: true);
            cutApplied = true;
            Thread.Sleep(500);
        }
        if (context is "animation-after-paste" or "animation-after-paste-undo" or
            "animation-single-after-paste" or "animation-single-after-paste-undo")
        {
            UiAutomation.SendCommand(session.Main, 57637, send: true);
            pasteApplied = true;
            Thread.Sleep(500);
        }
        if (context is "animation-after-cut-undo" or "animation-single-after-cut-undo")
        {
            UiAutomation.SendCommand(session.Main, 57643, send: true);
            undoApplied = true;
            Thread.Sleep(500);
        }
        if (context is "animation-after-paste-undo" or "animation-single-after-paste-undo")
        {
            UiAutomation.SendCommand(session.Main, 57643, send: true);
            undoApplied = true;
            Thread.Sleep(500);
        }
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after"));
        File.WriteAllText(Path.Combine(contextDir, "precondition.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            context,
            canvas = WindowInfo.FromHandle(session.Canvas),
            selectedAll,
            selectedObject,
            clipboardSeeded,
            clipboardFormatsAfterSeed,
            cutApplied,
            pasteApplied,
            undoApplied
        }, JsonOptions()), Encoding.UTF8);
    }

    private static object? ApplyAnimationToolProbePostCommandAction(string[] args, string context, string outDir, int pid, CanvasProbeSession session)
    {
        if (context is not ("animation-draw-object" or "animation-draw-table"))
            return null;

        var actionDir = Path.Combine(outDir, "post-command-" + context);
        Directory.CreateDirectory(actionDir);
        CaptureProcessWindows(pid, Path.Combine(actionDir, "before-draw"));

        var canvasRect = UiAutomation.GetWindowRect(session.Canvas);
        var x = ParseInt(args, "--draw-x", Math.Max(20, Math.Min(620, Math.Max(20, canvasRect.Width - 220))));
        var y = ParseInt(args, "--draw-y", Math.Max(20, Math.Min(360, Math.Max(20, canvasRect.Height - 140))));
        var width = ParseInt(args, "--draw-width", context == "animation-draw-table" ? 180 : 120);
        var height = ParseInt(args, "--draw-height", context == "animation-draw-table" ? 100 : 70);
        width = Math.Max(20, Math.Min(width, Math.Max(20, canvasRect.Width - x - 5)));
        height = Math.Max(20, Math.Min(height, Math.Max(20, canvasRect.Height - y - 5)));
        x = Math.Max(2, Math.Min(x, Math.Max(2, canvasRect.Width - width - 2)));
        y = Math.Max(2, Math.Min(y, Math.Max(2, canvasRect.Height - height - 2)));

        var dragError = "";
        var activated = false;
        try
        {
            UiAutomation.DragPoint(session.Canvas, x, y, x + width, y + height, mouse: true);
            Thread.Sleep(900);
            if (Has(args, "--activate-drawn-object"))
            {
                UiAutomation.ClickPoint(session.Canvas, x + width / 2, y + height / 2, MouseButton.Left,
                    doubleClick: context == "animation-draw-table", mouse: true);
                activated = true;
                Thread.Sleep(500);
            }
        }
        catch (Exception ex)
        {
            dragError = ex.Message;
        }

        CaptureProcessWindows(pid, Path.Combine(actionDir, "after-draw"));
        var evidence = new
        {
            schemaVersion = 1,
            context,
            action = "drag after selecting drawing tool",
            canvas = WindowInfo.FromHandle(session.Canvas),
            rect = new { x, y, width, height },
            activated,
            dragError
        };
        File.WriteAllText(Path.Combine(actionDir, "post-command-action.json"),
            JsonSerializer.Serialize(evidence, JsonOptions()), Encoding.UTF8);
        return evidence;
    }

    private static bool IsCandidateSafeReversibleReturnProbe(int commandId, string context)
        => commandId == 57643 && context is "animation-after-cut" or "animation-single-after-cut" or
            "animation-after-paste" or "animation-single-after-paste" or "strategy-after-add";

    private static bool TryReadAnimationSelection(string[] args, out int centerX, out int centerY, out object? selectedObject)
    {
        centerX = 0;
        centerY = 0;
        selectedObject = null;
        var mapPath = Opt(args, "--object-map") ?? Opt(args, "--property-map") ?? Opt(args, "--semantic-map");
        if (string.IsNullOrWhiteSpace(mapPath))
            return false;
        var fullPath = FullPath(mapPath);
        if (!File.Exists(fullPath))
            return false;
        var wantedId = Opt(args, "--object-id") ?? "";
        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath, Encoding.UTF8));
        if (!doc.RootElement.TryGetProperty("objects", out var objects) || objects.ValueKind != JsonValueKind.Array)
            return false;

        JsonElement? chosen = null;
        foreach (var obj in objects.EnumerateArray())
        {
            var objectId = JsonStringAny(obj, "objectId", "id", "ObjectId", "Id") ?? "";
            if (!string.IsNullOrWhiteSpace(wantedId) &&
                objectId.Equals(wantedId, StringComparison.OrdinalIgnoreCase))
            {
                chosen = obj;
                break;
            }
            if (string.IsNullOrWhiteSpace(wantedId) && chosen == null)
            {
                var kind = JsonStringAny(obj, "semanticKind", "kind", "SemanticKind", "Kind") ?? "";
                if (kind.Contains("button", StringComparison.OrdinalIgnoreCase) ||
                    kind.Contains("label", StringComparison.OrdinalIgnoreCase) ||
                    kind.Contains("static", StringComparison.OrdinalIgnoreCase))
                {
                    chosen = obj;
                }
            }
        }
        if (chosen == null && objects.GetArrayLength() > 0)
            chosen = objects.EnumerateArray().First();
        if (chosen == null)
            return false;

        var chosenElement = chosen.Value;
        if (!chosenElement.TryGetProperty("rect", out var rect) || rect.ValueKind != JsonValueKind.Object)
            return false;
        var x = LayoutJsonIntAny(rect, "x", "X");
        var y = LayoutJsonIntAny(rect, "y", "Y");
        var width = LayoutJsonIntAny(rect, "width", "Width");
        var height = LayoutJsonIntAny(rect, "height", "Height");
        if (x == null || y == null || width == null || height == null || width <= 0 || height <= 0)
            return false;
        centerX = x.Value + Math.Max(1, width.Value / 2);
        centerY = y.Value + Math.Max(1, height.Value / 2);
        selectedObject = new
        {
            objectMap = fullPath,
            objectId = JsonStringAny(chosenElement, "objectId", "id", "ObjectId", "Id") ?? "",
            semanticKind = JsonStringAny(chosenElement, "semanticKind", "kind", "SemanticKind", "Kind") ?? "",
            displayedText = JsonStringAny(chosenElement, "displayedText", "text", "DisplayedText", "Text") ?? "",
            rect = new { x, y, width, height },
            center = new { x = centerX, y = centerY }
        };
        return true;
    }

    private static void ApplyWorkbenchToolProbePrecondition(string[] args, string outDir, string context, int pid, IntPtr main)
    {
        if (string.IsNullOrWhiteSpace(context))
            return;
        switch (context)
        {
            case "menu-editor":
                ApplyMenuEditorToolProbePrecondition(args, outDir, pid, main);
                return;
            case "strategy-editor":
                ApplyStrategyEditorToolProbePrecondition(args, outDir, pid, main);
                return;
            case "strategy-after-add":
            case "strategy-after-distinct-toolbox":
            case "strategy-after-distinct-drag":
                ApplyStrategyAfterAddToolProbePrecondition(args, outDir, pid, main);
                return;
            case "device-editor":
            case "device-after-copy-paste":
                ApplyDeviceEditorToolProbePrecondition(args, outDir, pid, main);
                return;
            case "animation":
            case "animation-select-all":
            case "animation-clipboard-seed":
            case "animation-after-cut":
            case "animation-after-cut-undo":
            case "animation-after-paste":
            case "animation-after-paste-undo":
            case "animation-single-object":
            case "animation-single-after-paste":
            case "animation-single-after-paste-undo":
            case "animation-draw-object":
            case "animation-draw-table":
                return;
            default:
                throw new InvalidOperationException("Unsupported mcgs tool-probe --context: " + context);
        }
    }

    private static void ApplyMenuEditorToolProbePrecondition(string[] args, string outDir, int pid, IntPtr main)
    {
        var contextDir = Path.Combine(outDir, "precondition-menu-editor");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before"));

        UiAutomation.SendCommand(main, 33953);
        Thread.Sleep(700);
        TrySelectWorkbenchTab(main, 0);
        var list = FindVisibleListViewContaining(main, "\u4E3B\u63A7\u7A97\u53E3", minItems: 1);
        UiAutomation.ListViewSelectIndex(list, 0);
        Thread.Sleep(250);
        WriteListViewSummary(main, Path.Combine(contextDir, "menu-listviews.json"));
        CaptureProcessWindows(pid, Path.Combine(contextDir, "main-control-workbench"));

        if (!ClickButtonByNormalizedText(main, mouse: true, "\u83DC\u5355\u7EC4\u6001"))
            throw new InvalidOperationException("Menu configuration button was not found.");
        Thread.Sleep(900);

        var tree = WaitForTreeViewWithItems(main, TimeSpan.FromSeconds(5));
        var selected = SelectOptionalTreeItem(args, tree, "--menu-tree-index", "--menu-tree-text");
        File.WriteAllLines(Path.Combine(contextDir, "menu-editor.window-tree.txt"), UiAutomation.WindowTreeLines(main), Encoding.UTF8);
        File.WriteAllText(Path.Combine(contextDir, "menu-tree.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            tree = WindowInfo.FromHandle(tree),
            items = UiAutomation.TreeViewItems(tree),
            selected
        }, JsonOptions()), Encoding.UTF8);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-menu-editor"));
    }

    private static void ApplyStrategyEditorToolProbePrecondition(string[] args, string outDir, int pid, IntPtr main)
    {
        var contextDir = Path.Combine(outDir, "precondition-strategy-editor");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before"));

        UiAutomation.SendCommand(main, 33060);
        Thread.Sleep(700);
        TrySelectWorkbenchTab(main, 4);
        var list = FindVisibleListViewContaining(main, "\u5FAA\u73AF\u7B56\u7565", minItems: 1);
        UiAutomation.ListViewSelectText(list, "\u5FAA\u73AF\u7B56\u7565");
        Thread.Sleep(250);
        WriteListViewSummary(main, Path.Combine(contextDir, "strategy-listviews.json"));
        CaptureProcessWindows(pid, Path.Combine(contextDir, "strategy-workbench"));

        if (!ClickButtonByNormalizedText(main, mouse: true, "\u7B56\u7565\u7EC4\u6001"))
            throw new InvalidOperationException("Strategy configuration button was not found.");
        Thread.Sleep(900);
        var selectedLine = SelectOptionalStrategyLine(args, main, contextDir);
        File.WriteAllLines(Path.Combine(contextDir, "strategy-editor.window-tree.txt"), UiAutomation.WindowTreeLines(main), Encoding.UTF8);
        File.WriteAllText(Path.Combine(contextDir, "strategy-editor.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            selectedLine
        }, JsonOptions()), Encoding.UTF8);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-strategy-editor"));
    }

    private static void ApplyStrategyAfterAddToolProbePrecondition(string[] args, string outDir, int pid, IntPtr main)
    {
        ApplyStrategyEditorToolProbePrecondition(args, outDir, pid, main);
        var context = (Opt(args, "--context") ?? "").ToLowerInvariant();
        if (context == "strategy-after-distinct-toolbox")
        {
            ApplyStrategyDistinctToolboxFixture(args, outDir, pid, main);
            return;
        }
        if (context == "strategy-after-distinct-drag")
        {
            ApplyStrategyDistinctDragFixture(args, outDir, pid, main);
            return;
        }
        var contextDir = Path.Combine(outDir, "precondition-strategy-after-add");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before-add"));
        UiAutomation.SendCommand(main, 32851, send: true);
        Thread.Sleep(700);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-add"));
        File.WriteAllText(Path.Combine(contextDir, "precondition.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            context = "strategy-after-add",
            setupCommandId = 32851,
            setupAction = "add strategy policy line before probing undo"
        }, JsonOptions()), Encoding.UTF8);
    }

    private static void ApplyStrategyDistinctToolboxFixture(string[] args, string outDir, int pid, IntPtr main)
    {
        var contextDir = Path.Combine(outDir, "precondition-strategy-after-distinct-toolbox");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before-toolbox-insert"));

        var toolbox = FindVisibleListViewContainingInProcess(pid, "\u811A\u672C\u7A0B\u5E8F", minItems: 3);
        var beforeItems = UiAutomation.ListViewItems(toolbox);
        File.WriteAllText(Path.Combine(contextDir, "toolbox-items-before.json"),
            JsonSerializer.Serialize(beforeItems, JsonOptions()), Encoding.UTF8);
        var requestedItems = Opts(args, "--strategy-toolbox-item").ToArray();
        if (requestedItems.Length == 0)
            requestedItems = new[] { "\u811A\u672C\u7A0B\u5E8F", "\u8BA1\u6570\u5668" };

        var actions = new List<object>();
        for (var i = 0; i < requestedItems.Length; i++)
        {
            var item = requestedItems[i];
            var beforeDialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770")
                .Select(WindowInfo.FromHandle)
                .ToArray();
            UiAutomation.ListViewDoubleClickText(toolbox, item, mouse: true);
            Thread.Sleep(900);
            CaptureProcessWindows(pid, Path.Combine(contextDir, $"after-toolbox-{i:D2}-{SafeFile(item)}"));
            var afterDialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770")
                .Select(WindowInfo.FromHandle)
                .ToArray();
            var newDialogs = afterDialogs
                .Where(after => !beforeDialogs.Any(before => before.Handle.Equals(after.Handle, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            actions.Add(new
            {
                item,
                newDialogs
            });
            if (newDialogs.Length > 0)
                throw new InvalidOperationException("Strategy toolbox item opened a modal dialog before a deterministic fixture was created: " + item);
        }

        UiAutomation.SendCommand(main, 57603, send: true);
        Thread.Sleep(1000);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-fixture-save"));
        var selectedLine = SelectOptionalStrategyLine(args, main, contextDir);
        File.WriteAllText(Path.Combine(contextDir, "distinct-toolbox-result.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            context = "strategy-after-distinct-toolbox",
            insertionMethod = "double-click standard strategy toolbox ListView items",
            requestedItems,
            actions,
            selectedLine
        }, JsonOptions()), Encoding.UTF8);
    }

    private static void ApplyStrategyDistinctDragFixture(string[] args, string outDir, int pid, IntPtr main)
    {
        var contextDir = Path.Combine(outDir, "precondition-strategy-after-distinct-drag");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before-distinct-drag"));

        var addCount = ParseInt(args, "--strategy-line-count", 2);
        var addActions = new List<object>();
        for (var i = 0; i < addCount; i++)
        {
            UiAutomation.SendCommand(main, 32851, send: true);
            Thread.Sleep(650);
            CaptureProcessWindows(pid, Path.Combine(contextDir, $"after-add-line-{i:D2}"));
            addActions.Add(new { index = i, commandId = 32851 });
        }

        var toolbox = FindVisibleListViewContainingInProcess(pid, "\u811A\u672C\u7A0B\u5E8F", minItems: 3);
        var toolboxItems = UiAutomation.ListViewItems(toolbox);
        File.WriteAllText(Path.Combine(contextDir, "toolbox-items.json"),
            JsonSerializer.Serialize(toolboxItems, JsonOptions()), Encoding.UTF8);

        var frame = UiAutomation.EnumerateChildren(main)
            .Select(h => new { Handle = h, Info = WindowInfo.FromHandle(h) })
            .Where(h => h.Info.ClassName.Contains("AfxFrameOrView", StringComparison.OrdinalIgnoreCase) &&
                        h.Info.Text.Contains("\u7B56\u7565\u7EC4\u6001", StringComparison.OrdinalIgnoreCase) &&
                        h.Info.Width > 300 && h.Info.Height > 200)
            .OrderByDescending(h => h.Info.Width * h.Info.Height)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Visible strategy editor frame was not found for drag fixture.");

        var requestedItems = Opts(args, "--strategy-toolbox-item").ToArray();
        if (requestedItems.Length == 0)
            requestedItems = new[] { "\u811A\u672C\u7A0B\u5E8F", "\u8BA1\u6570\u5668" };

        var targetX = ParseInt(args, "--strategy-drop-x", 430);
        var targetY0 = ParseInt(args, "--strategy-drop-y", 92);
        var targetSpacing = ParseInt(args, "--strategy-line-spacing", 56);
        var dragActions = new List<object>();
        for (var i = 0; i < requestedItems.Length; i++)
        {
            var itemText = requestedItems[i];
            var item = toolboxItems.FirstOrDefault(t =>
                t.Texts.Any(text => text.Contains(itemText, StringComparison.OrdinalIgnoreCase)))
                ?? throw new InvalidOperationException("Strategy toolbox item not found: " + itemText);
            var toolboxRect = UiAutomation.GetWindowRect(toolbox);
            var frameRect = UiAutomation.GetWindowRect(frame.Handle);
            var startX = toolboxRect.Left + item.Rect.Left + Math.Max(8, Math.Min(40, item.Rect.Width / 2));
            var startY = toolboxRect.Top + item.Rect.Top + Math.Max(2, item.Rect.Height / 2);
            var endX = frameRect.Left + targetX;
            var endY = frameRect.Top + targetY0 + i * targetSpacing;

            var beforeDialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770")
                .Select(WindowInfo.FromHandle)
                .ToArray();
            DragScreenPoint(startX, startY, endX, endY);
            Thread.Sleep(900);
            CaptureProcessWindows(pid, Path.Combine(contextDir, $"after-drag-{i:D2}-{SafeFile(itemText)}"));
            var afterDialogs = UiAutomation.TopWindowsForPid(pid)
                .Where(h => Native.GetClass(h) == "#32770")
                .Select(WindowInfo.FromHandle)
                .ToArray();
            var newDialogs = afterDialogs
                .Where(after => !beforeDialogs.Any(before => before.Handle.Equals(after.Handle, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            dragActions.Add(new
            {
                item = itemText,
                source = new { hwnd = "0x" + toolbox.ToInt64().ToString("X"), x = startX, y = startY, item.Rect },
                target = new { hwnd = frame.Info.Handle, x = endX, y = endY, relativeX = targetX, relativeY = targetY0 + i * targetSpacing },
                newDialogs
            });
            if (newDialogs.Length > 0)
                throw new InvalidOperationException("Strategy drag opened a modal dialog before deterministic fixture completion: " + itemText);
        }

        UiAutomation.SendCommand(main, 57603, send: true);
        Thread.Sleep(1000);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-distinct-drag-save"));
        var selectedLine = SelectOptionalStrategyLine(args, main, contextDir);
        File.WriteAllText(Path.Combine(contextDir, "distinct-drag-result.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            context = "strategy-after-distinct-drag",
            insertionMethod = "add policy lines with command 32851, then drag toolbox ListView items to strategy frame slots",
            addActions,
            requestedItems,
            frame = frame.Info,
            dropPlan = new { targetX, targetY0, targetSpacing },
            dragActions,
            selectedLine
        }, JsonOptions()), Encoding.UTF8);
    }

    private static void DragScreenPoint(int startX, int startY, int endX, int endY)
    {
        Native.SetCursorPos(startX, startY);
        Thread.Sleep(100);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(120);
        Native.SetCursorPos(endX, endY);
        Native.MouseEvent(Native.MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(180);
        Native.MouseEvent(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    private static void ApplyDeviceEditorToolProbePrecondition(string[] args, string outDir, int pid, IntPtr main)
    {
        var contextDir = Path.Combine(outDir, "precondition-device-editor");
        Directory.CreateDirectory(contextDir);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "before"));

        var timeout = TimeSpan.FromSeconds(ParseInt(args, "--timeout", 20));
        var deviceText = Opt(args, "--device-text") ?? "Smart200";
        var tree = EnterDeviceConfiguration(pid, main, deviceText, timeout);
        Thread.Sleep(300);
        object selected;
        var context = (Opt(args, "--context") ?? "").ToLowerInvariant();
        if (context == "device-after-copy-paste")
            selected = ApplyDeviceCopyPasteFixture(args, contextDir, pid, main, tree, deviceText, timeout);
        else
            selected = SelectOptionalTreeItem(args, tree, "--device-tree-index", "--device-tree-text");
        File.WriteAllLines(Path.Combine(contextDir, "device-editor.window-tree.txt"), UiAutomation.WindowTreeLines(main), Encoding.UTF8);
        File.WriteAllText(Path.Combine(contextDir, "device-tree.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            context,
            deviceText,
            tree = WindowInfo.FromHandle(tree),
            items = UiAutomation.TreeViewItems(tree),
            selected
        }, JsonOptions()), Encoding.UTF8);
        CaptureProcessWindows(pid, Path.Combine(contextDir, "after-device-editor"));
    }

    private static object ApplyDeviceCopyPasteFixture(string[] args, string contextDir, int pid, IntPtr main,
        IntPtr tree, string deviceText, TimeSpan timeout)
    {
        var fixtureDir = Path.Combine(contextDir, "copy-paste-fixture");
        Directory.CreateDirectory(fixtureDir);
        var beforeItems = UiAutomation.TreeViewItems(tree);
        File.WriteAllText(Path.Combine(fixtureDir, "tree-before.json"),
            JsonSerializer.Serialize(beforeItems, JsonOptions()), Encoding.UTF8);

        var source = beforeItems.FirstOrDefault(i => i.Text.Contains(deviceText, StringComparison.OrdinalIgnoreCase))
                     ?? beforeItems.FirstOrDefault()
                     ?? throw new InvalidOperationException("Device copy/paste fixture cannot find a source device tree item.");
        var sourceHandle = ParseHwnd(source.Handle);
        UiAutomation.TreeViewSelect(tree, sourceHandle);
        var rect = UiAutomation.TreeViewItemRect(tree, sourceHandle);
        UiAutomation.ClickPoint(tree, Math.Max(2, rect.Left + Math.Max(1, rect.Width / 2)),
            Math.Max(2, rect.Top + Math.Max(1, rect.Height / 2)), MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(250);
        CaptureProcessWindows(pid, Path.Combine(fixtureDir, "after-source-select"));

        SendKeys.SendWait("^c");
        Thread.Sleep(300);
        CaptureProcessWindows(pid, Path.Combine(fixtureDir, "after-copy"));

        SendKeys.SendWait("^v");
        Thread.Sleep(1000);
        CaptureProcessWindows(pid, Path.Combine(fixtureDir, "after-paste"));
        RecordOpenPopups(pid, "device-copy-paste");

        tree = WaitForTreeViewWithItems(main, timeout);
        var afterItems = UiAutomation.TreeViewItems(tree);
        File.WriteAllText(Path.Combine(fixtureDir, "tree-after.json"),
            JsonSerializer.Serialize(afterItems, JsonOptions()), Encoding.UTF8);

        var newItems = afterItems
            .Where(after => !beforeItems.Any(before => before.Text.Equals(after.Text, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var created = afterItems.Length > beforeItems.Length;
        File.WriteAllText(Path.Combine(fixtureDir, "copy-paste-result.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            action = "select device tree item, Ctrl+C, Ctrl+V",
            beforeCount = beforeItems.Length,
            afterCount = afterItems.Length,
            created,
            source,
            newItems
        }, JsonOptions()), Encoding.UTF8);

        if (!created)
            throw new InvalidOperationException("Device copy/paste fixture did not create a second visible device tree item; see " + fixtureDir);

        UiAutomation.SendCommand(main, 57603, send: true);
        Thread.Sleep(1000);
        File.WriteAllText(Path.Combine(fixtureDir, "fixture-save.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            commandId = 57603,
            action = "save device copy/paste fixture before probing reorder commands"
        }, JsonOptions()), Encoding.UTF8);
        CaptureProcessWindows(pid, Path.Combine(fixtureDir, "after-fixture-save"));

        var preferredIndex = OptInt(args, "--device-tree-index") ??
                             (DeviceMoveCommandFromArgs(args) == 33050 ? 0 : Math.Min(1, afterItems.Length - 1));
        if (preferredIndex < 0 || preferredIndex >= afterItems.Length)
            throw new ArgumentOutOfRangeException("--device-tree-index", "Device tree index is outside the visible item range after copy/paste.");
        var selected = afterItems[preferredIndex];
        UiAutomation.TreeViewSelect(tree, ParseHwnd(selected.Handle));
        Thread.Sleep(300);
        CaptureProcessWindows(pid, Path.Combine(fixtureDir, "after-target-select"));
        return new
        {
            mode = "device-copy-paste-fixture",
            selectedIndex = preferredIndex,
            selected,
            beforeCount = beforeItems.Length,
            afterCount = afterItems.Length,
            newItems
        };
    }

    private static int DeviceMoveCommandFromArgs(string[] args)
    {
        var toolId = Opt(args, "--tool-id") ?? "";
        var match = Regex.Match(toolId, @":(?<id>\d+)$");
        return match.Success && int.TryParse(match.Groups["id"].Value, out var parsed) ? parsed : 0;
    }

    private static void TrySelectWorkbenchTab(IntPtr main, int index)
    {
        var tab = UiAutomation.EnumerateChildren(main)
            .FirstOrDefault(h => Native.GetClass(h).Equals("SysTabControl32", StringComparison.OrdinalIgnoreCase) &&
                                 UiAutomation.TabItems(h).Length > index);
        if (tab != IntPtr.Zero)
        {
            UiAutomation.TabSelectIndex(tab, index, mouse: true);
            Thread.Sleep(250);
        }
    }

    private static IntPtr FindVisibleListViewContaining(IntPtr root, string text, int minItems)
    {
        foreach (var list in UiAutomation.EnumerateChildren(root)
                     .Where(h => Native.GetClass(h).Equals("SysListView32", StringComparison.OrdinalIgnoreCase) &&
                                 Native.IsWindowVisible(h)))
        {
            try
            {
                var items = UiAutomation.ListViewItems(list);
                if (items.Length >= minItems && items.Any(i => i.Texts.Any(t => t.Contains(text, StringComparison.OrdinalIgnoreCase))))
                    return list;
            }
            catch { }
        }
        throw new InvalidOperationException("Visible ListView was not found for text: " + text);
    }

    private static IntPtr FindVisibleListViewContainingInProcess(int pid, string text, int minItems)
    {
        foreach (var root in UiAutomation.TopWindowsForPid(pid))
        foreach (var list in UiAutomation.EnumerateChildren(root)
                     .Prepend(root)
                     .Where(h => Native.GetClass(h).Equals("SysListView32", StringComparison.OrdinalIgnoreCase) &&
                                 Native.IsWindowVisible(h)))
        {
            try
            {
                var items = UiAutomation.ListViewItems(list);
                if (items.Length >= minItems && items.Any(i => i.Texts.Any(t => t.Contains(text, StringComparison.OrdinalIgnoreCase))))
                    return list;
            }
            catch { }
        }
        throw new InvalidOperationException("Visible process ListView was not found for text: " + text);
    }

    private static void WriteListViewSummary(IntPtr root, string path)
    {
        var views = UiAutomation.EnumerateChildren(root)
            .Where(h => Native.GetClass(h).Equals("SysListView32", StringComparison.OrdinalIgnoreCase))
            .Select(h =>
            {
                try
                {
                    var items = UiAutomation.ListViewItems(h);
                    return new
                    {
                        handle = $"0x{h.ToInt64():X}",
                        visible = Native.IsWindowVisible(h),
                        rect = WindowInfo.FromHandle(h),
                        itemCount = items.Length,
                        sampleTexts = items.SelectMany(i => i.Texts).Where(t => !string.IsNullOrWhiteSpace(t)).Take(18).ToArray()
                    };
                }
                catch (Exception ex)
                {
                    return new
                    {
                        handle = $"0x{h.ToInt64():X}",
                        visible = Native.IsWindowVisible(h),
                        rect = WindowInfo.FromHandle(h),
                        itemCount = -1,
                        sampleTexts = new[] { "error: " + ex.Message }
                    };
                }
            })
            .ToArray();
        File.WriteAllText(path, JsonSerializer.Serialize(views, JsonOptions()), Encoding.UTF8);
    }

    private static IntPtr WaitForTreeViewWithItems(IntPtr root, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            foreach (var tree in UiAutomation.EnumerateChildren(root)
                         .Where(h => Native.GetClass(h).Equals("SysTreeView32", StringComparison.OrdinalIgnoreCase) &&
                                     Native.IsWindowVisible(h)))
            {
                try
                {
                    if (UiAutomation.TreeViewItems(tree).Length > 0)
                        return tree;
                }
                catch { }
            }
            Thread.Sleep(200);
        }
        throw new TimeoutException("Visible TreeView with items was not found.");
    }

    private static object SelectOptionalTreeItem(string[] args, IntPtr tree, string indexOption, string textOption)
    {
        var items = UiAutomation.TreeViewItems(tree);
        if (OptInt(args, indexOption) is { } index)
        {
            if (index < 0 || index >= items.Length)
                throw new ArgumentOutOfRangeException(indexOption, "Tree index is outside the visible item range.");
            UiAutomation.TreeViewSelect(tree, ParseHwnd(items[index].Handle));
            Thread.Sleep(250);
            return new { mode = "index", index, item = items[index] };
        }
        var text = Opt(args, textOption);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var item = items.FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException("Tree item was not found: " + text);
            UiAutomation.TreeViewSelect(tree, ParseHwnd(item.Handle));
            Thread.Sleep(250);
            return new { mode = "text", text, item };
        }
        if (items.Length > 0)
        {
            UiAutomation.TreeViewSelect(tree, ParseHwnd(items[0].Handle));
            Thread.Sleep(250);
            return new { mode = "default-root", index = 0, item = items[0] };
        }
        return new { mode = "none" };
    }

    private static object SelectOptionalStrategyLine(string[] args, IntPtr main, string contextDir)
    {
        if (OptInt(args, "--strategy-line-index") is not { } lineIndex)
            return new { mode = "none" };
        var frame = UiAutomation.EnumerateChildren(main)
            .Where(h => Native.GetClass(h).Contains("AfxFrameOrView", StringComparison.OrdinalIgnoreCase) &&
                        Native.IsWindowVisible(h) &&
                        Native.GetText(h).Contains("\u7B56\u7565\u7EC4\u6001", StringComparison.OrdinalIgnoreCase))
            .Select(h => new { Handle = h, Info = WindowInfo.FromHandle(h) })
            .OrderByDescending(h => h.Info.Width * h.Info.Height)
            .FirstOrDefault();
        if (frame == null)
            throw new InvalidOperationException("Visible strategy editor frame was not found.");
        var target = UiAutomation.EnumerateChildren(frame.Handle)
            .Select(h => new { Handle = h, Info = WindowInfo.FromHandle(h) })
            .Where(h => Native.IsWindowVisible(h.Handle) &&
                        h.Info.ClassName.StartsWith("Afx:", StringComparison.OrdinalIgnoreCase) &&
                        h.Info.Width > 300 && h.Info.Height > 200)
            .OrderByDescending(h => h.Info.Width * h.Info.Height)
            .FirstOrDefault()
            ?? frame;
        var defaultX = Math.Min(Math.Max(70, target.Info.Width / 12), Math.Max(70, target.Info.Width - 20));
        var mode = (Opt(args, "--strategy-select-mode") ?? "coordinate").ToLowerInvariant();
        var selectX = ParseInt(args, "--strategy-select-x", defaultX);
        var selectY0 = ParseInt(args, "--strategy-select-y", 35);
        var selectSpacing = ParseInt(args, "--strategy-select-spacing", 28);
        var selectWidth = ParseInt(args, "--strategy-select-width", 180);
        var selectHeight = ParseInt(args, "--strategy-select-height", 24);
        var clickX = Math.Min(Math.Max(selectX, 20), Math.Max(20, target.Info.Width - 20));
        var clickY = Math.Min(Math.Max(selectY0 + lineIndex * selectSpacing, 20), Math.Max(20, target.Info.Height - 20));
        UiAutomation.ActivateForInput(target.Handle);
        Thread.Sleep(120);
        var keySequence = Opt(args, "--strategy-select-keys") ?? "";
        var selectionActions = new List<string>();
        switch (mode)
        {
            case "none":
                selectionActions.Add("none");
                break;
            case "double-click":
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Left, doubleClick: true, mouse: true);
                selectionActions.Add("left-double-click");
                break;
            case "post-coordinate":
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Left, doubleClick: false, mouse: false);
                selectionActions.Add("post-left-click");
                break;
            case "post-double-click":
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Left, doubleClick: true, mouse: false);
                selectionActions.Add("post-left-double-click");
                break;
            case "right-click":
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Right, doubleClick: false, mouse: true);
                Thread.Sleep(250);
                selectionActions.Add("right-click");
                CaptureProcessWindows(UiAutomation.GetWindowProcessId(main), Path.Combine(contextDir, "after-strategy-line-right-click-popup"));
                SendKeys.SendWait("{ESC}");
                selectionActions.Add("escape-popup");
                break;
            case "keyboard":
                keySequence = string.IsNullOrWhiteSpace(keySequence)
                    ? "{HOME}" + string.Concat(Enumerable.Repeat("{DOWN}", Math.Max(0, lineIndex))) + " "
                    : keySequence;
                SendKeys.SendWait(keySequence);
                selectionActions.Add("keys:" + keySequence);
                break;
            case "marquee":
                var endX = Math.Min(Math.Max(clickX + selectWidth, 20), Math.Max(20, target.Info.Width - 20));
                var endY = Math.Min(Math.Max(clickY + selectHeight, 20), Math.Max(20, target.Info.Height - 20));
                UiAutomation.DragPoint(target.Handle, clickX, clickY, endX, endY, mouse: true);
                selectionActions.Add($"marquee:{clickX},{clickY}->{endX},{endY}");
                break;
            case "post-marquee":
                var postEndX = Math.Min(Math.Max(clickX + selectWidth, 20), Math.Max(20, target.Info.Width - 20));
                var postEndY = Math.Min(Math.Max(clickY + selectHeight, 20), Math.Max(20, target.Info.Height - 20));
                UiAutomation.DragPoint(target.Handle, clickX, clickY, postEndX, postEndY, mouse: false);
                selectionActions.Add($"post-marquee:{clickX},{clickY}->{postEndX},{postEndY}");
                break;
            case "coordinate-then-keyboard":
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Left, doubleClick: false, mouse: true);
                Thread.Sleep(180);
                keySequence = string.IsNullOrWhiteSpace(keySequence) ? " " : keySequence;
                SendKeys.SendWait(keySequence);
                selectionActions.Add("left-click");
                selectionActions.Add("keys:" + keySequence);
                break;
            default:
                UiAutomation.ClickPoint(target.Handle, clickX, clickY, MouseButton.Left, doubleClick: false, mouse: true);
                selectionActions.Add("left-click");
                break;
        }
        Thread.Sleep(450);
        CaptureProcessWindows(UiAutomation.GetWindowProcessId(main), Path.Combine(contextDir, "after-strategy-line-select"));
        return new
        {
            mode,
            lineIndex,
            frame = frame.Info,
            target = target.Info,
            click = new { x = clickX, y = clickY },
            marquee = mode == "marquee"
                ? new
                {
                    width = selectWidth,
                    height = selectHeight
                }
                : null,
            actions = selectionActions
        };
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

        var probeRoot = Opt(args, "--probe-root");
        var probes = LoadMcgsToolProbeEvidence(probeRoot);
        var commandProbes = BuildCommandProbeIndex(probes.Values);
        var entries = catalog.Tools.Select(tool =>
        {
            probes.TryGetValue(tool.toolId, out var exactProbe);
            McgsToolProbeEvidence? equivalentProbe = null;
            if (exactProbe == null &&
                tool.commandId.GetValueOrDefault() != 0 &&
                commandProbes.TryGetValue(tool.commandId.GetValueOrDefault(), out var commandProbe) &&
                CommandProbeCanCoverTool(tool, commandProbe))
            {
                equivalentProbe = commandProbe;
            }
            if (exactProbe != null &&
                exactProbe.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase) &&
                !ProbeCoversTool(tool, exactProbe) &&
                tool.commandId.GetValueOrDefault() != 0 &&
                commandProbes.TryGetValue(tool.commandId.GetValueOrDefault(), out var strongerCommandProbe) &&
                !strongerCommandProbe.ToolId.Equals(exactProbe.ToolId, StringComparison.OrdinalIgnoreCase) &&
                CommandProbeCanCoverTool(tool, strongerCommandProbe) &&
                ToolProbeEvidenceStrength(strongerCommandProbe) > ToolProbeEvidenceStrength(exactProbe))
            {
                equivalentProbe = strongerCommandProbe;
            }
            var probe = exactProbe ?? equivalentProbe;
            if (equivalentProbe != null)
                probe = equivalentProbe;
            var blocked = tool.supportStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase);
            var candidateSafe = tool.safetyClass.Equals("candidate-safe-mutation", StringComparison.OrdinalIgnoreCase);
            var unknownRisk = tool.safetyClass.Equals("unknown-risk", StringComparison.OrdinalIgnoreCase);
            var probeHasCandidateSafeEvidence = probe != null && ProbeHasCandidateSafeEvidence(probe);
            var probeHasUnknownRiskEvidence = probe != null && ProbeHasUnknownRiskEvidence(probe);
            var documentedBlockerNextProbe = "";
            var documentedProbeBlocker = exactProbe != null && ProbeHasDocumentedBlocker(tool, exactProbe, out documentedBlockerNextProbe);
            var probed = !blocked &&
                         probe != null &&
                         probe.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase) &&
                         (!candidateSafe || probeHasCandidateSafeEvidence) &&
                         (!unknownRisk || probeHasUnknownRiskEvidence);
            blocked = blocked || documentedProbeBlocker;
            var status = probed ? "probed" : blocked ? "blocked" : tool.supportStatus;
            return new
            {
                tool.toolId,
                tool.displayName,
                tool.source,
                tool.commandId,
                status,
                invoked = tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase) || probed,
                tool.safetyClass,
                invocationEvidence = tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase)
                    ? tool.evidenceSource
                    : probed ? probe!.Path : "",
                probeStatus = probe?.Status ?? "",
                probePath = exactProbe?.Path ?? "",
                equivalentProbePath = equivalentProbe?.Path ?? "",
                equivalentProbeToolId = equivalentProbe?.ToolId ?? "",
                probeEvidenceKind = exactProbe != null ? "exact-tool" : equivalentProbe != null ? "equivalent-command" : "",
                nextProbe = probed ? "" : documentedProbeBlocker ? documentedBlockerNextProbe : tool.nextProbe
            };
        }).ToArray();
        var needsPreconditionCount = entries.Count(e => string.Equals(e.status, "needs-precondition", StringComparison.OrdinalIgnoreCase));
        var needsProbeCount = entries.Count(e => string.Equals(e.status, "needs-probe", StringComparison.OrdinalIgnoreCase));
        var blockedCount = entries.Count(e => string.Equals(e.status, "blocked", StringComparison.OrdinalIgnoreCase));
        var result = new
        {
            schemaVersion = 1,
            status = needsProbeCount > 0 || needsPreconditionCount > 0 ? "UNKNOWN" : "PASS",
            project,
            projectSha256 = Sha256(project),
            createdAt = DateTimeOffset.Now.ToString("O"),
            probeRoot = string.IsNullOrWhiteSpace(probeRoot) ? "" : FullPath(probeRoot),
            probeEvidenceCount = probes.Count,
            toolCount = entries.Length,
            invokedCount = entries.Count(e => e.invoked),
            accountedCount = entries.Count(e => !string.Equals(e.status, "needs-probe", StringComparison.OrdinalIgnoreCase)),
            needsProbeCount,
            needsPreconditionCount,
            blockedCount,
            entries,
            blockedReasons = new[]
            {
                needsPreconditionCount > 0
                    ? $"Stage-1 tool-sweep still has {needsPreconditionCount} tools with unmet preconditions; run targeted mcgs tool-probe evidence before treating them as understood."
                    : "Stage-1 tool-sweep has no unmet preconditions.",
                blockedCount > 0
                    ? $"Stage-1 tool-sweep has {blockedCount} blocked tools whose blocker/risk record is explicit."
                    : "Stage-1 tool-sweep has no blocked tools.",
                "Stage-1 tool-sweep does not invoke unknown-risk tools. Entries with needs-precondition/blocked are classified from local toolbar state and still require safe follow-up evidence or documented blockers."
            }
        };
        File.WriteAllText(Path.Combine(outDir, "tool-sweep.json"), JsonSerializer.Serialize(result, JsonOptions()), Encoding.UTF8);
        Console.WriteLine("mcgs tool-sweep: " + outDir);
        return result.status == "PASS" ? 0 : 2;
    }

    private static Dictionary<string, McgsToolProbeEvidence> LoadMcgsToolProbeEvidence(string? probeRoot)
    {
        var result = new Dictionary<string, McgsToolProbeEvidence>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(probeRoot))
            return result;
        var root = FullPath(probeRoot);
        if (!Directory.Exists(root))
            return result;

        foreach (var path in Directory.EnumerateFiles(root, "tool-probe.json", SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                var toolId = JsonString(doc.RootElement, "toolId") ?? "";
                if (string.IsNullOrWhiteSpace(toolId))
                    continue;
                var status = JsonString(doc.RootElement, "status") ?? "UNKNOWN";
                var commandId = JsonIntAny(doc.RootElement, "commandId", "CommandId") ?? 0;
                var safetyClass = JsonString(doc.RootElement, "safetyClass") ?? "";
                if (result.ContainsKey(toolId))
                    continue;
                var candidateSafeMutation = false;
                var candidateSafeMutationFunctionalDiff = false;
                var candidateSafeMutationReversibleReturn = false;
                var candidateSafeMutationUnexpectedFunctionalDiff = false;
                var candidateSafeMutationNotFunctional = false;
                var projectCopyHashChanged = false;
                var unknownRiskHashDriftExplained = false;
                var context = JsonString(doc.RootElement, "context") ?? "";
                if (doc.RootElement.TryGetProperty("evidence", out var evidence) &&
                    evidence.ValueKind == JsonValueKind.Object)
                {
                    candidateSafeMutation = JsonBoolAny(evidence, "candidateSafeMutation", "CandidateSafeMutation") == true;
                    candidateSafeMutationFunctionalDiff = JsonBoolAny(evidence, "candidateSafeMutationFunctionalDiff", "CandidateSafeMutationFunctionalDiff") == true;
                    candidateSafeMutationReversibleReturn = JsonBoolAny(evidence, "candidateSafeMutationReversibleReturn", "CandidateSafeMutationReversibleReturn") == true;
                    candidateSafeMutationUnexpectedFunctionalDiff = JsonBoolAny(evidence, "candidateSafeMutationUnexpectedFunctionalDiff", "CandidateSafeMutationUnexpectedFunctionalDiff") == true;
                    candidateSafeMutationNotFunctional = JsonBoolAny(evidence, "candidateSafeMutationNotFunctional", "CandidateSafeMutationNotFunctional") == true;
                    projectCopyHashChanged = JsonBoolAny(evidence, "projectCopyHashChanged", "ProjectCopyHashChanged") == true;
                    unknownRiskHashDriftExplained = JsonBoolAny(evidence, "unknownRiskHashDriftExplained", "UnknownRiskHashDriftExplained") == true;
                }
                var newWindowObserved = JsonBoolAny(doc.RootElement, "newWindowObserved", "NewWindowObserved") == true ||
                                        (doc.RootElement.TryGetProperty("commandObservedWindows", out var windows) &&
                                         windows.ValueKind == JsonValueKind.Array &&
                                         windows.GetArrayLength() > 0);
                result[toolId] = new McgsToolProbeEvidence(toolId, path, status, commandId, safetyClass, candidateSafeMutation,
                    candidateSafeMutationFunctionalDiff, candidateSafeMutationReversibleReturn,
                    candidateSafeMutationUnexpectedFunctionalDiff, candidateSafeMutationNotFunctional,
                    newWindowObserved, projectCopyHashChanged, context, unknownRiskHashDriftExplained);
            }
            catch
            {
                // Corrupt probe evidence should not make the sweep look better.
            }
        }
        return result;
    }

    private static Dictionary<int, McgsToolProbeEvidence> BuildCommandProbeIndex(IEnumerable<McgsToolProbeEvidence> probes)
    {
        var result = new Dictionary<int, McgsToolProbeEvidence>();
        foreach (var probe in probes)
        {
            if (probe.CommandId == 0 || !probe.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!result.TryGetValue(probe.CommandId, out var existing) ||
                ToolProbeEvidenceStrength(probe) > ToolProbeEvidenceStrength(existing))
            {
                result[probe.CommandId] = probe;
            }
        }
        return result;
    }

    private static bool CommandProbeCanCoverTool(McgsToolEntry tool, McgsToolProbeEvidence probe)
    {
        if (tool.commandId.GetValueOrDefault() == 0 || tool.commandId.GetValueOrDefault() != probe.CommandId)
            return false;
        if (tool.supportStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase) ||
            tool.supportStatus.Equals("implemented", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrWhiteSpace(probe.SafetyClass) &&
            !probe.SafetyClass.Equals(tool.safetyClass, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static bool ProbeCoversTool(McgsToolEntry tool, McgsToolProbeEvidence probe)
    {
        if (!probe.Status.Equals("PASS", StringComparison.OrdinalIgnoreCase))
            return false;
        if (tool.safetyClass.Equals("candidate-safe-mutation", StringComparison.OrdinalIgnoreCase))
            return ProbeHasCandidateSafeEvidence(probe);
        if (tool.safetyClass.Equals("unknown-risk", StringComparison.OrdinalIgnoreCase))
            return ProbeHasUnknownRiskEvidence(probe);
        return true;
    }

    private static bool ProbeHasCandidateSafeEvidence(McgsToolProbeEvidence probe)
        => probe.CandidateSafeMutationFunctionalDiff ||
           probe.CandidateSafeMutationReversibleReturn ||
           probe.NewWindowObserved;

    private static bool ProbeHasUnknownRiskEvidence(McgsToolProbeEvidence probe)
        => !probe.ProjectCopyHashChanged || probe.UnknownRiskHashDriftExplained;

    private static bool ProbeHasDocumentedBlocker(McgsToolEntry tool, McgsToolProbeEvidence probe, out string nextProbe)
    {
        nextProbe = "";
        if (!probe.Status.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return false;

        var commandId = tool.commandId.GetValueOrDefault();
        if (commandId == 57643 &&
            probe.Context.StartsWith("animation", StringComparison.OrdinalIgnoreCase) &&
            probe.CandidateSafeMutationUnexpectedFunctionalDiff)
        {
            nextProbe = "Animation undo probe was executed after controlled object paste/undo, but saved MCE evidence still produced functional blob_strings/blob_geometry diffs; keep this toolbar instance blocked until an object-tree-aware normalizer or a dedicated animation undo fixture proves the diff is orphan serialization only.";
            return true;
        }

        if ((commandId == 33049 || commandId == 33050) && probe.CandidateSafeMutationNotFunctional)
        {
            nextProbe = "Device move up/down was executed after selecting Smart200, but the disposable FG2 device tree contains only one device; build a multi-device throwaway fixture before expecting a functional move diff.";
            return true;
        }

        if ((commandId == 33062 || commandId == 33063) && probe.CandidateSafeMutationNotFunctional)
        {
            nextProbe = "Strategy move up/down was executed in a distinct two-row strategy editor fixture, but local MCGS did not expose a file-safe policy-row selection channel. Physical click, double-click, right-click, keyboard, posted WM_LBUTTON, and marquee selection probes produced only editor-context or normalized-equivalent diffs; keep this command blocked until a future row-selection channel is discovered from MCGS internals or manual editor documentation.";
            return true;
        }

        return false;
    }

    private static int ToolProbeEvidenceStrength(McgsToolProbeEvidence probe)
    {
        var score = 0;
        if (probe.CandidateSafeMutationFunctionalDiff) score += 8;
        if (probe.CandidateSafeMutationReversibleReturn) score += 6;
        if (probe.NewWindowObserved) score += 4;
        if (probe.UnknownRiskHashDriftExplained) score += 3;
        if (!probe.ProjectCopyHashChanged) score += 1;
        return score;
    }

    private sealed record McgsToolProbeEvidence(string ToolId, string Path, string Status, int CommandId, string SafetyClass,
        bool CandidateSafeMutation, bool CandidateSafeMutationFunctionalDiff, bool CandidateSafeMutationReversibleReturn,
        bool CandidateSafeMutationUnexpectedFunctionalDiff, bool CandidateSafeMutationNotFunctional,
        bool NewWindowObserved, bool ProjectCopyHashChanged, string Context, bool UnknownRiskHashDriftExplained);

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
        public string commandResourceText { get; set; } = "";
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
            57600 => new KnownMcgsCommand(
                "MFC file new",
                "blocked",
                "formal-apply-required",
                "WM_COMMAND 57600 in an isolated editor instance",
                "standard MFC File/New command; may replace the active project context or prompt to save",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Do not invoke in unattended sweeps. If needed, probe only in an isolated disposable editor session with no official project open; capture before/after process state and close without saving."),
            57601 => new KnownMcgsCommand(
                "MFC file open",
                "blocked",
                "formal-apply-required",
                "WM_COMMAND 57601 in an isolated editor instance",
                "standard MFC File/Open command; opens a file dialog and may replace the active project context",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Do not invoke in unattended sweeps. If needed, probe only in an isolated disposable editor session and cancel the dialog; never invoke against the official project."),
            57603 => new KnownMcgsCommand(
                "save project",
                "implemented",
                "candidate-safe-mutation",
                "WM_COMMAND 57603",
                "saves the currently opened candidate project after controlled GUI writes",
                "mutating workflow save/readback evidence",
                ""),
            57607 => new KnownMcgsCommand(
                "MFC print",
                "blocked",
                "unknown-risk",
                "WM_COMMAND 57607",
                "standard MFC File/Print command; can interact with OS printer configuration or external devices",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Do not invoke in unattended sweeps; if needed, probe manually in a printer-disabled VM and cancel before printing."),
            57609 => new KnownMcgsCommand(
                "MFC print preview",
                "needs-precondition",
                "read-only",
                "WM_COMMAND 57609 on a disposable candidate with preview close handling",
                "standard MFC Print Preview command; should be read-only but changes editor UI mode until closed",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe in a disposable candidate, capture window tree before/after, then close preview and verify project hash unchanged."),
            57634 => new KnownMcgsCommand(
                "MFC edit copy",
                "implemented",
                "read-only",
                "WM_COMMAND 57634 or Ctrl+C after selecting canvas objects",
                "standard MFC Edit/Copy command; copies the current selection to clipboard",
                "canvas clipboard-probe captured MCGS_DRAW_OBJ without saving candidate",
                ""),
            57635 => new KnownMcgsCommand(
                "MFC edit cut",
                "needs-precondition",
                "candidate-safe-mutation",
                "WM_COMMAND 57635 after selecting an object on a throwaway candidate",
                "standard MFC Edit/Cut command; removes the current selection and writes clipboard data",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe only on a throwaway candidate with a known disposable object; verify before/after hash and clipboard formats."),
            57637 => new KnownMcgsCommand(
                "MFC edit paste",
                "needs-precondition",
                "candidate-safe-mutation",
                "WM_COMMAND 57637 after seeding clipboard from a disposable object",
                "standard MFC Edit/Paste command; inserts clipboard content into the active editor surface",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe only on a throwaway candidate with known clipboard seed; verify created object through property-map/readback."),
            57643 => new KnownMcgsCommand(
                "MFC edit undo",
                "needs-precondition",
                "candidate-safe-mutation",
                "WM_COMMAND 57643 after a known reversible throwaway edit",
                "standard MFC Edit/Undo command; changes candidate state only when an undo stack exists",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe after a controlled disposable edit; verify candidate hash returns to the pre-edit value."),
            57644 => new KnownMcgsCommand(
                "MFC edit redo",
                "needs-precondition",
                "candidate-safe-mutation",
                "WM_COMMAND 57644 after a known undo on a throwaway candidate",
                "standard MFC Edit/Redo command; reapplies a reverted candidate edit",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe after a controlled undo; verify candidate hash returns to the post-edit value."),
            57669 => new KnownMcgsCommand(
                "MFC context help",
                "needs-precondition",
                "read-only",
                "WM_COMMAND 57669 with help-mode cancellation",
                "standard MFC context-help command; may switch cursor/help mode or open local help",
                "MFC standard command id plus toolbar-probe enabled-state evidence",
                "Probe only with deterministic help-mode cancellation and window-tree evidence; verify project hash unchanged."),
            _ => null
        };
    }

    private static KnownMcgsCommand? DescribeResourceBackedCommand(int? commandId, string toolbarText, string commandResourceText)
    {
        if (commandId.GetValueOrDefault() == 34026 &&
            toolbarText.Contains("动画组态", StringComparison.OrdinalIgnoreCase))
        {
            return new KnownMcgsCommand(
                "unidentified animation editor-context command 34026",
                "needs-precondition",
                "read-only",
                "WM_COMMAND 34026 in animation editor on a disposable candidate with normalized MCE diff",
                "McgsSetE.exe has no RT_STRING text for this command. Local active-animation probe showed no dialog/window-tree effect and normalized MCE diff reduced to editor-context-only state, so catalog it conservatively as an unidentified editor-state command rather than a project object mutation.",
                "toolbar-probe enabled-state evidence; mcgs-tool-probe normalized-diff evidence; Gemini conservative review of sanitized toolbar neighborhood",
                "If the exact purpose is required, extract the toolbar bitmap/icon or statically inspect the McgsSetE.exe message map for command 34026; otherwise keep it read-only with editor-context-only diff evidence.");
        }

        if (commandId is null)
            return null;

        var id = commandId.Value;
        var source = "McgsSetE.exe RT_STRING command resource plus toolbar-probe evidence";
        if (IsToolboxDrawingCommand(id))
            return ToolboxDrawingCommand(id, ToolboxDrawingCommandName(id), toolbarText, commandResourceText, source,
                ToolboxDrawingCommandEffect(id));

        return id switch
        {
            32781 => ReadOnlyResourceCommand(id, "workbench large-icon view", toolbarText, commandResourceText, source,
                "switches the workbench object list to large-icon view"),
            32782 => ReadOnlyResourceCommand(id, "workbench small-icon view", toolbarText, commandResourceText, source,
                "switches the workbench object list to small-icon view"),
            32783 => ReadOnlyResourceCommand(id, "workbench list view", toolbarText, commandResourceText, source,
                "switches the workbench object list to list view"),
            32784 => ReadOnlyResourceCommand(id, "workbench details view", toolbarText, commandResourceText, source,
                "switches the workbench object list to details view"),
            32787 => new KnownMcgsCommand(
                "run project in MCGSRUN",
                "blocked",
                "hardware-risk",
                "WM_COMMAND 32787",
                commandResourceText,
                source,
                "Do not invoke in unattended coverage; it starts MCGSRUN and can interact with runtime drivers or field hardware."),
            32790 => ReadOnlyResourceCommand(id, "show workspace", toolbarText, commandResourceText, source,
                "shows or focuses the workspace pane"),
            32801 => ReadOnlyResourceCommand(id, "browse realtime database objects", toolbarText, commandResourceText, source,
                "opens or focuses realtime database object browsing"),
            32802 => CandidateResourceCommand(id, "move current menu down", toolbarText, commandResourceText, source,
                "moves the current menu item down in menu configuration"),
            32803 => CandidateResourceCommand(id, "move current menu up", toolbarText, commandResourceText, source,
                "moves the current menu item up in menu configuration"),
            32805 => CandidateResourceCommand(id, "add pull-down menu", toolbarText, commandResourceText, source,
                "adds a pull-down menu in menu configuration"),
            32806 => CandidateResourceCommand(id, "add menu item", toolbarText, commandResourceText, source,
                "adds a menu item in menu configuration"),
            32807 => CandidateResourceCommand(id, "add menu separator", toolbarText, commandResourceText, source,
                "adds a menu separator line in menu configuration"),
            32876 or 32877 or 32878 or 32879 or 32880 or 32881 or 32882 or 32883 or 32884 or
            32885 or 32886 or 32887 or 32888 or 32889 or 32890 or 32891 or 32892 or 32893 or
            32894 or 32895 or 32896 or 32897 or 32898 or 32899 or 34016 =>
                CandidateResourceCommand(id, "animation object arrange/transform command " + id, toolbarText, commandResourceText, source,
                    "modifies the selected animation canvas object or object selection"),
            32827 or 32828 or 32829 or 32830 or 32998 or 33000 or 34060 =>
                CandidateResourceCommand(id, "animation object style command " + id, toolbarText, commandResourceText, source,
                    "opens or applies style settings for the selected animation canvas object"),
            32848 => ReadOnlyResourceCommand(id, "toggle toolbox", toolbarText, commandResourceText, source,
                "opens or closes the toolbox pane"),
            32851 => CandidateResourceCommand(id, "add strategy policy line", toolbarText, commandResourceText, source,
                "adds a policy line in strategy configuration"),
            33049 => CandidateResourceCommand(id, "move device driver up", toolbarText, commandResourceText, source,
                "moves the selected device driver up"),
            33050 => CandidateResourceCommand(id, "move device driver down", toolbarText, commandResourceText, source,
                "moves the selected device driver down"),
            33054 => CandidateResourceCommand(id, "move menu left", toolbarText, commandResourceText, source,
                "moves the current menu left"),
            33056 => CandidateResourceCommand(id, "move menu right", toolbarText, commandResourceText, source,
                "moves the current menu right"),
            33062 => CandidateResourceCommand(id, "move policy line down", toolbarText, commandResourceText, source,
                "moves the selected policy line down"),
            33063 => CandidateResourceCommand(id, "move policy line up", toolbarText, commandResourceText, source,
                "moves the selected policy line up"),
            33064 => ReadOnlyResourceCommand(id, "show policy notes", toolbarText, commandResourceText, source,
                "shows or hides policy notes"),
            33996 => ReadOnlyResourceCommand(id, "toggle animation edit toolbar", toolbarText, commandResourceText, source,
                "opens or closes the animation edit toolbar"),
            34024 => ReadOnlyResourceCommand(id, "toggle grid display", toolbarText, commandResourceText, source,
                "controls animation canvas grid display"),
            34027 => new KnownMcgsCommand(
                "select current editing language",
                "needs-precondition",
                "unknown-risk",
                $"WM_COMMAND {id} in a disposable editor session, then cancel/close the dialog",
                commandResourceText,
                source,
                "Probe on a throwaway candidate; capture the language dialog fields, cancel it, and verify project hash unchanged."),
            34059 => new KnownMcgsCommand(
                "multi-language configuration dialog",
                "needs-precondition",
                "candidate-safe-mutation",
                $"WM_COMMAND {id} in a disposable editor session, then cancel/close the dialog",
                commandResourceText,
                source,
                "Probe on a throwaway candidate; capture the dialog/window tree, cancel it, and verify project hash unchanged."),
            _ => null
        };
    }

    private static KnownMcgsCommand ReadOnlyResourceCommand(int commandId, string displayName, string toolbarText, string resourceText, string evidenceSource, string effect)
        => new(
            displayName,
            "needs-precondition",
            "read-only",
            $"WM_COMMAND {commandId} in {SafeToolbarContext(toolbarText)} with before/after project hash",
            string.IsNullOrWhiteSpace(resourceText) ? effect : resourceText,
            evidenceSource,
            "Probe on a disposable candidate, capture window tree before/after, then verify the project SHA is unchanged.");

    private static KnownMcgsCommand CandidateResourceCommand(int commandId, string displayName, string toolbarText, string resourceText, string evidenceSource, string effect)
        => new(
            displayName,
            "needs-precondition",
            "candidate-safe-mutation",
            $"WM_COMMAND {commandId} in {SafeToolbarContext(toolbarText)} on a throwaway candidate",
            string.IsNullOrWhiteSpace(resourceText) ? effect : resourceText,
            evidenceSource,
            "Probe on a throwaway candidate with a minimal matching editor context; record before/after SHA, dialog/window tree, and rollback evidence.");

    private static KnownMcgsCommand ToolboxDrawingCommand(int commandId, string displayName, string toolbarText, string resourceText, string evidenceSource, string effect)
        => new(
            displayName,
            "needs-precondition",
            "candidate-safe-mutation",
            $"WM_COMMAND {commandId} in animation canvas, then drag a disposable rectangle",
            string.IsNullOrWhiteSpace(resourceText) ? effect : resourceText,
            evidenceSource,
            "Probe on a throwaway candidate with --context animation-draw-object, --draw-x/--draw-y/--draw-width/--draw-height, --save-after-invoke, and --baseline-export. For table-like tools, prefer --context animation-draw-table and then rerun toolbar-probe with the disposable table selected.");

    private static bool IsToolboxDrawingCommand(int commandId)
        => commandId is 32901 or 32902 or 32903 or 32904 or 32905 or 32906 or 32907 or 32908 or
            32936 or 32937 or 32938 or 32939 or 32940 or 32941 or 32942 or 32943 or
            32944 or 32945 or 32946 or 32947 or 32948 or 32949 or 32950 or 32955 or 32956;

    private static string ToolboxDrawingCommandName(int commandId) => commandId switch
    {
        32901 => "line drawing tool",
        32902 => "arc drawing tool",
        32903 => "rectangle drawing tool",
        32904 => "rounded-rectangle drawing tool",
        32905 => "ellipse drawing tool",
        32906 => "polygon/polyline drawing tool",
        32907 => "native static text drawing tool",
        32908 => "bitmap drawing tool",
        32936 => "input box drawing tool",
        32937 => "flow block drawing tool",
        32938 => "standard button drawing tool",
        32939 => "animation button drawing tool",
        32940 => "slider input drawing tool",
        32941 => "native animation display/lamp drawing tool",
        32942 => "knob input drawing tool",
        32943 => "alarm display drawing tool",
        32944 => "realtime curve drawing tool",
        32945 => "historical curve drawing tool",
        32946 => "free table drawing tool",
        32947 => "historical table drawing tool",
        32948 => "percent-fill drawing tool",
        32949 => "rotating meter drawing tool",
        32950 => "saved-data browser drawing tool",
        32955 => "plan curve drawing tool",
        32956 => "combo box drawing tool",
        _ => "toolbox drawing command " + commandId
    };

    private static string ToolboxDrawingCommandEffect(int commandId) => commandId switch
    {
        32946 => "creates a free-table canvas component; table editing toolbar commands require this object selected as a fixture",
        32947 => "creates a historical-table canvas component; table editing toolbar commands require this object selected as a fixture",
        32907 => "creates a native static text/label object; full property readback is handled by window.static-text.add",
        32938 => "creates a standard button object; momentary/status workflows configure and read it back",
        32941 => "creates an animation display/native lamp-like component; full property readback is handled by window.lamp.add-native",
        _ => "creates the selected MCGS toolbox object by dragging a rectangle on the animation canvas"
    };

    private static string SafeToolbarContext(string toolbarText)
        => string.IsNullOrWhiteSpace(toolbarText) ? "the matching editor context" : toolbarText;

    private static string ShortResourceCommandName(string resourceText, int? commandId)
    {
        if (string.IsNullOrWhiteSpace(resourceText))
            return $"command {commandId}";
        var text = resourceText.Trim().TrimEnd('.');
        return text.Length <= 48 ? text : text[..48] + "...";
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
                            var commandResourceText = isSeparator ? "" : TryReadMcgsCommandResourceText(commandId.GetValueOrDefault()) ?? "";
                            var known = DescribeKnownToolbarCommand(commandId) ??
                                        DescribeResourceBackedCommand(commandId, toolbarText, commandResourceText) ??
                                        DescribeToolbarContextCommand(commandId, toolbarText, commandResourceText, enabled, hidden);
                            build.Tools.Add(new McgsToolEntry
                            {
                                toolId = isSeparator
                                    ? $"toolbar:{toolbarIndex}:{index}:separator"
                                    : $"toolbar:{toolbarIndex}:{index}:{commandId}",
                                displayName = string.IsNullOrWhiteSpace(text)
                                    ? (isSeparator ? "separator" : known?.DisplayName ?? ShortResourceCommandName(commandResourceText, commandId))
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
                                commandResourceText = commandResourceText,
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
        foreach (var tool in build.Tools)
            build.Functions.Add(McgsFunctionFromTool(tool));
        return build;
    }

    private static KnownMcgsCommand? DescribeToolbarContextCommand(int? commandId, string toolbarText, string commandResourceText, bool enabled, bool hidden)
    {
        if (commandId.GetValueOrDefault() == 0)
            return null;
        if (hidden)
        {
            return new KnownMcgsCommand(
                $"hidden toolbar command {commandId}",
                "needs-precondition",
                "unknown-risk",
                $"WM_COMMAND {commandId} after toolbar button is visible",
                "toolbar button is hidden in the captured local editor state",
                "toolbar-probe hidden-state evidence",
                "Identify the editor mode that shows this command, then rerun tool-catalog/tool-probe on a throwaway candidate.");
        }
        if (!enabled)
        {
            return new KnownMcgsCommand(
                $"disabled {toolbarText} command {commandId}",
                "needs-precondition",
                "unknown-risk",
                $"WM_COMMAND {commandId} after its toolbar precondition is met",
                "toolbar button is present but disabled in the captured local editor state",
                "toolbar-probe disabled-state evidence",
                "Identify the selection/editor-mode precondition that enables this command, then rerun on a throwaway candidate.");
        }

        if (toolbarText.Contains("表格编辑", StringComparison.OrdinalIgnoreCase))
        {
            return new KnownMcgsCommand(
                $"table editor command {commandId}",
                "needs-precondition",
                "candidate-safe-mutation",
                $"WM_COMMAND {commandId} only after a disposable MCGS canvas table object is created and selected on a throwaway candidate",
                string.IsNullOrWhiteSpace(commandResourceText)
                    ? "table-object editing toolbar command captured without an active table-object fixture"
                    : commandResourceText,
                "toolbar-probe table-edit context evidence; Gemini review of sanitized command text classified this as canvas table-object editing",
                "Create a disposable canvas table object fixture, select one or more cells, rerun toolbar-probe, then invoke only on that throwaway candidate with --context animation-single-object, before/after normalized MCE diff, and reopen readback.");
        }

        if (toolbarText.Contains("动画组态", StringComparison.OrdinalIgnoreCase) ||
            toolbarText.Contains("菜单组态", StringComparison.OrdinalIgnoreCase) ||
            toolbarText.Contains("策略组态", StringComparison.OrdinalIgnoreCase) ||
            toolbarText.Contains("设备组态", StringComparison.OrdinalIgnoreCase))
        {
            return new KnownMcgsCommand(
                $"{toolbarText} command {commandId}",
                "needs-probe",
                "candidate-safe-mutation",
                $"WM_COMMAND {commandId} on a throwaway candidate in {toolbarText}",
                string.IsNullOrWhiteSpace(commandResourceText) ? "enabled editor-profile toolbar command; side effect is not yet proven" : commandResourceText,
                string.IsNullOrWhiteSpace(commandResourceText) ? "toolbar-probe enabled-state evidence" : "McgsSetE.exe RT_STRING command resource plus toolbar-probe evidence",
                "Run mcgs tool-probe on a throwaway candidate with before/after project hash, window tree, and dialog capture.");
        }

        return null;
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
                    commandResourceText = JsonStringAny(tool, "commandResourceText", "CommandResourceText") ?? "",
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
            toolBackedFunctionCount = build.Tools.Count,
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

    private static readonly Dictionary<int, string?> McgsCommandResourceTextCache = new();

    private static string? TryReadMcgsCommandResourceText(int commandId)
    {
        if (McgsCommandResourceTextCache.TryGetValue(commandId, out var cached))
            return cached;

        string? value = null;
        try
        {
            var editor = FullPath(EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
            if (File.Exists(editor))
                value = TryReadStringTableResource(editor, commandId);
        }
        catch
        {
            value = null;
        }

        McgsCommandResourceTextCache[commandId] = value;
        return value;
    }

    private static string? TryReadStringTableResource(string file, int stringId)
    {
        const uint loadLibraryAsDataFile = 0x00000002;
        var module = LoadLibraryExFullCoverage(file, IntPtr.Zero, loadLibraryAsDataFile);
        if (module == IntPtr.Zero)
            return null;

        try
        {
            var blockId = (stringId / 16) + 1;
            var stringIndex = stringId % 16;
            var resource = FindResourceFullCoverage(module, MakeIntResourceFullCoverage(blockId), MakeIntResourceFullCoverage(6));
            if (resource == IntPtr.Zero)
                return null;

            var loaded = LoadResourceFullCoverage(module, resource);
            var locked = LockResourceFullCoverage(loaded);
            var size = SizeofResourceFullCoverage(module, resource);
            if (locked == IntPtr.Zero || size == 0)
                return null;

            var bytes = new byte[size];
            Marshal.Copy(locked, bytes, 0, bytes.Length);
            var offset = 0;
            for (var i = 0; i < 16; i++)
            {
                if (offset + 2 > bytes.Length)
                    return null;
                var len = BitConverter.ToUInt16(bytes, offset);
                offset += 2;
                var text = "";
                if (len > 0)
                {
                    var byteCount = len * 2;
                    if (offset + byteCount > bytes.Length)
                        return null;
                    text = Encoding.Unicode.GetString(bytes, offset, byteCount);
                    offset += byteCount;
                }
                if (i == stringIndex)
                    return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }
        finally
        {
            FreeLibraryFullCoverage(module);
        }

        return null;
    }

    private static IntPtr MakeIntResourceFullCoverage(int id) => (IntPtr)id;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryExFullCoverage(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "FindResourceW")]
    private static extern IntPtr FindResourceFullCoverage(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "LoadResource")]
    private static extern IntPtr LoadResourceFullCoverage(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "LockResource")]
    private static extern IntPtr LockResourceFullCoverage(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "SizeofResource")]
    private static extern uint SizeofResourceFullCoverage(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "FreeLibrary")]
    private static extern bool FreeLibraryFullCoverage(IntPtr hModule);

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
                uiLocations = new[] { "CLI/workflow" },
                commandRoute = "mcgsctl workflow run " + workflow,
                inputs = WorkflowInputs(workflow),
                outputs = WorkflowOutputs(workflow),
                sideEffects = WorkflowSideEffects(workflow),
                invocationRoute = "mcgsctl workflow run " + workflow,
                safetyClass = workflow.Contains("apply", StringComparison.OrdinalIgnoreCase)
                    ? "formal-apply-required"
                    : workflow.Contains("rollback", StringComparison.OrdinalIgnoreCase)
                        ? "formal-apply-required"
                        : "candidate-safe-mutation",
                supportStatus = "implemented",
                relatedObjectTypes = WorkflowRelatedObjectTypes(workflow),
                relatedPropertyDialogs = WorkflowRelatedPropertyDialogs(workflow),
                evidenceSource = "mcgsctl workflow result schema and existing GUI/readback workflows",
                validation = "workflow result checks plus candidate summarize/validate where applicable",
                validationReadbackMethod = WorkflowValidationReadback(workflow),
                confidence = 0.9,
                nextProbe = ""
            };
        }
    }

    private static object McgsFunctionFromTool(McgsToolEntry tool)
    {
        var commandRoute = string.IsNullOrWhiteSpace(tool.invocationRoute)
            ? (tool.commandId.HasValue ? $"WM_COMMAND {tool.commandId}" : tool.toolId)
            : tool.invocationRoute;
        var purpose = ToolPurpose(tool);
        var outputs = string.IsNullOrWhiteSpace(tool.expectedEffect)
            ? "effect is not yet proven; see nextProbe"
            : tool.expectedEffect;

        return new
        {
            functionId = "tool:" + tool.toolId,
            name = tool.displayName,
            purpose,
            uiLocations = new[] { tool.uiPath },
            commandRoute,
            inputs = ToolInputs(tool),
            outputs,
            sideEffects = ToolSideEffects(tool),
            relatedObjectTypes = ToolRelatedObjectTypes(tool),
            relatedPropertyDialogs = ToolRelatedPropertyDialogs(tool),
            validationReadbackMethod = ToolValidationReadback(tool),
            safetyClass = tool.safetyClass,
            supportStatus = tool.supportStatus,
            commandId = tool.commandId,
            commandResourceText = tool.commandResourceText,
            confidence = ToolFunctionConfidence(tool),
            evidenceSource = tool.evidenceSource,
            nextProbe = string.IsNullOrWhiteSpace(tool.nextProbe)
                ? ""
                : tool.nextProbe
        };
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

    private static string[] WorkflowInputs(string workflow) => workflow switch
    {
        "realtime-db.add" => new[] { "source/project candidate", "workdir", "name", "type", "initial value" },
        "window.button.add-momentary" => new[] { "project candidate", "text", "variable", "canvas rectangle" },
        "window.indicator.add" => new[] { "project candidate", "text", "expression", "canvas rectangle" },
        "window.static-text.add" => new[] { "project candidate", "text", "canvas rectangle" },
        "window.lamp.add-native" => new[] { "project candidate", "text", "variable/expression", "canvas rectangle" },
        "window.layout.apply" => new[] { "source/project candidate", "layout spec", "workdir" },
        "device.channel.map" => new[] { "source/project candidate", "area", "address", "count", "data-type-index", "connect-base" },
        "script.edit" => new[] { "source/project candidate", "script text", "button text", "verify token", "check flags" },
        "project.check" => new[] { "project candidate", "fail-on-warning flag" },
        "project.check-save" => new[] { "project candidate", "fail-on-warning flag" },
        "safety.verify" => new[] { "project candidate", "safety spec", "evidence dir", "optional AWL" },
        "project.apply-candidate" => new[] { "official source", "candidate", "approval" },
        "project.rollback" => new[] { "rollback package", "target official copy" },
        _ => new[] { "workflow arguments" }
    };

    private static string WorkflowOutputs(string workflow) => workflow switch
    {
        "window.layout.apply" => "candidate.MCE update plus layout/readback workflow result evidence",
        "project.check" => "project-check/check-result.json from a same-SHA temporary copy",
        "safety.verify" => "safety-result.json plus candidate-final snapshot evidence",
        "project.apply-candidate" => "File.Replace official update plus rollback package",
        "project.rollback" => "restored target file after rollback metadata checks",
        _ => "workflow result JSON, candidate SHA chain evidence, and optional GUI/readback artifacts"
    };

    private static string WorkflowSideEffects(string workflow) => workflow switch
    {
        "project.check" => "opens and checks a temporary copy; the real candidate must remain unchanged",
        "safety.verify" => "may refresh candidate-final evidence under the run directory; does not mutate candidate",
        "project.apply-candidate" => "replaces the approved official target and creates rollback evidence",
        "project.rollback" => "replaces the rollback target after metadata checks",
        _ when workflow.Contains("apply", StringComparison.OrdinalIgnoreCase) => "mutates only the configured candidate/workdir unless formal approval is supplied",
        _ => "mutates candidate copies only when the workflow is declared mutating"
    };

    private static string[] WorkflowRelatedObjectTypes(string workflow) => workflow switch
    {
        "window.button.add-momentary" => new[] { "standard-button", "momentary-button" },
        "window.indicator.add" => new[] { "standard-button", "status-button" },
        "window.static-text.add" => new[] { "native-static-text", "section-title", "static-label" },
        "window.lamp.add-native" => new[] { "native-lamp", "animation-display" },
        "window.layout.apply" => new[] { "layout-supported UI objects" },
        "device.channel.map" => new[] { "Smart200 channel rows", "Data objects" },
        "script.edit" => new[] { "script button/action properties", "Data objects" },
        _ => Array.Empty<string>()
    };

    private static string[] WorkflowRelatedPropertyDialogs(string workflow) => workflow switch
    {
        "window.button.add-momentary" => new[] { "button basic/action/script tabs" },
        "window.indicator.add" => new[] { "button basic/visibility/action/script tabs" },
        "window.static-text.add" => new[] { "label/static text property tabs" },
        "window.lamp.add-native" => new[] { "animation display/lamp property tabs" },
        "window.layout.apply" => new[] { "property dialogs for each object kind in the layout" },
        "script.edit" => new[] { "script editor/check dialog" },
        _ => Array.Empty<string>()
    };

    private static string WorkflowValidationReadback(string workflow) => workflow switch
    {
        "window.button.add-momentary" => "reopen candidate, select object, verify label plus press=set1/release=clear0 action pages",
        "window.indicator.add" => "reopen candidate, select object, verify label, visibility expression, no operation, and empty script",
        "window.static-text.add" => "reopen candidate, select native label, verify label text",
        "window.lamp.add-native" => "reopen candidate, select native animation display, verify text and display variable",
        "window.layout.apply" => "layout readback plus per-object workflow/readback evidence",
        "device.channel.map" => "reopen Smart200 table and compare smart200Channels with safety spec",
        "script.edit" => "script token/readback check plus Data delta evidence",
        "project.check" => "project-check result, same-SHA temporary copy evidence, and candidate unchanged check",
        "safety.verify" => "safety-result required checks and candidate-final SHA match",
        _ => "workflow result checks plus candidate summary/validate gates"
    };

    private static string ToolPurpose(McgsToolEntry tool)
    {
        if (!string.IsNullOrWhiteSpace(tool.commandResourceText))
            return FirstResourceLine(tool.commandResourceText);
        if (!string.IsNullOrWhiteSpace(tool.expectedEffect) && !tool.expectedEffect.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            return tool.expectedEffect;
        return tool.displayName;
    }

    private static string ToolInputs(McgsToolEntry tool)
    {
        if (tool.commandId.HasValue)
            return $"active MCGS editor context plus WM_COMMAND {tool.commandId}";
        return tool.source.Equals("mcgsctl", StringComparison.OrdinalIgnoreCase)
            ? "mcgsctl command arguments"
            : "UI selection/context";
    }

    private static string ToolSideEffects(McgsToolEntry tool) => tool.safetyClass switch
    {
        "read-only" => "expected to inspect, copy, navigate, or toggle editor UI without saving project content",
        "candidate-safe-mutation" => "may mutate the active candidate or editor selection; probe only on candidate/throwaway copies",
        "formal-apply-required" => "can affect official project/open-file state and requires explicit approval or isolated disposable session",
        "hardware-risk" => "may launch runtime or interact with live system behavior; do not invoke in unattended sweeps",
        _ => "side effect is unknown until a guarded probe provides before/after evidence"
    };

    private static string[] ToolRelatedObjectTypes(McgsToolEntry tool)
    {
        var text = (tool.displayName + " " + tool.expectedEffect + " " + tool.commandResourceText).ToLowerInvariant();
        if (text.Contains("button") || text.Contains("鎸夐挳", StringComparison.OrdinalIgnoreCase))
            return new[] { "button" };
        if (text.Contains("lamp") || text.Contains("display") || text.Contains("animation"))
            return new[] { "animation-display", "native-lamp" };
        if (text.Contains("static text") || text.Contains("label") || text.Contains("text"))
            return new[] { "static-text", "label" };
        if (text.Contains("table") || text.Contains("row") || text.Contains("column"))
            return new[] { "table-editor" };
        return Array.Empty<string>();
    }

    private static string[] ToolRelatedPropertyDialogs(McgsToolEntry tool)
    {
        if (tool.commandId == 32785)
            return new[] { "selected object property dialog" };
        var objectTypes = ToolRelatedObjectTypes(tool);
        if (objectTypes.Contains("button"))
            return new[] { "button property dialog" };
        if (objectTypes.Contains("animation-display") || objectTypes.Contains("native-lamp"))
            return new[] { "animation display property dialog" };
        if (objectTypes.Contains("static-text") || objectTypes.Contains("label"))
            return new[] { "label/static text property dialog" };
        return Array.Empty<string>();
    }

    private static string ToolValidationReadback(McgsToolEntry tool) => tool.supportStatus switch
    {
        "implemented" when tool.safetyClass == "read-only" => "catalog/resource/window-tree evidence; no candidate mutation expected",
        "implemented" => "existing workflow/readback evidence referenced by evidenceSource",
        "blocked" => "not invoked; blocker and nextProbe document why local file-safe validation is not complete",
        "needs-precondition" => "open the required editor context or controlled throwaway candidate, then run mcgs tool-probe with before/after evidence",
        _ => "run mcgs tool-probe on a throwaway candidate with before/after hash, screenshots, and window-tree evidence"
    };

    private static double ToolFunctionConfidence(McgsToolEntry tool) => tool.supportStatus switch
    {
        "implemented" when !string.IsNullOrWhiteSpace(tool.evidenceSource) => 0.85,
        "implemented" => 0.75,
        "needs-precondition" when !string.IsNullOrWhiteSpace(tool.commandResourceText) => 0.65,
        "needs-precondition" => 0.55,
        "blocked" => 0.6,
        _ => 0.35
    };

    private static string FirstResourceLine(string value)
    {
        return value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => s.Length > 0) ?? value.Trim();
    }

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
            var revealOverlap = Has(args, "--reveal-overlap-delete")
                ? TryRevealOverlapByDelete(process.Id, session.Main, session.Canvas, x, y, width, height, outDir)
                : null;
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
            var permissionDialog = Has(args, "--probe-permissions")
                ? TryProbePermissionDialog(process.Id, dialog, outDir)
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
                permissionDialog,
                revealOverlap,
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

    private static object TryRevealOverlapByDelete(int pid, IntPtr main, IntPtr canvas,
        int x, int y, int width, int height, string outDir)
    {
        var cx = x + width / 2;
        var cy = y + height / 2;
        TryScreenshot(canvas, Path.Combine(outDir, "reveal-overlap-before-delete.png"));

        Native.SetForegroundWindow(main);
        Thread.Sleep(150);
        UiAutomation.ClickPoint(canvas, cx, cy, MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(250);
        SendKeys.SendWait("{DEL}");
        Thread.Sleep(600);

        var dialogs = new List<object>();
        foreach (var dialog in UiAutomation.TopWindowsForPid(pid).Where(h =>
                     Native.GetClass(h) == "#32770" && Native.IsWindowVisible(h)))
        {
            var text = DialogText(dialog);
            var action = "observe";
            if (ContainsAny(text, "删除", "Delete", "是否"))
            {
                RecordDialogEvidence("dialogs.jsonl", pid, dialog, "click-yes", "reveal-overlap-delete.confirm");
                ClickButtonByNormalizedText(dialog, mouse: true, "是(&Y)", "是", "确定", "确认", "Yes");
                action = "confirm-delete";
                Thread.Sleep(300);
            }
            dialogs.Add(new
            {
                action,
                window = WindowInfo.FromHandle(dialog),
                text = ShortenForEvidence(text, 400)
            });
        }

        TryScreenshot(canvas, Path.Combine(outDir, "reveal-overlap-after-delete.png"));
        var evidence = new
        {
            status = "attempted",
            method = "select-topmost-at-target-center-and-delete-on-property-readback-copy",
            point = new { x = cx, y = cy },
            mutationScope = "property-readback-copy; editor is closed with saveIntent=false",
            dialogs
        };
        File.WriteAllText(Path.Combine(outDir, "reveal-overlap-delete.json"),
            JsonSerializer.Serialize(evidence, JsonOptions()), Encoding.UTF8);
        return evidence;
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

    private static object TryProbePermissionDialog(int pid, IntPtr ownerDialog, string outDir)
    {
        var permissionButton = UiAutomation.EnumerateChildren(ownerDialog)
            .FirstOrDefault(h =>
                Native.IsWindowVisible(h) &&
                Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase) &&
                CompactLabel(Native.GetText(h)).StartsWith(CompactLabel("权限"), StringComparison.OrdinalIgnoreCase));

        if (permissionButton == IntPtr.Zero)
            return new
            {
                status = "notApplicable",
                reason = "property dialog has no visible permission button on the current profile/page"
            };

        var before = UiAutomation.TopWindowsForPid(pid).ToHashSet();
        var buttonRect = UiAutomation.GetWindowRect(permissionButton);
        UiAutomation.ClickPoint(permissionButton, Math.Max(1, buttonRect.Width / 2), Math.Max(1, buttonRect.Height / 2),
            MouseButton.Left, doubleClick: false, mouse: true);
        Thread.Sleep(400);

        var permissionDialog = WaitForTopWindow(pid,
            h => h != ownerDialog &&
                 Native.GetClass(h) == "#32770" &&
                 Native.IsWindowVisible(h) &&
                 !before.Contains(h),
            TimeSpan.FromSeconds(5));

        if (permissionDialog == IntPtr.Zero)
            return new
            {
                status = "UNKNOWN",
                reason = "permission button was present, but no permission dialog appeared"
            };

        try
        {
            var controls = SnapshotDialogControls(permissionDialog);
            var extracted = ExtractPermissionDialogSnapshot(controls);
            File.WriteAllLines(Path.Combine(outDir, "permission-dialog.tree.txt"), UiAutomation.WindowTreeLines(permissionDialog), Encoding.UTF8);
            TryScreenshot(permissionDialog, Path.Combine(outDir, "permission-dialog.png"));
            return new
            {
                status = "PASS",
                window = WindowInfo.FromHandle(permissionDialog),
                controls,
                extracted
            };
        }
        finally
        {
            if (!ClickButtonByNormalizedText(permissionDialog, mouse: true, "取消", "取消(&C)", "关闭", "Cancel"))
                UiAutomation.CloseWindow(permissionDialog);
            Thread.Sleep(250);
        }
    }

    private static object ExtractPermissionDialogSnapshot(object[] controls)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(controls, JsonOptions()));
        var array = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();

        var checkedButtons = array
            .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
            .Where(c => JsonIntAny(c, "checkState", "CheckState") == 1)
            .Select(c => JsonStringAny(c, "text", "Text") ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var currentValues = array
            .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
            .Where(c =>
            {
                var cls = JsonStringAny(c, "className", "ClassName") ?? "";
                return cls.Equals("ComboBox", StringComparison.OrdinalIgnoreCase) ||
                       cls.Contains("ListBox", StringComparison.OrdinalIgnoreCase);
            })
            .Select(ControlCurrentText)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var visibleTexts = array
            .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
            .Select(c => JsonStringAny(c, "text", "Text") ?? "")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .Take(16)
            .ToArray();
        var summary = string.Join("; ", checkedButtons.Concat(currentValues));
        if (string.IsNullOrWhiteSpace(summary))
            summary = string.Join("; ", visibleTexts);

        return new
        {
            summary,
            checkedButtons,
            currentValues,
            visibleTexts
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

    private static string ShortenForEvidence(string text, int maxLength)
    {
        var compact = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        return compact.Length <= maxLength ? compact : compact[..maxLength] + "...";
    }

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
        public string? FontDialogStatus { get; set; }
        public bool? FontButtonPresent { get; set; }
        public string? FontFamily { get; set; }
        public string? FontSize { get; set; }
        public string? FontStyle { get; set; }
        public string? ForegroundColorSummary { get; set; }
        public string? BackgroundColorSummary { get; set; }
        public string? BorderColorSummary { get; set; }
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
        public string? DisplayExampleInput { get; set; }
        public string? DisplayExampleOutput { get; set; }
        public bool? PermissionButtonPresent { get; set; }
        public string? PermissionDialogStatus { get; set; }
        public string? PermissionSummary { get; set; }
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
                {
                    if (result.TryGetValue(evidence.ObjectId, out var existing))
                        result[evidence.ObjectId] = MergePropertyDialogEvidence(existing, evidence);
                    else
                        result[evidence.ObjectId] = evidence;
                }
            }
            catch
            {
                // Bad readback evidence is ignored here; the originating command keeps failure.txt.
            }
        }
        return result;
    }

    private static PropertyDialogEvidence MergePropertyDialogEvidence(PropertyDialogEvidence first, PropertyDialogEvidence second)
    {
        return new PropertyDialogEvidence
        {
            ObjectId = first.ObjectId,
            DialogTitle = !string.IsNullOrWhiteSpace(second.DialogTitle) ? second.DialogTitle : first.DialogTitle,
            EvidencePath = first.EvidencePath + ";" + second.EvidencePath,
            FontFamily = second.FontFamily ?? first.FontFamily,
            FontSize = second.FontSize ?? first.FontSize,
            FontStyle = second.FontStyle ?? first.FontStyle,
            HorizontalAlignment = second.HorizontalAlignment ?? first.HorizontalAlignment,
            VerticalAlignment = second.VerticalAlignment ?? first.VerticalAlignment,
            TextOrientation = second.TextOrientation ?? first.TextOrientation,
            ForegroundColorSummary = second.ForegroundColorSummary ?? first.ForegroundColorSummary,
            BackgroundColorSummary = second.BackgroundColorSummary ?? first.BackgroundColorSummary,
            BorderColorSummary = second.BorderColorSummary ?? first.BorderColorSummary,
            BorderStyle = second.BorderStyle ?? first.BorderStyle,
            ButtonType = second.ButtonType ?? first.ButtonType,
            TextEffect = second.TextEffect ?? first.TextEffect,
            UsesVector = second.UsesVector ?? first.UsesVector,
            UsesBitmap = second.UsesBitmap ?? first.UsesBitmap,
            DataObjectOperationEnabled = second.DataObjectOperationEnabled ?? first.DataObjectOperationEnabled,
            DataObjectOperation = second.DataObjectOperation ?? first.DataObjectOperation,
            DataObjectVariable = second.DataObjectVariable ?? first.DataObjectVariable,
            ScriptText = second.ScriptText ?? first.ScriptText,
            VisibilityUsesExpression = second.VisibilityUsesExpression ?? first.VisibilityUsesExpression,
            VisibilityExpression = second.VisibilityExpression ?? first.VisibilityExpression,
            VisibilityWhenNonZero = second.VisibilityWhenNonZero ?? first.VisibilityWhenNonZero,
            NumericMin = second.NumericMin ?? first.NumericMin,
            NumericMax = second.NumericMax ?? first.NumericMax,
            IntegerDigits = second.IntegerDigits ?? first.IntegerDigits,
            DecimalDigits = second.DecimalDigits ?? first.DecimalDigits,
            UnitText = second.UnitText ?? first.UnitText,
            NumericBase = second.NumericBase ?? first.NumericBase,
            LeadingZero = second.LeadingZero ?? first.LeadingZero,
            Rounding = second.Rounding ?? first.Rounding,
            Password = second.Password ?? first.Password,
            UnitEnabled = second.UnitEnabled ?? first.UnitEnabled,
            NaturalDecimalPlaces = second.NaturalDecimalPlaces ?? first.NaturalDecimalPlaces,
            DisplayExampleInput = second.DisplayExampleInput ?? first.DisplayExampleInput,
            DisplayExampleOutput = second.DisplayExampleOutput ?? first.DisplayExampleOutput,
            FontButtonPresent = (first.FontButtonPresent == true || second.FontButtonPresent == true)
                ? true
                : second.FontButtonPresent ?? first.FontButtonPresent,
            FontDialogStatus = string.Equals(second.FontDialogStatus, "PASS", StringComparison.OrdinalIgnoreCase)
                ? second.FontDialogStatus
                : first.FontDialogStatus ?? second.FontDialogStatus,
            PermissionButtonPresent = (first.PermissionButtonPresent == true || second.PermissionButtonPresent == true)
                ? true
                : second.PermissionButtonPresent ?? first.PermissionButtonPresent,
            PermissionDialogStatus = string.Equals(second.PermissionDialogStatus, "PASS", StringComparison.OrdinalIgnoreCase)
                ? second.PermissionDialogStatus
                : first.PermissionDialogStatus ?? second.PermissionDialogStatus,
            PermissionSummary = second.PermissionSummary ?? first.PermissionSummary
        };
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
            fontDialog.ValueKind == JsonValueKind.Object)
        {
            evidence.FontDialogStatus = JsonStringAny(fontDialog, "status", "Status");
            if (string.Equals(evidence.FontDialogStatus, "PASS", StringComparison.OrdinalIgnoreCase) &&
                fontDialog.TryGetProperty("extracted", out var extracted) && extracted.ValueKind == JsonValueKind.Object)
            {
                evidence.FontFamily = JsonStringAny(extracted, "fontFamily", "FontFamily");
                evidence.FontSize = JsonStringAny(extracted, "fontSize", "FontSize");
                evidence.FontStyle = JsonStringAny(extracted, "fontStyle", "FontStyle");
            }

            if (string.Equals(evidence.FontDialogStatus, "PASS", StringComparison.OrdinalIgnoreCase) &&
                fontDialog.TryGetProperty("controls", out var fontControls) && fontControls.ValueKind == JsonValueKind.Array)
            {
                var controls = fontControls.EnumerateArray().ToArray();
                evidence.FontFamily ??= TextValueAfterLabel(controls, "字体", "字体名", "Font");
                evidence.FontSize ??= TextValueAfterLabel(controls, "大小", "字号", "Size");
                evidence.FontStyle ??= TextValueAfterLabel(controls, "字形", "字型", "样式", "Font style");
            }
        }

        if (root.TryGetProperty("permissionDialog", out var permissionDialog) &&
            permissionDialog.ValueKind == JsonValueKind.Object)
        {
            evidence.PermissionDialogStatus = JsonStringAny(permissionDialog, "status", "Status");
            if (string.Equals(evidence.PermissionDialogStatus, "PASS", StringComparison.OrdinalIgnoreCase) &&
                permissionDialog.TryGetProperty("extracted", out var extracted) &&
                extracted.ValueKind == JsonValueKind.Object)
            {
                evidence.PermissionSummary = JsonStringAny(extracted, "summary", "Summary");
            }
        }

        foreach (var tab in tabs.EnumerateArray())
        {
            var tabName = JsonStringAny(tab, "Text", "text") ?? "";
            var allControls = tab.TryGetProperty("controls", out var controlArray) && controlArray.ValueKind == JsonValueKind.Array
                ? controlArray.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            var controls = allControls
                .Where(c => JsonBoolAny(c, "visible", "Visible") != false)
                .ToArray();
            evidence.FontButtonPresent = HasAnyText(controls, "字体") || evidence.FontButtonPresent == true;
            evidence.PermissionButtonPresent = HasAnyText(controls, "权限(&A)", "权限") || evidence.PermissionButtonPresent == true;
            evidence.ForegroundColorSummary ??= ColorSettingSummary(allControls, "文本颜色", "字符颜色", "字符颜色连接");
            evidence.BackgroundColorSummary ??= ColorSettingSummary(allControls, "背景色", "背景颜色", "填充颜色", "填充颜色连接");
            evidence.BorderColorSummary ??= ColorSettingSummary(allControls, "边线色", "边线颜色", "边线颜色连接");
            evidence.UsesVector ??= IsChecked(controls, "矢量图");
            evidence.UsesBitmap ??= IsChecked(controls, "位图");
            ParseVisibilityControls(allControls, evidence);

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
                evidence.DisplayExampleInput = TextAfterLabel(controls, "显示效果-例:");
                evidence.DisplayExampleOutput = EditTextAfterLabel(controls, "显示效果-例:");
                if (string.IsNullOrWhiteSpace(evidence.NumericBase) &&
                    LooksLikeDecimalFormatExample(evidence.DisplayExampleInput, evidence.DisplayExampleOutput))
                    evidence.NumericBase = "decimal-inferred-from-display-example";
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
                ParseVisibilityControls(controls, evidence);
            }
        }

        return evidence;
    }

    private static void ParseVisibilityControls(JsonElement[] controls, PropertyDialogEvidence evidence)
    {
        if (!HasAnyText(controls, "按钮可见", "按钮不可见", "对应图符可见", "对应图符不可见", "输入框构件可见", "输入框构件不可见"))
            return;
        evidence.VisibilityUsesExpression ??= IsChecked(controls, "表达式");
        var edit = controls.FirstOrDefault(c =>
            (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Edit", StringComparison.OrdinalIgnoreCase));
        evidence.VisibilityExpression ??= edit.ValueKind == JsonValueKind.Undefined
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

    private static bool HasAnyText(JsonElement[] controls, params string[] labels)
        => controls.Any(c => labels.Contains(JsonStringAny(c, "text", "Text") ?? "", StringComparer.Ordinal));

    private static string? ColorSettingSummary(JsonElement[] controls, params string[] labelsAndOptionalAnimationLabel)
    {
        if (labelsAndOptionalAnimationLabel.Length == 0)
            return null;
        var labels = labelsAndOptionalAnimationLabel.Take(labelsAndOptionalAnimationLabel.Length - 1).ToArray();
        var animationLabel = labelsAndOptionalAnimationLabel.Last();
        if (!HasAnyText(controls, labels))
            return null;
        var animation = IsChecked(controls, animationLabel);
        return animation switch
        {
            true => "configured; animationLink=enabled; rgb=unread",
            false => "configured; animationLink=disabled; rgb=unread",
            null => "configured; animationLink=not-exposed; rgb=unread"
        };
    }

    private static string? TextAfterLabel(JsonElement[] controls, string label)
    {
        var sequence = LayoutJsonIntAny(controls.FirstOrDefault(c => (JsonStringAny(c, "text", "Text") ?? "").Equals(label, StringComparison.Ordinal)), "sequence") ?? -1;
        if (sequence < 0) return null;
        return controls
            .Where(c => (LayoutJsonIntAny(c, "sequence") ?? 0) > sequence)
            .Where(c => (JsonStringAny(c, "className", "ClassName") ?? "").Equals("Static", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => LayoutJsonIntAny(c, "sequence") ?? 0)
            .Select(c => JsonStringAny(c, "text", "Text") ?? "")
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static bool LooksLikeDecimalFormatExample(string? input, string? output)
        => (!string.IsNullOrWhiteSpace(input) && input.Contains('.', StringComparison.Ordinal)) ||
           (!string.IsNullOrWhiteSpace(output) && output.Contains('.', StringComparison.Ordinal));

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
                : string.Equals(readback?.FontDialogStatus, "notApplicable", StringComparison.OrdinalIgnoreCase) ||
                  readback?.FontButtonPresent == false
                    ? NotApplicableProperty("property dialog exposes no font selector for this MCGS object/profile")
                : string.IsNullOrWhiteSpace(fontFamily)
                    ? UnresolvedProperty(id, "fontFamily", "Open property dialog text/style tab or run single-font diff.", unresolved)
                    : ValueProperty(fontFamily, "mce-text-anchor", 0.7),
            ["fontSize"] = !string.IsNullOrWhiteSpace(readback?.FontSize)
                ? ValueProperty(readback!.FontSize, "property-dialog-font-readback:" + readback.EvidencePath, 0.9)
                : string.Equals(readback?.FontDialogStatus, "notApplicable", StringComparison.OrdinalIgnoreCase) ||
                  readback?.FontButtonPresent == false
                    ? NotApplicableProperty("property dialog exposes no font-size selector for this MCGS object/profile")
                : UnresolvedProperty(id, "fontSize", "Run single-font-size diff or property-dialog text tab readback.", unresolved),
            ["fontStyle"] = !string.IsNullOrWhiteSpace(readback?.FontStyle)
                ? ValueProperty(readback!.FontStyle, "property-dialog-font-readback:" + readback.EvidencePath, 0.9)
                : string.Equals(readback?.FontDialogStatus, "notApplicable", StringComparison.OrdinalIgnoreCase) ||
                  readback?.FontButtonPresent == false
                    ? NotApplicableProperty("property dialog exposes no font-style selector for this MCGS object/profile")
                : UnresolvedProperty(id, "fontStyle", "Run property-dialog text tab readback for bold/italic/underline flags.", unresolved),
            ["alignment"] = readback?.HorizontalAlignment != null || readback?.VerticalAlignment != null
                ? ValueProperty(new
                {
                    horizontal = readback?.HorizontalAlignment ?? "unread",
                    vertical = readback?.VerticalAlignment ?? "unread"
                }, "property-dialog-readback:" + readback!.EvidencePath, 0.9)
                : UnresolvedProperty(id, "alignment", "Run property-dialog text/alignment tab readback or single-alignment diff.", unresolved),
            ["foregroundColor"] = !string.IsNullOrWhiteSpace(readback?.ForegroundColorSummary)
                ? ValueProperty(new { summary = readback!.ForegroundColorSummary, actualRgbStatus = "unread" },
                    "property-dialog-readback:" + readback.EvidencePath, 0.55)
                : UnresolvedProperty(id, "foregroundColor", "Run single-color diff or property-dialog color tab readback.", unresolved),
            ["backgroundColor"] = !string.IsNullOrWhiteSpace(readback?.BackgroundColorSummary)
                ? ValueProperty(new { summary = readback!.BackgroundColorSummary, actualRgbStatus = "unread" },
                    "property-dialog-readback:" + readback.EvidencePath, 0.55)
                : UnresolvedProperty(id, "backgroundColor", "Run single-color diff or property-dialog fill/background readback.", unresolved),
            ["borderColor"] = IsTextOnlyCanvasKind(kind)
                ? NotApplicableProperty("CDrawLabel/text-only object has no evidenced border property in the target row")
                : kind.Equals("numeric-input", StringComparison.OrdinalIgnoreCase) && readback != null &&
                  string.IsNullOrWhiteSpace(readback.BorderColorSummary)
                    ? NotApplicableProperty("numeric input property dialog exposes border type but no border-color selector in this profile")
                : !string.IsNullOrWhiteSpace(readback?.BorderColorSummary)
                    ? ValueProperty(new { summary = readback!.BorderColorSummary, actualRgbStatus = "unread" },
                        "property-dialog-readback:" + readback.EvidencePath, 0.55)
                : UnresolvedProperty(id, "borderColor", "Run property-dialog border tab readback or line-color diff.", unresolved),
            ["borderStyle"] = !string.IsNullOrWhiteSpace(readback?.BorderStyle)
                ? ValueProperty(readback!.BorderStyle, "property-dialog-readback:" + readback.EvidencePath, 0.9)
                : IsButtonLikeKind(kind) && !string.IsNullOrWhiteSpace(readback?.ButtonType)
                ? ValueProperty(new { mode = "intrinsic-button-frame", buttonType = readback!.ButtonType }, "property-dialog-readback:" + readback.EvidencePath, 0.8)
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
                ? readback != null
                    ? ValueProperty(new { status = "default-enabled", condition = "", evidence = "no enable-condition field exposed in captured property tabs" },
                        "property-dialog-readback:" + readback.EvidencePath, 0.7)
                    : UnresolvedProperty(id, "enableCondition", "Read operation/security tab for enable condition.", unresolved)
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
                    naturalDecimalPlaces = readback.NaturalDecimalPlaces,
                    displayExampleInput = readback.DisplayExampleInput,
                    displayExampleOutput = readback.DisplayExampleOutput
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
                ? !string.IsNullOrWhiteSpace(readback?.PermissionSummary)
                    ? ValueProperty(new { summary = readback!.PermissionSummary }, "property-dialog-permission-readback:" + readback.EvidencePath, 0.85)
                    : readback?.PermissionButtonPresent == true
                        ? UnresolvedProperty(id, "permissions", "Open read-only permission subdialog and capture permission level without saving.", unresolved)
                        : readback != null
                        ? NotApplicableProperty("no permission button exposed in captured property tabs")
                        : UnresolvedProperty(id, "permissions", "Read operation/security tab for permission level.", unresolved)
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

    private static bool IsButtonLikeKind(string kind)
        => kind.Contains("button", StringComparison.OrdinalIgnoreCase);

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
