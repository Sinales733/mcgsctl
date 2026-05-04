# mcgsctl Long-Running Codex Prompt

Use this file as the durable instruction contract for building and hardening the
FG2 MCGS command-line tool. It exists so the task survives context compression:
when resuming, re-read this file instead of relying on memory from the chat.

## Mission

Complete `tools/mcgsctl` as a reliable command-line automation layer for the
MCGS embedded editor used by `FG2_HMI.MCE`.

The tool must eventually support real HMI drawing through the visible MCGS GUI:
it should be able to create a candidate copy, open it in MCGS, place supported
controls on the animation canvas, configure their properties through editor
dialogs, save, reopen, read back evidence, and validate the result before any
formal apply.

Do not interpret "drawing" as direct binary patching of `.MCE` internals. In
this project, real drawing means using MCGS itself as the writer and proving the
created objects through exported evidence, screenshots, property readback, and
candidate validation.

## Compression-Resistant Startup

At the start of every new run, after any context compression, or whenever the
thread looks stale, do this before making decisions:

1. Read `AGENTS.md`.
2. Read this file: `tools/mcgsctl/MCGSCTL_LONGRUN_PROMPT.md`.
3. Run `git status --short` from `E:\myproject\FG2`.
4. Read `tools/mcgsctl/README.md` and `docs\operator-runbook.md` if the task
   touches workflow behavior, release gates, or GUI writes.
5. Inspect current code and tests before assuming which features already exist.
6. Do not trust compressed chat memory over current files and command output.

If progress state is unclear, reconstruct it from:

- `git status --short`
- `git diff -- tools/mcgsctl`
- `tools/mcgsctl\README.md`
- `tools/mcgsctl\docs\operator-runbook.md`
- `tools/mcgsctl\tests\McgsCtl.Tests`
- recent evidence under `.mcgsctl-work` and `.mcgsctl-runs`

## Current Project Facts To Preserve

- Main project root: `E:\myproject\FG2`.
- Tool root: `E:\myproject\FG2\tools\mcgsctl`.
- Official HMI project: `FG2_HMI.MCE`.
- PLC logic file: `FG2HMI.awl`.
- The existing tool is .NET Windows code and should stay on
  `net7.0-windows` unless the user explicitly changes that boundary.
- `.MCE` files are opaque project files. Treat private blobs as read-only
  evidence; never patch object blobs directly.
- Candidate-copy safety is not optional. All write workflows must operate on a
  candidate copy unless the user explicitly authorizes a formal apply.

## Non-Negotiable Safety Rules

- Never silently modify the official `FG2_HMI.MCE`.
- Never use `git reset --hard`, `git clean`, `git checkout --`, or `git restore`
  to erase user work.
- Never treat a successful string search in an exported blob as proof of full
  HMI object semantics.
- Never let `UNKNOWN` pass release gates or become apply-ready.
- Never run GUI automation when the desktop is locked, RDP is minimized, MCGS
  is hidden in a service session, or a person is actively using the editor.
- Never broaden the task into PLC logic changes, hardware wiring assumptions, or
  CAD edits unless the user asks for that scope.
- Do not approve industrial-control behavior. The tool can produce evidence; a
  human still approves release.

## Working Style

Work continuously through safe subtasks instead of stopping at the first small
ambiguity. Ask the user only when the next action would risk official project
files, real hardware behavior, irreversible drawing state, or a scope decision
that cannot be inferred from current files.

## No Premature Stop Mode

When the user authorizes unattended completion, do not end the run with a loose
list of "remaining work" if there is still a reasonable technical path to try.
Treat every blocker as a new engineering task:

- Reproduce the failure and save evidence.
- Classify the blocker: code defect, GUI state, MCGS profile drift, missing
  command ID, coordinate mapping, UIA/MSAA gap, clipboard gap, validation
  failure, build/test failure, Git/push failure, or environment condition.
- Try at least one narrower local fix before abandoning a path.
- If the first path fails, try a different evidence source or implementation
  route: UIA, MSAA, `WM_GETOBJECT`, clipboard, toolbar command discovery,
  read-only MCE geometry, GUI readback, screenshots, or Gemini review.
- If Gemini is available, ask Gemini a bounded question with sanitized evidence,
  convert the answer into a local test or code change, and continue.
