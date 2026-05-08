# mcgsctl Unattended Relay Prompt

This file is the durable continuation contract for long-running, checkpointed
MCGS/MCGSE closure work. Read it after `AGENTS.md`,
`MCGSCTL_LONGRUN_PROMPT.md`, `MCGSCTL_FULL_COVERAGE_PROMPT.md`,
`MCGSCTL_GEMINI_PROMPT.md`, and
`MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md`, plus
`MCGSCTL_REVIEW_REPAIR_PLAN.md`, whenever:

- the context was compressed;
- the thread was resumed after a long run;
- the previous response was a checkpoint or resume packet;
- the task says to continue unattended;
- the latest evidence still contains `UNKNOWN`, `notClosedLoop`,
  `needsProbe`, unresolved properties, or missing closure records.

Do not rely on chat memory for queue order or stopping conditions. Re-read this
file and the latest machine evidence every time.

The 2026-05-07 review plan supersedes old "ALL PHASES COMPLETED" drawing
reports. If a resume packet says all drawing tools are complete, re-audit it
against `MCGSCTL_REVIEW_REPAIR_PLAN.md` before trusting the claim.

## Current Checkpoint Anchor

The last known checkpoint reported on 2026-05-05 was not completion:

- local FG2 commits:
  - `72742ea Add MCGS tool closure records`
  - `aa2f270 Tighten MCGS tool closure queue`
- GitHub clone push:
  - branch: `codex/mcgsctl-gui-drawing`
  - latest pushed commit: `eace6dc Tighten MCGS tool closure queue`
- latest evidence at that checkpoint:
  - `E:\myproject\FG2\.mcgsctl-runs\mcgs-tool-sweep-20260505-closure5\tool-sweep.json`
  - `E:\myproject\FG2\.mcgsctl-runs\mcgs-tool-sweep-20260505-closure5\tool-closure-records.json`
- reported closure counts:
  - `toolCount=250`
  - legacy `status=PASS`
  - `closureStatus=UNKNOWN`
  - `closedLoopPass=8`
  - `readOnlyClosedLoopPass=71`
  - `notClosedLoop=150`
  - `blockedBySafety=19`
  - `blockedNeedsHuman=2`
  - `needsProbe=0`
- reported open queue:
  - `drawing-edit=52 notClosedLoop`
  - `selection=26 notClosedLoop`
  - `view-toggle=25 notClosedLoop`
  - `drawing-create=23 notClosedLoop`
  - `management=10 notClosedLoop`
  - `unknown=9 notClosedLoop`
  - `property-dialog=5 notClosedLoop`

This anchor may be stale. Always verify the latest evidence directories before
acting. If newer `tool-closure-records.json` or `tool-sweep.json` files exist,
prefer the newest verified evidence over this anchor.

## Required Startup Loop

At the beginning of every relay run:

1. Set the working directory to `E:\myproject\FG2`.
2. Read:
   - `AGENTS.md`
   - `tools/mcgsctl/MCGSCTL_LONGRUN_PROMPT.md`
   - `tools/mcgsctl/MCGSCTL_FULL_COVERAGE_PROMPT.md`
   - `tools/mcgsctl/MCGSCTL_GEMINI_PROMPT.md`
   - `tools/mcgsctl/MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md`
   - `tools/mcgsctl/MCGSCTL_REVIEW_REPAIR_PLAN.md`
   - this file
3. Run `git status --short`.
4. Locate the newest relevant evidence under `.mcgsctl-runs` and
   `.mcgsctl-work`.
5. Load the latest `tool-closure-records.json` and `tool-sweep.json`.
6. Recompute the closure counts.
7. Rebuild the queue from machine evidence, not from memory.
8. Select the next highest-priority file-safe item.
9. Start work immediately. Do not ask the user for ordinary prioritization.

If runtime or platform limits force a response, output a resume packet with the
latest evidence path and next concrete command. The next run must repeat this
startup loop.

## Queue Priority

Use this order unless the newest evidence proves a stronger dependency:

0. `capability-level-reporting` — **COMPLETED** (commit f12e179).
   - `capabilityLevel` field exists; `drawableOnlyCount`/`layoutIntegratedCount`
     in sweep summaries; tests pass. Do not repeat this work.
   - Proceed directly to item 0.5.
