# mcgsctl

`mcgsctl` is a local CLI helper for the MCGS embedded editor used by the FG2 HMI project.

Design rules:

- `.MCE` files are read only for inspection and verification.
- Project writes go through the visible MCGS editor GUI.
- The tool does not patch `WndUser.lbObjects`, `WndDevice.lbObjects`, or other private binary object blobs.

## Quick Start

From the FG2 repository root:

```powershell
tools\mcgsctl\mcgsctl.ps1 doctor --project FG2_HMI.MCE
tools\mcgsctl\mcgsctl.ps1 --version
tools\mcgsctl\mcgsctl.ps1 mce export --project FG2_HMI.MCE --out .mcgsctl-runs\export
tools\mcgsctl\mcgsctl.ps1 snapshot --project FG2_HMI.MCE --out .mcgsctl-runs\snapshot
tools\mcgsctl\mcgsctl.ps1 workflow run project.check --source FG2_HMI.MCE --fail-on-warning
tools\mcgsctl\mcgsctl.ps1 open --project FG2_HMI.MCE
tools\mcgsctl\mcgsctl.ps1 menus
tools\mcgsctl\mcgsctl.ps1 command --id 57603 --send
```

Write workflows should normally use `--source`:

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run realtime-db.add --source FG2_HMI.MCE --workdir .mcgsctl-work\e2e --name HMI_UP --type switch --initial 0
tools\mcgsctl\mcgsctl.ps1 workflow run window.button.add-momentary --project .mcgsctl-work\e2e\candidate.MCE --text HMI_UP --variable HMI_UP --x 610 --y 320
tools\mcgsctl\mcgsctl.ps1 workflow run device.channel.map --source FG2_HMI.MCE --area V --address 603 --count 4 --data-type-index 0 --connect-base HMI_PTZ
tools\mcgsctl\mcgsctl.ps1 workflow run script.edit --source FG2_HMI.MCE --text "TEST_FLAG=1" --button-text SCRIPT_TEST --verify-token TEST_FLAG --check
tools\mcgsctl\mcgsctl.ps1 workflow run window.indicator.add --source FG2_HMI.MCE --text LIMIT_ON --expression LIMIT_EXPR
tools\mcgsctl\mcgsctl.ps1 workflow run window.static-text.add --source FG2_HMI.MCE --workdir .mcgsctl-work\native-text --text SECTION_TITLE --x 560 --y 260 --width 220 --height 44
tools\mcgsctl\mcgsctl.ps1 workflow run window.lamp.add-native --project .mcgsctl-work\native-text\candidate.MCE --text LAMP_STATE --expression LAMP_STATE_VAR --x 560 --y 330 --width 140 --height 80
```

`mcgsctl.cmd` provides the same interface for `cmd.exe`.

## Workflow Safety

Write-capable workflows use a common safety gate:

- Prefer `--source <official.mce> --workdir <runDir>`. The tool copies the source to `<runDir>\candidate.MCE` and writes only that working copy.
- If `<runDir>\candidate.MCE` already exists, continue chained edits with `--project <runDir>\candidate.MCE`. Reusing `--source` for the same workdir fails unless `--replace-workdir` is explicit.
- Before copying, `--source` refuses Access lock files (`.ldb` / `.laccdb`), requires exclusive read access, checks source SHA before and after copy, and requires the working-copy SHA to match.
- Each `.mcgsctl-work` run writes chain-aware `mcgsctl-workspace.json`; `--project <copy.mce>` under `.mcgsctl-work` must match that marker.
- Successful mutating workflows append `workflow-results\<operationId>.json`, update `workflow-results\index.json`, and update `currentCandidateSha256` in the workspace marker.
- `--project <copy.mce>` under `.codex_tmp` remains available for profiling evidence and is marked as a profiling copy in audit output.
- Direct writes to any other `.MCE` require both `--allow-original` and `--expected-project-sha256 <sha256>`.
- Source, workdir, and project paths containing junctions, symlinks, or other reparse points are rejected by default.
- Every write workflow emits `audit-start.json` and `audit-end.json` with source path, working-copy path, before/after SHA256, save status, and success status.
- `audit-start.json` redacts `--text` script bodies by default and records only SHA256/length. Use `--audit-include-script-text` only for local debugging.
- Write workflows export `mce-before`, `mce-after`, and, where applicable, `mce-reopen` evidence so PASS depends on a before/after delta instead of a global string contains check.
- Unexpected property/script dialogs fail closed. Unknown script objects are not auto-created unless `--allow-create-dataobjects` is supplied.
- GUI workflows record dialog evidence in `dialogs.jsonl`, `popups.jsonl`, and `startup-dialogs.jsonl`. Any automatically clicked dialog records title, body, buttons, action, and screenshot path.
- `script.edit --allow-create-dataobjects` requires `--expected-new-dataobjects <name[,name...]>`; the saved Data-table delta must match exactly.
- `device.channel.map --skip-reopen-verify` is disabled unless `--debug-allow-skip-reopen-verify` is also supplied.
- `device.channel.map` refuses expected quick-connect variables that already exist unless `--allow-existing-variables` is supplied.
- `device.channel.map` refuses target Smart200 channels that already exist unless `--allow-existing-channels` is supplied, and fails if the same PLC address is mapped to a different expected variable.
- `project.check-save --pid <pid>` requires `--allow-attached` because it can save an already-open editor instance.

## Candidate Release Flow

`mcgsctl` treats a release candidate as a directory, not just one `.MCE` file:

```text
.mcgsctl-work\<run>\
  candidate.MCE
  mcgsctl-workspace.json
  workflow-results\
  profile-check.json
  project-check\check-result.json
  safety-result.json
  candidate-final\
  candidate-summary.json
  candidate-summary.md
  approval.template.json