- If Gemini is unavailable, continue from local evidence and public/known
  Windows automation patterns instead of stopping.
- Keep a local work log in generated evidence or the final summary so a later
  run can resume without relying on chat memory.

Only stop before full completion when the next required action is outside a
file-revertible development task: real hardware operation, field-device action,
credential exposure, irreversible external service change, or a formal release
approval that cannot be simulated with a candidate/temporary copy. In that
case, skip that action, document it as requiring human approval, and continue
all remaining file-safe work.

Definition of "done" for unattended completion:

- The intended command surface exists and is documented.
- Relevant tests/builds pass or failing old/environmental tests are clearly
  isolated with evidence.
- GUI-capable work has candidate evidence, screenshots, readback, and validator
  results when the desktop environment allows it.
- Canvas reading/planning succeeds with `canvas-objects.json` and
  `layout-plan.json`. If surface channels return `UNKNOWN`, that is not final;
  continue into `MCGS_DRAW_OBJ` / `lbObjects` differential reverse engineering
  until tool-created object geometry can be read back or every file-safe
  decoding path has concrete negative evidence.
- A branch contains the verified work, with a commit and push when possible.

When blocked on GUI access, keep advancing offline pieces:

- schema
- parser
- validator
- preview renderer
- tests
- command help
- runbook
- safety evidence integration
- candidate summary behavior

Before editing files, inspect nearby code and follow the existing style. Keep
patches small and reviewable. Do not rewrite `Program.cs` wholesale just because
it is large.

## Gemini Bridge Collaboration

This machine also has a local Vertex/OpenClaw bridge at:

```text
E:\googlecli\bridge
```

Use it as an optional external reviewer for UI, visual layout, GUI automation,
and frontend-style reasoning when that would help. The bridge listens on
`127.0.0.1:4000` by default and exposes OpenAI-compatible endpoints including:

- `GET /health`
- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/images/generations`

The bridge README currently lists `gemini-3.1-pro-preview` as the default chat
model. Verify live bridge state before relying on it:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:4000/health
Invoke-RestMethod -Uri http://127.0.0.1:4000/v1/models
```

If `/v1/models` or chat requests return 401, the bridge may require
`BRIDGE_API_KEY`. It is acceptable to read `BRIDGE_API_KEY` from
`E:\googlecli\bridge\.env` only to build a local `Authorization: Bearer ...`
header. Do not print the key, include it in prompts, commit it, or copy the
`.env` file.

If the bridge is not running, start it from `E:\googlecli\bridge`:

```powershell
.\start_bridge.ps1
```

Use Gemini for bounded questions, not open-ended delegation. Good prompts:

- "Given this MCGS canvas screenshot and the current control spec, which object
  looks misplaced or too small?"
- "Here is the `FindCanvas`/drag/readback failure evidence. What failure modes
  should I test next?"
- "Review this layout JSON and preview screenshot for HMI usability issues."
- "Compare these before/after screenshots and list likely GUI automation
  mistakes."

Rules for Gemini collaboration:

- Do not send `.env`, API keys, credentials, private tokens, or full secret
  logs.
- Do not send raw `.MCE` private blobs. Send specs, screenshots, exported
  evidence, command output, or small code snippets instead.
- Do not let Gemini make release decisions. It can suggest; local tests,
  readback evidence, project checks, safety checks, and user approval decide.
- Do not present Gemini's advice as verified. Convert advice into a local test,
  code change, or rejected hypothesis.
- If sending screenshots, prefer generated preview PNGs, MCGS captures, or
  evidence images under `.mcgsctl-work` / `.mcgsctl-runs`.
- If multimodal/image input through the bridge fails, continue with text-only
  summaries and local evidence. Do not block the core `mcgsctl` work on Gemini.

## Unattended Git Update Mode

Default behavior remains conservative: do not commit or push unless the user
explicitly asks for a Git update. When the user says the run is unattended and
asks Codex to finish the work and upload/update Git, use this mode.

In unattended Git update mode:

- Create or reuse a task branch with the `codex/` prefix when practical, for
  example `codex/mcgsctl-gui-drawing`.
- Continue working without asking the user about ordinary implementation
  choices. Use local evidence and Gemini advice to decide.
