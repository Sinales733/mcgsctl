# mcgsctl Full MCGS Coverage Prompt

This file is the durable contract for the next stage of `mcgsctl`: move beyond
layout, geometry, and semantic summaries into full MCGS software understanding,
full tool traversal, and full element-property extraction.

Read this file after `AGENTS.md` and `tools/mcgsctl/MCGSCTL_LONGRUN_PROMPT.md`
whenever the work touches MCGS tools, canvas elements, property dialogs,
automation coverage, or the question "does mcgsctl understand the whole MCGS
editor yet?"

## Mission

The new target is not "draw a few controls" and not "best-effort semantic
mapping." The target is to make `mcgsctl` understand and operate the local MCGS
embedded editor as a software system:

1. Understand what every discoverable MCGS function/tool does.
2. Be able to call every discoverable MCGS tool through a documented, reversible
   automation path.
3. Traverse and inspect every discoverable tool one by one.
4. Read every canvas element and every relevant property of that element.
5. Produce structured evidence that supports both property understanding and
   layout planning.
6. Use Gemini continuously as an auxiliary UI/visual/reverse-engineering
   collaborator whenever it can help, not only at fixed milestones.

This is a full-coverage objective. Do not treat a partial semantic map as done.
Do not treat "can avoid occupied rectangles" as done. Do not treat "can read
variable, press set-1, release clear-0, script empty" as complete property
coverage.

Do not claim proprietary MCGS/MCGSE source-code knowledge unless such source is
actually present locally. The required "bottom-layer understanding" is practical
and evidence-backed: read the `mcgsctl` source, MCGS local help/manual material,
window/control trees, menu and toolbar command IDs, property dialogs, exported
MCE evidence, clipboard summaries, and controlled candidate-copy diffs. Convert
that evidence into parsers, probes, workflows, readbacks, validators, and
documented limitations.

## Continuous Work Loop

In unattended mode, never treat a verified milestone as the end of the task.
Build/test/commit/push is only a checkpoint. After each checkpoint, inspect the
latest machine-readable evidence and immediately continue with the next
file-safe probe.

For unattended relay runs, also read
`tools/mcgsctl/MCGSCTL_UNATTENDED_RELAY_PROMPT.md`. It is the queue-specific
continuation contract used after context compression, checkpoint reports, or
platform-forced responses.

Use this loop:

1. Re-read `AGENTS.md`, `MCGSCTL_LONGRUN_PROMPT.md`, this file,
   `MCGSCTL_GEMINI_PROMPT.md`, `MCGSCTL_UNATTENDED_RELAY_PROMPT.md`,
   `MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md`,
   `MCGSCTL_REVIEW_REPAIR_PLAN.md`,
   `git status`, current diffs, and latest `.mcgsctl-runs` / `.mcgsctl-work`
   evidence.
2. Load the current queues:
   - `property-map.json` unresolved properties and `nextProbe` records
   - `tool-sweep.json` uninvoked or unresolved tools
   - `tool-catalog.json` tools without behavior evidence
   - `function-catalog.json` functions missing purpose/effect/readback evidence
   - build/test failures if any
3. Pick the highest-value next file-safe item.
4. Ask Gemini immediately if UI interpretation, field naming, visual grouping,
   closure review, failure triage, or offset hypotheses would benefit from it.
   Use the AI-to-AI packet protocol in `MCGSCTL_GEMINI_PROMPT.md`; do not send
   open-ended "what do you think" prompts.
5. Run the probe on a candidate/throwaway copy, or implement the missing probe.
6. Save structured evidence.
7. Run the smallest relevant build/test/check.
8. Commit/push a coherent milestone when allowed.
9. Return to step 1 without asking the user.

Stopping rules:

- `UNKNOWN` is a queue item, not completion.
- `unresolvedPropertyCount > 0` is a queue item, not completion.
- `tool-sweep invokedCount < toolCount` is a queue item, not completion.
- `tool-sweep status=PASS` is not completion unless every candidate-safe item
  has a closure record with `closedLoopPass` or `readOnlyClosedLoopPass`, and
  every unsafe item has a specific safety blocker.
