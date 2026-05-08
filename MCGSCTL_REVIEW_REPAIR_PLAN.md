# mcgsctl Review-Driven Repair Plan

This file is the durable repair plan distilled from the external MCGS review
PDF delivered on 2026-05-07. Read it after `AGENTS.md`,
`MCGSCTL_LONGRUN_PROMPT.md`, `MCGSCTL_FULL_COVERAGE_PROMPT.md`,
`MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md`, and
`MCGSCTL_UNATTENDED_RELAY_PROMPT.md` whenever work touches canvas opening,
drawing workflows, layout planning, property readback, tool closure, or MCGS
editor understanding.

## Review Verdict

The current mcgsctl implementation may contain many closure records and passing
tests, but that does not prove real MCGS drawing capability. The review found
five systemic risks:

1. Canvas identification is not proven. Finding the first canvas-like window is
   not the same as opening the intended user window in animation edit mode.
2. Drawing workflows are oversimplified. Many tools are modeled as
   commandId-plus-rectangle-drag even when MCGS requires tool-specific gestures,
   multi-click sequences, object selection, or secondary dialogs.
3. Property configuration is under-proven. Blob or geometry changes are not
   enough; tools must set properties, save, reopen, and read back fields.
4. Layout evidence is too weak when it falls back to screenshots or partial
   object maps. Occupancy must come from internal canvas evidence.
5. Closure vocabulary is ambiguous. `closedLoopPass`, `tool-sweep PASS`, and
   `layoutIntegratedCount` can describe probe accounting, not user-facing
   capability, unless the L4/L5 evidence chain is present.

Do not treat older text saying "ALL PHASES COMPLETED" as final. It is now a
historical checkpoint that must be re-audited against this plan.

## Required Root-Cause Hypotheses

Every repair run must consider and either prove, disprove, or reduce these
hypotheses with local evidence:

| ID | Hypothesis | Minimum local experiment |
|----|------------|--------------------------|
| H1 | Wrong canvas/window is opened | Open a named user window through the project tree, enter animation edit mode, and emit `canvas-state.json` with target name, handles, client rect, edit-state proof, and window ancestry. |
| H2 | Menu/toolbar/toolbox enumeration is incomplete | Compare menu, toolbar, toolbox, context-menu, project-tree, and property-dialog inventory with local help/manual/PDF-derived expectations; record missing or disabled entries with preconditions. |
| H3 | Coordinate transform is wrong | Draw reference objects at known logical coordinates and verify via internal object map after save/reopen; record DPI, client rect, scroll offsets, and border offsets. |
| H4 | MCE object parsing is insufficient | Cross-check MCE export, clipboard summaries, known-created objects, CDraw markers, text/variable anchors, and property readback. Unknown objects remain open with `nextProbe`. |
| H5 | Tool invocation only clicks commands | For each tool, record selected-state proof and the exact gesture sequence required by MCGS. Multi-point and dialog-driven tools need tool-specific workflows. |
| H6 | Property-dialog automation is insufficient | Enumerate tabs/fields, set one candidate-safe property at a time, save, reopen, and read back through UI/property/MCE evidence. |
| H7 | Closure status is too weak | Reclassify L3-only evidence as `drawable`; add `configuredPass` and `layoutIntegratedPass` or equivalent fields before claiming usability. |
| H8 | Tests validate fixtures more than real GUI behavior | Add negative tests and candidate GUI smoke evidence that fail when canvas state, property readback, or save/reopen proof is missing. |
| H9 | Layout uses an incomplete canvas model | Block automatic layout when internal object map/semantic/property/z-order evidence is incomplete; screenshots cannot decide placement. |
| H10 | Prompts encouraged queue-zero behavior | Keep `nextProbe` until L5 evidence exists; commit/push is a checkpoint, never completion. |

## Repair Phases

### Phase 0: Re-audit Current Claims