- Commit only relevant source, tests, schemas, docs, runbooks, and intentional
  project artifacts.
- Do not commit `.env`, credentials, API keys, secrets, personal logs,
  `.mcgsctl-work`, `.mcgsctl-runs`, `.codex_tmp`, `bin`, `obj`, `dist`, or large
  generated evidence unless the user explicitly requested those artifacts in
  Git.
- If `FG2_HMI.MCE` must change for the final deliverable, do not edit it by raw
  blob patching. Produce the change through the candidate/apply workflow,
  preserve rollback evidence, run available validators, and commit the official
  file only after the tool-created candidate path is the source of the change.
- Git rollback is acceptable for files, but it is not a substitute for hardware
  safety. Do not perform real hardware actions, field-device commands, or
  irreversible external operations.
- After a coherent milestone passes verification, run `git diff --stat`,
  review the staged file list, commit with a specific message, and push the
  branch if a remote is configured.
- If push fails because no remote, no credentials, network failure, or branch
  policy, keep the local commit and report the exact failure.

## Target Command Surface

Build toward this staged surface. Keep names stable unless the current code has
a clearly better convention.

### Offline Layout Layer

```powershell
tools\mcgsctl\mcgsctl.ps1 layout validate --spec <layout.json>
tools\mcgsctl\mcgsctl.ps1 layout preview --spec <layout.json> --out <dir>
```

Required behavior:

- Validate schema, coordinates, control names, variable names, duplicate IDs,
  window index, and supported object kinds.
- Produce machine-readable validation results.
- Produce a human-readable preview, preferably PNG or HTML plus JSON evidence,
  without opening MCGS.
- Fail closed on unsupported native controls, ambiguous bindings, overlapping
  critical controls, or missing variables.

### GUI Candidate Apply Layer

```powershell
tools\mcgsctl\mcgsctl.ps1 workflow run window.layout.apply `
  --source FG2_HMI.MCE `
  --workdir .mcgsctl-work\layout-e2e `
  --spec <layout.json>