- A tool/function being absent from this prompt's examples is not a reason to
  ignore it. Newly discovered functions are queue items until inventoried and
  closed-loop classified.
- A Gemini failure is a local-evidence fallback, not completion.
- A successful push is a checkpoint, not completion.
- `closedLoopPass` on a drawing-create tool with `capabilityLevel=drawable`
  means only L3 (can place object on canvas). It does NOT mean the tool is
  usable through `layout.apply`. The drawing-create queue item stays open
  until `capabilityLevel=layoutIntegrated`. See
  `tools/mcgsctl/MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md` for the full
  L1–L5 taxonomy and repair work order.

The 2026-05-07 external review found that older `layoutIntegrated` and
"ALL PHASES COMPLETED" reports can still be pseudo-closures if they do not
prove correct canvas state, coordinate calibration, tool-specific gestures,
property-dialog readback, save/reopen persistence, and internal layout
evidence. See `tools/mcgsctl/MCGSCTL_REVIEW_REPAIR_PLAN.md`; apply the stricter
rule when reports conflict.

The 2026-05-08 follow-up adds a hard FullCoverage rule: static implementation
metadata is not capability evidence. `supportStatus=implemented`, declared
workflow names, and evidence text containing `window.<kind>.add` or
`window.layout.apply` must not infer `layoutIntegrated`. L5 requires real recent
evidence: workflow result PASS, property readback PASS, layout apply PASS,
layout readback PASS, coordinate calibration PASS, and candidate validation
PASS. If a table tool has recent failure, table-cell popup, workbench-only, or
missing property-readback evidence, downgrade or block it even if a workflow name
exists.

Only stop when there are no remaining file-safe probes, or when the next action
requires human approval for official `FG2_HMI.MCE` apply, real hardware/PLC
behavior, secrets, destructive git, or another irreversible boundary. If a model
runtime or context limit forces a response, make the response a resume packet
with the exact next probe and evidence path, not a "done" report.

Latest known unfinished queues from the 2026-05-04 full-coverage scaffold:

- `property-map.json`: `status=UNKNOWN`, `objectCount=15`,
  `unresolvedPropertyCount=197`.
- `tool-catalog.json`: `status=PASS`, `toolCount=219`.
- `tool-sweep.json`: `status=UNKNOWN`, `invokedCount=0`.

Treat those as direct inputs for the next continuation: start reducing the
property unresolved queue and start invoking/probing the tool-sweep queue.

## Scope Of Whole Software

For this project, "whole software" means the local MCGS embedded editor version
available on this Windows machine and relevant to `FG2_HMI.MCE`, including:

- top-level menus
- toolbar buttons
- toolbox palettes
- canvas context menus
- project tree/context menus
- animation component tools
- property dialogs and all tabs/pages inside them
- realtime database editors
- device/channel editors
- user window/animation window editors
- script editor and operation/action dialogs
- strategy/function editors if reachable in this local profile
- preview/check/save/open/export/project commands
- help/manual content available locally, including extracted CHM/manual HTML
  under `.codex_tmp` when present

This list is deliberately open-ended. Prompt text, user examples, and existing
catalog schemas are starting points, not a whitelist. If exploration reveals a
new menu branch, hidden toolbar, dialog button, context-menu item, accelerator,
palette item, property page, help-documented command, or command ID with a
stable invocation route, add it to `tool-catalog.json`, `function-catalog.json`,
and the closure queue.

If a tool is disabled or unavailable, it still needs an inventory record: where
it was found, why it was disabled if known, what precondition may enable it, and
what safe follow-up probe should be tried.

## Closure Is The Usability Standard

For this project, "usable" means closed-loop, not merely discovered or clicked.
Every discoverable MCGS function/tool must eventually have a machine-readable
closure record. Use a stable schema or equivalent fields:

