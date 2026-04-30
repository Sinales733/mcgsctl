namespace McgsCtl.Tests;

public sealed class ApplyRollbackTests
{
    [Fact]
    public void ApplyAndRollbackDummyOfficialSucceeds()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        var rollbackDir = Path.Combine(fixture.Root, "rollback-package");
        var originalSha = TestFixtureBuilder.Sha256(fixture.OfficialPath);
        var candidateSha = TestFixtureBuilder.Sha256(fixture.CandidatePath);

        var apply = fixture.Apply(rollbackDir);
        Assert.Equal(0, apply.ExitCode);
        Assert.Equal(candidateSha, TestFixtureBuilder.Sha256(fixture.OfficialPath));
        Assert.True(File.Exists(Path.Combine(rollbackDir, "rollback-metadata.json")));
        Assert.True(File.Exists(Path.Combine(rollbackDir, "original.MCE")));
        Assert.True(File.Exists(Path.Combine(rollbackDir, "candidate.MCE")));
        Assert.True(File.Exists(Path.Combine(rollbackDir, "approval.json")));

        var rollback = fixture.Rollback(rollbackDir);
        Assert.Equal(0, rollback.ExitCode);
        Assert.Equal(originalSha, TestFixtureBuilder.Sha256(fixture.OfficialPath));
    }

    [Fact]
    public void OfficialLockFileBlocksApply()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        File.WriteAllText(Path.ChangeExtension(fixture.OfficialPath, ".ldb"), "lock");

        var apply = fixture.Apply(Path.Combine(fixture.Root, "rollback-lock"));

        Assert.NotEqual(0, apply.ExitCode);
        Assert.Contains("Access lock", apply.CombinedOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovalResultHashMismatchBlocksApply()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json =>
        {
            json["resultSha256"]!.AsObject()["profile-check.json"] = TestFixtureBuilder.Sha256Text("wrong");
        });

        var apply = fixture.Apply(Path.Combine(fixture.Root, "rollback-hash"));

        Assert.NotEqual(0, apply.ExitCode);
        Assert.Contains("Approval is invalid", apply.CombinedOutput);
    }

    [Fact]
    public void RollbackCurrentShaMismatchFails()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        var rollbackDir = Path.Combine(fixture.Root, "rollback-mismatch");
        var apply = fixture.Apply(rollbackDir);
        Assert.Equal(0, apply.ExitCode);
        File.WriteAllText(fixture.OfficialPath, "changed-after-apply");

        var rollback = fixture.Rollback(rollbackDir);

        Assert.NotEqual(0, rollback.ExitCode);
        Assert.Contains("Current official SHA does not match", rollback.CombinedOutput);
    }

    [Fact]
    public void UnknownRequiredResultBlocksApply()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.SafetyResultPath, json =>
        {
            json["status"] = "UNKNOWN";
            json["checks"]!.AsArray()[0]!["status"] = "UNKNOWN";
        });
        fixture.RefreshApproval();

        var apply = fixture.Apply(Path.Combine(fixture.Root, "rollback-unknown"));

        Assert.NotEqual(0, apply.ExitCode);
        Assert.Contains("Candidate summary is not apply-ready", apply.CombinedOutput);
    }
}
