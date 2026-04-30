# mcgsctl Operator Runbook

This runbook treats `mcgsctl` as a candidate-copy tool. It does not approve hardware behavior and does not make locked or minimized GUI automation safe.

## 1. Generate A Candidate

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run realtime-db.add --source FG2_HMI.MCE --workdir .mcgsctl-work\e2e --name MCGSCTL_SW --type switch
tools\mcgsctl\mcgsctl.ps1 workflow run window.button.add-momentary --project .mcgsctl-work\e2e\candidate.MCE --text MCGSCTL_JOG --variable MCGSCTL_SW
```

The candidate path is fixed:

```text
.mcgsctl-work\e2e\candidate.MCE
```

## 2. Run Final Validators

Final validators must run after the last mutating workflow:

```powershell
tools\mcgsctl\mcgsctl.ps1 profile check --workdir .mcgsctl-work\e2e --profile profiles\local-mcgs-7.7-smart200.json
tools\mcgsctl\mcgsctl.ps1 workflow run project.check --project .mcgsctl-work\e2e\candidate.MCE --fail-on-warning
tools\mcgsctl\mcgsctl.ps1 workflow run safety.verify --project .mcgsctl-work\e2e\candidate.MCE --spec safety-spec.json --evidence-dir .mcgsctl-work\e2e --awl FG2HMI.awl
```

`profile-check.json` records editor SHA/bitness, mcgsctl bitness, OS/runtime facts, DPI, and Smart200 DLL SHA. Profile drift is `UNKNOWN` and blocks apply.

`safety.verify` reads `candidate-final\mce` and the workflow evidence. It blocks direct dangerous `Q` mappings, duplicate Smart200 mappings, missing momentary press/release readback evidence, and missing required AWL evidence.

## 3. Summarize And Validate

```powershell
tools\mcgsctl\mcgsctl.ps1 candidate summarize --workdir .mcgsctl-work\e2e
tools\mcgsctl\mcgsctl.ps1 candidate validate --workdir .mcgsctl-work\e2e
```

Review:

```text
.mcgsctl-work\e2e\candidate-summary.md
.mcgsctl-work\e2e\candidate-summary.json
.mcgsctl-work\e2e\candidate-final\
.mcgsctl-work\e2e\dialogs.jsonl
.mcgsctl-work\e2e\popups.jsonl
.mcgsctl-work\e2e\startup-dialogs.jsonl
```

Typical blocked reasons:

```text
profile-check.json status is UNKNOWN
safety-result.json status is FAIL
project-check/check-result.json is not later than the last mutating workflow
candidate-summary.verdict is not apply-ready
```

## 4. Approve

Copy `approval.template.json` to `approval.json`, then fill:

```json
{
  "operator": "your-name",
  "approvedAt": "2026-04-30T12:00:00+08:00"
}
```

Do not edit result files after approval. Any result SHA mismatch blocks apply.

## 5. Apply

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run project.apply-candidate --source FG2_HMI.MCE --candidate .mcgsctl-work\e2e\candidate.MCE --approval .mcgsctl-work\e2e\approval.json
```

Apply creates a rollback package before replacing the official file.

## 6. Rollback

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run project.rollback --rollback <rollbackDir> --target FG2_HMI.MCE
```

Rollback refuses to overwrite a changed official file unless `--expected-current-sha256 <sha>` is supplied.
