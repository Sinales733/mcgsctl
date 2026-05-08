# mcgsctl Drawing Capability Repair Prompt

This file is the durable contract for fixing the gap between tool-sweep closure
and actual user-facing drawing capability. Read it after `AGENTS.md`,
`MCGSCTL_LONGRUN_PROMPT.md`, `MCGSCTL_FULL_COVERAGE_PROMPT.md`, and
`MCGSCTL_UNATTENDED_RELAY_PROMPT.md` whenever:

- the task touches drawing capability, layout integration, or `layout.apply`;
- a closure report shows `closedLoopPass` for drawing tools;
- a function-catalog or tool-sweep report is being interpreted;
- context was compressed and drawing capability status must be reconstructed.

## 2026-05-07 Review Override

The external MCGS review PDF delivered on 2026-05-07 supersedes any optimistic
"ALL PHASES COMPLETED" wording below. Treat this file's old completion tables as
historical claims that must be re-audited through
`MCGSCTL_REVIEW_REPAIR_PLAN.md`.

Current rule: no drawing/control/edit tool may be called user-facing usable
unless the evidence proves the full chain for the intended target canvas:

1. correct named user window opened through the project tree or equivalent route;
2. animation canvas edit state verified by `canvas.verify-state` or equivalent;
3. screen/client/logical coordinate transform calibrated;
4. tool-selected state and exact gesture sequence recorded;
5. created object type, geometry, and MCE/export diff verified;
6. property dialog writes and readback verified;
7. save/reopen persistence verified;
8. collision/occlusion/z-order checked from internal canvas evidence;
9. `layout.apply` schema, dispatch, workflow result, readback, and validators
   prove L5.

`closedLoopPass`, `layoutIntegratedCount`, `drawableOnlyCount=0`, and ordinary
build/test PASS do not override this review. If any item above lacks evidence,
restore `nextProbe` and continue file-safe repair work.

## 2026-05-08 Follow-Up Override

Treat single-tool GUI workflow PASS as `L4-pass` or `L5-candidate`, not final
L5, until `layout.apply` proves coordinate calibration, internal occupancy,
layout readback, and validators. The current L5-candidate list includes
`rectangle`, `rounded-rectangle`, `arc`, `polyline`, `ellipse`, and
`saved-data-browser` unless newer evidence proves the full L5 chain.

`free-table` and `historical-table` are not layout-integrated while table object
selection/readback is unresolved. Their readback must classify the scope as
object property, table-cell popup, table workbench, user-window property, or
selection failure.

Any workflow that clamps coordinates must record requested/applied rectangles
and clamp reason. Any workflow using a canvas must record requested and selected
window identity plus canvas state evidence.

## The Problem This File Exists To Prevent

Previous Codex runs reported `closedLoopPass` for ~23 drawing-create tools and
declared `closureComplete=true` / `tool-sweep status=PASS`. The user could not
use any of those tools to create configured HMI elements through `layout.apply`
or CLI. The root cause:

- `closedLoopPass` for drawing tools only proved **L3 drawable**: the toolbar
  command can place an object blob on canvas.
- No workflow, property automation, or layout schema entry existed for these
  tools.
- `layout.apply` hard-gates on `GuiSupported`, which only allows 4 kinds.
- Reports were misread as "tool is usable" when they meant "tool can be invoked."
- `nextProbe` was cleared for L3-only tools, so the unattended loop stopped.

This must never happen again. Every prompt, report, and evidence interpretation
must distinguish the five capability levels defined below.

## Capability Level Taxonomy (L1–L5)

Every MCGS drawing/control tool has exactly one capability level. The level must
be recorded in closure records, function catalogs, and tool-sweep summaries.

| Level | Name | Definition | Evidence Required |
|-------|------|------------|-------------------|
| L1 | `discovered` | commandId or toolbar button found | tool-catalog entry exists |
| L2 | `invokable` | command can be sent without crash | tool-probe `invoked=true` |
| L3 | `drawable` | object appears in candidate MCE with CDraw* class + rect | `DrawingCreateClosurePass=true` |
| L4 | `configurable` | core properties set AND read back: text, variable, expression, color, range | property-dialog automation + reopen readback |
| L5 | `layoutIntegrated` | `layout.apply` can create, configure, save, reopen, readback, validate | workflow function + layout schema + readback test |

### Critical Interpretation Rules

- **L3 ≠ usable.** `closedLoopPass` + `DrawingCreateClosurePass=true` proves
  only that the toolbar command works. It does NOT prove the user can request a
  rectangle/curve/slider through JSON and get a configured, verified HMI
  element.
