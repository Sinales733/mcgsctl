# mcgsctl

`mcgsctl` is a local CLI helper for the MCGS embedded editor used by the FG2 HMI project. It intentionally uses a hybrid model:

- `.MCE` files are read only for inspection and verification.
- Project writes must go through the visible MCGS editor GUI.
- Direct binary patching of `lbObjects` is not implemented because the object format is not public and can corrupt projects.

## Quick Start

From the repository root:

```powershell
.\mcgsctl.ps1 doctor --project FG2_HMI.MCE
.\mcgsctl.ps1 mce export --project FG2_HMI.MCE --out .mcgsctl-runs\export
.\mcgsctl.ps1 open --project FG2_HMI.MCE
.\mcgsctl.ps1 menus
.\mcgsctl.ps1 command --id 57603 --send
.\mcgsctl.ps1 mdi --pid <pid>
.\mcgsctl.ps1 guiinfo --hwnd 0x123456 --activate
.\mcgsctl.ps1 modules --pid <pid> --filter Smart200
.\mcgsctl.ps1 wndproc --hwnd 0x123456
.\mcgsctl.ps1 pe exports --file E:\MCGSE\Program\Drivers\PLC\西门子\Smart200\Smart200.dll --filter SvrEdit
.\mcgsctl.ps1 strings --file E:\MCGSE\Program\McgsSetE.exe --filter 内部属性
.\mcgsctl.ps1 windows
.\mcgsctl.ps1 children --pid <pid> --class Button
.\mcgsctl.ps1 find --pid <pid> --class SysTreeView32 --index 0
.\mcgsctl.ps1 treeview --hwnd 0x123456 --caret
.\mcgsctl.ps1 treeview --hwnd 0x123456 --notify-selchanged-text Smart200 --send
.\mcgsctl.ps1 toolbar --hwnd 0x123456
.\mcgsctl.ps1 listview --hwnd 0x123456 --double-text Smart200 --mouse
.\mcgsctl.ps1 point --hwnd 0x123456 --x 20 --y 20 --right
.\mcgsctl.ps1 drag --hwnd 0x123456 --x1 20 --y1 20 --x2 120 --y2 60 --mouse
.\mcgsctl.ps1 popup --pid <pid> --open-hwnd 0x123456 --x 20 --y 20 --choose-id 32785 --exact --mouse
.\mcgsctl.ps1 capture --out .mcgsctl-runs\capture
.\mcgsctl.ps1 run --file workflow.actions.json
.\mcgsctl.ps1 snapshot --project FG2_HMI.MCE --out .mcgsctl-runs\snapshot
.\mcgsctl.ps1 workflow run realtime-db.add --project .codex_tmp\test.MCE --name HMI_UP --type switch --initial 0
.\mcgsctl.ps1 workflow run window.button.add-momentary --project .codex_tmp\test.MCE --text 仰 --variable HMI_UP --x 610 --y 320
.\mcgsctl.ps1 workflow run device.channel.map --project .codex_tmp\test.MCE --area V --address 603 --count 4 --data-type-index 0 --access 读写 --connect-base HMI_PTZ
.\mcgsctl.ps1 workflow run script.edit --project .codex_tmp\test.MCE --text "TEST_FLAG=1" --button-text SCRIPT_TEST --verify-token TEST_FLAG
.\mcgsctl.ps1 workflow run window.indicator.add --project .codex_tmp\test.MCE --text LIMIT_ON --expression LIMIT_EXPR
```

`mcgsctl.cmd` provides the same interface for `cmd.exe`.

## Implemented Commands