```

Use these commands after the last mutating workflow:

```powershell
tools\mcgsctl\mcgsctl.ps1 profile check --workdir .mcgsctl-work\e2e --profile profiles\local-mcgs-7.7-smart200.json
tools\mcgsctl\mcgsctl.ps1 workflow run project.check --project .mcgsctl-work\e2e\candidate.MCE --fail-on-warning
tools\mcgsctl\mcgsctl.ps1 workflow run safety.verify --project .mcgsctl-work\e2e\candidate.MCE --spec safety-spec.json --evidence-dir .mcgsctl-work\e2e --awl FG2HMI.awl
tools\mcgsctl\mcgsctl.ps1 candidate summarize --workdir .mcgsctl-work\e2e
tools\mcgsctl\mcgsctl.ps1 candidate validate --workdir .mcgsctl-work\e2e
```

`candidate summarize` verifies the mutation SHA chain, exports `candidate-final\mce`, writes an approval template, and marks the candidate `apply-ready` only when all required final validators are `PASS` and newer than the last mutation.

`candidate-summary.json.resultSha256` records required result hashes but does not include the summary file's own hash. `approval.template.json.resultSha256` adds the `candidate-summary.json` hash, so approval locks both the summary and every required result file.

`profile check` writes `profile-check.json` with the editor path/SHA, editor PE bitness, mcgsctl process bitness, Windows/runtime facts, DPI, and Smart200 DLL path/SHA when found. A `--profile` baseline is required for a publishable `PASS`; without it the result is `UNKNOWN`. A profile mismatch is `UNKNOWN`, and `--allow-profile-drift` is diagnostics-only: the candidate remains blocked.

`safety.verify` reads `candidate-final\mce`, `workflow-results`, `profile-check.json`, `project-check\check-result.json`, and `safety-spec.json`. It fails direct HMI/control mappings to dangerous `Q` outputs, duplicate Smart200 address/variable mappings, and newly changed momentary variables without press/release readback evidence. `requiresAwl=true` without `--awl`, or AWL evidence that cannot prove a required check, returns `UNKNOWN`.

## Layout Preview And Apply

The layout layer generates a simple, reviewable HMI control area from JSON. It does not write `.MCE` blobs directly.

```powershell
tools\mcgsctl\mcgsctl.ps1 layout validate --layout layouts\ptz-basic.json --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 layout preview --layout layouts\ptz-basic.json --out .mcgsctl-runs\layout-preview --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 workflow run window.layout.apply --source FG2_HMI.MCE --workdir .mcgsctl-work\layout-gui-smoke --layout layouts\ptz-basic.json --safety safety-spec.json
```

`layout preview` writes `preview.svg`, `preview.html`, `preview.json`, and `validate.json` without opening MCGS. `window.layout.apply` creates GUI-supported `momentary-button`, `status-button`, `native-static-text`, and `native-lamp` objects by chaining the readback-verified GUI workflows. `section-title` and `static-label` now default to native static text; `renderAs: "status-button"` remains available as an explicit compatibility fallback. See `docs/layout-spec.md`.

Common blocked summaries:

```text
blocked: profile-check.json status is UNKNOWN
blocked: safety-result.json status is FAIL
blocked: project-check/check-result.json is not later than the last mutating workflow
blocked: mutation chain mismatch at workflow-results/0002-...
```

Formal apply is file-level replacement only:

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run project.apply-candidate --source FG2_HMI.MCE --candidate .mcgsctl-work\e2e\candidate.MCE --approval .mcgsctl-work\e2e\approval.json
```

