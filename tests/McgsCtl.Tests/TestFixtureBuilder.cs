using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace McgsCtl.Tests;

internal sealed class TestFixtureBuilder : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Root { get; }
    public string WorkDir { get; }
    public string SourcePath { get; }
    public string OfficialPath { get; }
    public string CandidatePath { get; }
    public string ApprovalPath => Path.Combine(WorkDir, "approval.json");
    public string ApprovalTemplatePath => Path.Combine(WorkDir, "approval.template.json");
    public string CandidateSummaryPath => Path.Combine(WorkDir, "candidate-summary.json");
    public string WorkflowResultPath => Path.Combine(WorkDir, WorkflowResultRelativePath.Replace('/', Path.DirectorySeparatorChar));
    public string ProfileResultPath => Path.Combine(WorkDir, "profile-check.json");
    public string ProjectCheckResultPath => Path.Combine(WorkDir, "project-check", "check-result.json");
    public string SafetyResultPath => Path.Combine(WorkDir, "safety-result.json");
    public string SafetySpecPath => Path.Combine(WorkDir, "safety-spec.json");
    public string WorkflowResultRelativePath => "workflow-results/0001-realtime-db.add-20260430T101500.json";
    public string SourceSha { get; private set; } = "";
    public string CandidateSha { get; private set; } = "";
    public string SummaryGeneratedAt { get; } = "2026-04-30T10:20:00+08:00";
    public string MutationStartedAt { get; set; } = "2026-04-30T10:15:00+08:00";
    public string MutationFinishedAt { get; set; } = "2026-04-30T10:16:00+08:00";
    public string ValidatorStartedAt { get; set; } = "2026-04-30T10:17:00+08:00";
    public string ValidatorFinishedAt { get; set; } = "2026-04-30T10:18:00+08:00";

    private bool _disposed;

    private TestFixtureBuilder(string scenario)
    {
        Root = Path.Combine(Path.GetTempPath(), "mcgsctl-tests", SafeName(scenario) + "-" + Guid.NewGuid().ToString("N"));
        WorkDir = Path.Combine(Root, "run");
        SourcePath = Path.Combine(Root, "source.MCE");
        OfficialPath = Path.Combine(Root, "official.MCE");
        CandidatePath = Path.Combine(WorkDir, "candidate.MCE");
    }

    public static TestFixtureBuilder CreatePass(string scenario = "pass")
    {
        var fixture = new TestFixtureBuilder(scenario);
        fixture.WritePassFixture();
        return fixture;
    }

    public CliResult Validate() => TestCli.Run("candidate", "validate", "--workdir", WorkDir);

    public CliResult ValidateWithApproval() => TestCli.Run("candidate", "validate", "--workdir", WorkDir, "--approval", ApprovalPath);

    public CliResult SafetyVerify(string? specPath = null, params string[] extraArgs)
    {
        var args = new List<string>
        {
            "workflow", "run", "safety.verify",
            "--project", CandidatePath,
            "--spec", specPath ?? SafetySpecPath,
            "--evidence-dir", WorkDir
        };
        args.AddRange(extraArgs);
        return TestCli.Run(args.ToArray());
    }

    public CliResult Apply(string? rollbackDir = null)
    {
        var args = new List<string>
        {
            "workflow", "run", "project.apply-candidate",
            "--source", OfficialPath,
            "--candidate", CandidatePath,
            "--approval", ApprovalPath
        };
        if (rollbackDir != null)
        {
            args.Add("--rollback-dir");
            args.Add(rollbackDir);
        }
        return TestCli.Run(args.ToArray());
    }

    public CliResult Rollback(string rollbackDir)
        => TestCli.Run("workflow", "run", "project.rollback", "--rollback", rollbackDir, "--target", OfficialPath);

    public JsonObject ReadJsonObject(string path)
        => JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8))!.AsObject();

    public void WriteJsonObject(string path, JsonObject obj)
        => File.WriteAllText(path, obj.ToJsonString(JsonOptions), Encoding.UTF8);

    public void WriteJsonNode(string path, JsonNode node)
        => File.WriteAllText(path, node.ToJsonString(JsonOptions), Encoding.UTF8);

    public void MutateJson(string path, Action<JsonObject> mutate)
    {
        var obj = ReadJsonObject(path);
        mutate(obj);
        WriteJsonObject(path, obj);
    }

    public void RefreshApproval()
    {
        var approval = BuildApprovalObject();
        WriteJsonObject(ApprovalPath, approval);
    }

    public void RewriteCandidate(string content)
    {
        File.WriteAllText(CandidatePath, content, Encoding.UTF8);
        CandidateSha = Sha256(CandidatePath);
    }

    public void WriteSafetySpec(JsonObject spec) => WriteJsonObject(SafetySpecPath, spec);

    public JsonObject ReadSafetyResult() => ReadJsonObject(Path.Combine(WorkDir, "safety-result.json"));

    public void WriteCandidateFinalSnapshot(IEnumerable<string> dataObjects, bool matchingSha = true)
    {
        var finalRoot = Path.Combine(WorkDir, "candidate-final");
        var mce = Path.Combine(finalRoot, "mce");
        Directory.CreateDirectory(mce);
        var sha = matchingSha ? CandidateSha : Sha256Text("stale-candidate");
        File.WriteAllText(Path.Combine(finalRoot, "candidate.sha256"), sha + "  candidate.MCE", Encoding.UTF8);
        var data = new JsonArray(dataObjects.Select(name => new JsonObject
        {
            ["strName"] = name,
            ["strInitValue"] = "0",
            ["strUnit"] = "",
            ["strNote"] = "",
            ["iDataType"] = 1
        }).ToArray<JsonNode?>());
        File.WriteAllText(Path.Combine(mce, "data.json"), data.ToJsonString(JsonOptions), Encoding.UTF8);
        File.WriteAllText(Path.Combine(mce, "blob_strings.json"), "[]", Encoding.UTF8);
        File.WriteAllText(Path.Combine(mce, "summary.json"), "{}", Encoding.UTF8);
        File.WriteAllText(Path.Combine(mce, "schema.json"), "{}", Encoding.UTF8);
    }

    public string LatestRollbackDir()
    {
        var root = Path.Combine(Path.GetDirectoryName(OfficialPath)!, ".mcgsctl-rollback");
        return Directory.GetDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).First();
    }

    private void WritePassFixture()
    {
        Directory.CreateDirectory(WorkDir);
        Directory.CreateDirectory(Path.Combine(WorkDir, "workflow-results"));
        Directory.CreateDirectory(Path.Combine(WorkDir, "project-check"));

        File.WriteAllText(SourcePath, "official-source", Encoding.UTF8);
        File.Copy(SourcePath, OfficialPath, overwrite: true);
        File.WriteAllText(CandidatePath, "candidate-after-mutation", Encoding.UTF8);
        SourceSha = Sha256(SourcePath);
        CandidateSha = Sha256(CandidatePath);

        WriteJsonObject(Path.Combine(WorkDir, "mcgsctl-workspace.json"), new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdBy"] = "mcgsctl",
            ["source"] = SourcePath,
            ["sourceSha256"] = SourceSha,
            ["workingCopy"] = CandidatePath,
            ["initialWorkingCopySha256"] = SourceSha,
            ["currentCandidateSha256"] = CandidateSha,
            ["mutationResultsIndex"] = "workflow-results/index.json",
            ["createdAt"] = "2026-04-30T10:00:00+08:00"
        });

        WriteJsonNode(Path.Combine(WorkDir, "workflow-results", "index.json"), new JsonArray
        {
            new JsonObject
            {
                ["operationId"] = "0001-realtime-db.add-20260430T101500",
                ["workflow"] = "realtime-db.add",
                ["path"] = WorkflowResultRelativePath,
                ["mutatesCandidate"] = true,
                ["startedAt"] = MutationStartedAt,
                ["finishedAt"] = MutationFinishedAt
            }
        });

        WriteJsonObject(WorkflowResultPath, MutatingResult("0001-realtime-db.add-20260430T101500", "realtime-db.add"));
        WriteFinalValidators();
        WriteCandidateFinalSnapshot(new[] { "MCGSCTL_SW", "MCGSCTL_A", "MCGSCTL_B", "HMI_DANGER" });
        WriteCandidateSummaryAndApproval();
    }

    public void WriteFinalValidators()
    {
        WriteJsonObject(ProfileResultPath, FinalValidator("profile", "profile.check", "0002-profile.check-20260430T101700"));
        WriteJsonObject(ProjectCheckResultPath, FinalValidator("project-check", "project.check", "0003-project.check-20260430T101730"));
        WriteJsonObject(SafetyResultPath, FinalValidator("safety", "safety.verify", "0004-safety.verify-20260430T101800"));
    }

    public JsonObject MutatingResult(string operationId, string workflow)
        => new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = "workflow",
            ["operationId"] = operationId,
            ["workflow"] = workflow,
            ["mutatesCandidate"] = true,
            ["status"] = "PASS",
            ["sourceSha256"] = SourceSha,
            ["candidate"] = CandidatePath,
            ["candidateSha256Before"] = SourceSha,
            ["candidateSha256After"] = CandidateSha,
            ["startedAt"] = MutationStartedAt,
            ["finishedAt"] = MutationFinishedAt,
            ["checks"] = PassChecks("reopen-readback"),
            ["limitations"] = new JsonArray(),
            ["touchedDataObjects"] = new JsonArray(),
            ["createdDataObjects"] = new JsonArray(),
            ["modifiedDataObjects"] = new JsonArray(),
            ["controlEvidence"] = new JsonArray()
        };

    public JsonObject DeviceChannelResult(JsonArray smart200Channels)
    {
        var result = MutatingResult("0001-device.channel.map-20260430T101500", "device.channel.map");
        result["smart200Channels"] = smart200Channels;
        return result;
    }

    public JsonObject FinalValidator(string kind, string workflow, string operationId)
        => new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = kind,
            ["operationId"] = operationId,
            ["workflow"] = workflow,
            ["mutatesCandidate"] = false,
            ["status"] = "PASS",
            ["sourceSha256"] = SourceSha,
            ["candidate"] = CandidatePath,
            ["candidateSha256"] = CandidateSha,
            ["candidateShaRead"] = "PASS",
            ["candidateShaSource"] = "actual",
            ["candidateShaReadError"] = null,
            ["startedAt"] = ValidatorStartedAt,
            ["finishedAt"] = ValidatorFinishedAt,
            ["checks"] = PassChecks(workflow + "-pass"),
            ["limitations"] = new JsonArray()
        };

    public void WriteCandidateSummaryAndApproval()
    {
        var resultSha = ResultShaMap(includeSummary: false);
        var required = RequiredResultsArray();
        var summary = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["generatedAt"] = SummaryGeneratedAt,
            ["verdict"] = "apply-ready",
            ["source"] = SourcePath,
            ["sourceSha256"] = SourceSha,
            ["candidate"] = CandidatePath,
            ["finalCandidateSha256"] = CandidateSha,
            ["candidateFinalExport"] = "candidate-final/mce",
            ["mutationChain"] = new JsonArray
            {
                new JsonObject
                {
                    ["operationId"] = "0001-realtime-db.add-20260430T101500",
                    ["path"] = WorkflowResultRelativePath,
                    ["before"] = SourceSha,
                    ["after"] = CandidateSha,
                    ["finishedAt"] = MutationFinishedAt
                }
            },
            ["blockedReasons"] = new JsonArray(),
            ["requiredResults"] = JsonNode.Parse(required.ToJsonString(JsonOptions)),
            ["resultSha256"] = JsonObjectFromDictionary(resultSha)
        };
        WriteJsonObject(CandidateSummaryPath, summary);
        WriteJsonObject(ApprovalTemplatePath, BuildApprovalObject());
        File.Copy(ApprovalTemplatePath, ApprovalPath, overwrite: true);
    }

    public JsonObject BuildApprovalObject()
    {
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["operator"] = "tester",
            ["approvedAt"] = "2026-04-30T10:21:00+08:00",
            ["source"] = SourcePath,
            ["sourceSha256"] = SourceSha,
            ["candidate"] = CandidatePath,
            ["candidateSha256"] = CandidateSha,
            ["requiredResults"] = RequiredResultsArray(),
            ["resultSha256"] = JsonObjectFromDictionary(ResultShaMap(includeSummary: true)),
            ["notes"] = "test fixture"
        };
    }

    private Dictionary<string, string> ResultShaMap(bool includeSummary)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [WorkflowResultRelativePath] = Sha256(WorkflowResultPath),
            ["profile-check.json"] = Sha256(ProfileResultPath),
            ["project-check/check-result.json"] = Sha256(ProjectCheckResultPath),
            ["safety-result.json"] = Sha256(SafetyResultPath)
        };
        if (includeSummary && File.Exists(CandidateSummaryPath))
            map["candidate-summary.json"] = Sha256(CandidateSummaryPath);
        return map;
    }

    private static JsonArray RequiredResultsArray()
        => new()
        {
            new JsonObject
            {
                ["kind"] = "workflow",
                ["path"] = "workflow-results/0001-realtime-db.add-20260430T101500.json",
                ["requiredStatus"] = "PASS",
                ["shaPolicy"] = "chain"
            },
            new JsonObject
            {
                ["kind"] = "profile",
                ["path"] = "profile-check.json",
                ["requiredStatus"] = "PASS",
                ["shaPolicy"] = "finalCandidate"
            },
            new JsonObject
            {
                ["kind"] = "project-check",
                ["path"] = "project-check/check-result.json",
                ["requiredStatus"] = "PASS",
                ["shaPolicy"] = "finalCandidate"
            },
            new JsonObject
            {
                ["kind"] = "safety",
                ["path"] = "safety-result.json",
                ["requiredStatus"] = "PASS",
                ["shaPolicy"] = "finalCandidate"
            }
        };

    public void ReplaceWorkflowIndexForDeviceChannel()
    {
        WriteJsonNode(Path.Combine(WorkDir, "workflow-results", "index.json"), new JsonArray
        {
            new JsonObject
            {
                ["operationId"] = "0001-device.channel.map-20260430T101500",
                ["workflow"] = "device.channel.map",
                ["path"] = WorkflowResultRelativePath,
                ["mutatesCandidate"] = true,
                ["startedAt"] = MutationStartedAt,
                ["finishedAt"] = MutationFinishedAt
            }
        });
    }

    public void WriteSafetySpecSmart200(string address, string dataObject, string kind = "momentary")
    {
        WriteSafetySpec(new JsonObject
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
                ["type"] = "Smart200",
                ["addressPlan"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = address,
                        ["dataObject"] = dataObject,
                        ["direction"] = "hmi_to_plc",
                        ["kind"] = kind
                    }
                }
            },
            ["dangerousOutputs"] = new JsonArray()
        });
    }

    public void WriteSafetySpecOther(string address, string dataObject, string kind = "momentary", bool requiresAwl = false)
    {
        WriteSafetySpec(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["project"] = "TEST",
            ["plcStaticVerification"] = new JsonObject
            {
                ["requiresAwl"] = requiresAwl,
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
                        ["kind"] = kind
                    }
                }
            },
            ["dangerousOutputs"] = new JsonArray()
        });
    }

    public JsonArray PassChecks(string name)
        => new()
        {
            new JsonObject
            {
                ["name"] = name,
                ["status"] = "PASS",
                ["required"] = true
            }
        };

    public static JsonObject JsonObjectFromDictionary(IDictionary<string, string> values)
    {
        var obj = new JsonObject();
        foreach (var kv in values) obj[kv.Key] = kv.Value;
        return obj;
    }

    public static string Sha256(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string Sha256Text(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string SafeName(string raw)
        => string.Concat(raw.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // Test cleanup should not hide the assertion that already ran.
        }
    }
}