- **L4 requires property-dialog automation.** Opening the property dialog for
  the newly created object, setting fields (variable, expression, text, range,
  color, data source), saving, reopening, and reading back those fields.
- **L5 requires workflow + schema + readback.** A `workflow run window.<tool>.add`
  command exists, the layout schema accepts the kind, `layout.apply` dispatches
  it, and readback verifies the result.
- **`closedLoopPass` with `capabilityLevel=drawable` must NOT clear `nextProbe`**
  unless the tool is genuinely only a drawing primitive with no configurable
  properties (rare in MCGS).
- **`tool-sweep PASS` and `closureComplete=true` do NOT mean drawing capability
  is complete.** They mean probe accounting is complete. The function catalog
  must separately report `layoutIntegratedCount` vs `drawableOnlyCount`.

### What Each Evidence File Means

| Evidence | Proves | Does NOT Prove |
|----------|--------|----------------|
| `DrawingCreateClosurePass` | L3: object blob on canvas | L4: properties configured; L5: layout integration |
| `closedLoopPass` on drawing tool | L3: invocation→create→save→CDraw class | L4/L5: any property set or read back |
| `tool-sweep status=PASS` | All tools have probe accounting | Any tool is layout-integrated |
| `closureComplete=true` | No `notClosedLoop`/`needsProbe` remains | Drawing capability is functionally complete |
| `functionCount=263` | 263 catalog entries exist | 263 working user-facing functions |
| `workflowFunctionCount=13` | 13 reusable CLI workflows exist | — (this is the actual usable count) |
| `closureBackedFunctionCount=250` | 250 tool-level closure records | 250 usable drawing tools |

## Current L5 Tools (Layout-Integrated)

As of 2026-05-06 phase2f completion, **25 kinds** have full L5 layout
integration (workflow + property config + readback + layout.apply):

| Kind | Workflow | Layout Schema |
|------|----------|---------------|
| `momentary-button` | `window.button.add-momentary` | ✅ |
| `status-button` | `window.indicator.add` | ✅ |
| `native-static-text` | `window.static-text.add` | ✅ |
| `native-lamp` | `window.lamp.add-native` | ✅ |
| `line` | `window.line.add` | ✅ |
| `arc` | `window.arc.add` | ✅ |
| `rectangle` | `window.rectangle.add` | ✅ |
| `rounded-rectangle` | `window.rounded-rectangle.add` | ✅ |
| `ellipse` | `window.ellipse.add` | ✅ |
| `polyline` | `window.polyline.add` | ✅ |
| `input-box` | `window.input-box.add` | ✅ |
| `animation-button` | `window.animation-button.add` | ✅ |
| `combo-box` | `window.combo-box.add` | ✅ |
| `flow-block` | `window.flow-block.add` | ✅ |
| `percent-fill` | `window.percent-fill.add` | ✅ |
| `slider-input` | `window.slider-input.add` | ✅ |
| `knob-input` | `window.knob-input.add` | ✅ |
| `rotating-meter` | `window.rotating-meter.add` | ✅ |
| `realtime-curve` | `window.realtime-curve.add` | ✅ |
| `historical-curve` | `window.historical-curve.add` | ✅ |
| `plan-curve` | `window.plan-curve.add` | ✅ |
| `alarm-display` | `window.alarm-display.add` | ✅ |
| `free-table` | `window.free-table.add` | ✅ |
| `historical-table` | `window.historical-table.add` | ✅ |
| `saved-data-browser` | `window.saved-data-browser.add` | ✅ |

## L3 Tools That Needed Upgrade — ALL COMPLETED

As of phase2f, `drawableOnlyCount=0`. All 21 target drawing-create tools have
been upgraded from L3 drawable to L5 layoutIntegrated. No L3-only tools remain.

## Repair Work Order — ALL PHASES COMPLETED

### Phase 1: Fix Reporting — COMPLETED (commit f12e179)

### Phase 2+: L3→L5 Upgrade — COMPLETED (phase2f, 2026-05-06)

All 21 drawing-create tools have reached `layoutIntegrated`.
`drawableOnlyCount=0`, `layoutIntegratedCount=25`, `workflowFunctionCount=35`.
Evidence: `E:\myproject\FG2\.mcgsctl-runs\mcgs-tool-sweep-20260506-priorityd-phase2f`

