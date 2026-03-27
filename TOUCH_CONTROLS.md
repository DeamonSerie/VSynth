# Touch Controls Documentation

This document explains how to use the touch-control YAML files in this repository when building an Android app (for example with Android Studio + Jetpack Compose).

## Files

- `touch-controls.yaml`: canonical definition of touch UI controls, gestures, editor fields, and behavior.
- `touch-controls-androidstudio-example.yaml`: reference binding map for wiring IDs/actions to Kotlin handlers.

## 1) `touch-controls.yaml` overview

### Top-level keys

- `version`: schema revision for compatibility checks.
- `profile`: unique profile name for this control layout.
- `meta`: app-level metadata.
- `layout`: canvas gestures and top bar controls.
- `block_editor`: controls shown when editing a note block.
- `block_gestures`: direct touch interactions on blocks.
- `accessibility`: baseline touch accessibility settings.

### Canvas gestures

The canvas gestures are declared under:

- `layout.canvas.gestures.tap` → add block.
- `layout.canvas.gestures.long_press` → open editor.
- `layout.canvas.gestures.pan` → scroll timeline.
- `layout.canvas.gestures.pinch` → zoom timeline.

### Transport and timing controls

`layout.top_bar.controls` contains stable IDs:

- `record`, `clear`, `save`, `load`, `export`, `play`, `stop`
- `bpm` stepper (`40..240`, step `1`)
- `snap` stepper (`5..200`, step `5`)

These IDs are intended to be referenced from your UI layer and tests.

### Block editor

`block_editor` is designed for a bottom sheet:

- `modifier`: segmented options (`Natural`, `#`, `##`, `b`, `bb`)
- `chord_type`: dropdown options
- `lyric`: text field
- `delete` action: destructive delete button

### Block gestures and snapping

- `tap` selects block
- `double_tap` opens editor
- `drag` moves block and references snap source (`snap_x_source: snap`, `snap_y: 60`)
- `swipe_left` deletes block

### Accessibility defaults

- `min_touch_target_dp: 48`
- haptic suggestions for add/delete/transport actions

## 2) Android Studio integration guide

### Parse YAML

At app start:

1. Read both YAML files from your app assets/raw resources.
2. Parse into typed model classes.
3. Validate `version` before using fields.

### Bind IDs to handlers

Use `touch-controls-androidstudio-example.yaml` as a direct mapping reference.

Examples:

- `record` → `onRecord()`
- `play` → `onPlay()`
- `add_block` → `onCanvasTap(x, y)`
- `move_block` → `onBlockDrag(blockId, x, y)`

### Apply validation rules

Use rules from `validation_rules` before committing state changes:

- `bpm in [40,240]`
- `snap in [5,200]`
- `block.y % 60 == 0`

## 3) Recommended implementation checklist

- Keep control IDs stable across releases.
- Treat unknown keys as forward-compatible extensions.
- Add analytics/testing tags using control IDs.
- If `version` changes, migrate with explicit schema adapters.

## 4) Suggested next step

If you want, I can also generate Kotlin data classes + a small parser adapter interface that maps these YAML files into strongly typed Compose state.
