# mcgsctl

`mcgsctl` is a local CLI helper for the MCGS embedded editor used by the FG2 HMI project.

Design rules:

- `.MCE` files are read only for inspection and verification.
- Project writes go through the visible MCGS editor GUI.
- The tool does not patch `WndUser.lbObjects`, `WndDevice.lbObjects`, or other private binary object blobs.

## Quick Start

From this repository root:

```powershell
.\\mcgsctl.ps1 doctor --project FG2_HMI.MCE
.\\mcgsctl.ps1 --version
.\\mcgsctl.ps1 mce export --project FG2_HMI.MCE --out .mcgsctl-runs\export
.\\mcgsctl.ps1 snapshot --project FG2_HMI.MCE --out .mcgsctl-runs\snapshot
.\\mcgsctl.ps1 workflow run project.check --source FG2_HMI.MCE --fail-on-warning
.\\mcgsctl.ps1 open --project FG2_HMI.MCE
.\\mcgsctl.ps1 menus
.\\mcgsctl.ps1 command --id 57603 --send
```

Write workflows should normally use `--source`:

```powershell
.\\mcgsctl.ps1 workflow run realtime-db.add --source FG2_HMI.MCE --name HMI_UP --type switch --initial 0
.\\mcgsctl.ps1 workflow run window.button.add-momentary --source FG2_HMI.MCE --text HMI_UP --variable HMI_UP --x 610 --y 320
.\\mcgsctl.ps1 workflow run device.channel.map --source FG2_HMI.MCE --area V --address 603 --count 4 --data-type-index 0 --connect-base HMI_PTZ
.\\mcgsctl.ps1 workflow run script.edit --source FG2_HMI.MCE --text "TEST_FLAG=1" --button-text SCRIPT_TEST --verify-token TEST_FLAG --check
.\\mcgsctl.ps1 workflow run window.indicator.add --source FG2_HMI.MCE --text LIMIT_ON --expression LIMIT_EXPR
```

`mcgsctl.cmd` provides the same interface for `cmd.exe`.

## Workflow Safety

Write-capable workflows use a common safety gate:

- Prefer `--source <official.mce>`. The tool copies the source into `.mcgsctl-work/<workflow>-<timestamp>/` and writes only that working copy.
- Before copying, `--source` refuses Access lock files (`.ldb` / `.laccdb`), requires exclusive read access, checks source SHA before and after copy, and requires the working-copy SHA to match.
- Each `.mcgsctl-work` run writes `mcgsctl-workspace.json`; `--project <copy.mce>` under `.mcgsctl-work` must match that marker.
- `--project <copy.mce>` under `.codex_tmp` remains available for profiling evidence and is marked as a profiling copy in audit output.
- Direct writes to any other `.MCE` require both `--allow-original` and `--expected-project-sha256 <sha256>`.
- Source, workdir, and project paths containing junctions, symlinks, or other reparse points are rejected by default.
- Every write workflow emits `audit-start.json` and `audit-end.json` with source path, working-copy path, before/after SHA256, save status, and success status.
- `audit-start.json` redacts `--text` script bodies by default and records only SHA256/length. Use `--audit-include-script-text` only for local debugging.
- Write workflows export `mce-before`, `mce-after`, and, where applicable, `mce-reopen` evidence so PASS depends on a before/after delta instead of a global string contains check.
- Unexpected property/script dialogs fail closed. Unknown script objects are not auto-created unless `--allow-create-dataobjects` is supplied.
- `script.edit --allow-create-dataobjects` requires `--expected-new-dataobjects <name[,name...]>`; the saved Data-table delta must match exactly.
- `device.channel.map --skip-reopen-verify` is disabled unless `--debug-allow-skip-reopen-verify` is also supplied.
- `device.channel.map` refuses expected quick-connect variables that already exist unless `--allow-existing-variables` is supplied.
- `device.channel.map` refuses target Smart200 channels that already exist unless `--allow-existing-channels` is supplied, and fails if the same PLC address is mapped to a different expected variable.
- `project.check-save --pid <pid>` requires `--allow-attached` because it can save an already-open editor instance.

