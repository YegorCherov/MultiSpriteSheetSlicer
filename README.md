# Slice Sprite Sheets

Unity editor tool for slicing sprite sheets into individual sprites, with control over the slicing grid and pivot placement. Supports PNG and TGA source sheets.

## Demo

![Slice Sprite Sheets Demo](Images/SlicerVideo.gif)

## Features

- **Modern API slicing.** Built on Unity's `ISpriteEditorDataProvider` API, no deprecated calls, no compile warnings.
- **Grid auto-detection.** Estimates grid dimensions from transparency analysis of the sheet, useful for sheets where the cell size isn't known ahead of time.
- **Task queue.** Load folders or individual files into an editable queue rather than slicing one sheet at a time.
- **Exclude/discard controls.** Toggle items on or off, or discard a sheet entirely to slice later.
- **Selection lock.** Freeze the current queue so changing your selection in the Project window doesn't wipe your per-item settings.
- **Pivot controls.** Set a custom pivot per sheet or pick from presets.
- **Folder or file processing.** Point the tool at a whole folder or select individual sheets.

## Requirements

The tool relies on Unity's Sprite Editor API, which needs the 2D Sprite package installed, otherwise it won't compile.

**Install the 2D Sprite package**

1. Open your Unity project.
2. Go to **Window > Package Manager**.
3. Set the view filter (top-left dropdown) to **Packages: Unity Registry**.
4. Search for `2D Sprite`.
5. Select it and click **Install**.

**Add the script**

1. Clone or download this repository.
2. Place `SliceSpriteSheets.cs` inside an `Editor` folder, or a `MultiSpriteSheetSlicer` folder under `Assets`.

   ![Script Location](Images/Slicer2.png)

## Usage

1. Open the tool via `Tools > Slice Sprite Sheets` in the Unity menu.

   ![Open Project](Images/SlicerToolsImage.png)

2. Select one or more sprite sheets in the Project window, folders and individual textures both work.

3. Set slicing options per item:

   | Option | Description |
   |---|---|
   | Slice Mode | Slice by cell count or by cell size |
   | Lock Selection List | Freezes the queue so editing settings doesn't clear it when your selection changes |
   | Auto-Detect | Scans the sheet for its grid size, e.g. detects a 64x64 animation sheet automatically |
   | Ignore Empty Sprites | On by default, skips writing fully transparent slices to the asset database |
   | Pivot Preset | Preset pivot positions, or a custom pivot |

4. Click **Slice Only** to process a single sheet, or **Slice All Enabled** to batch the whole queue.

## Contributing

Issues and pull requests are welcome, bug fixes, features, whatever you've got.

## License

[MIT License](LICENSE).