- `doctor`: checks editor path, Windows SDK Inspect, Java/Javac, Jackcess jars, FlaUI package references, interactive desktop, and `.MCE` file signature.
- `open --project <mce>`: starts `McgsSetE.exe` with the project and returns the process/window identity.
- `tree [--pid <pid>]`: dumps visible child window handles, class names, text, and rectangles.
- `children [--pid <pid>] [--hwnd|--root <hex>] [--class <class>] [--text <text>]`: returns child windows as JSON, with optional filtering.
- `find [--pid <pid>] [--hwnd <hex>] [--class <class>] [--text <text>] [--index <n>]`: returns matching child controls as JSON.
- `windows [--pid <pid>]`: lists all visible top-level MCGS windows and modal dialogs for a process.
- `menus [--pid <pid>]`: enumerates editor menu paths and command IDs.
- `command --id <id> [--hwnd <hex>] [--target main|mdi|view] [--send] [--hiword <n>]`: sends a `WM_COMMAND` to the editor, active MDI child, active MDI view, or explicit HWND. `--send` uses `SendMessageTimeout` for MFC routing experiments; otherwise the command is posted.
- `mdi [--pid <pid>]`: reports the MDI client, active MDI child, and active MDI view.
- `guiinfo [--hwnd <hex>] [--activate]`: reports `GetGUIThreadInfo` focus/active handles for verifying that old MFC controls really own focus. `--activate` first attempts foreground/focus activation.
- `modules [--pid <pid>] [--filter <text>]`: lists loaded modules with base/end addresses. It uses Toolhelp snapshots so it works against the 32-bit MCGS editor from the 64-bit CLI process.
- `wndproc --hwnd <hex>`: reports a window's class/window/dialog procedure pointers and maps known addresses back to loaded modules. This is used to tell whether a captured dialog is handled by `McgsSetE.exe`, MFC/common-controls, or a driver DLL.
- `pe exports --file <dll-or-exe> [--filter <text>]`: reads a PE export table without launching the binary. This is used for driver DLLs such as `Smart200.dll`.
- `strings --file <file> [--filter <text>] [--encoding ansi|unicode|both] [--min <n>] [--limit <n>]`: extracts ANSI and UTF-16 strings with file offsets. This is used to find hidden dialog labels, class names, and driver entry-point evidence in MCGS binaries or copied project files.
- `sendmsg --hwnd <hex> --msg <n> [--wparam <n>] [--lparam <n>] [--post]`: sends or posts a raw Win32 message for profiling.
- `notify --hwnd <control> --code <n> [--parent <hwnd>] [--send]`: sends a standard `WM_NOTIFY` header from a child control to its parent.
- `toolbar --hwnd <hex>`: reads `ToolbarWindow32` buttons with command IDs, states, and rectangles. It can invoke a button by command/index without relying on screen coordinates.
- `treeview --hwnd <hex>`: reads `SysTreeView32` item handles/text/rectangles, can synchronously select/expand items, can report the current caret item with `--caret`, and can send a full `NMTREEVIEW/TVN_SELCHANGED` notification for old MFC selection-routing tests.
- `listview --hwnd <hex>`: reads `SysListView32` rows/subitems/rectangles and can select or double-click rows by index or contained text.
- `combo`, `list`, `tab`, `check`: low-level helpers for standard combo boxes, list boxes, tab controls, and check/radio buttons. `tab` can enumerate tab labels or select by text; `tab --mouse` performs a real tab click for old MFC property sheets.
- `click --text <text> [--mouse]`: clicks a visible Win32 button by exact or partial text. `--mouse` uses the button rectangle center as a fallback for old MFC paths.
- `point --hwnd <hex> --x <n> --y <n> [--right] [--double] [--mouse]`: clicks by handle-relative coordinates. The default posts Win32 messages; `--mouse` is the physical cursor fallback.
- `drag --hwnd <hex> --x1 <n> --y1 <n> --x2 <n> --y2 <n> [--mouse]`: drags by handle-relative coordinates; drawing workflows use `--mouse`.
- `set-text --hwnd <hex> (--text <text>|--file <txt>) [--paste]`: writes text into an Edit-like control. `--paste` uses clipboard paste plus change notifications and is preferred for Chinese text in old MFC dialogs.
- `get-text --hwnd <hex>`: reads a window/control title or text.
- `keys --text <sendkeys>`: sends a Windows Forms `SendKeys` sequence to the active UI.
- `wait [--pid <pid>] [--title <text>] [--class <class>]`: waits for a visible top-level MCGS window/dialog and prints JSON.
- `popup [--pid <pid>] [--open-hwnd <hex> --x <n> --y <n>] [--context] [--choose-index <n>|--choose-id <id>|--choose-text <text>] [--exact] [--mouse]`: opens/reads `#32768` popup menus. `--exact` uses the popup HMENU and `GetMenuItemRect` instead of row-height guessing.
- `capture [--pid <pid>]`: captures all top-level MCGS windows, control trees, and screenshots. Screenshots use `PrintWindow`, so they work even when the MCGS window is covered.
- `run --file <actions.json>`: executes a JSON action list using the same primitive operations. The runner supports `open`, `command`, `find`, `click`, `toolbar`, `treeview`, `listview`, `notify`, `point`, `drag`, `set-text`, `keys`, `wait`, `capture`, `sleep`, and `close`.
- `close --save|--discard`: closes the editor and handles the common exit confirmation.
- `snapshot`: writes `snapshot.json`, `.MCE` export files, window tree, menu list, screenshot, MDI/focus state, module list, and top-level window procedure mapping when a GUI window is attached.
- `mce export`: exports `summary.json`, `schema.json`, `data.json`, and `blob_strings.json`.
- `verify --spec <json>`: verifies expected variables, table row counts, and blob string contents.
- `workflow run project.check-save`: sends save and project-check commands, then captures evidence.
- `workflow run realtime-db.add --project <mce> --name <object>`: adds one realtime database object through the GUI, sets type/initial/unit/note on the property dialog, saves, captures evidence, and verifies the object in `Data`. It refuses the default project unless `--allow-original` is supplied.
- `workflow run window.button.add-momentary --project <mce> --text <label> --variable <name>`: opens a copied project, adds a standard button to user window index `2` by default, configures press `置1` and release `清0`, saves, captures evidence, and verifies the label/variable in `.MCE`. It refuses the default project unless `--allow-original` is supplied.
- `workflow run device.channel.map --project <mce> --area V --address 603 --count 4 --connect-base HMI_PTZ`: opens a copied project, enters the Smart200 device editor, adds PLC channels such as `读写V603.0` through `读写V603.3`, optionally runs Smart200 quick variable connection, accepts the `全部添加` data-object prompt, saves, reopens the copied project, and verifies the Smart200 channel table again. It refuses the default project unless `--allow-original` is supplied. Use `--skip-reopen-verify` only when deliberately trading assurance for speed.
  For non-bit channel types, omit `--expected-channel`; the workflow reads the newly added Smart200 row and uses the real displayed channel text for save/reopen verification. Example: `--data-type-index 8 --address 604 --count 1` was verified as `读写VBUB604` on this installation.
