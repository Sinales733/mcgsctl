using System.Text.Json.Nodes;

namespace McgsCtl.Tests;

public sealed class CandidateSummaryTests
{
    [Fact]
    public void PassFixtureValidates()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        var result = fixture.Validate();
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void MissingRequiredFinalValidatorIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        File.Delete(fixture.ProfileResultPath);

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("missing required final validator: profile-check.json", result.CombinedOutput);
    }

    [Theory]
    [InlineData("startedAt", "bad-date", "invalid or missing startedAt")]
    [InlineData("finishedAt", "bad-date", "invalid or missing finishedAt")]
    public void BadMutationTimestampIsBlocked(string property, string value, string expected)
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json => json[property] = value);

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.CombinedOutput);
    }

    [Fact]
    public void FinishedAtBeforeStartedAtIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json => json["finishedAt"] = "2026-04-30T10:14:00+08:00");

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("finishedAt is earlier than startedAt", result.CombinedOutput);
    }

    [Fact]
    public void FinalValidatorEarlierThanLastMutationIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ProfileResultPath, json =>
        {
            json["startedAt"] = "2026-04-30T10:10:00+08:00";
            json["finishedAt"] = "2026-04-30T10:11:00+08:00";
        });

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("profile-check.json is not later than the last mutating workflow", result.CombinedOutput);
    }

    [Fact]
    public void FinalValidatorMissingMutatesCandidateIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.ProfileResultPath, json => json.Remove("mutatesCandidate"));

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("profile-check.json missing mutatesCandidate", result.CombinedOutput);
    }

    [Fact]
    public void MutationChainMismatchIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json => json["candidateSha256Before"] = TestFixtureBuilder.Sha256Text("wrong-before"));

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("mutation chain mismatch before", result.CombinedOutput);
    }

    [Fact]
    public void CandidateModifiedAfterSummaryIsBlocked()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.RewriteCandidate("candidate-changed-after-summary");

        var result = fixture.Validate();

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("candidate-summary finalCandidateSha256 does not match actual candidate", result.CombinedOutput);
    }

    [Fact]
    public void CandidateSummarizeWithDummyMceIsBlockedByExportFailure()
    {
        using var fixture = TestFixtureBuilder.CreatePass();

        var result = TestCli.Run("candidate", "summarize", "--workdir", fixture.WorkDir);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("candidate final MCE export failed", result.Stdout);
    }
}
