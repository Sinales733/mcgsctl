# HMI Layout Spec

`mcgsctl layout` is the offline planning layer for drawing simple, readable MCGS HMI control areas.

It does not patch `.MCE` private blobs. `workflow run window.layout.apply` uses existing GUI workflows to create supported controls in a candidate copy.

## Commands

```powershell
tools\mcgsctl\mcgsctl.ps1 layout validate --layout layouts\ptz-basic.json --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 layout preview --layout layouts\ptz-basic.json --out .mcgsctl-runs\layout-preview --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 canvas verify-state --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-verify-state --window-index 0
tools\mcgsctl\mcgsctl.ps1 canvas coordinate-calibrate --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-coordinate-calibration --window-index 0
tools\mcgsctl\mcgsctl.ps1 canvas inspect --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-inspect
tools\mcgsctl\mcgsctl.ps1 workflow run window.layout.apply --source FG2_HMI.MCE --workdir .mcgsctl-work\layout-gui-smoke --layout layouts\ptz-basic.json --safety safety-spec.json --canvas-objects .mcgsctl-runs\canvas-semantic-map\canvas-objects.json --coordinate-calibration .mcgsctl-runs\canvas-coordinate-calibration\coordinate-calibration.json
tools\mcgsctl\mcgsctl.ps1 layout readback --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --layout layouts\ptz-basic.json --out .mcgsctl-runs\layout-readback
```

`--spec` is accepted as an alias for `--layout` on layout commands for compatibility with older draft prompts.

## Supported Object Kinds

GUI-supported set:

```text
momentary-button
status-button
native-static-text
native-lamp
rectangle
line
ellipse
rounded-rectangle
arc
polyline
bitmap
input-box
animation-button
combo-box
flow-block
percent-fill
slider-input
knob-input
rotating-meter
realtime-curve
historical-curve
plan-curve
alarm-display
free-table
historical-table
saved-data-browser
section-title   -> native-static-text by default
static-label    -> native-static-text by default
```

`section-title` and `static-label` default to native MCGS static text. The workflow inserts the native label tool, writes the text in the label property dialog, saves, reopens, and verifies the text through property readback.

The older compatibility fallback remains explicit:

```text
{ "kind": "static-label", "renderAs": "status-button", ... }
```

This fallback renders the label as a standard-button status object with a constant visibility expression of `1`; it is useful only when the native label dialog is unavailable in a profile.

Preview-only mode is still available:

```text
{ "kind": "static-label", "renderAs": "preview-only", ... }
```

Preview-only objects appear in `preview.svg` / `preview.html` and validation output, but are not MCGS readback evidence.

`status-button` remains a standard-button style object, not a native MCGS lamp. Use `native-lamp` when a native MCGS animation display component is required. Native lamp evidence proves GUI property readback only; hardware indication and field acceptance remain outside mcgsctl.

## Capability Level Matrix

Every MCGS drawing/control tool has a capability level (L1鈥揕5). Only L5
(`layoutIntegrated`) tools can be used in `layout.apply`. Tools at L3
(`drawable`) have been proven to place objects on canvas via toolbar commands,
but they lack property automation, workflow integration, and layout schema
support. See `MCGSCTL_DRAWING_CAPABILITY_REPAIR_PROMPT.md` for full details.