Before adding new drawing features, inspect the newest evidence and build a
fresh capability report:

- latest `tool-sweep.json`
- latest `tool-closure-records.json`
- latest `function-catalog.json`
- latest `layout-apply-result.json`
- latest `layout-readback.json`
- latest `canvas-objects.json`, `semantic-map.json`, and `property-map.json`

For each drawing/control/edit tool, emit:

- current claimed level
- evidence paths
- whether evidence proves L1, L2, L3, L4, or L5
- missing evidence
- next file-safe probe
- whether older status was a pseudo-closure

If evidence is absent, stale, fixture-only, screenshot-only, or lacks save/reopen
readback, downgrade the tool and keep the queue open.

### Phase 1: Correct Canvas And Coordinate Foundation

Implement or repair these command surfaces or equivalents:

- `user-window.open --name <windowName>`
- `canvas verify-state --project <candidate.MCE> --window-name <name> --out <dir>`
- `canvas coordinate-calibrate --project <candidate.MCE> --window-name <name> --out <dir>`

Required evidence:

- `canvas-state.json`: target user window name, project-tree route, main/window
  handles, canvas handle, edit mode proof, client rect, scroll/zoom/DPI/border
  data, toolbar/toolbox state, and screenshot audit path.
- `coordinate-calibration.json`: requested logical rects, actual decoded rects,
  error tolerance, transform formula, and save/reopen proof.

Completion standard:

- The tool can intentionally open a named target user window and reject a wrong
  window.
- The internal object map is available before automatic placement.
- `layout.apply --placement internal-occupancy` blocks on `UNKNOWN` internal
  canvas state instead of using screenshot fallback.

### Phase 2: Tool Gesture And Property Framework

Build a reusable workflow framework with per-tool adapters:

- select tool
- prove selected state
- perform exact gesture sequence
- locate created object internally
- open property dialog for that object
- set core fields
- save
- reopen
- read back properties
- diff MCE/export evidence
- verify no unintended collision or object loss

Do not reuse a simple rectangle drag for every tool. Polyline, arc, bitmap,
insert component, curves, alarm/table widgets, and edit tools need their own
gesture and dialog contracts.

Introduce machine-readable fields equivalent to:

- `configuredPass` for L4
- `layoutIntegratedPass` for L5
- `capabilityEvidencePaths`
- `missingL4Evidence`
- `missingL5Evidence`

### Phase 3: Layout Integration And Editing Tools

Only L5 tools may be used by automatic `layout.apply`. For each supported kind:

- schema accepts the kind and tool-specific properties
- workflow creates the exact MCGS object type
- property readback verifies key fields
- save/reopen verifies persistence
- internal map verifies rect, z-order/order, group, and no collision
- preview and final validators run

Add edit workflows for selection, bring-to-front/back, rotate, align, group,
ungroup, and symbol creation only after selection is proven. Edit closure must
prove the expected internal z-order/geometry/grouping change, not just a visible
screen change.

## Pseudo-Closure Rules

Mark evidence as invalid or incomplete when any of these are true:

- command ID discovered but no tool-selected proof
- tool clicked but no object type/geometry proof
- object blob appears but type is not verified
- geometry exists but properties are not set/read back
- properties are set but not verified after save/reopen
- `layout.apply` dispatches a kind but does not prove final candidate state
- screenshot is the only collision or readability evidence
- fixture test passes but real candidate GUI path is untested
- mcgsctl-created objects work but legacy project objects stay unknown
- `nextProbe` is empty before L5 evidence exists

## Verification Matrix

Every repaired workflow must select the smallest relevant subset of this matrix:

- correct canvas/window proof
- internal object map proof
- coordinate calibration proof
- command/tool selected-state proof
- tool-specific gesture proof
- object type and geometry proof
- property dialog set/readback proof
- save/reopen persistence proof
- MCE/export diff proof
- collision/occlusion proof from internal rectangles and z-order
- layout schema and `layout.apply` dispatch proof
- candidate summary/validate proof
- negative tests for missing canvas, wrong window, UNKNOWN map, and missing
  property readback
