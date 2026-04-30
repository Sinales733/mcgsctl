using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string CandidateFileName = "candidate.MCE";
    private const string WorkflowResultsDirName = "workflow-results";
    private static readonly string[] FinalValidatorPaths =
    {
        "profile-check.json",
        "project-check/check-result.json",
        "safety-result.json"
    };

    private sealed class ResultCheck
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "UNKNOWN";
        public bool Required { get; set; }
        public string? Message { get; set; }
    }

    private sealed class WorkflowIndexEntry
    {
        public string OperationId { get; set; } = "";
        public string Workflow { get; set; } = "";
        public string Path { get; set; } = "";
        public bool MutatesCandidate { get; set; }
        public string StartedAt { get; set; } = "";
        public string FinishedAt { get; set; } = "";
    }

    private sealed class ApprovalRequiredResult
    {
        public string Kind { get; set; } = "";
        public string Path { get; set; } = "";
        public string RequiredStatus { get; set; } = "PASS";
        public string ShaPolicy { get; set; } = "finalCandidate";
    }

    private sealed class ApprovalDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string Operator { get; set; } = "template";
        public string ApprovedAt { get; set; } = "";
        public string Source { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public string Candidate { get; set; } = "";
        public string CandidateSha256 { get; set; } = "";
        public List<ApprovalRequiredResult> RequiredResults { get; set; } = new();
        public Dictionary<string, string> ResultSha256 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Notes { get; set; } = "";
    }

    private sealed class CandidateSummaryBuild
    {
        public string WorkDir { get; init; } = "";
        public string Candidate { get; init; } = "";
        public string Source { get; init; } = "";
        public string SourceSha256 { get; init; } = "";
        public string FinalCandidateSha256 { get; init; } = "";
        public string GeneratedAt { get; init; } = "";
        public string Verdict { get; set; } = "blocked";
        public List<string> BlockedReasons { get; } = new();
        public List<Dictionary<string, object?>> MutationChain { get; } = new();
        public List<ApprovalRequiredResult> RequiredResults { get; } = new();
        public Dictionary<string, string> ResultSha256 { get; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTimeOffset MaxRequiredFinishedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset LastMutationFinishedAt { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset SummaryGeneratedAt { get; set; } = DateTimeOffset.MinValue;
        public string CandidateFinalExport { get; init; } = "candidate-final/mce";
    }

    private sealed record Smart200ChannelFact(
        string ChannelText,
        string ParsedAddress,
        string Variable,
        string Access,
        int RowIndex,
        string ResultPath);

    private static JsonSerializerOptions ResultJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static JsonSerializerOptions JsonlOptions() => new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string? _dialogEvidenceRoot;
    private static int _dialogEvidenceSequence;

    private static void SetDialogEvidenceRoot(string? outDir)
    {
        _dialogEvidenceRoot = string.IsNullOrWhiteSpace(outDir) ? null : outDir;
        _dialogEvidenceSequence = 0;
        if (_dialogEvidenceRoot == null) return;
        Directory.CreateDirectory(_dialogEvidenceRoot);
        EnsureJsonlFile("dialogs.jsonl");
        EnsureJsonlFile("popups.jsonl");
        EnsureJsonlFile("startup-dialogs.jsonl");
    }

    private static void EnsureJsonlFile(string name)
    {
        if (_dialogEvidenceRoot == null) return;
        var path = Path.Combine(_dialogEvidenceRoot, name);
        if (!File.Exists(path)) File.WriteAllText(path, "", Encoding.UTF8);
    }

    private static void RecordDialogEvidence(string stream, int pid, IntPtr hwnd, string action, string state)
    {
        if (_dialogEvidenceRoot == null || hwnd == IntPtr.Zero) return;
        try
        {
            var screenshotRel = "";
            if (Native.IsWindow(hwnd))
            {
                var shotDir = Path.Combine(_dialogEvidenceRoot, "dialog-screenshots");
                Directory.CreateDirectory(shotDir);
                screenshotRel = "dialog-screenshots/" + Interlocked.Increment(ref _dialogEvidenceSequence).ToString("D4") +
                                "-" + SafeFile(stream) + "-" + hwnd.ToInt64().ToString("X") + ".png";
                TryScreenshot(hwnd, Path.Combine(_dialogEvidenceRoot, screenshotRel.Replace('/', Path.DirectorySeparatorChar)));
            }

            var buttons = Native.IsWindow(hwnd)
                ? UiAutomation.EnumerateChildren(hwnd)
                    .Where(h => Native.GetClass(h).Contains("Button", StringComparison.OrdinalIgnoreCase))
                    .Select(Native.GetText)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();

            var record = new
            {
                timestamp = DateTimeOffset.Now.ToString("O"),
                pid,
                hwnd = "0x" + hwnd.ToInt64().ToString("X"),
                className = Native.IsWindow(hwnd) ? Native.GetClass(hwnd) : "",
                title = Native.IsWindow(hwnd) ? Native.GetText(hwnd) : "",
                body = Native.IsWindow(hwnd) ? DialogText(hwnd) : "",
                buttons,
                action,
                state,
                screenshot = screenshotRel
            };
            var path = Path.Combine(_dialogEvidenceRoot, stream);
            File.AppendAllText(path, JsonSerializer.Serialize(record, JsonlOptions()) + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Dialog evidence must not hide the original GUI automation result.
        }
    }

    private static void RecordOpenPopups(int pid, string state)
    {
        foreach (var popup in UiAutomation.TopWindowsForPid(pid).Where(h => Native.GetClass(h) == "#32768"))
            RecordDialogEvidence("popups.jsonl", pid, popup, "observed", state);
    }

    private static ResultCheck RequiredPass(string name, string? message = null)
        => new() { Name = name, Status = "PASS", Required = true, Message = message };

    private static ResultCheck RequiredUnknown(string name, string message)
        => new() { Name = name, Status = "UNKNOWN", Required = true, Message = message };

    private static ResultCheck RequiredFail(string name, string message)
        => new() { Name = name, Status = "FAIL", Required = true, Message = message };

    private static string WorkflowResultsDir(string workDir)
        => Path.Combine(workDir, WorkflowResultsDirName);

    private static string WorkflowIndexPath(string workDir)
        => Path.Combine(WorkflowResultsDir(workDir), "index.json");

    private static string NewOperationId(string? workDir, string workflowName)
    {
        var sequence = 1;
        if (!string.IsNullOrWhiteSpace(workDir))
        {
            var index = LoadWorkflowIndex(workDir);
            sequence = index.Count + 1;
        }
        return $"{sequence:D4}-{SafeFile(workflowName)}-{DateTime.Now:yyyyMMddTHHmmss}";
    }

    private static List<WorkflowIndexEntry> LoadWorkflowIndex(string workDir)
    {
        var path = WorkflowIndexPath(workDir);
        if (!File.Exists(path)) return new List<WorkflowIndexEntry>();
        return JsonSerializer.Deserialize<List<WorkflowIndexEntry>>(File.ReadAllText(path, Encoding.UTF8),
            ResultJsonOptions()) ?? new List<WorkflowIndexEntry>();
    }

    private static void AppendWorkflowIndex(string workDir, WorkflowIndexEntry entry)
    {
        Directory.CreateDirectory(WorkflowResultsDir(workDir));
        var index = LoadWorkflowIndex(workDir);
        if (index.Any(x => x.OperationId.Equals(entry.OperationId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Workflow result operationId already exists: " + entry.OperationId);
        index.Add(entry);
        File.WriteAllText(WorkflowIndexPath(workDir), JsonSerializer.Serialize(index, ResultJsonOptions()), Encoding.UTF8);
    }

    private static string RelativeResultPath(string workDir, string resultPath)
        => Path.GetRelativePath(workDir, resultPath).Replace('\\', '/');

    private static bool IsValidResultStatus(string status)
        => status is "PASS" or "FAIL" or "UNKNOWN";

    private static bool IsValidCheckStatus(string status)
        => status is "PASS" or "FAIL" or "UNKNOWN" or "NOT_REQUESTED" or "WARNING";

    private static string ComputeResultStatus(IEnumerable<ResultCheck> checks, IEnumerable<string>? limitations = null)
    {
        var list = checks.ToArray();
        var limitationList = (limitations ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (list.Any(c => !IsValidCheckStatus(c.Status))) return "FAIL";
        if (list.Any(c => c.Required && c.Status == "FAIL")) return "FAIL";
        if (list.Any(c => c.Required && c.Status is "UNKNOWN" or "NOT_REQUESTED")) return "UNKNOWN";
        if (list.Any(c => !c.Required && c.Status is "WARNING" or "UNKNOWN" &&
                          !limitationList.Any(l => l.Contains(c.Name, StringComparison.OrdinalIgnoreCase))))
            return "UNKNOWN";
        return "PASS";
    }

    private static string? ValidateResultDocument(JsonElement root, string actualCandidateSha, bool finalCandidateRequired)
    {
        if (!TryParseRequiredDate(root, "startedAt", out var startedAt, out var startedError))
            return startedError;
        if (!TryParseRequiredDate(root, "finishedAt", out var finishedAt, out var finishedError))
            return finishedError;
        if (finishedAt < startedAt) return "finishedAt is earlier than startedAt";
        var status = JsonString(root, "status") ?? "";
        if (!IsValidResultStatus(status)) return "invalid result.status: " + status;
        if (!root.TryGetProperty("checks", out var checks) || checks.ValueKind != JsonValueKind.Array)
            return "missing checks[]";
        var limitations = root.TryGetProperty("limitations", out var lim) && lim.ValueKind == JsonValueKind.Array
            ? lim.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToArray()
            : Array.Empty<string>();
        foreach (var check in checks.EnumerateArray())
        {
            var checkStatus = JsonString(check, "status") ?? "";
            var required = check.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True;
            var name = JsonString(check, "name") ?? "<unnamed>";
            if (!IsValidCheckStatus(checkStatus)) return $"invalid check.status for {name}: {checkStatus}";
            if (required && checkStatus == "NOT_REQUESTED") return $"required check is NOT_REQUESTED: {name}";
            if (status == "PASS" && required && checkStatus != "PASS") return $"PASS result has non-PASS required check: {name}";
            if (!required && checkStatus is "WARNING" or "UNKNOWN" &&
                !limitations.Any(l => l.Contains(name, StringComparison.OrdinalIgnoreCase)))
                return $"non-required {checkStatus} check is missing from limitations: {name}";
        }
        if (finalCandidateRequired)
        {
            var resultSha = JsonString(root, "candidateSha256");
            if (!actualCandidateSha.Equals(resultSha, StringComparison.OrdinalIgnoreCase))
                return "final validator candidateSha256 does not match actual candidate";
        }
        return null;
    }

    private static void WriteMutatingWorkflowResult(
        WorkflowProjectContext context,
        string outDir,
        IEnumerable<ResultCheck> checks,
        IEnumerable<string>? limitations = null,
        Dictionary<string, object?>? extra = null)
    {
        var limitationList = (limitations ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var checkList = checks.ToArray();
        var status = ComputeResultStatus(checkList, limitationList);
        var finishedAt = DateTimeOffset.Now;
        var afterSha = Sha256(context.Project);
        var workDir = context.WorkDir ?? outDir;
        var resultDir = WorkflowResultsDir(workDir);
        Directory.CreateDirectory(resultDir);
        var resultPath = Path.Combine(resultDir, context.OperationId + ".json");
        if (File.Exists(resultPath)) throw new InvalidOperationException("Workflow result already exists: " + resultPath);
        var relPath = RelativeResultPath(workDir, resultPath);
        var doc = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["kind"] = "workflow",
            ["operationId"] = context.OperationId,
            ["workflow"] = context.WorkflowName,
            ["mutatesCandidate"] = true,
            ["status"] = status,
            ["sourceSha256"] = context.SourceSha256,
            ["candidate"] = context.Project,
            ["candidateSha256Before"] = context.ProjectSha256Before,
            ["candidateSha256After"] = afterSha,
            ["startedAt"] = context.OperationStartedAt.ToString("O"),
            ["finishedAt"] = finishedAt.ToString("O"),
            ["checks"] = checkList,
            ["limitations"] = limitationList
        };
        if (extra != null)
        {
            foreach (var kv in extra) doc[kv.Key] = kv.Value;
        }
        File.WriteAllText(resultPath, JsonSerializer.Serialize(doc, ResultJsonOptions()), Encoding.UTF8);
        AppendWorkflowIndex(workDir, new WorkflowIndexEntry
        {
            OperationId = context.OperationId,
            Workflow = context.WorkflowName,
            Path = relPath,
            MutatesCandidate = true,
            StartedAt = context.OperationStartedAt.ToString("O"),
            FinishedAt = finishedAt.ToString("O")
        });
        if (!string.IsNullOrWhiteSpace(context.WorkspaceMarker))
            UpdateWorkspaceMarkerCurrentSha(context.WorkspaceMarker, afterSha);
    }

    private static void WriteFinalValidatorResult(
        WorkflowProjectContext context,
        string relativePath,
        string kind,
        string operationName,
        IEnumerable<ResultCheck> checks,
        IEnumerable<string>? limitations = null,
        Dictionary<string, object?>? extra = null)
    {
        if (context.WorkDir == null) return;
        var limitationList = (limitations ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var checkList = checks.ToArray();
        var status = ComputeResultStatus(checkList, limitationList);
        var candidateSha = TrySha256(context.Project, out _) ?? context.ProjectSha256Before;
        var doc = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["kind"] = kind,
            ["operationId"] = context.OperationId,
            ["workflow"] = operationName,
            ["mutatesCandidate"] = false,
            ["status"] = status,
            ["sourceSha256"] = context.SourceSha256,
            ["candidate"] = context.Project,
            ["candidateSha256"] = candidateSha,
            ["startedAt"] = context.OperationStartedAt.ToString("O"),
            ["finishedAt"] = DateTimeOffset.Now.ToString("O"),
            ["checks"] = checkList,
            ["limitations"] = limitationList
        };
        if (extra != null)
        {
            foreach (var kv in extra) doc[kv.Key] = kv.Value;
        }
        var output = Path.Combine(context.WorkDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? context.WorkDir);
        File.WriteAllText(output, JsonSerializer.Serialize(doc, ResultJsonOptions()), Encoding.UTF8);
    }

    private static IEnumerable<ResultCheck> ProjectCheckChecks(ProjectCheckResult check)
    {
        if (check.Passed) return new[] { RequiredPass("mcgs-project-check", check.VerdictReason) };
        if (check.Unknown) return new[] { RequiredUnknown("mcgs-project-check", check.VerdictReason) };
        return new[] { RequiredFail("mcgs-project-check", check.VerdictReason) };
    }

    private static Dictionary<string, object?> ProjectCheckExtra(ProjectCheckResult check)
        => new()
        {
            ["errors"] = check.ErrorCount,
            ["warnings"] = check.WarningCount,
            ["dialogTexts"] = check.DialogTexts,
            ["dialogTitles"] = check.DialogTitles,
            ["resultRows"] = check.ResultRows,
            ["resultControlCount"] = check.ResultControlCount,
            ["resultControlsReadable"] = check.ResultControlsReadable,
            ["verdictReason"] = check.VerdictReason
        };

    private static void UpdateWorkspaceMarkerCurrentSha(string markerPath, string currentSha)
    {
        if (!File.Exists(markerPath)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
        var root = doc.RootElement;
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["schemaVersion"] = JsonInt(root, "schemaVersion") ?? JsonInt(root, "SchemaVersion") ?? 1,
            ["createdBy"] = JsonMarkerString(root, "createdBy") ?? "mcgsctl",
            ["source"] = JsonMarkerString(root, "source") ?? "",
            ["sourceSha256"] = JsonMarkerString(root, "sourceSha256") ?? "",
            ["workingCopy"] = JsonMarkerString(root, "workingCopy") ?? "",
            ["initialWorkingCopySha256"] = JsonMarkerString(root, "initialWorkingCopySha256") ??
                                           JsonMarkerString(root, "workingCopySha256Before") ?? "",
            ["currentCandidateSha256"] = currentSha,
            ["mutationResultsIndex"] = JsonMarkerString(root, "mutationResultsIndex") ?? "workflow-results/index.json",
            ["createdAt"] = JsonMarkerString(root, "createdAt") ?? DateTimeOffset.Now.ToString("O")
        };
        File.WriteAllText(markerPath, JsonSerializer.Serialize(map, ResultJsonOptions()), Encoding.UTF8);
    }

    private static int Candidate(string[] args)
    {
        if (args.Length < 2)
            return Fail("Usage: mcgsctl candidate summarize|validate --workdir <runDir> [--approval <approval.json>]");
        return args[1].ToLowerInvariant() switch
        {
            "summarize" => CandidateSummarize(args),
            "validate" => CandidateValidate(args),
            _ => Fail("Unknown candidate command: " + args[1])
        };
    }

    private static int CandidateSummarize(string[] args)
    {
        try
        {
            var workDir = FullPath(Required(args, "--workdir"));
            var summary = BuildCandidateSummary(workDir, writeOutputs: true);
            Console.WriteLine("candidate summary: " + Path.Combine(workDir, "candidate-summary.json"));
            Console.WriteLine("verdict: " + summary.Verdict);
            foreach (var reason in summary.BlockedReasons) Console.WriteLine("blocked: " + reason);
            return summary.Verdict == "apply-ready" ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("candidate summarize failed: " + ex.Message);
            return 1;
        }
    }

    private static int CandidateValidate(string[] args)
    {
        try
        {
            var workDir = FullPath(Required(args, "--workdir"));
            var summary = BuildCandidateSummary(workDir, writeOutputs: false);
            var approval = Opt(args, "--approval");
            if (!string.IsNullOrWhiteSpace(approval))
            {
                var approvalProblems = ValidateApproval(FullPath(approval), summary, forApply: false);
                summary.BlockedReasons.AddRange(approvalProblems);
            }
            if (summary.BlockedReasons.Count > 0)
            {
                foreach (var reason in summary.BlockedReasons) Console.Error.WriteLine("blocked: " + reason);
                return 2;
            }
            return 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine("candidate validate parse error: " + ex.Message);
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine("candidate validate I/O error: " + ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("candidate validate failed: " + ex.Message);
            return 1;
        }
    }

    private static CandidateSummaryBuild BuildCandidateSummary(string workDir, bool writeOutputs)
    {
        workDir = FullPath(workDir);
        var markerPath = Path.Combine(workDir, "mcgsctl-workspace.json");
        if (!File.Exists(markerPath)) throw new FileNotFoundException("Missing workspace marker: " + markerPath);
        using var markerDoc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
        var marker = markerDoc.RootElement;
        var candidate = FullPath(JsonMarkerString(marker, "workingCopy") ?? Path.Combine(workDir, CandidateFileName));
        if (!File.Exists(candidate)) throw new FileNotFoundException("Missing candidate: " + candidate);
        var actualSha = Sha256(candidate);
        var source = JsonMarkerString(marker, "source") ?? "";
        var sourceSha = JsonMarkerString(marker, "sourceSha256") ?? "";
        var generatedAt = DateTimeOffset.Now;
        var summary = new CandidateSummaryBuild
        {
            WorkDir = workDir,
            Candidate = candidate,
            Source = source,
            SourceSha256 = sourceSha,
            FinalCandidateSha256 = actualSha,
            GeneratedAt = generatedAt.ToString("O"),
            SummaryGeneratedAt = generatedAt
        };

        var initialSha = JsonMarkerString(marker, "initialWorkingCopySha256") ??
                         JsonMarkerString(marker, "workingCopySha256Before") ?? "";
        var markerCurrent = JsonMarkerString(marker, "currentCandidateSha256") ?? initialSha;
        if (!actualSha.Equals(markerCurrent, StringComparison.OrdinalIgnoreCase))
            summary.BlockedReasons.Add("marker.currentCandidateSha256 does not match actual candidate SHA");

        if (writeOutputs)
        {
            var finalDir = Path.Combine(workDir, "candidate-final");
            Directory.CreateDirectory(finalDir);
            File.WriteAllText(Path.Combine(finalDir, "candidate.sha256"), actualSha + "  candidate.MCE", Encoding.UTF8);
            try
            {
                ExportMceSnapshot(candidate, Path.Combine(finalDir, "mce"));
            }
            catch (Exception ex)
            {
                summary.BlockedReasons.Add("candidate final MCE export failed: " + ex.Message);
            }
        }

        var index = LoadWorkflowIndex(workDir);
        var previousAfter = initialSha;
        foreach (var entry in index)
        {
            var resultPath = Path.Combine(workDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(resultPath))
            {
                summary.BlockedReasons.Add("missing workflow result: " + entry.Path);
                continue;
            }
            using var resultDoc = JsonDocument.Parse(File.ReadAllText(resultPath, Encoding.UTF8));
            var root = resultDoc.RootElement;
            var operationId = JsonString(root, "operationId") ?? "";
            if (!operationId.Equals(entry.OperationId, StringComparison.OrdinalIgnoreCase))
                summary.BlockedReasons.Add("index/result operationId mismatch: " + entry.Path);
            if (!Path.GetFileNameWithoutExtension(resultPath).Equals(operationId, StringComparison.OrdinalIgnoreCase))
                summary.BlockedReasons.Add("result file stem does not match operationId: " + entry.Path);
            if (!entry.MutatesCandidate)
                summary.BlockedReasons.Add("index entry is not mutating: " + entry.Path);
            var resultMutates = root.TryGetProperty("mutatesCandidate", out var mutates) &&
                                mutates.ValueKind == JsonValueKind.True;
            if (!resultMutates)
                summary.BlockedReasons.Add("workflow result mutatesCandidate is not true: " + entry.Path);
            if (!root.TryGetProperty("candidateSha256Before", out var beforeProp) ||
                beforeProp.ValueKind is not JsonValueKind.String)
                summary.BlockedReasons.Add("mutating result missing candidateSha256Before: " + entry.Path);
            if (!root.TryGetProperty("candidateSha256After", out var afterProp) ||
                afterProp.ValueKind is not JsonValueKind.String)
                summary.BlockedReasons.Add("mutating result missing candidateSha256After: " + entry.Path);
            var resultProblem = ValidateResultDocument(root, actualSha, finalCandidateRequired: false);
            if (resultProblem != null) summary.BlockedReasons.Add(entry.Path + ": " + resultProblem);
            var status = JsonString(root, "status") ?? "";
            if (status != "PASS") summary.BlockedReasons.Add(entry.Path + " status is " + status);
            var before = JsonString(root, "candidateSha256Before") ?? "";
            var after = JsonString(root, "candidateSha256After") ?? "";
            if (!before.Equals(previousAfter, StringComparison.OrdinalIgnoreCase))
                summary.BlockedReasons.Add("mutation chain mismatch before " + operationId);
            previousAfter = after;
            var finishedAt = TryGetValidFinishedAt(root);
            if (finishedAt.HasValue && finishedAt.Value > summary.LastMutationFinishedAt)
                summary.LastMutationFinishedAt = finishedAt.Value;
            summary.MutationChain.Add(new Dictionary<string, object?>
            {
                ["operationId"] = operationId,
                ["path"] = entry.Path,
                ["before"] = before,
                ["after"] = after,
                ["finishedAt"] = JsonString(root, "finishedAt")
            });
            summary.RequiredResults.Add(new ApprovalRequiredResult
            {
                Kind = "workflow",
                Path = entry.Path,
                RequiredStatus = "PASS",
                ShaPolicy = "chain"
            });
            summary.ResultSha256[entry.Path] = Sha256(resultPath);
            if (finishedAt.HasValue && finishedAt.Value > summary.MaxRequiredFinishedAt)
                summary.MaxRequiredFinishedAt = finishedAt.Value;
        }
        if (index.Count > 0 && !previousAfter.Equals(actualSha, StringComparison.OrdinalIgnoreCase))
            summary.BlockedReasons.Add("last mutation SHA does not match actual candidate SHA");
        if (index.Count == 0 && !initialSha.Equals(actualSha, StringComparison.OrdinalIgnoreCase))
            summary.BlockedReasons.Add("candidate changed without workflow mutation results");

        foreach (var rel in FinalValidatorPaths)
        {
            var path = Path.Combine(workDir, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                summary.BlockedReasons.Add("missing required final validator: " + rel);
                continue;
            }
            using var resultDoc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var root = resultDoc.RootElement;
            if (!root.TryGetProperty("mutatesCandidate", out var finalMutatesProp))
                summary.BlockedReasons.Add(rel + " missing mutatesCandidate");
            else if (finalMutatesProp.ValueKind != JsonValueKind.False)
                summary.BlockedReasons.Add(rel + " mutatesCandidate must be false");
            var problem = ValidateResultDocument(root, actualSha, finalCandidateRequired: true);
            if (problem != null) summary.BlockedReasons.Add(rel + ": " + problem);
            var status = JsonString(root, "status") ?? "";
            if (status != "PASS") summary.BlockedReasons.Add(rel + " status is " + status);
            var finishedAt = TryGetValidFinishedAt(root);
            if (summary.LastMutationFinishedAt != DateTimeOffset.MinValue &&
                (!finishedAt.HasValue || finishedAt.Value <= summary.LastMutationFinishedAt))
                summary.BlockedReasons.Add(rel + " is not later than the last mutating workflow");
            if (finishedAt.HasValue && finishedAt.Value > summary.MaxRequiredFinishedAt)
                summary.MaxRequiredFinishedAt = finishedAt.Value;
            summary.RequiredResults.Add(new ApprovalRequiredResult
            {
                Kind = rel.StartsWith("profile", StringComparison.OrdinalIgnoreCase) ? "profile" :
                    rel.StartsWith("project-check", StringComparison.OrdinalIgnoreCase) ? "project-check" : "safety",
                Path = rel,
                RequiredStatus = "PASS",
                ShaPolicy = "finalCandidate"
            });
            summary.ResultSha256[rel] = Sha256(path);
        }

        summary.Verdict = summary.BlockedReasons.Count == 0 ? "apply-ready" : "blocked";
        if (writeOutputs)
        {
            WriteCandidateSummaryFiles(summary);
        }
        else
        {
            ValidateExistingCandidateSummary(summary);
        }
        return summary;
    }

    private static void ValidateExistingCandidateSummary(CandidateSummaryBuild summary)
    {
        var summaryPath = Path.Combine(summary.WorkDir, "candidate-summary.json");
        if (!File.Exists(summaryPath))
        {
            summary.BlockedReasons.Add("missing candidate-summary.json; run candidate summarize first");
            summary.Verdict = "blocked";
            return;
        }
        summary.ResultSha256["candidate-summary.json"] = Sha256(summaryPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(summaryPath, Encoding.UTF8));
        var root = doc.RootElement;
        var verdict = JsonString(root, "verdict") ?? "";
        var finalSha = JsonString(root, "finalCandidateSha256") ?? "";
        if (!TryParseRequiredDate(root, "generatedAt", out var generatedAt, out var generatedError))
            summary.BlockedReasons.Add("candidate-summary.json: " + generatedError);
        else
            summary.SummaryGeneratedAt = generatedAt;
        if (verdict != "apply-ready") summary.BlockedReasons.Add("candidate-summary.verdict is not apply-ready");
        if (!finalSha.Equals(summary.FinalCandidateSha256, StringComparison.OrdinalIgnoreCase))
            summary.BlockedReasons.Add("candidate-summary finalCandidateSha256 does not match actual candidate");
        if (generatedAt < summary.MaxRequiredFinishedAt)
            summary.BlockedReasons.Add("candidate-summary.generatedAt is older than required results");
        summary.Verdict = summary.BlockedReasons.Count == 0 ? "apply-ready" : "blocked";
    }

    private static void WriteCandidateSummaryFiles(CandidateSummaryBuild summary)
    {
        var summaryPath = Path.Combine(summary.WorkDir, "candidate-summary.json");
        var jsonDoc = new
        {
            schemaVersion = 1,
            generatedAt = summary.GeneratedAt,
            verdict = summary.Verdict,
            source = summary.Source,
            sourceSha256 = summary.SourceSha256,
            candidate = summary.Candidate,
            finalCandidateSha256 = summary.FinalCandidateSha256,
            candidateFinalExport = summary.CandidateFinalExport,
            mutationChain = summary.MutationChain,
            blockedReasons = summary.BlockedReasons,
            requiredResults = summary.RequiredResults,
            resultSha256 = summary.ResultSha256
        };
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(jsonDoc, ResultJsonOptions()), Encoding.UTF8);
        summary.ResultSha256["candidate-summary.json"] = Sha256(summaryPath);
        var md = new StringBuilder();
        md.AppendLine("# Candidate Summary");
        md.AppendLine();
        md.AppendLine("- Verdict: `" + summary.Verdict + "`");
        md.AppendLine("- Candidate SHA256: `" + summary.FinalCandidateSha256 + "`");
        md.AppendLine("- Candidate: `" + summary.Candidate + "`");
        md.AppendLine();
        if (summary.BlockedReasons.Count > 0)
        {
            md.AppendLine("## Blocked Reasons");
            foreach (var reason in summary.BlockedReasons) md.AppendLine("- " + reason);
        }
        else
        {
            md.AppendLine("No blocked reasons. Review evidence before approval.");
        }
        File.WriteAllText(Path.Combine(summary.WorkDir, "candidate-summary.md"), md.ToString(), Encoding.UTF8);
        var approval = new ApprovalDocument
        {
            Operator = "template",
            ApprovedAt = "",
            Source = summary.Source,
            SourceSha256 = summary.SourceSha256,
            Candidate = summary.Candidate,
            CandidateSha256 = summary.FinalCandidateSha256,
            RequiredResults = summary.RequiredResults,
            ResultSha256 = summary.ResultSha256,
            Notes = "Fill operator and approvedAt after reviewing candidate-summary.md and evidence."
        };
        File.WriteAllText(Path.Combine(summary.WorkDir, "approval.template.json"),
            JsonSerializer.Serialize(approval, ResultJsonOptions()), Encoding.UTF8);
    }

    private static bool TryParseRequiredDate(JsonElement root, string property, out DateTimeOffset value, out string error)
    {
        value = default;
        error = "";
        var raw = JsonString(root, property);
        if (string.IsNullOrWhiteSpace(raw) || !DateTimeOffset.TryParse(raw, out value))
        {
            error = "invalid or missing " + property;
            return false;
        }
        return true;
    }

    private static DateTimeOffset? TryGetValidFinishedAt(JsonElement root)
    {
        if (!TryParseRequiredDate(root, "startedAt", out var startedAt, out _)) return null;
        if (!TryParseRequiredDate(root, "finishedAt", out var finishedAt, out _)) return null;
        return finishedAt >= startedAt ? finishedAt : null;
    }

    private static bool SamePathForApproval(string approvalPath, string summaryPath)
    {
        if (string.IsNullOrWhiteSpace(approvalPath) || string.IsNullOrWhiteSpace(summaryPath)) return false;
        try
        {
            return Path.GetFullPath(approvalPath).Equals(Path.GetFullPath(summaryPath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return approvalPath.Equals(summaryPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadSha256Sidecar(string path)
    {
        if (!File.Exists(path)) return "";
        var text = File.ReadAllText(path, Encoding.UTF8).Trim();
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    }

    private static string? JsonMarkerString(JsonElement root, string camelName)
    {
        var exact = JsonString(root, camelName);
        if (exact != null) return exact;
        if (string.IsNullOrEmpty(camelName)) return null;
        var pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return JsonString(root, pascalName);
    }

    private static List<string> ValidateApproval(string approvalPath, CandidateSummaryBuild summary, bool forApply)
    {
        var problems = new List<string>();
        using var doc = JsonDocument.Parse(File.ReadAllText(approvalPath, Encoding.UTF8));
        var root = doc.RootElement;
        var op = JsonString(root, "operator") ?? "";
        if (string.IsNullOrWhiteSpace(op) ||
            op.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("template", StringComparison.OrdinalIgnoreCase) ||
            op.Equals("TODO", StringComparison.OrdinalIgnoreCase))
            problems.Add("approval.operator is not a real operator");
        var approvedAtRaw = JsonString(root, "approvedAt");
        if (string.IsNullOrWhiteSpace(approvedAtRaw) || !DateTimeOffset.TryParse(approvedAtRaw, out var approvedAt))
        {
            problems.Add("approval.approvedAt is not a valid ISO-8601 timestamp");
        }
        else
        {
            if (approvedAt < summary.MaxRequiredFinishedAt)
                problems.Add("approval.approvedAt is older than required result finishedAt");
            if (summary.SummaryGeneratedAt != DateTimeOffset.MinValue && approvedAt < summary.SummaryGeneratedAt)
                problems.Add("approval.approvedAt is older than candidate-summary.generatedAt");
        }
        var approvalSource = JsonString(root, "source") ?? "";
        if (!SamePathForApproval(approvalSource, summary.Source))
            problems.Add("approval source path does not match candidate summary");
        var approvalCandidate = JsonString(root, "candidate") ?? "";
        if (!SamePathForApproval(approvalCandidate, summary.Candidate))
            problems.Add("approval candidate path does not match candidate summary");
        var approvalCandidateSha = JsonString(root, "candidateSha256") ?? "";
        if (!approvalCandidateSha.Equals(summary.FinalCandidateSha256, StringComparison.OrdinalIgnoreCase))
            problems.Add("approval candidateSha256 does not match final candidate");
        var approvalSourceSha = JsonString(root, "sourceSha256") ?? "";
        if (!approvalSourceSha.Equals(summary.SourceSha256, StringComparison.OrdinalIgnoreCase))
            problems.Add("approval sourceSha256 does not match workspace source");

        if (!root.TryGetProperty("resultSha256", out var shaMap) || shaMap.ValueKind != JsonValueKind.Object)
        {
            problems.Add("approval.resultSha256 is missing");
            return problems;
        }

        foreach (var kv in summary.ResultSha256)
        {
            if (!shaMap.TryGetProperty(kv.Key, out var value))
            {
                problems.Add("approval.resultSha256 missing " + kv.Key);
                continue;
            }
            if (!string.Equals(value.GetString(), kv.Value, StringComparison.OrdinalIgnoreCase))
                problems.Add("approval.resultSha256 mismatch for " + kv.Key);
        }

        if (!root.TryGetProperty("requiredResults", out var required) || required.ValueKind != JsonValueKind.Array)
        {
            problems.Add("approval.requiredResults is missing");
            return problems;
        }
        var approvalRequiredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in required.EnumerateArray())
        {
            var path = JsonString(item, "path") ?? "";
            if (!string.IsNullOrWhiteSpace(path)) approvalRequiredPaths.Add(path);
            var kind = JsonString(item, "kind") ?? "";
            var requiredStatus = JsonString(item, "requiredStatus") ?? "";
            var shaPolicy = JsonString(item, "shaPolicy") ?? "";
            var expected = summary.RequiredResults.FirstOrDefault(x =>
                x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (shaPolicy is not ("chain" or "finalCandidate"))
                problems.Add("approval required result has invalid shaPolicy: " + path);
            if (expected == null)
            {
                problems.Add("approval required result is not in candidate summary: " + path);
                continue;
            }
            if (!requiredStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase))
                problems.Add("approval required result requiredStatus is not PASS: " + path);
            if (!kind.Equals(expected.Kind, StringComparison.OrdinalIgnoreCase))
                problems.Add("approval required result kind mismatch for " + path);
            if (!shaPolicy.Equals(expected.ShaPolicy, StringComparison.OrdinalIgnoreCase))
                problems.Add("approval required result shaPolicy mismatch for " + path);
        }
        foreach (var expected in summary.RequiredResults)
        {
            if (!approvalRequiredPaths.Contains(expected.Path))
                problems.Add("approval.requiredResults missing " + expected.Path);
        }
        return problems;
    }

    private static int WorkflowProjectApplyCandidate(string[] args)
    {
        var official = RequiredPath(args, "--source");
        var candidate = RequiredPath(args, "--candidate");
        var approval = RequiredPath(args, "--approval");
        try
        {
            if (Path.GetFullPath(official).Equals(Path.GetFullPath(candidate), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("officialPath and candidatePath must be different.");
            var ext = Path.GetExtension(candidate);
            if (!ext.Equals(".MCE", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Candidate extension must be .MCE/.mce.");
            if (candidate.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(p => p.Equals("rollback", StringComparison.OrdinalIgnoreCase) ||
                              p.Equals(".mcgsctl-rollback", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Candidate must not be inside a rollback directory.");
            EnsureSourceCanBeCopied(official, allowOpenSource: false);
            var markerPath = ValidateWorkflowWorkspaceMarker(candidate);
            var workDir = Path.GetDirectoryName(markerPath) ?? throw new InvalidOperationException("Workspace directory not found.");
            var summary = BuildCandidateSummary(workDir, writeOutputs: false);
            if (summary.Verdict != "apply-ready")
                throw new InvalidOperationException("Candidate summary is not apply-ready: " + string.Join("; ", summary.BlockedReasons));
            var approvalProblems = ValidateApproval(approval, summary, forApply: true);
            if (approvalProblems.Count > 0)
                throw new InvalidOperationException("Approval is invalid: " + string.Join("; ", approvalProblems));
            var officialSha = Sha256(official);
            if (!officialSha.Equals(summary.SourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Official project SHA does not match approval/source SHA.");
            if (!Sha256(candidate).Equals(summary.FinalCandidateSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Candidate SHA changed after summarize.");

            var officialDir = Path.GetDirectoryName(official) ?? Environment.CurrentDirectory;
            var rollbackDir = FullPath(Opt(args, "--rollback-dir") ??
                                       Path.Combine(officialDir, ".mcgsctl-rollback", "apply-" + Timestamp()));
            Directory.CreateDirectory(rollbackDir);
            var originalCopy = Path.Combine(rollbackDir, "original.MCE");
            var candidateCopy = Path.Combine(rollbackDir, "candidate.MCE");
            File.Copy(official, originalCopy, overwrite: false);
            File.Copy(candidate, candidateCopy, overwrite: false);
            File.Copy(approval, Path.Combine(rollbackDir, "approval.json"), overwrite: false);
            File.WriteAllText(Path.Combine(rollbackDir, "original.sha256"), officialSha + "  original.MCE", Encoding.UTF8);
            File.WriteAllText(Path.Combine(rollbackDir, "candidate.sha256"), summary.FinalCandidateSha256 + "  candidate.MCE", Encoding.UTF8);

            var tempCandidate = Path.Combine(officialDir, ".mcgsctl-apply-" + Timestamp() + ".tmp");
            var replaceBackup = Path.Combine(rollbackDir, "official.replace-backup.MCE");
            File.Copy(candidate, tempCandidate, overwrite: false);
            try
            {
                File.Replace(tempCandidate, official, replaceBackup, ignoreMetadataErrors: false);
            }
            catch
            {
                if (File.Exists(tempCandidate)) TryDeleteFile(tempCandidate);
                if (!Sha256(official).Equals(officialSha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("File.Replace failed and official SHA changed. Manual inspection required.");
                throw;
            }
            var appliedSha = Sha256(official);
            if (!appliedSha.Equals(summary.FinalCandidateSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Apply completed but official SHA does not match candidate SHA.");
            var approvalSha = Sha256(approval);
            var metadata = new
            {
                schemaVersion = 1,
                officialPath = official,
                originalSha256 = officialSha,
                candidateSha256 = summary.FinalCandidateSha256,
                appliedOfficialSha256 = appliedSha,
                appliedAt = DateTimeOffset.Now.ToString("O"),
                approvalSha256 = approvalSha
            };
            File.WriteAllText(Path.Combine(rollbackDir, "rollback-metadata.json"),
                JsonSerializer.Serialize(metadata, ResultJsonOptions()), Encoding.UTF8);
            File.WriteAllText(Path.Combine(rollbackDir, "audit-apply.json"),
                JsonSerializer.Serialize(new
                {
                    appliedAt = DateTimeOffset.Now.ToString("O"),
                    official,
                    candidate,
                    approval,
                    rollbackDir,
                    officialSha256Before = officialSha,
                    officialSha256After = appliedSha
                }, ResultJsonOptions()), Encoding.UTF8);
            Console.WriteLine("applied candidate to official project");
            Console.WriteLine("rollback: " + rollbackDir);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("apply-candidate failed: " + ex.Message);
            return 1;
        }
    }

    private static int WorkflowProjectRollback(string[] args)
    {
        var rollbackDir = FullPath(Required(args, "--rollback"));
        var target = RequiredPath(args, "--target");
        try
        {
            var metadataPath = Path.Combine(rollbackDir, "rollback-metadata.json");
            var original = Path.Combine(rollbackDir, "original.MCE");
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("Missing rollback-metadata.json");
            if (!File.Exists(original)) throw new FileNotFoundException("Missing original.MCE");
            using var doc = JsonDocument.Parse(File.ReadAllText(metadataPath, Encoding.UTF8));
            var root = doc.RootElement;
            var appliedSha = JsonString(root, "appliedOfficialSha256") ?? "";
            var originalSha = JsonString(root, "originalSha256") ?? "";
            var expectedCurrent = Opt(args, "--expected-current-sha256") ?? appliedSha;
            var currentSha = Sha256(target);
            if (!currentSha.Equals(expectedCurrent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Current official SHA does not match rollback expected current SHA.");
            if (!Sha256(original).Equals(originalSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Rollback original.MCE SHA does not match metadata.");
            EnsureSourceCanBeCopied(target, allowOpenSource: false);
            var targetDir = Path.GetDirectoryName(target) ?? Environment.CurrentDirectory;
            var tempOriginal = Path.Combine(targetDir, ".mcgsctl-rollback-" + Timestamp() + ".tmp");
            var backupPath = Path.Combine(rollbackDir, "rollback-current-backup.MCE");
            File.Copy(original, tempOriginal, overwrite: false);
            File.Replace(tempOriginal, target, backupPath, ignoreMetadataErrors: false);
            var afterSha = Sha256(target);
            if (!afterSha.Equals(originalSha, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Rollback completed but target SHA does not match original SHA.");
            Console.WriteLine("rollback complete: " + target);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("rollback failed: " + ex.Message);
            return 1;
        }
    }

    private static int Profile(string[] args)
    {
        if (args.Length < 2) return Fail("Usage: mcgsctl profile check (--project <mce>|--workdir <runDir>) --profile <profile.json> [--facts-only] [--out <json>]");
        if (!args[1].Equals("check", StringComparison.OrdinalIgnoreCase))
            return Fail("Unknown profile command: " + args[1]);
        try
        {
            var workDirOpt = Opt(args, "--workdir");
            var project = !string.IsNullOrWhiteSpace(workDirOpt)
                ? Path.Combine(FullPath(workDirOpt), CandidateFileName)
                : RequiredPath(args, "--project");
            if (!File.Exists(project)) throw new FileNotFoundException(project);
            var workDir = !string.IsNullOrWhiteSpace(workDirOpt)
                ? FullPath(workDirOpt)
                : Path.GetDirectoryName(ValidateWorkflowWorkspaceMarker(project))!;
            var markerPath = Path.Combine(workDir, "mcgsctl-workspace.json");
            using var markerDoc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
            var sourceSha = JsonString(markerDoc.RootElement, "sourceSha256") ?? "";
            var context = new WorkflowProjectContext(
                project,
                JsonString(markerDoc.RootElement, "source"),
                workDir,
                markerPath,
                Sha256(project),
                sourceSha,
                CreatedCopy: false,
                ProfilingCopy: false,
                WorkflowName: "profile.check",
                OperationId: NewOperationId(workDir, "profile.check"),
                OperationStartedAt: DateTimeOffset.Now);
            var editor = FullPath(Opt(args, "--editor") ?? EnvOrDefault("MCGS_EDITOR", DefaultEditor()));
            var smart200Dll = FindSmart200Dll(editor);
            var profilePath = Opt(args, "--profile");
            var profileFacts = CollectProfileFacts(editor, smart200Dll);
            var checks = new List<ResultCheck>
            {
                File.Exists(editor) ? RequiredPass("mcgs-editor-exists", editor) : RequiredFail("mcgs-editor-exists", editor),
                RequiredPass("candidate-sha", Sha256(project))
            };
            var limitations = new List<string>();
            if (smart200Dll == null)
                checks.Add(RequiredUnknown("smart200-dll-found", "Smart200.dll was not found under the MCGS editor directory."));
            else
                checks.Add(RequiredPass("smart200-dll-found", smart200Dll));
            if (profilePath == null)
            {
                var factsOnly = Has(args, "--facts-only");
                limitations.Add(factsOnly
                    ? "profile-baseline: --facts-only captures local facts for diagnostics; apply remains blocked"
                    : "profile-baseline: no --profile baseline was supplied; profile lock cannot be proven");
                checks.Add(RequiredUnknown("profile-baseline",
                    factsOnly
                        ? "--facts-only was used; profile facts are diagnostic only."
                        : "No --profile baseline was supplied; profile lock cannot be proven."));
            }
            else
            {
                checks.AddRange(CheckProfileBaseline(FullPath(profilePath), profileFacts));
            }
            if (Has(args, "--allow-profile-drift"))
            {
                checks.Add(RequiredUnknown("profile-drift", "--allow-profile-drift was used; candidate cannot be applied"));
                limitations.Add("profile-drift allowed for diagnostics only");
            }
            WriteFinalValidatorResult(context, "profile-check.json", "profile", "profile.check", checks, limitations,
                new Dictionary<string, object?> { ["profileFacts"] = profileFacts, ["profile"] = profilePath });
            Console.WriteLine("profile-check: " + Path.Combine(workDir, "profile-check.json"));
            return checks.Any(c => c.Required && c.Status != "PASS") ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("profile check failed: " + ex.Message);
            return 1;
        }
    }

    private static Dictionary<string, object?> CollectProfileFacts(string editor, string? smart200Dll)
    {
        var editorInfo = File.Exists(editor) ? PeInspector.ReadExports(editor) : null;
        var smartInfo = smart200Dll != null && File.Exists(smart200Dll) ? PeInspector.ReadExports(smart200Dll) : null;
        var dpi = 0f;
        try
        {
            using var graphics = Graphics.FromHwnd(IntPtr.Zero);
            dpi = graphics.DpiX;
        }
        catch { }
        return new Dictionary<string, object?>
        {
            ["mcgsEditorPath"] = editor,
            ["mcgsEditorSha256"] = File.Exists(editor) ? Sha256(editor) : null,
            ["mcgsEditorPeFormat"] = editorInfo?.Bitness,
            ["mcgsEditorMachine"] = editorInfo?.Machine,
            ["smart200DllPath"] = smart200Dll,
            ["smart200DllSha256"] = smart200Dll != null && File.Exists(smart200Dll) ? Sha256(smart200Dll) : null,
            ["smart200PeFormat"] = smartInfo?.Bitness,
            ["smart200Machine"] = smartInfo?.Machine,
            ["mcgsctlProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["osDescription"] = RuntimeInformation.OSDescription,
            ["windowsVersion"] = Environment.OSVersion.VersionString,
            ["dpiX"] = dpi
        };
    }

    private static IEnumerable<ResultCheck> CheckProfileBaseline(string profilePath, Dictionary<string, object?> facts)
    {
        var checks = new List<ResultCheck>();
        if (!File.Exists(profilePath))
        {
            checks.Add(RequiredUnknown("profile-baseline-file", "Profile baseline file not found: " + profilePath));
            return checks;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(profilePath, Encoding.UTF8));
        var root = doc.RootElement;
        var expectedEditorSha = JsonNestedString(root, "mcgs", "mcgsSetExeSha256");
        if (!string.IsNullOrWhiteSpace(expectedEditorSha))
        {
            var actual = facts.TryGetValue("mcgsEditorSha256", out var value) ? value?.ToString() : null;
            checks.Add(string.Equals(expectedEditorSha, actual, StringComparison.OrdinalIgnoreCase)
                ? RequiredPass("profile-mcgs-editor-sha")
                : RequiredUnknown("profile-mcgs-editor-sha", "McgsSetE.exe SHA does not match profile baseline"));
        }
        var expectedDpi = JsonNestedInt(root, "mcgs", "expectedDpi");
        if (expectedDpi.HasValue)
        {
            var actualDpi = facts.TryGetValue("dpiX", out var dpiValue) && float.TryParse(dpiValue?.ToString(), out var parsed)
                ? parsed
                : 0;
            checks.Add(Math.Abs(actualDpi - expectedDpi.Value) < 1
                ? RequiredPass("profile-dpi")
                : RequiredUnknown("profile-dpi", $"DPI {actualDpi} does not match profile baseline {expectedDpi.Value}"));
        }
        if (root.TryGetProperty("drivers", out var drivers) &&
            drivers.ValueKind == JsonValueKind.Object &&
            drivers.TryGetProperty("Smart200.dll", out var smart200) &&
            smart200.ValueKind == JsonValueKind.Object &&
            smart200.TryGetProperty("sha256", out var smartShaProp))
        {
            var expectedSmartSha = smartShaProp.GetString();
            var actualSmartSha = facts.TryGetValue("smart200DllSha256", out var smartValue) ? smartValue?.ToString() : null;
            checks.Add(string.Equals(expectedSmartSha, actualSmartSha, StringComparison.OrdinalIgnoreCase)
                ? RequiredPass("profile-smart200-sha")
                : RequiredUnknown("profile-smart200-sha", "Smart200.dll SHA does not match profile baseline"));
        }
        if (checks.Count == 0) checks.Add(RequiredPass("profile-baseline-loaded", profilePath));
        return checks;
    }

    private static string? FindSmart200Dll(string editor)
    {
        try
        {
            var root = Path.GetDirectoryName(editor);
            if (root == null || !Directory.Exists(root)) return null;
            return Directory.EnumerateFiles(root, "Smart200.dll", SearchOption.AllDirectories)
                .OrderBy(path => path.Length)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? JsonNestedString(JsonElement root, string objectName, string propertyName)
        => root.TryGetProperty(objectName, out var obj) && obj.ValueKind == JsonValueKind.Object
           && obj.TryGetProperty(propertyName, out var prop)
            ? prop.GetString()
            : null;

    private static int? JsonNestedInt(JsonElement root, string objectName, string propertyName)
    {
        if (!root.TryGetProperty(objectName, out var obj) || obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(propertyName, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value)
            ? value
            : int.TryParse(prop.GetString(), out var parsed) ? parsed : null;
    }

    private static int WorkflowSafetyVerify(string[] args)
    {
        var project = RequiredPath(args, "--project");
        var spec = RequiredPath(args, "--spec");
        var evidenceDir = FullPath(Required(args, "--evidence-dir"));
        try
        {
            var markerPath = Path.Combine(evidenceDir, "mcgsctl-workspace.json");
            using var markerDoc = JsonDocument.Parse(File.ReadAllText(markerPath, Encoding.UTF8));
            var marker = markerDoc.RootElement;
            var sourceSha = JsonString(marker, "sourceSha256") ?? "";
            var context = new WorkflowProjectContext(
                project,
                JsonString(marker, "source"),
                evidenceDir,
                markerPath,
                Sha256(project),
                sourceSha,
                CreatedCopy: false,
                ProfilingCopy: false,
                WorkflowName: "safety.verify",
                OperationId: NewOperationId(evidenceDir, "safety.verify"),
                OperationStartedAt: DateTimeOffset.Now);
            var checks = new List<ResultCheck>();
            var limitations = new List<string>();
            var currentCandidateSha = Sha256(project);
            var candidateFinalRoot = Path.Combine(evidenceDir, "candidate-final");
            var candidateFinal = Path.Combine(candidateFinalRoot, "mce");
            var candidateFinalShaPath = Path.Combine(candidateFinalRoot, "candidate.sha256");
            MceSnapshot? finalSnapshot = null;
            var candidateFinalMatches = Directory.Exists(candidateFinal) &&
                                        ReadSha256Sidecar(candidateFinalShaPath)
                                            .Equals(currentCandidateSha, StringComparison.OrdinalIgnoreCase);
            if (!candidateFinalMatches)
            {
                try
                {
                    if (Directory.Exists(candidateFinal)) Directory.Delete(candidateFinal, recursive: true);
                    Directory.CreateDirectory(candidateFinalRoot);
                    finalSnapshot = ExportMceSnapshot(project, candidateFinal);
                    File.WriteAllText(candidateFinalShaPath, currentCandidateSha + "  candidate.MCE", Encoding.UTF8);
                    checks.Add(RequiredPass("candidate-final-export", "candidate-final/mce refreshed for current candidate SHA"));
                }
                catch (Exception ex)
                {
                    checks.Add(RequiredUnknown("candidate-final-export", ex.Message));
                }
            }
            else
            {
                finalSnapshot = LoadMceSnapshot(candidateFinal);
                checks.Add(RequiredPass("candidate-final-export", "candidate-final/mce matches current candidate SHA"));
            }

            using var specDoc = JsonDocument.Parse(File.ReadAllText(spec, Encoding.UTF8));
            var requiresAwl = specDoc.RootElement.TryGetProperty("plcStaticVerification", out var staticVerification) &&
                              staticVerification.TryGetProperty("requiresAwl", out var reqAwl) &&
                              reqAwl.ValueKind == JsonValueKind.True;
            var awl = Opt(args, "--awl");
            if (requiresAwl && string.IsNullOrWhiteSpace(awl))
                checks.Add(RequiredUnknown("awl-required", "safety spec requires AWL but --awl was not provided"));
            else if (!string.IsNullOrWhiteSpace(awl))
            {
                checks.Add(File.Exists(FullPath(awl)) ? RequiredPass("awl-present", FullPath(awl)) : RequiredUnknown("awl-present", "AWL file not found"));
                if (File.Exists(FullPath(awl)))
                    checks.AddRange(SafetyAwlScan(specDoc.RootElement, File.ReadAllText(FullPath(awl), Encoding.UTF8)));
            }

            checks.AddRange(SafetySpecHeuristicChecks(specDoc.RootElement, evidenceDir, finalSnapshot));
            if (checks.Count == 0) checks.Add(RequiredPass("safety-spec-loaded", spec));
            WriteFinalValidatorResult(context, "safety-result.json", "safety", "safety.verify", checks, limitations,
                new Dictionary<string, object?> { ["spec"] = spec, ["evidenceDir"] = evidenceDir });
            var status = ComputeResultStatus(checks, limitations);
            Console.WriteLine("safety-result: " + Path.Combine(evidenceDir, "safety-result.json"));
            Console.WriteLine("status: " + status);
            return status == "PASS" ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("safety.verify failed: " + ex.Message);
            return 1;
        }
    }

    private static IEnumerable<ResultCheck> SafetySpecHeuristicChecks(JsonElement specRoot, string evidenceDir, MceSnapshot? finalSnapshot)
    {
        var checks = new List<ResultCheck>();
        var smart200Facts = LoadSmart200ChannelFacts(evidenceDir, out var sawDeviceChannelResult, out var missingSmart200Facts);
        var dangerousOutputs = specRoot.TryGetProperty("dangerousOutputs", out var dangerous) &&
                               dangerous.ValueKind == JsonValueKind.Array
            ? dangerous.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (specRoot.TryGetProperty("plc", out var plc) &&
            plc.TryGetProperty("addressPlan", out var plan) &&
            plan.ValueKind == JsonValueKind.Array)
        {
            var plcType = JsonString(plc, "type") ?? "";
            var requiresSmart200Evidence = plcType.Contains("Smart200", StringComparison.OrdinalIgnoreCase);
            if (requiresSmart200Evidence)
            {
                checks.AddRange(Smart200ChannelEvidenceChecks(plan, smart200Facts, sawDeviceChannelResult, missingSmart200Facts));
            }
            var seenAddresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seenVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in plan.EnumerateArray())
            {
                var address = JsonString(item, "address") ?? "";
                var dataObject = JsonString(item, "dataObject") ?? "";
                var direction = JsonString(item, "direction") ?? "";
                var kind = JsonString(item, "kind") ?? "";
                var controlVariable = IsHmiControlVariable(dataObject, direction, kind);
                if ((address.StartsWith("Q", StringComparison.OrdinalIgnoreCase) || dangerousOutputs.Contains(address)) &&
                    controlVariable)
                    checks.Add(RequiredFail("dangerous-output-direct-map:" + dataObject,
                        dataObject + " is mapped directly to dangerous Q output " + address));
                if (!string.IsNullOrWhiteSpace(address))
                {
                    if (seenAddresses.TryGetValue(address, out var existing) &&
                        !existing.Equals(dataObject, StringComparison.OrdinalIgnoreCase))
                        checks.Add(RequiredFail("duplicate-address:" + address,
                            address + " maps to both " + existing + " and " + dataObject));
                    else
                        seenAddresses[address] = dataObject;
                }
                if (!string.IsNullOrWhiteSpace(dataObject) && !string.IsNullOrWhiteSpace(address))
                {
                    if (seenVariables.TryGetValue(dataObject, out var existingAddress) &&
                        !existingAddress.Equals(address, StringComparison.OrdinalIgnoreCase))
                        checks.Add(RequiredFail("duplicate-variable:" + dataObject,
                            dataObject + " maps to both " + existingAddress + " and " + address));
                    else
                        seenVariables[dataObject] = address;
                }
                if (finalSnapshot != null && !string.IsNullOrWhiteSpace(dataObject) &&
                    finalSnapshot.FindDataObjects(dataObject).Length == 0)
                    checks.Add(RequiredUnknown("data-object-present:" + dataObject,
                        dataObject + " was not found in candidate-final Data table"));
                if (kind.Equals("momentary", StringComparison.OrdinalIgnoreCase))
                {
                    var evidence = FindMomentaryEvidence(evidenceDir, dataObject);
                    if (evidence == "pass") checks.Add(RequiredPass("momentary-readback:" + dataObject));
                    else if (WasVariableTouchedByWorkflow(evidenceDir, dataObject))
                        checks.Add(RequiredFail("momentary-readback:" + dataObject,
                            "New or modified momentary variable lacks press/release readback evidence: " + dataObject));
                    else
                        checks.Add(RequiredUnknown("momentary-readback:" + dataObject,
                            "No press/release readback evidence was found for existing variable " + dataObject));
                }
            }
        }
        if (checks.Count == 0) checks.Add(RequiredPass("safety-spec-heuristics", "No blocking heuristic findings"));
        return checks;
    }

    private static bool IsHmiControlVariable(string dataObject, string direction, string kind)
        => dataObject.StartsWith("HMI", StringComparison.OrdinalIgnoreCase) ||
           direction.Equals("hmi_to_plc", StringComparison.OrdinalIgnoreCase) ||
           kind.Equals("momentary", StringComparison.OrdinalIgnoreCase) ||
           kind.Equals("control", StringComparison.OrdinalIgnoreCase);

    private static string FindMomentaryEvidence(string evidenceDir, string dataObject)
    {
        var results = Directory.Exists(WorkflowResultsDir(evidenceDir))
            ? Directory.GetFiles(WorkflowResultsDir(evidenceDir), "*.json")
            : Array.Empty<string>();
        foreach (var file in results)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
                var root = doc.RootElement;
                if (!root.TryGetProperty("controlEvidence", out var evidence) ||
                    evidence.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var item in evidence.EnumerateArray())
                {
                    var itemObject = JsonString(item, "dataObject") ?? "";
                    if (!itemObject.Equals(dataObject, StringComparison.OrdinalIgnoreCase)) continue;
                    var type = JsonString(item, "type") ?? "";
                    var press = JsonString(item, "press") ?? "";
                    var release = JsonString(item, "release") ?? "";
                    var readback = JsonString(item, "readback") ?? "";
                    if (type.Equals("momentary", StringComparison.OrdinalIgnoreCase) &&
                        press.Equals("set1", StringComparison.OrdinalIgnoreCase) &&
                        release.Equals("clear0", StringComparison.OrdinalIgnoreCase) &&
                        readback.Equals("PASS", StringComparison.OrdinalIgnoreCase))
                        return "pass";
                }
            }
            catch
            {
                // Malformed workflow results are handled by candidate validation.
            }
        }
        return "missing";
    }

    private static bool WasVariableTouchedByWorkflow(string evidenceDir, string dataObject)
    {
        var results = Directory.Exists(WorkflowResultsDir(evidenceDir))
            ? Directory.GetFiles(WorkflowResultsDir(evidenceDir), "*.json")
            : Array.Empty<string>();
        return results.Any(file =>
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8));
                var root = doc.RootElement;
                return JsonArrayContainsString(root, "touchedDataObjects", dataObject) ||
                       JsonArrayContainsString(root, "createdDataObjects", dataObject) ||
                       JsonArrayContainsString(root, "modifiedDataObjects", dataObject) ||
                       ControlEvidenceContainsDataObject(root, dataObject);
            }
            catch
            {
                return false;
            }
        });
    }

    private static IEnumerable<ResultCheck> Smart200ChannelEvidenceChecks(JsonElement addressPlan,
        IReadOnlyList<Smart200ChannelFact> facts, bool sawDeviceChannelResult, bool missingFacts)
    {
        var checks = new List<ResultCheck>();
        if (!sawDeviceChannelResult || missingFacts || facts.Count == 0)
        {
            checks.Add(RequiredUnknown("smart200-channel-evidence",
                "Smart200 address plan requires structured device.channel.map smart200Channels evidence."));
            return checks;
        }

        foreach (var group in facts.Where(f => !string.IsNullOrWhiteSpace(f.ParsedAddress))
                     .GroupBy(f => NormalizePlcAddress(f.ParsedAddress), StringComparer.OrdinalIgnoreCase))
        {
            var variables = group.Select(f => f.Variable)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (variables.Length > 1)
                checks.Add(RequiredFail("smart200-address-conflict:" + group.Key,
                    group.Key + " maps to multiple variables: " + string.Join(", ", variables)));
        }

        foreach (var group in facts.Where(f => !string.IsNullOrWhiteSpace(f.Variable))
                     .GroupBy(f => f.Variable, StringComparer.OrdinalIgnoreCase))
        {
            var addresses = group.Select(f => NormalizePlcAddress(f.ParsedAddress))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (addresses.Length > 1)
                checks.Add(RequiredFail("smart200-variable-conflict:" + group.Key,
                    group.Key + " maps to multiple PLC addresses: " + string.Join(", ", addresses)));
        }

        foreach (var item in addressPlan.EnumerateArray())
        {
            var address = NormalizePlcAddress(JsonString(item, "address") ?? "");
            var dataObject = JsonString(item, "dataObject") ?? "";
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(dataObject)) continue;
            var addressFacts = facts.Where(f => NormalizePlcAddress(f.ParsedAddress).Equals(address, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (addressFacts.Length == 0)
            {
                checks.Add(RequiredFail("smart200-address-missing:" + address,
                    "Smart200 channel evidence does not contain spec address " + address));
                continue;
            }
            if (!addressFacts.Any(f => f.Variable.Equals(dataObject, StringComparison.OrdinalIgnoreCase)))
            {
                checks.Add(RequiredFail("smart200-address-variable-mismatch:" + address,
                    "Spec maps " + address + " to " + dataObject + " but evidence maps it to " +
                    string.Join(", ", addressFacts.Select(f => f.Variable).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))));
            }
        }

        if (checks.Count == 0)
            checks.Add(RequiredPass("smart200-channel-evidence", "Structured Smart200 channel evidence matches safety spec."));
        return checks;
    }

    private static IReadOnlyList<Smart200ChannelFact> LoadSmart200ChannelFacts(string evidenceDir,
        out bool sawDeviceChannelResult, out bool missingFacts)
    {
        sawDeviceChannelResult = false;
        missingFacts = false;
        var facts = new List<Smart200ChannelFact>();
        var index = Directory.Exists(evidenceDir) ? LoadWorkflowIndex(evidenceDir) : new List<WorkflowIndexEntry>();
        foreach (var entry in index.Where(e => e.Workflow.Equals("device.channel.map", StringComparison.OrdinalIgnoreCase)))
        {
            sawDeviceChannelResult = true;
            var path = Path.Combine(evidenceDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                missingFacts = true;
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                var root = doc.RootElement;
                if (!root.TryGetProperty("smart200Channels", out var channels) ||
                    channels.ValueKind != JsonValueKind.Array ||
                    channels.GetArrayLength() == 0)
                {
                    missingFacts = true;
                    continue;
                }
                foreach (var item in channels.EnumerateArray())
                {
                    facts.Add(new Smart200ChannelFact(
                        JsonString(item, "channelText") ?? "",
                        NormalizePlcAddress(JsonString(item, "parsedAddress") ?? ""),
                        JsonString(item, "variable") ?? "",
                        JsonString(item, "access") ?? "",
                        item.TryGetProperty("rowIndex", out var rowIndex) && rowIndex.TryGetInt32(out var parsedIndex)
                            ? parsedIndex
                            : -1,
                        entry.Path));
                }
            }
            catch
            {
                missingFacts = true;
            }
        }
        return facts;
    }

    private static bool JsonArrayContainsString(JsonElement root, string property, string expected)
    {
        if (!root.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array) return false;
        return array.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String &&
            (item.GetString() ?? "").Equals(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ControlEvidenceContainsDataObject(JsonElement root, string expected)
    {
        if (!root.TryGetProperty("controlEvidence", out var array) || array.ValueKind != JsonValueKind.Array) return false;
        return array.EnumerateArray().Any(item =>
            (JsonString(item, "dataObject") ?? "").Equals(expected, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePlcAddress(string value)
        => Regex.Replace(value ?? "", @"\s+", "").ToUpperInvariant();

    private static int Safety(string[] args)
    {
        if (args.Length >= 2 && args[1].Equals("scan-awl", StringComparison.OrdinalIgnoreCase))
        {
            var file = RequiredPath(args, "--file");
            var spec = RequiredPath(args, "--spec");
            var text = File.ReadAllText(file, Encoding.UTF8);
            using var specDoc = JsonDocument.Parse(File.ReadAllText(spec, Encoding.UTF8));
            var checks = SafetyAwlScan(specDoc.RootElement, text).ToArray();
            WriteJson(new { status = ComputeResultStatus(checks), checks });
            return checks.Any(c => c.Required && c.Status != "PASS") ? 2 : 0;
        }
        return Fail("Usage: mcgsctl safety scan-awl --file <plc.awl> --spec <safety-spec.json>");
    }

    private static IEnumerable<ResultCheck> SafetyAwlScan(JsonElement specRoot, string awlText)
    {
        var checks = new List<ResultCheck>();
        if (specRoot.TryGetProperty("heartbeat", out var heartbeat))
        {
            var variable = JsonString(heartbeat, "hmiVariable") ?? "";
            if (!string.IsNullOrWhiteSpace(variable))
                checks.Add(awlText.Contains(variable, StringComparison.OrdinalIgnoreCase)
                    ? RequiredPass("awl-heartbeat-reference:" + variable)
                    : RequiredUnknown("awl-heartbeat-reference:" + variable, "Heartbeat variable not found in AWL"));
        }
        if (specRoot.TryGetProperty("plc", out var plc) &&
            plc.TryGetProperty("addressPlan", out var plan) &&
            plan.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in plan.EnumerateArray())
            {
                var address = JsonString(item, "address") ?? "";
                var dataObject = JsonString(item, "dataObject") ?? "";
                var kind = JsonString(item, "kind") ?? "";
                if (!string.IsNullOrWhiteSpace(address))
                    checks.Add(awlText.Contains(address, StringComparison.OrdinalIgnoreCase)
                        ? RequiredPass("awl-address-reference:" + address)
                        : RequiredUnknown("awl-address-reference:" + address, "PLC address not found in AWL"));
                if (!string.IsNullOrWhiteSpace(dataObject))
                    checks.Add(awlText.Contains(dataObject, StringComparison.OrdinalIgnoreCase)
                        ? RequiredPass("awl-dataobject-reference:" + dataObject)
                        : RequiredUnknown("awl-dataobject-reference:" + dataObject, "Data object not found in AWL"));
                if (kind.Equals("momentary", StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetProperty("opposes", out var opposes) &&
                    opposes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var oppose in opposes.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var bothReferenced = awlText.Contains(dataObject, StringComparison.OrdinalIgnoreCase) &&
                                             awlText.Contains(oppose, StringComparison.OrdinalIgnoreCase);
                        checks.Add(bothReferenced
                            ? RequiredPass("awl-opposition-reference:" + dataObject + ":" + oppose)
                            : RequiredUnknown("awl-opposition-reference:" + dataObject + ":" + oppose,
                                "Opposing momentary variables were not both found in AWL"));
                    }
                }
            }
        }
        if (specRoot.TryGetProperty("dangerousOutputs", out var dangerousOutputs) &&
            dangerousOutputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var output in dangerousOutputs.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var directHmiLine = awlText.Replace("\r", "")
                    .Split('\n')
                    .Any(line => line.Contains(output, StringComparison.OrdinalIgnoreCase) &&
                                 line.Contains("HMI", StringComparison.OrdinalIgnoreCase));
                if (directHmiLine)
                    checks.Add(RequiredFail("awl-dangerous-output-direct-hmi:" + output,
                        output + " appears on the same AWL line as an HMI symbol"));
            }
        }
        if (checks.Count == 0) checks.Add(RequiredPass("awl-scan-loaded"));
        return checks;
    }
}