| Kind | Capability Level | `layout.apply` | Notes |
|------|-----------------|-----------------|-------|
| `momentary-button` | L5 layoutIntegrated | 鉁?| Full workflow + readback |
| `status-button` | L5 layoutIntegrated | 鉁?| Full workflow + readback |
| `native-static-text` | L5 layoutIntegrated | 鉁?| Full workflow + readback |
| `native-lamp` | L5 layoutIntegrated | 鉁?| Full workflow + readback |
| `section-title` | L5 layoutIntegrated | 鉁?| Maps to native-static-text |
| `static-label` | L5 layoutIntegrated | 鉁?| Maps to native-static-text |
| line | L5 layoutIntegrated | ✅ | window.line.add + property readback |
| rectangle | L5 layoutIntegrated | 鉁?| `window.rectangle.add` + property readback |
| ellipse | L5 layoutIntegrated | ✅ | `window.ellipse.add` + property readback |
| rounded-rectangle | L5 layoutIntegrated | ✅ | `window.rounded-rect.add` + property readback |
| arc | L5 layoutIntegrated | ✅ | `window.arc.add` + property readback |
| polyline | L5 layoutIntegrated | ✅ | `window.polyline.add` + property readback |
| bitmap | L5 layoutIntegrated | ✅ | `window.bitmap.add` + property readback |
| input-box | L5 layoutIntegrated | ✅ | `window.input-box.add` + property readback |
| animation-button | L5 layoutIntegrated | ✅ | `window.animation-button.add` + property readback |
| combo-box | L5 layoutIntegrated | ✅ | `window.combo-box.add` + property readback |
| flow-block | L5 layoutIntegrated | ✅ | `window.flow-block.add` + property readback |
| percent-fill | L5 layoutIntegrated | ✅ | `window.percent-fill.add` + property readback |
| slider-input | L5 layoutIntegrated | ✅ | `window.slider-input.add` + property readback |
| knob-input | L5 layoutIntegrated | ✅ | `window.knob-input.add` + property readback |
| rotating-meter | L5 layoutIntegrated | ✅ | `window.rotating-meter.add` + property readback |
| realtime-curve | L5 layoutIntegrated | ✅ | `window.realtime-curve.add` + property readback |
| historical-curve | L5 layoutIntegrated | ✅ | `window.historical-curve.add` + property readback |
| plan-curve | L5 layoutIntegrated | ✅ | `window.plan-curve.add` + property readback |
| alarm-display | L5 layoutIntegrated | ✅ | `window.alarm-display.add` + property readback |
| free-table | L5 layoutIntegrated | ✅ | `window.free-table.add` + property readback |
| historical-table | L5 layoutIntegrated | ✅ | `window.historical-table.add` + property readback |
| saved-data-browser | L5 layoutIntegrated | ✅ | `window.saved-data-browser.add` + property readback |

**Warning**: `closedLoopPass` in the tool-sweep for L3 tools means the toolbar
command works, NOT that the tool is usable through `layout.apply`. The gap
between L3 and L5 requires: property-dialog automation (L4), workflow
implementation, layout schema entry, and readback verification (L5).

## Minimal Example

```json
{
  "schemaVersion": 1,
  "windowIndex": 0,
  "canvas": { "width": 1024, "height": 768, "grid": 10 },
  "style": {
    "buttonWidth": 90,
    "buttonHeight": 50,
    "statusWidth": 110,
    "statusHeight": 32
  },
  "sections": [
    {
      "id": "ptz",
      "title": "PTZ Control",
      "x": 560,
      "y": 280,
      "width": 360,
      "height": 260,
      "titleRenderAs": "native-static-text",
      "layout": "direction-pad",
      "controls": [
        { "kind": "momentary-button", "id": "ptz-up", "text": "UP", "variable": "PTZ_CMD00", "position": "up" },
        { "kind": "momentary-button", "id": "ptz-down", "text": "DOWN", "variable": "PTZ_CMD01", "position": "down" },
        { "kind": "momentary-button", "id": "ptz-left", "text": "LEFT", "variable": "PTZ_CMD02", "position": "left" },
        { "kind": "momentary-button", "id": "ptz-right", "text": "RIGHT", "variable": "PTZ_CMD03", "position": "right" }
      ],
      "indicators": [
        { "kind": "native-lamp", "id": "ptz-ready", "text": "READY", "expression": "PTZ_READY" }
      ]
    }
  ]
}
```