## Diagnostics

The tool includes low-level Win32/MFC diagnostics used to profile old MCGS dialogs:

```powershell
.\\mcgsctl.ps1 windows
.\\mcgsctl.ps1 children --pid <pid> --class Button
.\\mcgsctl.ps1 find --pid <pid> --class SysTreeView32 --index 0
.\\mcgsctl.ps1 treeview --hwnd 0x123456 --caret
.\\mcgsctl.ps1 treeview --hwnd 0x123456 --notify-selchanged-text Smart200 --send
.\\mcgsctl.ps1 toolbar --hwnd 0x123456
.\\mcgsctl.ps1 listview --hwnd 0x123456 --double-text Smart200 --mouse
.\\mcgsctl.ps1 popup --pid <pid> --open-hwnd 0x123456 --x 20 --y 20 --choose-id 32785 --exact --mouse
.\\mcgsctl.ps1 capture --out .mcgsctl-runs\capture
.\\mcgsctl.ps1 modules --pid <pid> --filter Smart200
.\\mcgsctl.ps1 wndproc --hwnd 0x123456
.\\mcgsctl.ps1 pe exports --file E:\MCGSE\Program\Drivers\PLC\Siemens\Smart200\Smart200.dll --filter SvrEdit
.\\mcgsctl.ps1 strings --file E:\MCGSE\Program\McgsSetE.exe --filter internal
```

## Implemented Workflows

- `project.check`: runs MCGS project check without saving, captures check windows, writes `check-result.json`, and only passes on explicit zero-error/pass evidence or a readable empty result list.
- `project.check-save`: runs MCGS project check first, saves only after a PASS result, then captures evidence. When it opens a project itself, it follows the workflow safety gate.
- `realtime-db.add`: adds one realtime database object through the GUI, rejects pre-existing names, saves, exports before/after/reopen snapshots, and verifies the added `Data` row plus base properties. Verified types are currently `switch` and `numeric`; `string`, `event`, and `group` fail until their type codes are profiled.
- `window.button.add-momentary`: creates a standard button, configures press set-1 and release clear-0 operations through the MCGS variable picker, saves, reopens the working copy, reads the button property page, and verifies press/release operations plus empty script text.
- `device.channel.map`: opens the Smart200 device editor, checks duplicate target channels, adds PLC channels, optionally quick-connects variables, saves, closes, reopens the working copy, and verifies the Smart200 channel table again.
- `script.edit`: creates a standard button, opens the script editor, writes script text, refuses unknown objects unless an expected object whitelist is supplied, runs optional script check, saves, exports before/after/reopen snapshots, and verifies script tokens plus Data-table deltas.
- `window.indicator.add`: creates a standard-button status indicator, configures its visibility expression, saves, exports before/after/reopen snapshots, and verifies label/expression evidence.

## Verification Limits

`blob_strings.json` verification is evidence, not proof of full object semantics. `device.channel.map` reopens the saved working copy and reads the Smart200 channel table again. `window.button.add-momentary` now also reopens the button property page and reads back the two operation sub-tabs; variable binding is set through the editor's picker instead of raw text injection. Indicator and script workflows still rely on MCE delta plus reopen export for some properties, so production edits still need evidence review.

## Release Build

Development entrypoints use `dotnet run`. For a fixed release build:

```powershell
.\\publish.ps1
```

The release is written to `dist\\win-x64\\` with `mcgsctl.exe`, `mcgsctl.exe.sha256`, `SHA256SUMS.txt`, `version.json`, and dependency notes. Because this is a framework-dependent .NET build, `SHA256SUMS.txt` includes the assembly DLL hash as the code-bearing artifact.

## Known Limits

- GUI automation needs a logged-in, visible Windows desktop. Locked screens, minimized RDP sessions, and service sessions are not supported.
- Unknown MCGS versions, different DPI/font settings, changed driver DLLs, or changed language packs can break dialog profiles.
- The CLI cannot bypass MCGS licensing, project passwords, Windows permissions, or vendor protections.
- Industrial control changes still require PLC/HMI safety review, project-check evidence, rollback artifacts, and human approval before release.