The ordered list below is preserved for reference. All items are done:
reached L5 `layoutIntegrated` or has been classified as `blockedBySafety` /
`blockedNeedsHuman` with a concrete reason.

**Processing order** (work through each tool in this order, do not skip ahead
to report or stop after finishing one tool):

1. rectangle (Priority A)
2. line (Priority A)
3. ellipse (Priority A)
4. rounded-rect (Priority A)
5. arc (Priority A)
6. polyline (Priority A)
7. input-box (Priority B)
8. animation-button (Priority B)
9. combo-box (Priority B)
10. flow-block (Priority C)
11. percent-fill (Priority C)
12. slider-input (Priority C)
13. knob-input (Priority C)
14. rotating-meter (Priority C)
15. realtime-curve (Priority D)
16. historical-curve (Priority D)
17. plan-curve (Priority D)
18. alarm-display (Priority D)
19. free-table (Priority D)
20. historical-table (Priority D)
21. saved-data-browser (Priority D)

**Per-tool upgrade procedure** (apply to each tool above in sequence):

1. Implement `workflow run window.<tool>.add` in Program.cs.
2. Automate property dialog: set coordinates, line color, fill color, style,
   variable, expression, and tool-specific fields.
3. Save → reopen → readback property fields.
4. Add kind to `GuiSupported` set in Program.Layout.cs.
5. Add dispatch case in `BuildLayoutChildWorkflowArgs`.
6. Add layout-spec schema documentation.
7. Add tests.
8. Set `capabilityLevel=layoutIntegrated` in closure record.
9. Build, test, commit.
10. **Immediately proceed to the next tool. Do not stop, report, or
    ask the user.**

Commit/push after every 2–3 tools or when a coherent milestone is ready.
Commit/push is a checkpoint, never a reason to stop or write a final report.

### Phase boundary is NOT a stopping point

**CRITICAL ANTI-STOP RULE**: Completing one tool, one priority group, or one
"phase" is NOT a reason to stop, write a final-style report, or output a resume
packet. The only valid reasons to stop are listed in the Stop Conditions section
of `MCGSCTL_UNATTENDED_RELAY_PROMPT.md`. If the next tool in the list above
is file-safe and the desktop is visible, continue immediately.

If a specific tool is blocked (e.g., property dialog cannot be automated for
that tool), skip it with a `blockedBySafety` / `blockedNeedsHuman` record and
continue to the next tool. Do not stop because one tool is blocked.

## Rules For This Repair Work

- **Never clear `nextProbe` for an L3-only drawing tool.** `nextProbe` must
  describe the path to L4 (property automation) or L5 (layout integration).
- **Never report "drawing capability complete" based on tool-sweep counts.**
  Drawing capability is measured by how many kinds `layout.apply` can handle.
- **Never stop after completing one tool or one priority group.** Immediately
  continue to the next tool in the ordered list above.
- **Never write a final-style report while L3 drawable tools remain.** A
  report that says "Phase N complete, here is a resume packet" while L3 tools
  exist is a failure. The correct action is to continue working.
- **Always use candidate copies.** Never touch official `FG2_HMI.MCE`.
- **Always verify commandIds against local evidence.** This prompt's tables are
  a starting point, not ground truth. If `tool-catalog.json` or toolbar-probe
  evidence shows different commandIds, use the local evidence.
- **Consult Gemini for property-dialog layout and field naming** when automating
  new property dialogs. Use the AI-to-AI packet protocol.
- **After context compression, re-read this file.** The capability level
  taxonomy and the L3-vs-L5 distinction are the most compression-vulnerable
  rules. If you cannot remember whether `closedLoopPass` means "usable" or
  "drawable", re-read this file. Then check how many L3 drawable tools remain
  and continue the loop from the next one.

## Completion Criteria — ALL SATISFIED (2026-05-06)

- `capabilityLevel` field exists in every closure record. ✅
- No L3-only tool has empty `nextProbe`. ✅ (no L3 tools remain)
- `layout-spec.md` has a capability matrix. ✅
- Function catalog reports `layoutIntegratedCount` separately. ✅
- **Every Priority A tool** has reached L5. ✅
- **Every Priority B tool** has reached L5. ✅
- **Every Priority C tool** has reached L5. ✅
- **Every Priority D tool** has reached L5. ✅
- `layout.apply` can create geometry, controls, and displays. ✅
- Tests enforce the L3/L4/L5 distinction. ✅ (107 tests pass)
- Documentation warns `closedLoopPass` ≠ layout-integrated. ✅
- `drawableOnlyCount` = 0. ✅
