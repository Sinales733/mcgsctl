# HMI Layout Spec

`mcgsctl layout` is the offline planning layer for drawing simple, readable MCGS HMI control areas.

It does not patch `.MCE` private blobs. `workflow run window.layout.apply` uses existing GUI workflows to create supported controls in a candidate copy.

## Commands

```powershell
tools\mcgsctl\mcgsctl.ps1 layout validate --layout layouts\ptz-basic.json --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 layout preview --layout layouts\ptz-basic.json --out .mcgsctl-runs\layout-preview --safety safety-spec.json
tools\mcgsctl\mcgsctl.ps1 workflow run window.layout.apply --source FG2_HMI.MCE --workdir .mcgsctl-work\layout-gui-smoke --layout layouts\ptz-basic.json --safety safety-spec.json
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

## Evidence

`layout preview` writes:

```text
preview.svg
preview.html
preview.json
validate.json
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