- `workflow run script.edit --project <mce> (--text <script>|--file <txt>)`: opens a copied project, creates a standard button, opens the press/release script editor, writes the script, handles the MCGS unknown-object confirmation, re-finds the rebuilt button-property dialog, saves, exports `.MCE`, and verifies script tokens in `blob_strings.json`. It refuses the default project unless `--allow-original` is supplied.
- `workflow run window.indicator.add --project <mce> --text <label> --expression <expr>`: opens a copied project, creates a standard-button status indicator, configures its visibility expression, handles property confirmation dialogs, saves, exports `.MCE`, and verifies the label/expression in `blob_strings.json`. It refuses the default project unless `--allow-original` is supplied.

## Registered But Not Yet Enabled

No workflow is currently registered in this category. New write-capable workflows should stay here until their dialogs have been profiled and verified on copied `.MCE` projects.

Dialog profile drafts live in `profiles/dialogs/`. They record the observed controls, command IDs, remaining blockers, and evidence folders from copied `.MCE` experiments.

## Action Script Example

```json
[
  { "action": "open", "project": ".codex_tmp/FG2_HMI_test.MCE" },
  { "action": "command", "id": 33955 },
  { "action": "find", "root": "main", "class": "SysTreeView32", "index": 0 },
  { "action": "treeview", "selectRoot": true },
  { "action": "capture", "out": ".mcgsctl-runs/user-window" },
  { "action": "close" }
]
```

## Verify Spec Example

```json
{
  "variables": [
    "喷射允许",
    { "name": "喷水状态" }
  ],
  "blobContains": [
    "西门子_Smart200",
    "V601.0"
  ],
  "tables": {
    "Data": 58,
    "WndUser": 3
  }
}
```

Run:

```powershell
.\mcgsctl.ps1 verify --project FG2_HMI.MCE --spec spec.json
```

## Known Limits

- GUI automation needs a logged-in, visible Windows desktop. Locked screens, minimized RDP sessions, and service sessions are not supported.
- The first write-capable workflows require one profiling pass per dialog/page so the tool can bind controls reliably.
- The CLI cannot bypass MCGS licensing, project passwords, Windows permissions, or vendor protections.