If `--safety` is provided, every `momentary-button.variable` must exist in the safety spec `plc.addressPlan`, and direct mappings to dangerous `Q` outputs are blocked before any GUI write.

Section indicators are auto-planned in the upper safe area after the controls instead of pinned to the bottom of the section. MCGS animation windows are self-drawn and lower canvas coordinates are less reliable for property-page readback on the default editor profile. Explicit `x` / `y` on an indicator still overrides this planner.

## Internal Occupancy Placement

Screenshots are evidence only. They are not used as the primary source for automatic placement.

Automatic placement requires both:

1. `canvas verify-state` proving the target user window/canvas context.
2. `canvas coordinate-calibrate` status `PASS` plus a reliable internal canvas object map.

Without calibration evidence, `layout validate/preview/readback/workflow run window.layout.apply` with `--placement internal-occupancy` remains `UNKNOWN` by design.

Internal-occupancy placement example:

```powershell
tools\mcgsctl\mcgsctl.ps1 canvas mce-object-map-probe `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --out .mcgsctl-runs\canvas-mce-object-map `
  --row-key 2 `
  --canvas-width 800 `
  --canvas-height 480

tools\mcgsctl\mcgsctl.ps1 canvas semantic-map-probe `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --out .mcgsctl-runs\canvas-semantic-map `
  --workflow-results .mcgsctl-work\layout-gui-smoke\workflow-results `
  --row-key 2 `
  --canvas-width 800 `
  --canvas-height 480

tools\mcgsctl\mcgsctl.ps1 canvas property-map-probe `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --out .mcgsctl-runs\canvas-property-map `
  --semantic-map .mcgsctl-runs\canvas-semantic-map\semantic-map.json `
  --row-key 2 `
  --property-readback-dir .mcgsctl-runs

tools\mcgsctl\mcgsctl.ps1 canvas property-readback `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --semantic-map .mcgsctl-runs\canvas-semantic-map\semantic-map.json `
  --object-id <semantic-object-id> `
  --out .mcgsctl-runs\canvas-property-readback `
  --probe-font

tools\mcgsctl\mcgsctl.ps1 layout preview `
  --layout layouts\ptz-basic.json `
  --out .mcgsctl-runs\layout-preview-auto `
  --placement internal-occupancy `
  --canvas-objects .mcgsctl-runs\canvas-semantic-map\canvas-objects.json `
  --coordinate-calibration .mcgsctl-runs\canvas-coordinate-calibration\coordinate-calibration.json
```

`canvas inspect` remains available for UIA/MSAA/WM_GETOBJECT diagnostics. On current MCGS 7.7 profiles, the self-drawn animation canvas may expose no child geometry through those channels, so the preferred automatic-placement source is `canvas mce-object-map-probe`.

`canvas mce-object-map-probe` exports the candidate read-only, scans `WndUser.lbObjects` / `WndDevice.lbObjects`, and decodes high-confidence little-endian LTRB rectangles. It can match objects that mcgsctl created from workflow `createdUiObjects` evidence, and it can also emit generic occupied rectangles for existing controls. `--row-key` restricts inference to one exported window/device row so rectangles from multiple screens are not overlaid; `--canvas-width` / `--canvas-height` should match the visible editor viewport used for automatic placement.

`canvas semantic-map-probe` is the preferred source once a target row must be understood rather than only avoided geometrically. It consumes the same read-only MCE export and emits:

```text
semantic-map.json
semantic-map-probe.json
canvas-objects.json
mce-export/
```

Each semantic object includes:

```text
rect
semanticKind
displayedText
variable / expression
pressOperation / releaseOperation or otherOperations
scriptStatus / scriptSummary
confidence
evidenceSources
evidenceChain
```

The evidence chain may include MCE rect offsets, text/variable/expression anchors, workflow result paths, and property readback paths. For mcgsctl-created controls, workflow results and GUI readback are used to override ambiguous MCE payloads. For existing legacy controls, the probe uses control anchors plus decoded text/script anchors and returns `UNKNOWN` with `nextProbe` when an object cannot be resolved. A final semantic map must not silently leave `unknown-mce-object` records.