- screenshot/video audit only after internal evidence is collected

## Gemini Use

Use Gemini through the transcript API when it can accelerate UI interpretation,
tool gesture design, property-dialog field naming, or closure review. Send a
structured evidence packet. Do not send secrets, `.env`, full `.MCE` files, or
private full blobs. Gemini advice is not proof; accepted advice must become a
local probe, test, or evidence file before it changes capability status.

## Stop Rules

Do not stop because a report says:

- `ALL PHASES COMPLETED`
- `drawableOnlyCount=0`
- `tool-sweep status=PASS`
- `closureComplete=true`
- `layoutIntegratedCount=25`
- build/test passed
- commit/push succeeded

Stop only when the next required step needs official `FG2_HMI.MCE` apply, real
PLC/hardware behavior, secrets, destructive git, printing/running external side
effects, or another irreversible boundary. Otherwise continue to the next
file-safe probe.

## 2026-05-08 GPT Pro Follow-Up: L5-Candidate And Table Repair Rules

GPT Pro reviewed the next Codex checkpoint and agreed that the direction is
improving, especially around property entry, dialog cleanup, coordinate clamping,
and table readback branching. It also found new pseudo-closure risks. These
rules are now part of the durable repair contract.

### Do Not Promote L4-Pass To L5

Single-tool GUI workflow success is not L5. Evidence such as
`success=true`, `classDelta=true`, `saved=true`,
`propertyReadbackStatus=PASS`, `selectionVerified=true`, and `tabCount > 0`
can support L3 or L4. It is only an L5 candidate until the whole layout chain is
proven.

For the current checkpoint, treat these kinds as `L4-pass` or `L5-candidate`,
not final L5, unless a newer evidence bundle proves the full chain:

- `rectangle`
- `rounded-rectangle`
- `arc`
- `polyline`
- `ellipse`
- `saved-data-browser`

L5 requires all of:

- `layout.apply` dispatches the kind;
- placement uses `internal-occupancy`;
- coordinate calibration is PASS;
- internal object map is not `UNKNOWN`;
- no collision/occlusion is proven from internal rectangles and z-order;
- save/reopen persistence is proven;
- `layout-readback.json` matches the layout spec;
- candidate validators pass.

### Table Tools Are Blocked Until Scope Is Classified

`free-table` and `historical-table` must not be statically promoted to L5. They
are at most L3/L4-blocked until table-specific readback proves the correct
property scope.

Every table readback probe must classify the result as exactly one of:

- `objectPropertyOpened`
- `tableCellPopupOpened`
- `tableWorkbenchOpened`
- `userWindowPropertyOpened`
- `selectionFailed`

Do not let a table-cell context menu or table workbench appear as ordinary
object property readback PASS.

Required table selection strategy:

1. Select arrow tool.
2. Try table outer frame, corners, and borders before center points.
3. Prefer `WM_COMMAND 32785` only after object-level selection evidence exists.
4. Reject table-cell popup as `tableCellPopupOpened`, not generic failure.
5. If MCGS opens a table workbench, record `propertyScope=table-workbench`,
   `objectLevelPropertyDialog=false`, and `tableWorkbenchVerified=true`.
6. Promote only after proving that the workbench can read/write object-level
   table properties and save/reopen preserves them.

Table-specific workflows should be split from generic data-display workflows:

- `WorkflowFreeTableAdd`
- `WorkflowHistoricalTableAdd`
- `ReadbackTableObject`
- `ReadbackTableCell`
- `ReadbackTableWorkbench`

Minimum table L4 evidence:

- create the table object;
- select the object as a whole;
- enter object properties or verified table workbench;
- read at least one object-level property such as row count, column count,
  header, column width, or data binding;