```json
{
  "toolId": "",
  "name": "",
  "uiPath": "",
  "source": "menu|toolbar|toolbox|context-menu|project-tree|dialog|accelerator|unknown",
  "category": "view-toggle|drawing-create|drawing-edit|selection|property-dialog|management|project-command|layout|unknown",
  "commandId": "",
  "preconditions": [],
  "candidateOrFixture": "",
  "beforeEvidence": [],
  "actionEvidence": [],
  "afterEvidence": [],
  "expectedEffect": "",
  "actualEffect": "",
  "persistenceEvidence": [],
  "readbackEvidence": [],
  "internalCanvasEvidence": [],
  "collisionAnalysis": {},
  "layoutPlanEvidence": [],
  "visualEvidence": [],
  "projectDiffEvidence": [],
  "sideEffects": [],
  "rollbackPath": "",
  "safetyClass": "read-only|candidate-safe-mutation|formal-apply-required|hardware-risk|external-side-effect|unknown-risk",
  "closureStatus": "closedLoopPass|readOnlyClosedLoopPass|notClosedLoop|needsProbe|blockedBySafety|blockedNeedsHuman|invalidEvidence",
  "missingEvidence": [],
  "nextProbe": "",
  "geminiReview": ""
}
```

Allowed closure statuses:

- `closedLoopPass`: mutation-capable tool is proven on a candidate or safe
  fixture, with expected effect, readback/persistence if applicable, side-effect
  accounting, rollback, and relevant validators.
- `readOnlyClosedLoopPass`: read-only or view/toggle tool is proven with
  before/after visible/window-tree evidence, no unintended project mutation, and
  a reversible restore path when the UI state changes.
- `notClosedLoop`: evidence is insufficient; do not call the tool usable.
- `needsProbe`: a safe next probe exists and must be run before stopping.
- `blockedBySafety`: the next direct proof would require official project
  writes, print/run/hardware behavior, secrets, destructive git, or another
  irreversible external side effect.
- `blockedNeedsHuman`: a human must provide approval, fixture setup, visible UI
  state, real-device readiness, or a screenshot/context that cannot be inferred.
- `invalidEvidence`: the evidence does not prove the claim and must be replaced.

Category-specific closure requirements:

- View/toggle/menu visibility tools: capture before/after UI state, command
  route, visible or window-tree proof, project hash or normalized diff showing no
  unintended mutation, and restore/re-toggle evidence when applicable.
- Drawing creation tools: create one minimal object per tool on a candidate
  canvas, save, reopen, locate the object, read geometry and semantic/property
  data, read the updated internal canvas object distribution, prove collision
  and occlusion status from object rectangles/z-order/grouping/property-map data,
  export/diff the candidate, and run relevant project/candidate validators.
  Screenshot evidence is useful for audit and human review, but it is not the
  primary occlusion proof.
- Drawing edit tools: start from known selected object(s), invoke the operation,
  prove the expected geometry/order/layer/group/style/property change, save and
  reopen when persistent, capture visual evidence, and provide rollback.
- Selection tools: prove deterministic selection of single and multi-object
  targets through window state, handles, property-dialog target, clipboard, or
  equivalent evidence. Selection must be reliable before edit tools can be
  closed-loop.
- Property/dialog tools: enumerate every tab/page/control, identify each field,
  change one safe property at a time where mutation is allowed, read it back
  after save/reopen, and mark non-applicable fields only with evidence.
- Management/project commands: separate read-only inspection from mutations.
  Permission, project, run, print, import/export, and similar tools require safe
  fixtures or explicit blockers; do not run irreversible or external actions on
  the official project.

Do not use any of these as a usability claim: command ID found, button enabled,
tool-sweep counted it, one click did not crash, screenshot changed, export diff
exists without interpretation, tests passed for unrelated code, or Gemini said a
route is plausible.

## Required Command Surface

The exact command names may evolve, but the tool must provide these capabilities
or documented equivalents:

```powershell
tools\mcgsctl\mcgsctl.ps1 mcgs inventory --project <candidate.MCE> --out <dir>
tools\mcgsctl\mcgsctl.ps1 mcgs tool-catalog --project <candidate.MCE> --out <dir>
tools\mcgsctl\mcgsctl.ps1 mcgs tool-probe --project <candidate.MCE> --tool-id <id> --out <dir>
tools\mcgsctl\mcgsctl.ps1 mcgs tool-sweep --project <candidate.MCE> --out <dir>
tools\mcgsctl\mcgsctl.ps1 canvas property-map-probe --project <candidate.MCE> --row-key <key> --out <dir>
tools\mcgsctl\mcgsctl.ps1 canvas property-readback --project <candidate.MCE> --object-id <id> --out <dir>
```