0.5. `review-repair-foundation`
   - Re-audit current "completed" drawing/tool claims against
     `MCGSCTL_REVIEW_REPAIR_PLAN.md`.
   - Implement or repair correct target user-window opening, canvas edit-state
     verification, and coordinate calibration before trusting layout or tool
     closure.
   - Any old `layoutIntegrated` claim without canvas-state, coordinate,
     property-dialog, save/reopen, and internal occupancy evidence stays open
     with `nextProbe`.
   - Apply the 2026-05-08 follow-up rules: L4-pass/L5-candidate tools are not
     final L5; table tools require explicit scope classification; FullCoverage
     cannot infer L5 from static workflow names; clamp and canvas-opening
     evidence must be recorded.
0.6. `table-scope-repair`
   - Process `free-table` before `historical-table`.
   - Classify property-readback as `objectPropertyOpened`,
     `tableCellPopupOpened`, `tableWorkbenchOpened`,
     `userWindowPropertyOpened`, or `selectionFailed`.
   - If table-cell popup opens, implement table object frame selection before
     retrying readback.
   - If table workbench opens, record `propertyScope=table-workbench`; do not
     mark ordinary object property PASS.
0.7. `fullcoverage-l5-downgrade`
   - Change capability inference so `supportStatus=implemented` and workflow
     names never directly produce `layoutIntegrated`.
   - Require recent workflow result, property readback, layout apply, layout
     readback, and candidate validation evidence for L5.
1. `drawing-create` (L3→L5 upgrade)
   - Prove that drawing tools create real MCGS objects, can configure
     properties, and can be driven by `layout.apply`.
   - This is the user's highest-priority capability.
   - `closedLoopPass` with `capabilityLevel=drawable` is NOT done.
     The tool must reach `capabilityLevel=layoutIntegrated`.
2. `selection`
   - Reliable selection is required before many `drawing-edit` tools can close.
3. `drawing-edit`
   - Rotation, z-order, grouping, alignment, distribution, and similar tools
     must be tested on known selected fixtures.
4. `property-dialog`
   - Enumerate tabs/fields, perform safe single-field diffs, and read back.
5. `view-toggle`
   - Close read-only/menu/toolbar/status UI toggle tools.
6. `management`
   - Close read-only parts first; classify unsafe mutation parts explicitly.
7. `unknown`
   - Reclassify into a real category, then close or block.

Capability level rule: when processing `drawing-create` queue items, the
closure is NOT complete at `closedLoopPass` if `capabilityLevel` is only
`drawable`. The next probe must target property-dialog automation (L4) and
then workflow/layout integration (L5). Only `capabilityLevel=layoutIntegrated`
terminates the drawing-create queue item.

Do not process the queue by broad catalog counts. Work from `closureStatus` and
category.

## First Concrete Probe For The Known Checkpoint

If the latest evidence still matches the 2026-05-05 checkpoint above, start with
the line drawing tool:

- `toolId=toolbar:9:1:32901`
- `context=animation-draw-object`
- category should be `drawing-create`

Do not copy placeholder coordinates. Derive every input locally:

- throwaway candidate path;
- baseline export directory;
- current internal canvas object map / semantic map / property map;
- safe `draw-x`;
- safe `draw-y`;
- `draw-width`;
- `draw-height`;
- output evidence directory.

The goal is not "click the line tool." The goal is a closure record proving that
the line tool can create a persistent, readable object without colliding with
existing canvas content.

## Drawing-Create Closure Template

For every drawing creation tool:

1. Create or locate a throwaway candidate under `.mcgsctl-work` or `.codex_tmp`.
2. Do not write to the official `FG2_HMI.MCE`.
3. Export or hash the baseline.
4. Read the internal canvas object distribution.
5. Use decoded rectangles, z-order/order evidence, semantic/property maps,
   grouping, and existing layout-plan data to compute a safe slot.
6. Select the drawing tool and create a minimal object.
7. Save the candidate.
8. Reopen the candidate.
9. Read back the new object from internal evidence.
10. Prove object type, geometry, and relevant properties.
11. Prove collision/occlusion status from internal evidence, not screenshots.
12. Use screenshots only as audit evidence for human-visible anomalies.
13. Export/diff and explain candidate changes.
14. Run relevant validators.
15. Update the closure record.
16. Move immediately to the next queued item.