```

Required behavior:

- Create or reuse a candidate according to the existing candidate workspace
  rules.
- Open the candidate in visible MCGS.
- Select the target window.
- Draw supported controls on the real animation canvas using GUI operations.
- Configure properties through MCGS dialogs, not by patching blobs.
- Save, reopen, and read back evidence for every created or touched object.
- Emit structured workflow evidence under `workflow-results`.
- Capture dialogs, popups, screenshots, and readback JSON.
- Leave the official `FG2_HMI.MCE` untouched.

### Readback Layer

```powershell
tools\mcgsctl\mcgsctl.ps1 layout readback --project <candidate.MCE> --spec <layout.json> --out <dir>
```

Required behavior:

- Reopen the candidate.
- Locate each object by deterministic evidence such as label, coordinates,
  operation binding, visibility expression, or property dialog contents.
- Report `PASS`, `FAIL`, or `UNKNOWN` per object.
- Block release when any required object is `FAIL` or `UNKNOWN`.

### Canvas Inspection Layer

The latest handoff direction is to stop relying on screenshots as the primary
layout source. Screenshots are evidence and review aids. The planner must first
try to understand the MCGS animation canvas from internal or semi-internal
signals.

Add read-only diagnostics before claiming automatic layout:

```powershell
tools\mcgsctl\mcgsctl.ps1 canvas inspect --project <candidate.MCE> --window-index <n> --out <dir>
tools\mcgsctl\mcgsctl.ps1 canvas context-menu-probe --project <candidate.MCE> --point <x,y> --out <dir>
tools\mcgsctl\mcgsctl.ps1 canvas clipboard-probe --project <candidate.MCE> --select-all --out <dir>
```

`canvas inspect` must not save or mutate the project. It should open only a
candidate or temporary copy and emit:

- canvas HWND, class, rect, client rect, style, and exstyle
- parent chain, MDI active child, and view handle
- window-proc module, process module, and known toolbar command map
- scrollbars, DPI, and client-to-screen coordinate mapping
- UIA probe: `ElementFromHandle(canvas)`, child elements, control type, name,
  bounding rectangles, and supported patterns
- MSAA probe: `AccessibleObjectFromWindow(canvas, OBJID_CLIENT)`, child count,
  `accName`, `accRole`, `accLocation`, and `accHitTest`
- `WM_GETOBJECT` probe for `OBJID_CLIENT`, `UiaRootObjectId`, and
  `OBJID_NATIVEOM`
- result field: `objectProvider = uia | msaa | nativeom | clipboard | mce-geometry | none`

`context-menu-probe` and `clipboard-probe` exist to discover object channels:

- whether right-click menus expose object properties, select-all, copy,
  alignment, distribution, or arrange command IDs
- whether selecting objects and pressing `Ctrl+C` creates custom clipboard
  formats that contain object geometry or object lists
- whether that clipboard data can support a read-only `canvas-objects.json`
  object map

If UIA/MSAA/native object model/clipboard paths expose no object geometry, try
read-only `.MCE` export geometry inference only as evidence. Do not patch `.MCE`
private blobs.

Probe continuation rule:

- A single probe returning `UNKNOWN`, timing out, or observing no menu/object is
  not a stopping point.
- If `canvas inspect` shows UIA/MSAA/WM_GETOBJECT/NativeOM expose no children,
  mark those channels exhausted and immediately continue to context-menu,
  clipboard, toolbar command, and read-only MCE geometry routes.
- If `context-menu-probe` returns `UNKNOWN` because no context menu was observed,
  do not stop. Improve its close/timeout behavior if needed, record the
  negative evidence, and run `clipboard-probe`.
- If `clipboard-probe` finds custom formats, hash and summarize them, then
  build a read-only decoder or at least a geometry extractor.
- If clipboard exposes nothing useful, inspect MCGS export/output evidence and
  `.MCE` internal read-only structures for coordinates, but never write private
  blobs.
- After UIA, MSAA, WM_GETOBJECT, NativeOM, context menu, clipboard, toolbar
  command discovery, and read-only MCE geometry have all been tried, do not treat
  `UNKNOWN` as completion. Treat it as input to the differential reverse
  engineering stage below.

Known current evidence as of the 2026-05-04 resume:

- `canvas-inspect-20260504-004732` showed UIA root `Pane`, `childCount=0`;
  MSAA `childCount=0`; `OBJID_CLIENT`, `UiaRootObjectId`, and `OBJID_NATIVEOM`
  returned no object model.
- `canvas-context-20260504-004757\context-menu.json` returned `UNKNOWN` with
  `no canvas context menu was observed`.
- The next unattended continuation should not rerun the same probes by reflex.
  It should fix any probe lifecycle issue if obvious, then continue with
  `canvas clipboard-probe`, toolbar command discovery, and read-only MCE geometry
  inference.

### MCGS Geometry Reverse Engineering Stage

If the normal object channels cannot produce a reliable object map, continue
with file-safe reverse engineering. This stage is required before declaring the
automatic internal-occupancy planner impossible.

Target evidence sources:

- clipboard `MCGS_DRAW_OBJ` format
- `WndUser.lbObjects`
- `WndDevice.lbObjects`
- exported MCE evidence and Java `MceExport` output
- `CDraw*` class markers
- text/variable anchors from objects created by this tool
- known-coordinate GUI objects created in candidate copies

Rules:

- Do not patch `.MCE` private blobs.
- Do not commit raw private payloads or large binary evidence.
- Raw evidence may live under `.mcgsctl-runs` or `.codex_tmp` for local
  analysis, but commits should contain only source, tests, docs, schemas, and
  sanitized summaries.
- Do not infer a field from one sample. Use differential experiments.
- Gemini may review sanitized offset summaries, field hypotheses, and tiny hex
  windows, but must not receive `.env`, keys, full raw blobs, or full `.MCE`
  files.

Minimum differential experiment set:

1. Create candidate copies with one known object at a time.
2. Use objects this tool can create and read back, such as momentary buttons,
   status-buttons, synthetic labels, native static text, and native lamps when
   their GUI path is available.
3. Hold all fields constant except one variable per run:
   - `x`
   - `y`
   - `width`
   - `height`
   - `text`
   - `variable` or visibility expression
4. For every run, capture sanitized evidence:
   - project SHA and candidate path
   - object kind and known x/y/width/height
   - clipboard format names, IDs, sizes, and hashes
   - `MCGS_DRAW_OBJ` length/hash and small bounded hex windows around changed
     offsets
   - `WndUser.lbObjects` / `WndDevice.lbObjects` length/hash and small bounded
     hex windows around changed offsets
   - `CDraw*` marker offsets
   - text/variable anchor offsets
   - changed byte ranges between paired samples
5. Build a read-only differential analyzer that emits:
   - object class candidate
   - possible x/y/width/height fields
   - anchor text/variable evidence
   - confidence score
   - rejected hypotheses
   - enough evidence to reproduce the inference

Completion target for this stage:

- It is sufficient to decode geometry for objects created by `mcgsctl`; full
  universal MCGS object decoding is not required for the first planner.
- Once tool-created object geometry can be decoded, emit `canvas-objects.json`
  with reliable occupied rectangles.
- Use that object map to make `layout preview --placement internal-occupancy`
  return a real plan instead of `blocked`.
- Then run `window.layout.apply`, readback, `project.check`, `safety.verify`,
  `candidate validate`, build, tests, commit, and push.

Fallback rule:

- The tool may return `UNKNOWN` only after the differential reverse engineering
  stage has tried clipboard diffing, MCE blob diffing, Java export enhancement,
  class-marker anchors, text/variable anchors, little-endian coordinate searches,
  single-variable experiments, multi-sample statistics, and Gemini review.
- That `UNKNOWN` must be encoded as tool behavior with evidence, not as a chat
  excuse to stop early.

### Automatic Layout Planner

Do not advertise "automatic visual planning" until the tool can produce a
canvas object map. The planner should be:

```text
canvas object map -> occupied rectangles -> free-space planner -> layout plan -> preview overlay -> GUI apply -> readback
```

Rules:

- Internal evidence is the primary planner input: UIA/MSAA/native object model,
  clipboard object format, or high-confidence read-only MCE geometry inference.
- Screenshots are for audit evidence, before/after comparison, and Gemini/human
  review, not the primary source of object geometry.
- If the layout spec contains explicit coordinates, continue to support them
  and mark results as `placementSource=explicit`.
- If automatic placement uses internal occupancy data, mark results as
  `placementSource=internal-occupancy`.
- Automatic placement must emit `canvas-objects.json` and `layout-plan.json`.
- If no reliable object map exists, do not guess. Return `UNKNOWN` for automatic
  placement and continue to support explicit-coordinate drawing with readback.
- Prefer readable HMI layout over dense packing: preserve margins, avoid
  bottom-edge unstable zones, size labels from text length, avoid overlap with
  existing controls, and keep related controls grouped.

## First Supported Object Set

Start small. The first useful drawing layer should support:

- `section-title`: static text used as a readable area header.
- `static-label`: static explanatory text.
- `momentary-button`: standard button with press set-1 and release clear-0
  operation evidence.
- `status-button`: standard-button style indicator with visibility expression
  evidence.

Defer these until the small set is stable:

- native MCGS lamps
- complex vector graphics
- custom bitmap import
- automatic beautification
- freeform drawing tools
- multi-window layout refactors
- hardware-specific behavior approval

## Layout Spec Principles

The spec should be declarative and reviewable. Prefer JSON with explicit fields:

```json
{
  "version": 1,
  "windowIndex": 0,
  "canvas": { "width": 800, "height": 480 },
  "objects": [
    {
      "id": "jog_up",
      "kind": "momentary-button",
      "text": "上升点动",
      "variable": "HMI_UP",
      "x": 610,
      "y": 320,
      "width": 90,
      "height": 42
    }
  ]
}
```

Validation should reject:

- missing `id`, `kind`, `text`, coordinates, or required binding fields
- duplicate IDs
- object kinds not in the supported set
- coordinates outside the canvas
- zero or negative width/height
- obvious accidental overlap between critical buttons
- variables that are unsafe, malformed, or not backed by expected evidence

## Evidence Contract

Every mutating workflow must produce enough evidence that another run can review
what happened without trusting the chat:

- operation ID
- command arguments with sensitive script bodies redacted
- source path, candidate path, and before/after SHA256
- list of created or touched UI objects
- control evidence per object
- screenshots or captures before/after when GUI is involved
- dialog and popup JSONL
- reopen readback result
- final status: `PASS`, `FAIL`, or `UNKNOWN`

Use existing evidence conventions where possible:

- `workflow-results\<operationId>.json`
- `dialogs.jsonl`
- `popups.jsonl`
- `startup-dialogs.jsonl`
- `candidate-summary.json`
- `candidate-summary.md`

## Candidate Release Flow

After the last mutating workflow, run final validators when practical:

```powershell
tools\mcgsctl\mcgsctl.ps1 profile check --workdir .mcgsctl-work\layout-e2e --profile profiles\local-mcgs-7.7-smart200.json
tools\mcgsctl\mcgsctl.ps1 workflow run project.check --project .mcgsctl-work\layout-e2e\candidate.MCE --fail-on-warning
tools\mcgsctl\mcgsctl.ps1 workflow run safety.verify --project .mcgsctl-work\layout-e2e\candidate.MCE --spec <safety-spec.json> --evidence-dir .mcgsctl-work\layout-e2e --awl FG2HMI.awl
tools\mcgsctl\mcgsctl.ps1 candidate summarize --workdir .mcgsctl-work\layout-e2e
tools\mcgsctl\mcgsctl.ps1 candidate validate --workdir .mcgsctl-work\layout-e2e
```

If a validator cannot run, say exactly which one was skipped and why. Do not
describe the candidate as release-ready unless the release gates actually pass.

## Implementation Plan

Proceed in this order unless current code shows a better safe path:

1. Baseline: inspect current `tools/mcgsctl` code, command help, README, tests,
   and git diff. Build once before significant changes if feasible.
2. Offline schema: add or update a layout schema and typed model.
3. Offline validation: implement `layout validate` with JSON output and focused
   tests.
4. Offline preview: implement `layout preview` so the user can inspect proposed
   layout before MCGS writes.
5. GUI apply MVP: implement `window.layout.apply` by composing existing drawing
   workflows where possible. Do not bypass their safety/readback logic.
6. Readback: add `layout readback` only after the GUI MVP has deterministic
   object evidence.
7. Canvas inspection: implement read-only `canvas inspect` and probe UIA, MSAA,
   `WM_GETOBJECT`, `OBJID_NATIVEOM`, clipboard, and coordinate mapping.
8. Geometry reverse engineering: if surface probes return `UNKNOWN`, run
   differential experiments against `MCGS_DRAW_OBJ`, `WndUser.lbObjects`,
   `WndDevice.lbObjects`, `CDraw*` markers, text anchors, variable anchors, and
   known-coordinate tool-created objects until tool-created object geometry can
   be decoded or every file-safe path has concrete negative evidence.
9. Object map and planner: implement `canvas-objects.json`, occupied rectangles,
   `layout-plan.json`, preview overlay, and `placementSource` evidence.
10. Release integration: connect layout evidence to candidate summary and
   `safety.verify`.
11. Documentation: update README, operator runbook, examples, and schemas.
12. Publish check: run build, tests, and publish script when the implementation
   is ready for release packaging.

## Definition Of Done

The task is not done when code merely compiles. A useful completion must include:

- command help lists the new or changed commands
- schema and examples exist
- tests cover valid layout, invalid layout, duplicate IDs, bad coordinates, and
  blocked unsafe bindings
- preview output can be inspected without MCGS
- GUI apply writes only to a candidate copy
- GUI apply records before/after and readback evidence
- `canvas inspect` can report whether MCGS exposes objects through UIA, MSAA,
  native object model, clipboard, read-only MCE geometry, or none
- if those channels return `UNKNOWN`, the tool has run differential
  reverse-engineering experiments against `MCGS_DRAW_OBJ`, `WndUser.lbObjects`,
  `WndDevice.lbObjects`, `CDraw*` markers, text anchors, variable anchors, and
  known-coordinate tool-created objects
- automatic layout emits `canvas-objects.json` and `layout-plan.json` from
  decoded tool-created object geometry and internal occupancy evidence
- any remaining `UNKNOWN` is backed by reverse-engineering evidence, not just by
  surface probe failures
- candidate summary sees layout workflow results
- release gates block `FAIL` and `UNKNOWN`
- README/runbook explain the workflow and limitations
- final response reports exact verification results and skipped checks

## Suggested All-Night User Prompt

The user can paste this to start an unattended run:

```text
在 E:\myproject\FG2 里连续推进 mcgsctl，目标是完成 MCGS 组态软件命令行工具，并把 HMI 绘图能力做成安全的候选副本工作流。

