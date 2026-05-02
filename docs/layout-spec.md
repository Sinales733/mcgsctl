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

First GUI-supported set:

```text
momentary-button
status-button
```

Preview-only kinds:

```text
section-title
static-label
```

The preview-only kinds appear in `preview.svg` / `preview.html` and validation output, but the current GUI apply implementation does not create native text objects for them. Do not treat preview-only labels as MCGS readback evidence.

`status-button` remains a standard-button style indicator, not a native MCGS lamp.

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
      "layout": "direction-pad",
      "controls": [
        { "kind": "momentary-button", "id": "ptz-up", "text": "UP", "variable": "PTZ_CMD00", "position": "up" },
        { "kind": "momentary-button", "id": "ptz-down", "text": "DOWN", "variable": "PTZ_CMD01", "position": "down" },
        { "kind": "momentary-button", "id": "ptz-left", "text": "LEFT", "variable": "PTZ_CMD02", "position": "left" },
        { "kind": "momentary-button", "id": "ptz-right", "text": "RIGHT", "variable": "PTZ_CMD03", "position": "right" }
      ],
      "indicators": [
        { "kind": "status-button", "id": "ptz-ready", "text": "READY", "expression": "PTZ_READY" }
      ]
    }
  ]
}
```

If `--safety` is provided, every `momentary-button.variable` must exist in the safety spec `plc.addressPlan`, and direct mappings to dangerous `Q` outputs are blocked before any GUI write.

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