If the current implementation chooses different names, keep the same behavior
and document aliases in README/runbook.

## Tool Inventory Requirements

The inventory must cover every discoverable UI command source:

- menu text/path
- menu command ID
- toolbar index/button index
- toolbar command ID
- toolbox group/tool index
- context menu owner/path
- accelerator/shortcut if visible
- enabled/disabled state
- target window/dialog/context where it applies
- safe invocation method
- expected dialog/window/effect
- whether invocation mutates project state
- required candidate-copy guard
- discovered evidence path
- support status: `implemented`, `probed`, `blocked`, or `needs-precondition`

Do not stop after counting toolbars. Count plus enumerate plus test. A command
ID without behavior evidence is not full coverage.

## Tool Traversal Requirements

Each tool must be inspected one by one. For each tool, record:

- `toolId`
- stable display name/path
- source: menu/toolbar/toolbox/context/project-tree/dialog
- command ID or invocation route
- preconditions
- whether it opens a dialog, changes selection, changes current drawing tool,
  creates an object, edits a property, navigates a window, saves/exports/checks,
  or performs another action
- evidence before invocation
- invocation evidence
- evidence after invocation
- whether a candidate copy changed
- rollback/recovery method
- screenshots/window trees where useful
- MCE export/hash delta when a candidate changes
- status and next probe if still unresolved

Use candidate copies for mutation tests. For unknown or risky tools, first probe
on a throwaway candidate in `.codex_tmp` or `.mcgsctl-work`; never use the
official `FG2_HMI.MCE` as the direct mutation target.

## Function Understanding Requirements

For every MCGS function/tool, produce a `function-catalog.json` or equivalent
record with:

- human-readable purpose
- UI location(s)
- command/invocation route
- input fields/options
- output/effect
- side effects
- file/project tables or blobs affected, if discoverable
- related object types
- related property dialogs
- validation/readback method
- safety class: `read-only`, `candidate-safe-mutation`,
  `formal-apply-required`, `hardware-risk`, or `unknown-risk`
- confidence per field
- evidence source per field

"I think this is a button tool" is not enough. Tie each statement to local help,
UI observation, command response, candidate diff, property readback, or another
specific evidence source.

## Full Element Property Map

`canvas semantic-map-probe` is not the final target. The next target is a full
property map.

For every canvas element in the selected target window/row, emit a structured
record containing at least:

- object ID / stable locator
- row/window identity
- z-order or ordering evidence if recoverable
- geometry: x, y, width, height
- semantic kind / MCGS class candidate
- displayed text
- all variable bindings
- all expressions
- all operations/actions
- full script text if safely readable, otherwise a structured script summary
  with assignment targets, navigation targets, function calls, and conditions
- visibility/enable/display conditions
- font family, size, style, alignment
- foreground/background/border colors
- border/line/fill style
- data format/unit/precision/range for input or display objects
- permissions/security settings if present
- navigation target if present
- animation/state mapping if present
- alarm/color/state rules if present
- grouping/parenting/layer evidence if present
- source evidence per property
- confidence per property, not only per object
- `nextProbe` for any property not yet understood

The final target-scope property map must not hide missing properties. Unknown
properties keep the stage open unless they are truly not applicable to that
object type and that non-applicability is evidenced.

## Property Readback Strategy

Use multiple evidence channels and cross-check them:

1. Visible property dialogs and every tab/page inside them.
2. UIA/MSAA/window-tree extraction from dialogs.
3. Screenshots for audit only, not as sole semantic, layout, or occlusion proof.
4. Workflow result JSON for objects created by `mcgsctl`.
5. Reopen/readback evidence after saving candidate copies.
6. Read-only MCE export anchors: text, variable, expression, script, class, and
   rectangle offsets.
7. Clipboard `MCGS_DRAW_OBJ` summaries and bounded diff windows.
8. Differential experiments varying one property at a time.
9. Local MCGS manuals/help pages.
10. Gemini review of sanitized hypotheses and screenshots.