- modify one safe property;
- save, reopen, and read back the same property.

Minimum table L5 evidence additionally requires `layout.apply` and
`layout-readback` PASS.

### FullCoverage Must Not Infer L5 From Static Workflow Names

`supportStatus=implemented`, a declared workflow name, or evidence text
containing `window.<kind>.add` or `window.layout.apply` must never directly
infer `layoutIntegrated`.

`InferMcgsToolCapabilityLevel`, `IsDrawingCreateLayoutIntegrated`, and any
equivalent capability logic must read real recent evidence before L5:

- latest workflow `result.json` has `success=true`;
- object creation has `classDelta=true`;
- property readback has `propertyReadbackStatus=PASS`;
- selection has `selectionVerified=true`;
- layout evidence has `layout-apply-result.json` PASS;
- layout readback has `layout-readback.json` PASS;
- candidate validation passes.

If latest GUI result or property readback for `free-table` or
`historical-table` is failure/blocker/table-cell/workbench-only, capability must
be downgraded or blocked regardless of static workflow lists.

Preferred durable capability file:

```json
{
  "kind": "free-table",
  "declaredWorkflow": true,
  "latestWorkflowPass": false,
  "latestPropertyReadbackPass": false,
  "latestLayoutApplyPass": false,
  "capabilityLevel": "L3",
  "blockedReason": "table-cell menu opens instead of object property dialog",
  "nextProbe": "table-object-frame-select"
}
```

### Clamp Must Be Evidenced

`ClampDrawRectToCanvas` or any equivalent coordinate clamp must not be silent.
Every drawing workflow result must include:

```json
{
  "requestedRect": { "x": 0, "y": 0, "width": 0, "height": 0 },
  "appliedRect": { "x": 0, "y": 0, "width": 0, "height": 0 },
  "clamped": false,
  "clampReason": ""
}
```

Direct workflow commands may clamp to keep GUI gestures file-safe, but
`layout.apply` should normally reject invalid or out-of-canvas rectangles during
layout validation instead of silently changing the requested layout.

### Canvas Opening Must Be Unified

Old `OpenAnimationConfiguration` paths that hard-code project tree heuristics
such as `FindListViewByItemCount(main, 3)` must be deleted, deprecated, or routed
through the same shared canvas-opening implementation as `Program.Canvas.cs`.

All `window.*.add` workflows must support `--window-name` as well as
`--window-index`, and every workflow result must record:

- `requestedWindowIndex`
- `requestedWindowName`
- `selectedWindowIndex`
- `selectedWindowTexts`
- `selectionSource`
- `canvasHwnd`
- `canvasRect`
- `canvasStateEvidence`

### Required Tests From The Follow-Up

Add or update tests for:

- table popup classifier: `表元` / add-row / delete-column / similar popup
  items must produce `tableCellPopupOpened`, not PASS;
- user-window property false positive: user window property dialogs must keep
  `selectionVerified=false`;
- MDI/table workbench false positive: workbench windows must use
  `propertyScope=table-workbench`, not ordinary object property PASS;
- FullCoverage downgrade: failed or blocked table evidence must prevent
  `layoutIntegrated`;
- L5 layout smoke: L4-pass/L5-candidate tools must run through
  `layout.apply`, coordinate calibration, internal occupancy, layout readback,
  and validators before final L5.

### Immediate Next Probe Order

1. Run the free-table manual property-readback probe, but record popup/dialog
   classification.
2. If `tableCellPopupOpened`, add table object frame selection and try edge,
   corner, and border points before center/double-click.
3. If `tableWorkbenchOpened`, write `table-workbench-readback.json`; do not
   mark ordinary property readback PASS.
4. If object-level property opens, implement free-table-specific readback.
5. Reuse the table-specific branch for `historical-table`.
6. Run `layout.apply` smoke for both table tools only after table readback works.
7. Then run L5 layout smoke for the remaining L5-candidate tools.