先读取 AGENTS.md 和 tools/mcgsctl/MCGSCTL_LONGRUN_PROMPT.md。不要依赖聊天上下文记忆；如果上下文被压缩或线程恢复，就重新读这两个文件和 git status。

允许你读代码、改 tools/mcgsctl 下的源码/测试/文档/示例/schema，允许运行 dotnet build/test/publish，允许在 .mcgsctl-work 或 .codex_tmp 下生成候选、预览和证据。你也可以通过 E:\googlecli\bridge 里的本地 Vertex/OpenClaw Bridge 向 Gemini 3.1 Pro Preview 寻求 UI、图形、布局和 GUI 自动化建议，但 Gemini 只能作为顾问，最终必须以本地代码、截图、readback、project.check、safety.verify 和 candidate validate 为准。

这次我允许你进入无人值守 Git 更新模式：完成一个可验证里程碑后，创建或使用 codex/mcgsctl-gui-drawing 之类的任务分支，提交相关源码、测试、schema、文档和必要项目文件，并 push 到已配置的 Git 远端。如果 push 因权限、网络或远端配置失败，就保留本地 commit 并在最终总结里说明。不要提交 .env、API key、密钥、完整私有日志、.mcgsctl-work、.mcgsctl-runs、.codex_tmp、bin、obj、dist 或大型生成证据。