`canvas property-map-probe` is the full-coverage follow-up to semantic mapping. It writes:

```text
property-map.json
property-map-probe.json
semantic-map-source/       # only when the probe generated a semantic map itself
```

`canvas property-readback` is the GUI evidence feeder for `property-map-probe`.
It opens a temporary candidate copy, selects one semantic object, captures every
property-dialog tab/control, and writes `property-readback.json`,
`property-dialog.tree.txt`, and `property-dialog.png`. With `--probe-font`, it
opens the font subdialog, records `font-dialog.tree.txt` and `font-dialog.png`,
extracts font family/style/size, then cancels the subdialog without saving.
`property-map-probe --property-readback-dir <dir>` ingests those readbacks and
uses the newest verified evidence for each object ID. If a readback reports
`selectionVerified=false`, it is diagnostic evidence only and is ignored by
`property-map-probe` so a failed selection cannot populate another object's
property fields.

Every object record carries a `properties` object with stable keys for geometry, semantic kind, displayed text, variable/expression bindings, operations, script, font, alignment, colors, borders, visibility, input/display format, permissions, navigation, animation/alarm rules, grouping, and z-order. The value contract is deliberately strict:

```text
status=value          # value is supported by an evidence source
status=notApplicable  # the object kind makes the property irrelevant
status=unresolved     # the property is relevant or unknown and needs nextProbe
```

Unresolved properties keep the probe status `UNKNOWN`. This is intentional: property coverage is not complete until the missing fields are proved by property-dialog readback, one-property MCE/clipboard diff experiments, or another internal evidence source. Layout planning may use property maps for preserving known semantic groups, but it must not treat unresolved style/action fields as understood.

Full MCGS editor coverage uses a separate tool inventory layer:

```powershell
tools\mcgsctl\mcgsctl.ps1 mcgs tool-catalog `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --toolbar-probe .mcgsctl-runs\canvas-toolbar\toolbar-probe.json `
  --out .mcgsctl-runs\mcgs-tool-catalog

tools\mcgsctl\mcgsctl.ps1 mcgs tool-sweep `
  --project .mcgsctl-work\layout-gui-smoke\candidate.MCE `
  --tool-catalog .mcgsctl-runs\mcgs-tool-catalog\tool-catalog.json `
  --out .mcgsctl-runs\mcgs-tool-sweep
