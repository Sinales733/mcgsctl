using System.Text.Json.Nodes;

namespace McgsCtl.Tests;

public sealed class SafetyVerifyTests
{
    [Fact]
    public void Smart200SameAddressDifferentVariableFails()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.ReplaceWorkflowIndexForDeviceChannel();
        fixture.WriteJsonObject(fixture.WorkflowResultPath, fixture.DeviceChannelResult(new JsonArray
        {
            Channel("读写V603.0", "V603.0", "MCGSCTL_A", 1),
            Channel("读写V603.0", "V603.0", "MCGSCTL_B", 2)
        }));
        fixture.WriteSafetySpecSmart200("V603.0", "MCGSCTL_A");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("FAIL", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "smart200-address-conflict:V603.0", "FAIL");
    }

    [Fact]
    public void Smart200SameVariableMultipleAddressesFails()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.ReplaceWorkflowIndexForDeviceChannel();
        fixture.WriteJsonObject(fixture.WorkflowResultPath, fixture.DeviceChannelResult(new JsonArray
        {
            Channel("读写V603.0", "V603.0", "MCGSCTL_A", 1),
            Channel("读写V604.0", "V604.0", "MCGSCTL_A", 2)
        }));
        fixture.WriteSafetySpecSmart200("V603.0", "MCGSCTL_A");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("FAIL", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "smart200-variable-conflict:MCGSCTL_A", "FAIL");
    }

    [Fact]
    public void MissingSmart200ChannelsIsUnknown()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.ReplaceWorkflowIndexForDeviceChannel();
        fixture.WriteJsonObject(fixture.WorkflowResultPath, fixture.MutatingResult("0001-device.channel.map-20260430T101500", "device.channel.map"));
        fixture.WriteSafetySpecSmart200("V603.0", "MCGSCTL_A");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("UNKNOWN", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "smart200-channel-evidence", "UNKNOWN");
    }

    [Fact]
    public void MomentaryTouchedWithoutReadbackFails()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json =>
        {
            json["touchedDataObjects"] = new JsonArray("MCGSCTL_SW");
            json["createdDataObjects"] = new JsonArray();
            json["modifiedDataObjects"] = new JsonArray("MCGSCTL_SW");
            json["controlEvidence"] = new JsonArray();
        });
        fixture.WriteSafetySpecOther("V603.0", "MCGSCTL_SW", "momentary");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("FAIL", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "momentary-readback:MCGSCTL_SW", "FAIL");
    }

    [Fact]
    public void LimitationsOnlyMentionDoesNotCountAsTouched()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.MutateJson(fixture.WorkflowResultPath, json =>
        {
            json["touchedDataObjects"] = new JsonArray();
            json["createdDataObjects"] = new JsonArray();
            json["modifiedDataObjects"] = new JsonArray();
            json["controlEvidence"] = new JsonArray();
            json["limitations"] = new JsonArray("MCGSCTL_SW appears here only");
        });
        fixture.WriteSafetySpecOther("V603.0", "MCGSCTL_SW", "momentary");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("UNKNOWN", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "momentary-readback:MCGSCTL_SW", "UNKNOWN");
        Assert.DoesNotContain("lacks press/release", safety.ToJsonString());
    }

    [Fact]
    public void RequiresAwlWithoutAwlIsUnknown()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.WriteSafetySpecOther("V603.0", "MCGSCTL_SW", "control", requiresAwl: true);

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("UNKNOWN", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "awl-required", "UNKNOWN");
    }

    [Fact]
    public void DangerousQDirectHmiControlFails()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.WriteSafetySpec(new JsonObject
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
                ["type"] = "Other",
                ["addressPlan"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = "Q0.0",
                        ["dataObject"] = "HMI_DANGER",
                        ["direction"] = "hmi_to_plc",
                        ["kind"] = "control"
                    }
                }
            },
            ["dangerousOutputs"] = new JsonArray("Q0.0")
        });

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("FAIL", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "dangerous-output-direct-map:HMI_DANGER", "FAIL");
    }

    [Fact]
    public void StaleCandidateFinalWithDummyMceDoesNotPass()
    {
        using var fixture = TestFixtureBuilder.CreatePass();
        fixture.WriteCandidateFinalSnapshot(new[] { "MCGSCTL_SW" }, matchingSha: false);
        fixture.WriteSafetySpecOther("V603.0", "MCGSCTL_SW", "control");

        var result = fixture.SafetyVerify();
        var safety = fixture.ReadSafetyResult();

        Assert.Equal(2, result.ExitCode);
        Assert.NotEqual("PASS", safety["status"]!.GetValue<string>());
        AssertCheck(safety, "candidate-final-export", "UNKNOWN");
    }

    private static JsonObject Channel(string text, string address, string variable, int rowIndex)
        => new()
        {
            ["channelText"] = text,
            ["parsedAddress"] = address,
            ["variable"] = variable,
            ["access"] = "读写",
            ["rowIndex"] = rowIndex
        };

    private static void AssertCheck(JsonObject result, string name, string status)
    {
        var checks = result["checks"]!.AsArray();
        Assert.Contains(checks, check =>
            check?["name"]?.GetValue<string>() == name &&
            check["status"]?.GetValue<string>() == status);
    }
}