禁止直接修改 FG2_HMI.MCE 原件，禁止直接 patch .MCE 私有二进制 blob，禁止 destructive git 操作，禁止在桌面锁屏或 MCGS 被人操作时跑 GUI 自动化，禁止把 UNKNOWN 当作通过。

禁止把 .env、API key、密钥、完整私有日志或 .MCE 私有二进制 blob 发给 Gemini；只发送必要的 spec、截图、导出证据、错误日志片段和小段代码。

实现路线按最后聊天记录的新方向推进：不要继续靠截图猜坐标。先实现只读 `canvas inspect`，用 UIA/MSAA/WM_GETOBJECT/OBJID_NATIVEOM/clipboard/read-only MCE geometry 探测 MCGS 画布内部对象。表层通道返回 UNKNOWN 不是完成，而是进入 `MCGS_DRAW_OBJ` / `WndUser.lbObjects` / `WndDevice.lbObjects` 差分反解阶段：用单变量候选副本实验、clipboard diff、MCE blob diff、Java MceExport 增强、`CDraw*` 类标记、文本/变量 anchor、小端坐标搜索、多样本统计和 Gemini 复核，至少可靠读回本工具创建控件的 x/y/width/height。然后生成 `canvas-objects.json`、occupied rectangles、`layout-plan.json` 和 preview overlay；最后才让 `window.layout.apply` 根据 internal occupancy 做自动布局。显式坐标仍可支持，但必须标记 `placementSource=explicit`；自动规划必须标记 `placementSource=internal-occupancy`。没有完成差分反解前，不要把 UNKNOWN 当成阶段完成。

新的完成标准不是“诚实返回 UNKNOWN”。新的完成标准是：至少能解析本工具创建的 MCGS 控件几何，`canvas-objects.json` 有真实 occupied rectangles，`layout preview --placement internal-occupancy` 不再因为没有 object map blocked，自动布局生成 `layout-plan.json`，GUI apply + readback + validators 通过，然后 build/test、commit、push。

如果 GUI 条件不满足，不要停工；继续完成离线 schema、验证、预览、测试和文档。只有会影响正式 MCE、PLC 安全、真实硬件行为或不可逆图形写入时才停下来问我。

每完成一个阶段，检查 git diff 和最小相关验证。最后给我一份中文总结：改了哪些文件、实现了哪些命令、验证命令和结果、哪些 GUI/硬件检查没做、下一步最小风险路线。
```

## Final Reporting Contract

When finishing a run, answer in Chinese and include:

- changed files
- implemented commands
- build/test/publish results
- candidate/evidence paths if generated
- GUI checks actually performed
- explicit skipped checks and why
- risks that remain before touching the official `FG2_HMI.MCE`