No single channel is enough for high-confidence coverage when another channel is
cheap to check. Prefer independent confirmation.

## Full Tool Sweep Strategy

Use a staged sweep instead of random clicking:

1. Inventory all menus, toolbars, palettes, context menus, project tree actions,
   and dialog tabs.
2. Normalize them into a stable `tool-catalog.json`.
3. Classify each tool by risk and precondition.
4. Probe read-only tools first.
5. Probe candidate-safe mutation tools on throwaway candidates.
6. For drawing/component tools, create one minimal object per tool when safe,
   save, reopen, export, and property-readback it.
7. For property dialogs, enumerate every tab and every control on each tab.
8. For each component/property, run at least one differential sample where a
   single field changes.
9. Add structured parser/readback support after the field is understood.
10. Re-run the sweep and keep iterating until no target-scope `unknown` remains.

Do not jump from "tool was clicked" to "tool is understood." A tool is
understood only after its purpose, inputs, outputs, side effects, and readback
method are documented with evidence.

After each sweep pass, split the next queue by `closureStatus`, not by whether
the command was invoked. Work first on candidate-safe `needsProbe` and
`notClosedLoop` items. Then work on read-only closure gaps. Leave
`blockedBySafety` and `blockedNeedsHuman` as explicit blocker records, but only
after all file-safe probes for that item have been exhausted.

Queue order for long relay work is defined in
`MCGSCTL_UNATTENDED_RELAY_PROMPT.md`. If context was compressed, re-read that
file before choosing the next item. Do not drift back to random clicking,
general summaries, or broad tool counts.

## Gemini Collaboration Rule

Gemini is available as a continuous collaborator. The agent may ask Gemini at
any time during:

- tool inventory design
- UI traversal strategy
- screenshot/window-tree interpretation
- property dialog field naming
- MCE/clipboard offset hypothesis review
- layout and visual grouping review
- failure-mode triage
- prioritizing the next probe when multiple safe paths exist

Do not wait for a fixed milestone before asking. If a question can be answered
faster or more reliably with Gemini's UI/visual reasoning, ask immediately with
sanitized evidence, then convert the advice into local probes/tests.

Before using Gemini, read `tools/mcgsctl/MCGSCTL_GEMINI_PROMPT.md`. Treat it as
the Gemini-side protocol. Codex-side requests must use structured packets, not
casual chat.

For visible Codex/Gemini collaboration, use the local viewer and transcript API:

