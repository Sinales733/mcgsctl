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

## Continuous Work Loop

In unattended mode, never treat a verified milestone as the end of the task.
Build/test/commit/push is only a checkpoint. After each checkpoint, inspect the
latest machine-readable evidence and immediately continue with the next
file-safe probe.

Use this loop:

1. Re-read `AGENTS.md`, `MCGSCTL_LONGRUN_PROMPT.md`, this file, `git status`,
   current diffs, and latest `.mcgsctl-runs` / `.mcgsctl-work` evidence.
2. Load the current queues:
   - `property-map.json` unresolved properties and `nextProbe` records
   - `tool-sweep.json` uninvoked or unresolved tools
   - `tool-catalog.json` tools without behavior evidence
   - `function-catalog.json` functions missing purpose/effect/readback evidence
   - build/test failures if any
3. Pick the highest-value next file-safe item.
4. Ask Gemini immediately if UI interpretation, field naming, visual grouping,
   or offset hypotheses would benefit from it.
5. Run the probe on a candidate/throwaway copy, or implement the missing probe.
6. Save structured evidence.
7. Run the smallest relevant build/test/check.
8. Commit/push a coherent milestone when allowed.
9. Return to step 1 without asking the user.

Stopping rules:

- `UNKNOWN` is a queue item, not completion.
- `unresolvedPropertyCount > 0` is a queue item, not completion.
- `tool-sweep invokedCount < toolCount` is a queue item, not completion.
- A Gemini failure is a local-evidence fallback, not completion.
- A successful push is a checkpoint, not completion.

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

If a tool is disabled or unavailable, it still needs an inventory record: where
it was found, why it was disabled if known, what precondition may enable it, and
what safe follow-up probe should be tried.

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
3. Screenshots for audit only, not as sole semantic proof.
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
- every tool has been probed or has a documented precondition/risk blocker
- every candidate-safe tool has a reversible invocation test
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