```

`mcgs tool-catalog` writes inventory-level `tool-catalog.json` and
`function-catalog.json`; those inventory records are not proof that every MCGS
command is understood. `mcgs tool-sweep` writes `tool-sweep.json` for stage-1
accounting, `tool-closure-records.json` for the usability gate, and a
closure-backed `function-catalog.json` that links each tool function to its
closure status, closure evidence, missing evidence, capability level, and next
probe. A tool is
understood only after its purpose, inputs, side effects, safety class,
invocation path, readback/evidence path, rollback path, and closure status are
recorded. `tool-sweep status=PASS` is therefore only a probe-accounting result;
`closureStatus=UNKNOWN` still means more file-safe closure work remains.

For drawing tools, also read:

- `drawableOnlyCount`: number of L3-only drawing tools.
- `layoutIntegratedCount`: number of L5 drawing tools usable by `layout.apply`.
- `workflowFunctionCount` vs `closureBackedFunctionCount`: workflow coverage and
  tool-closure coverage are different metrics and must not be mixed.

`capabilityLevel` values:

```text
discovered       command is known but not closed as usable
invokable        command/probe route works but drawing closure is incomplete
drawable         L3 only (toolbar draw proven)
configurable     L4 only (property readback proven)
layoutIntegrated L5 (workflow + schema + save/reopen/readback proven)
```

`closedLoopPass` on a drawing-create tool can still be `drawable` or
`configurable`. In those cases `nextProbe` must stay non-empty until
`capabilityLevel=layoutIntegrated`.

Closure statuses are intentionally stricter than probe statuses:

```text
closedLoopPass          candidate-safe tool proven on candidate/fixture with readback/persistence/rollback
readOnlyClosedLoopPass  read-only tool proven with before/after evidence and no unintended project mutation
notClosedLoop           clicked or probed, but evidence is insufficient for usability
needsProbe              safe next probe is known and must be run
blockedBySafety         next proof would cross hardware, print/run, formal apply, secret, or irreversible boundary
blockedNeedsHuman       a human-provided fixture/state/approval is required
invalidEvidence         existing evidence does not prove the claim
```

Read-only probes with candidate hash drift are accepted only when normalized-diff evidence proves the drift is `normalized-equivalent` or editor-context-only.

The older canvas diagnostics probe:

```text
UIA / CUIAutomation
MSAA / IAccessible
WM_GETOBJECT
OBJID_NATIVEOM
```

It writes:

```text
canvas-inspect.json
canvas-objects.json
main-window.png
window-tree.txt
```

If no internal channel exposes reliable child object rectangles, `canvas-objects.json` has:

```json
{
  "Status": "UNKNOWN",
  "ObjectProvider": "none",
  "ReliableGeometry": false
}
```

In that state, `layout preview --placement internal-occupancy` and `window.layout.apply --placement internal-occupancy` return blocked/UNKNOWN instead of silently falling back to screenshot guessing. Explicit coordinate layouts can still be applied, but their `placementSource` remains `layout` or `explicit`, not `internal-occupancy`.

When the map is reliable, `layout preview` writes:

```text
layout-plan.json
preview-overlay.svg
```

`layout-plan.json` records the occupied rectangles, chosen free rectangle, and offset applied to the planned objects. `preview-overlay.svg` draws occupied rectangles under the planned layout for human review.

Additional probes:

```powershell
tools\mcgsctl\mcgsctl.ps1 canvas context-menu-probe --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-context
tools\mcgsctl\mcgsctl.ps1 canvas clipboard-probe --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-clipboard --select-all
tools\mcgsctl\mcgsctl.ps1 canvas toolbar-probe --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-toolbar
tools\mcgsctl\mcgsctl.ps1 canvas mce-geometry-probe --project .mcgsctl-work\layout-gui-smoke\candidate.MCE --out .mcgsctl-runs\canvas-mce-geometry
tools\mcgsctl\mcgsctl.ps1 canvas mce-blob-diff-probe --before .mcgsctl-work\sample-a\candidate.MCE --after .mcgsctl-work\sample-b\candidate.MCE --out .mcgsctl-runs\canvas-mce-blob-diff
```

These commands are read-only with respect to the real candidate because they operate on temporary copies. Clipboard probing records format names, sizes, and hashes only; it does not store raw proprietary object payloads.

`toolbar-probe` records `ToolbarWindow32` command IDs for command-discovery evidence without clicking them. `mce-geometry-probe` exports the candidate read-only and scans likely HMI object blobs for class/string/geometry candidates. `mce-blob-diff-probe` compares paired candidate files and writes sanitized changed ranges plus bounded hex windows for decoder experiments; it does not store full raw blobs and never patches the project.

## Evidence

`layout preview` writes:

```text
preview.svg
preview.html
preview.json
validate.json
layout-plan.json        # only when internal occupancy placement is requested
preview-overlay.svg     # only when internal occupancy placement is requested
```

`window.layout.apply` writes:

```text
layout-preview\
layout-apply-result.json
objects\<id>\...
```

The actual candidate mutation evidence is still produced by the underlying GUI workflows under:

```text
<workDir>\workflow-results\
```

Run profile/project/safety final validators and `candidate summarize` / `candidate validate` after layout apply before considering a candidate apply-ready.