If the tool lacks safe-slot support, implement it before claiming closure. Do
not use arbitrary explicit coordinates as proof of automatic layout.

## Selection Closure Template

For selection tools:

1. Create a fixture candidate with multiple known objects.
2. Record each object's internal ID, rectangle, z-order/order, semantic data,
   and property map.
3. Test single selection.
4. Test multi-selection or marquee selection when applicable.
5. Prove selected targets via property-dialog target, clipboard summary,
   selection handles, window state, or equivalent local evidence.
6. Verify no unintended project mutation when selection should be transient.
7. Update the closure record.

If selection is not reliable, do not claim downstream drawing-edit closure.

## Drawing-Edit Closure Template

For drawing-edit tools:

1. Use a known fixture candidate.
2. Record before internal canvas evidence.
3. Select known target objects through a verified or currently-tested selection
   path.
4. Invoke the edit tool.
5. Record after internal canvas evidence.
6. Prove the expected change:
   - rotation: geometry/orientation/bounding evidence changes correctly;
   - z-order: order/layer/hit-test order changes correctly;
   - group/ungroup: group or parent-child structure changes correctly;
   - align/distribute: coordinate relationships change correctly;
   - style/edit: the intended property changes correctly.
7. Save and reopen if the effect is persistent.
8. Read back after reopen.
9. Verify no unintended object loss, collision, or drift.
10. Update the closure record.

## Property-Dialog Closure Template

For property dialogs:

1. Enumerate every tab/page/control.
2. Record field name, control type, read/write status, and owning object type.
3. For candidate-safe fields, change one field at a time.
4. Save and reopen.
5. Read back the field.
6. Prove field meaning via property readback, MCE anchors, clipboard diff, or
   candidate diff.
7. Mark `notApplicable` only with evidence.
8. Record blockers for unsafe fields and continue other file-safe fields.

## View-Toggle Closure Template

For view, toolbar, status bar, and other read-only/toggle tools:

1. Record before UI/window-tree state.
2. Invoke the command.
3. Record after UI/window-tree state.
4. Prove the expected visible/toggle state.
5. Prove no unintended project mutation through hash or normalized diff.
6. Restore the original state when reversible.
7. Update the closure record as `readOnlyClosedLoopPass` or keep it open with a
   concrete next probe.

## Management Closure Template

For user/security/project/print/run/import/export/management tools:

1. Close read-only dialog discovery first.
2. Enumerate windows, tabs, fields, buttons, and safe actions.
3. If a safe isolated fixture exists, use it.
4. If direct proof would touch official project state, hardware, printing,
   running, secrets, or irreversible external side effects, mark the unsafe part
   as `blockedBySafety` or `blockedNeedsHuman`.
5. Include the exact reason, exhausted file-safe probes, and what human/safety
   precondition would allow continuation.
6. Continue all other file-safe items.

## Unknown Tool Closure Template

For unknown tools:

1. Use command ID, UI path, help/manual text, window behavior, normalized diffs,
   and Gemini review to classify the tool.
2. Do not leave a tool as `unknown` without a `nextProbe`.
3. Once classified, move it to the correct category queue.
4. If classification remains unclear, write the next specific file-safe probe:
   help lookup, dialog capture, candidate diff, window tree, clipboard probe, or
   Gemini packet.

## Gemini Relay Rules

Use Gemini when it can reduce uncertainty, but only through structured packets.

Bridge and viewer:

- bridge path: `E:\googlecli\bridge`
- viewer: `http://127.0.0.1:4000/codex-gemini`
- system prompt: `tools/mcgsctl/MCGSCTL_GEMINI_PROMPT.md`
- default model: `gemini-3.1-pro-preview`
- default `maxOutputTokens`: `65536`
- default `thinkingLevel`: `HIGH`
- default `includeFullContext`: `true`

Allowed Gemini request types:

- `closure_review`
- `probe_design`
- `failure_triage`
- `visual_review`
- `semantic_hypothesis`

Do not send:

- `.env`
- API keys
- credentials
- full `.MCE`
- full private blobs
- complete private logs

Gemini timeout is not a stopping condition. Record it and continue locally.
Gemini advice is not proof. Convert accepted advice into local code, probes,
diffs, readbacks, screenshots for audit, project checks, safety checks, or
candidate validation.

## Internal Layout And Occlusion Rule

