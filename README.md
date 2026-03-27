# VSynth

VSynth is a desktop vocal sequencing app inspired by Vocaloid-style workflows. It lets you record your voice, place/edit vocal blocks on a grid, play arrangements, save/load projects, and export to MP3.

---

## User Guide (GUI-Only)

This guide is written for end users who only have the application UI.

## 1) Main interface overview

When you open VSynth, you’ll see:

- **Top toolbar** with controls:
  - Record
  - Clear
  - Save
  - Load
  - Export MP3
  - Play
  - Stop
  - BPM input
  - Snap(px) input
- **Sequencer area**:
  - Left side has pitch rows labeled **G F E D C B A**
  - Right side is the timeline/canvas where note blocks are placed
- **Status bar** at the bottom for feedback messages

## 2) Step one: record your voice bank

Before arranging music, record your source samples.

1. Click **Record**.
2. In the recording window, click **Start Recording**.
3. Say the prompted sentence clearly:
   - **THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG**
4. Click **Stop & Process**.
5. Wait for processing to finish, then the dialog closes.

### Recording tips

- Speak clearly and consistently.
- Add small pauses between sounds/words.
- Re-record if playback sounds unclear.

## 3) Build your arrangement

## Add blocks

- Left-click anywhere on the sequencer canvas to add a block.
- Blocks snap to timing and row grid automatically.

## Move blocks

- Click and drag blocks to move them.
- Releasing snaps them back to grid.

## Remove blocks

- Right-click a block to delete it.

## Clear all blocks

- Click **Clear** to wipe the current sequence.

## 4) Edit each block

Each block has three editable settings:

1. **Modifier** (accidental):
   - Natural, #, ##, b, bb
2. **Chord Type**:
   - Major, Minor, Diminished, Augmented, 7th, Major7
3. **Voice Text**:
   - Enter letters/words to be sung by the engine

## 5) Playback controls

- Click **Play** to run the arrangement.
- Click **Stop** to stop playback and reset playhead.
- A vertical playhead line appears while playing.

If no blocks are present, playback will not produce a sequence.

## 6) Timing and quantization

- **BPM**: controls arrangement timing speed.
- **Snap(px)**: controls horizontal grid snap size (timing precision for block placement).

Smaller snap values allow finer timing placement.

## 7) Save and load projects

## Save

- Click **Save**.
- VSynth stores your arrangement as:
  - `Desktop/VSynthProject.json`

## Load

- Click **Load** to restore from that same file.
- If file is missing/corrupted, the status bar will show an error.

## 8) Export audio

- Click **Export MP3**.
- VSynth renders and writes:
  - `Desktop/VSynthTrack.mp3`

## 9) Recommended workflow

1. Record voice bank
2. Add and position blocks
3. Edit modifier/chord/text per block
4. Adjust BPM and Snap(px)
5. Play and iterate
6. Save project
7. Export MP3

## 10) Troubleshooting

## No sound

- Make sure you recorded a voice bank first.
- Ensure block text contains letters.
- Ensure there are blocks on the timeline.

## Playback sounds messy

- Shorten block text.
- Use simpler chord types (Major/Minor).
- Lower BPM and retest.

## Load fails

- Confirm `VSynthProject.json` exists on Desktop.
- Save a fresh project, then try loading again.

## Export fails or empty result

- Ensure arrangement contains blocks with valid text.
- Re-record if sample capture was poor.

---

## Developer note

The current README focuses on GUI usage so non-technical users can operate the app without inspecting source files.


## Android touch control YAML presets

For building an Android APK in Android Studio, this repo now includes touch-control YAML presets:

- `touch-controls.yaml` — canonical touch layout, gestures, and block editor controls.
- `touch-controls-androidstudio-example.yaml` — integration mapping example for Jetpack Compose handlers.

Full integration documentation is available in `TOUCH_CONTROLS.md`.

These files are intended as source-of-truth config assets you can read from your Android app layer.