- Viewer URL: `http://127.0.0.1:4000/codex-gemini`
- Bridge path: `E:\googlecli\bridge`
- Saved transcripts: `E:\googlecli\bridge\output\chat_sessions\`
- Session API: `POST /bridge/chat/sessions`
- Message API: `POST /bridge/chat/sessions/{sessionId}/messages`

The viewer is for observing saved transcripts; Codex still sends structured
packets through the API. Session list/read are read-only viewer paths; session
create/message send are mutating paths and need bridge auth when auth mode is
enabled. Use `MCGSCTL_GEMINI_PROMPT.md` as `systemPrompt`, and default to
`gemini-3.1-pro-preview`, `maxOutputTokens=65536`,
`thinkingLevel=HIGH`, and `includeFullContext=true` unless the probe needs a
smaller request. If auth is required, read `BRIDGE_API_KEY` from
`E:\googlecli\bridge\.env` only for the local Authorization header and never
print or store the key. Gemini advice must be converted into local probes,
readback, diff, screenshot, project-check, or candidate-validation evidence
before any closure claim.

If full-coverage work requires changing any durable prompt/rule file, Gemini
consultations about that prompt work must be sent through the transcript API so
the user can inspect the exchange later. If the current full-coverage task does
not require prompt/rule changes, do not touch prompt files merely to make the
viewer show activity.

Use exactly one of these request types for each Gemini consultation:

- `closure_review`: decide whether a tool/function/property is closed-loop
  usable.
- `probe_design`: design the next smallest file-safe probe.
- `failure_triage`: interpret a failed probe and choose the next branch.
- `visual_review`: review screenshot/window-tree evidence for visible anomalies,
  selection state, dialog fields, and visible tool state. Visual review may flag
  likely occlusion, but closure still needs internal canvas evidence.
- `semantic_hypothesis`: review sanitized MCE/clipboard/property/dialog evidence
  and suggest field meanings to verify locally.

Codex request packet shape:

```json
{
  "type": "closure_review",
  "taskId": "",
  "currentClaim": "",
  "goal": "",
  "toolOrFeature": {
    "name": "",
    "uiPath": "",
    "commandId": "",
    "category": "view|drawing|drawing-edit|property|management|semantic|layout|unknown"
  },
  "evidence": {
    "beforeState": "",
    "action": "",
    "afterState": "",
    "screenshots": [],
    "windowTreeSummary": "",
    "toolCatalogEntry": {},
    "propertyReadback": {},
    "semanticMap": {},
    "propertyMap": {},
    "mceOrClipboardDiffSummary": {},
    "savedAndReopened": null,
    "projectCheck": "",
    "safetyVerify": "",
    "candidateValidate": ""
  },
  "knownMissing": [],
  "allowedActions": [],
  "forbiddenActions": [],
  "question": "",
  "requiredOutput": [
    "decisionLabel",
    "proven",
    "missing",
    "recommendedProbe",
    "expectedEvidence",
    "failureMeaning",
    "safety"
  ]
}
```

Expected Gemini response packet:

```json
{
  "decisionLabel": "notClosedLoop",
  "conclusion": "",
  "proven": [],
  "missing": [],
  "recommendedProbe": {
    "action": "",
    "inputsNeeded": [],
    "expectedLocalCommandOrWorkflow": "",
    "expectedEvidence": "",
    "minimumPassCondition": ""
  },
  "failureMeaning": [],
  "safety": "candidate-safe|read-only|blocked-by-safety|needs-human|unknown-risk",
  "confidence": "low|medium|high",
  "doNotClaim": []
}
```

Only these `decisionLabel` values are valid:

- `closedLoopPass`
- `readOnlyClosedLoopPass`
- `notClosedLoop`
- `needsProbe`
- `blockedBySafety`
- `blockedNeedsHuman`
- `invalidEvidence`

Gemini output is never release evidence by itself. After each response, Codex
must convert accepted advice into a local probe, local code/test change, or an
explicitly rejected hypothesis. Do not record a tool as usable just because
Gemini says the route is plausible.

Safety rules for Gemini:

- Never send `.env`, API keys, credentials, private tokens, or secret logs.
- Never send full `.MCE` files or full raw private object blobs.
- Prefer screenshots, small code snippets, schema excerpts, bounded hex windows,
  hashes, command output summaries, and extracted help/manual snippets.
- Gemini is not the authority. Local evidence, build/test results, readback,
  candidate validation, and explicit user approval decide.

## Completion Standard

This full-coverage stage is not done until:

- every discoverable MCGS tool has an inventory record
- every newly discovered function/tool beyond the prompt examples has been added
  to the catalogs and closure queue
- every discoverable MCGS function/tool has a closure record
- every candidate-safe tool/function is `closedLoopPass` or
  `readOnlyClosedLoopPass`
- every blocked tool/function has `blockedBySafety` or `blockedNeedsHuman` with
  a concrete reason, exhausted file-safe probes, and the exact human/safety
  precondition needed to proceed
- every supported component type has at least one create/save/reopen/readback
  sample
- every canvas element in the selected target scope has a complete property map
- every property has a value, an evidenced non-applicable marker, or an active
  unresolved record with next probe
- no final target-scope `UNKNOWN` remains hidden in success output
- layout planning uses the property map to avoid collisions and preserve
  meaningful groups
- docs and schemas describe the tool catalog, function catalog, property map,
  and known blockers
- `dotnet build` and relevant tests pass
- GUI evidence is captured when visible desktop conditions allow it
- verified milestones are committed/pushed in unattended Git mode when allowed

If a true safety boundary prevents completion, do not mark the stage done.
Document the blocker, skip only the unsafe action, and continue all remaining
file-safe probes.
