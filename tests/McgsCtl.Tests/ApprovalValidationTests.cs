namespace McgsCtl.Tests;

public sealed class ApprovalValidationTests
{
    [Fact]
    public void PassFixtureApprovalValidates()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        var result = fixture.ValidateWithApproval();
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ApprovalMissingRequiredResultIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json =>
        {
            var required = json["requiredResults"]!.AsArray();
            required.RemoveAt(1);
        });

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval.requiredResults missing profile-check.json", result.CombinedOutput);
    }

    [Theory]
    [InlineData("shaPolicy", "finalCandidate", "shaPolicy mismatch")]
    [InlineData("kind", "profile", "kind mismatch")]
    [InlineData("requiredStatus", "FAIL", "requiredStatus is not PASS")]
    public void ApprovalRequiredResultMismatchIsBlocked(string property, string value, string expected)
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json =>
        {
            json["requiredResults"]!.AsArray()[0]![property] = value;
        });

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.CombinedOutput);
    }

    [Fact]
    public void ApprovalOlderThanCandidateSummaryIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json => json["approvedAt"] = "2026-04-30T10:19:00+08:00");

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval.approvedAt is older than candidate-summary.generatedAt", result.CombinedOutput);
    }

    [Fact]
    public void ApprovalResultShaMismatchIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json =>
        {
            json["resultSha256"]!.AsObject()["safety-result.json"] = TestFixtureBuilder.Sha256Text("bad-sha");
        });

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval.resultSha256 mismatch for safety-result.json", result.CombinedOutput);
    }

    [Fact]
    public void ApprovalSourcePathMismatchIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json => json["source"] = Path.Combine(fixture.Root, "other-source.MCE"));

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval source path does not match candidate summary", result.CombinedOutput);
    }

    [Fact]
    public void ApprovalCandidatePathMismatchIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ApprovalPath, json => json["candidate"] = Path.Combine(fixture.Root, "other-candidate.MCE"));

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval candidate path does not match candidate summary", result.CombinedOutput);
    }

    [Fact]
    public void ResultModifiedAfterApprovalIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json => json["auditNote"] = "modified after approval");

        var result = fixture.ValidateWithApproval();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("approval.resultSha256 mismatch for workflow-results/0001-realtime-db.add-20260430T101500.json", result.CombinedOutput);
    }
}