Apply refuses `UNKNOWN`, missing results, result SHA mismatches, old final validators, official Access lock files, and candidate/official path equality. It creates a rollback package before `File.Replace`.

Rollback:

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run project.rollback --rollback <rollbackDir> --target FG2_HMI.MCE
```

Rollback requires the current official SHA to match the applied candidate SHA unless `--expected-current-sha256 <sha>` is supplied.

## Diagnostics

The tool includes low-level Win32/MFC diagnostics used to profile old MCGS dialogs:

```powershell
tools\mcgsctl\mcgsctl.ps1 windows
tools\mcgsctl\mcgsctl.ps1 children --pid <pid> --class Button
tools\mcgsctl\mcgsctl.ps1 find --pid <pid> --class SysTreeView32 --index 0
tools\mcgsctl\mcgsctl.ps1 treeview --hwnd 0x123456 --caret
tools\mcgsctl\mcgsctl.ps1 treeview --hwnd 0x123456 --notify-selchanged-text Smart200 --send
tools\mcgsctl\mcgsctl.ps1 toolbar --hwnd 0x123456
tools\mcgsctl\mcgsctl.ps1 listview --hwnd 0x123456 --double-text Smart200 --mouse
tools\mcgsctl\mcgsctl.ps1 popup --pid <pid> --open-hwnd 0x123456 --x 20 --y 20 --choose-id 32785 --exact --mouse
tools\mcgsctl\mcgsctl.ps1 capture --out .mcgsctl-runs\capture
tools\mcgsctl\mcgsctl.ps1 modules --pid <pid> --filter Smart200
tools\mcgsctl\mcgsctl.ps1 wndproc --hwnd 0x123456
tools\mcgsctl\mcgsctl.ps1 pe exports --file E:\MCGSE\Program\Drivers\PLC\Siemens\Smart200\Smart200.dll --filter SvrEdit
tools\mcgsctl\mcgsctl.ps1 strings --file E:\MCGSE\Program\McgsSetE.exe --filter internal
```

## Implemented Workflows

- `project.check`: runs MCGS project check without saving, captures check windows, writes `check-result.json`, and only passes on explicit zero-error/pass evidence or a readable empty result list.
- `project.check-save`: runs MCGS project check first, saves only after a PASS result, then captures evidence. When it opens a project itself, it follows the workflow safety gate.
- `realtime-db.add`: adds one realtime database object through the GUI, rejects pre-existing names, saves, exports before/after/reopen snapshots, and verifies the added `Data` row plus base properties. Verified types are currently `switch` and `numeric`; `string`, `event`, and `group` fail until their type codes are profiled.
- `window.button.add-momentary`: creates a standard button, configures press set-1 and release clear-0 operations through the MCGS variable picker, saves, reopens the working copy, reads the button property page, and verifies press/release operations plus empty script text.
- `device.channel.map`: opens the Smart200 device editor, checks duplicate target channels, adds PLC channels, optionally quick-connects variables, saves, closes, reopens the working copy, and verifies the Smart200 channel table again.
- `script.edit`: creates a standard button, opens the script editor, writes script text, refuses unknown objects unless an expected object whitelist is supplied, requires `--check` for production candidates, saves, exports before/after/reopen snapshots, and verifies script tokens plus Data-table deltas.
- `window.indicator.add`: creates a standard-button status indicator, configures its visibility expression, saves, exports before/after/reopen snapshots, reopens the property page, and verifies label/expression evidence plus no operation and empty script state. It is not a native lamp workflow.
- `window.static-text.add`: creates a native MCGS static text label through the animation toolbox, saves, exports before/after/reopen snapshots, reopens the native label property page, and verifies text readback.
- `window.lamp.add-native`: creates a native MCGS animation display component through the animation toolbox, sets display text and display variable, saves, exports before/after/reopen snapshots, reopens the native property page, and verifies both fields. It is GUI evidence for an HMI indicator, not hardware acceptance.
- `layout validate`: checks a declarative HMI layout spec offline for duplicate IDs, bad geometry, unsupported object kinds, missing control bindings, and optional safety-spec mismatches.
- `layout preview`: renders the layout to SVG/HTML/JSON evidence without opening MCGS.
- `window.layout.apply`: applies GUI-supported layout objects (`momentary-button`, `status-button`, `native-static-text`, `native-lamp`, and native static text defaults for `section-title` / `static-label`) to a candidate by chaining the existing readback-verified GUI workflows.

## Verification Limits

`blob_strings.json` verification is evidence, not proof of full object semantics. `device.channel.map` reopens the saved working copy and reads the Smart200 channel table again. `window.button.add-momentary` now also reopens the button property page and reads back the two operation sub-tabs; variable binding is set through the editor's picker instead of raw text injection. Native static text and native lamp workflows additionally reopen the native property dialogs and verify configured fields. AWL scanning is heuristic and is not a formal PLC proof. Hardware wiring, drive parameters, relay behavior, and field safety validation remain outside this tool.

Layout preview proves planned geometry and operator-readable structure only. A layout candidate is not release-ready until the underlying GUI workflow results, profile check, project.check, safety.verify, candidate summarize, and candidate validate all pass.

## Release Build

Development entrypoints use `dotnet run`. For a fixed release build:

```powershell
tools\mcgsctl\publish.ps1
```

The release writes both:

```text
tools\mcgsctl\dist\win-x86\
tools\mcgsctl\dist\win-x64\
```

Both include `docs`, `profiles`, `schemas`, `java`, `lib`, dependency notes, third-party notices, and hash manifests. Because these are framework-dependent .NET builds, the matching .NET 7 Windows Desktop Runtime must be installed for the selected architecture. Prefer `win-x86` for MCGS 7.7 when the x86 runtime is installed because old MFC/common-control remote structures are more reliable when the automation process bitness matches the 32-bit editor.

## Known Limits

- GUI automation needs a logged-in, visible Windows desktop. Locked screens, minimized RDP sessions, and service sessions are not supported.
- Unknown MCGS versions, different DPI/font settings, changed driver DLLs, or changed language packs can break dialog profiles.
- The CLI cannot bypass MCGS licensing, project passwords, Windows permissions, or vendor protections.
- Industrial control changes still require PLC/HMI safety review, project-check evidence, rollback artifacts, and human approval before release.