Screenshots are audit evidence only. They may reveal obvious visual mistakes,
but they are not the primary proof of non-overlap, no occlusion, or good layout.

For drawing and layout closure, prove placement with internal evidence:

- decoded rectangles;
- `canvas-objects.json`;
- semantic map;
- property map;
- z-order/order evidence;
- grouping evidence;
- generated layout plan;
- post-apply readback;
- export/diff summaries.

If evidence only says "screenshot looks fine", mark it `invalidEvidence` or
`needsProbe` and collect internal canvas evidence.

## Verification Rhythm

Run verification proportional to the change:

- Code changes:
  `dotnet build .\tools\mcgsctl\McgsCtl.csproj`
- Full-coverage or closure logic changes:
  `dotnet test .\tools\mcgsctl\tests\McgsCtl.Tests\McgsCtl.Tests.csproj --filter FullyQualifiedName~FullCoverageTests`
- Larger milestones:
  `dotnet test .\tools\mcgsctl\tests\McgsCtl.Tests\McgsCtl.Tests.csproj`
- Push clone milestones:
  repeat build/test in `E:\myproject\FG2\.codex_tmp\mcgsctl-github-push`.
- Diff hygiene:
  `git diff --check` may show CRLF warnings but no real whitespace errors.

## Git And Dirty Worktree Rules

The FG2 delivery workspace may not have a usable remote. Use the existing
GitHub-backed clone when pushing:

`E:\myproject\FG2\.codex_tmp\mcgsctl-github-push`

Do not commit:

- `.env`
- API keys
- secrets
- `.mcgsctl-work`
- `.mcgsctl-runs`
- `.codex_tmp`
- `bin`
- `obj`
- `dist`
- large generated evidence
- complete private logs
- `FG2_HMI.MCE`

If `FG2_HMI.MCE` is dirty, treat it as user/MCGS editor state. Do not stage,
revert, or commit it.

## Continuous Execution Loop

Keep running this loop:

1. Re-read this file after context compression or checkpoint output.
2. Load the newest `tool-closure-records.json`.
3. Count closure statuses.
4. If any `notClosedLoop`, `needsProbe`, or `UNKNOWN` remains, pick the highest
   priority file-safe item.
5. If the item needs missing probe capability, implement the capability and
   tests first.
6. Run the probe on a candidate or throwaway project.
7. Interpret the evidence.
8. Update closure records.
9. Run relevant build/test.
10. Commit/push a coherent verified milestone when allowed.
11. Return to step 1 immediately.

Do not final after every small milestone. Do not final just because push
succeeded. Do not final just because the queue is large. The queue is the work.

**CRITICAL ANTI-PHASE-STOP RULE**: Completing one tool, one priority group,
one "phase," or one commit/push does NOT qualify as a stopping point.
A response that says "Phase N complete; here is a resume packet for Phase N+1"
while L3 drawable tools remain is a **failure**, not a checkpoint. The correct
action is to continue to the next tool in the
`MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md` ordered list without writing a
final report, without asking the user, and without outputting a resume packet.
The only valid reasons to stop are in the Stop Conditions section below.

## Stop Conditions

Stop only when:

- every drawing-create tool in the
  `MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md` ordered list has reached L5
  `layoutIntegrated` or is documented as `blockedBySafety`/`blockedNeedsHuman`;
  AND every other file-safe closure queue is also complete; or
- the next required action must touch official `FG2_HMI.MCE`; or
- the next required action must operate real PLC/hardware; or
- the next required action must print, run, or trigger irreversible external
  behavior; or
- the next required action needs secrets; or
- the next required action needs destructive git; or
- the next required action needs human-provided live state that cannot be
  inferred locally.

**Explicitly NOT stop conditions**:
- Completing one tool's L3→L5 upgrade.
- Completing one priority group (A, B, C, or D).
- A successful commit or push.
- A successful build or test run.
- Reaching the end of a "phase" described in any prompt.
- Running out of easy tools (hard tools are still work, not a reason to stop).

If the platform forces a final response, write a resume packet, not a completion
report. Include:

- newest evidence path;
- current closure counts;
- current `toolId`;
- newly closed items;
- remaining counts by status/category;
- next exact command or probe;
- what the last failure proved and did not prove;
- build/test/commit/push state;
- Gemini transcript session if any.
