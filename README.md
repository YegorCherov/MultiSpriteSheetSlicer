# Slice Sprite Sheets

This Unity editor tool allows you to easily slice sprite sheets into individual sprites, with various options for controlling the slicing process and pivot positions. The tool supports both PNG and TGA sprite sheet formats.

## Demo

Here's a quick demo showcasing the tool in action:

![Slice Sprite Sheets Demo](Images/SlicerVideo.gif)

## Features

- **Modern API Slicing:** Uses Unity's modern, warnings-free `ISpriteEditorDataProvider` API.
- **Smart Grid Auto-Detection:** Automatically estimates grid dimensions based on mathematical transparency evaluation (extremely useful for complex sheets).
- **Task-Queue Management:** Select folders or files to load them into an editable queue.
- **Exclude/Discard Controls:** Toggle active items or discard specific sheets to save them for later slicing.
- **Lock Selection List:** Lock your current task list to prevent selection changes from clearing your customized settings.
- **Pivot Controls:** Set custom pivot positions or choose from preset pivot points.
- **Supports Folder Processing:** Process an entire folder or individual sprite sheet files.

## Prerequisites & Installation

This tool uses Unity's modern Sprite Editor API. To compile this script without warnings or errors, **the 2D Sprite package must be installed in your project.**

### 1. Install 2D Sprite Package
1. Open your Unity project.
2. In the top menu, go to **Window > Package Manager**.
3. In the Package Manager, change the view filter dropdown (top-left) to **Packages: Unity Registry**.
4. Type `2D Sprite` in the search bar.
5. Select **2D Sprite** from the list and click **Install** in the bottom-right.

### 2. Add the Script
1. Clone or download this repository.
2. The `SliceSpriteSheets.cs` script should be placed inside an `Editor` folder (or a folder named `MultiSpriteSheetSlicer` inside your `Assets` directory).

   ![Script Location](Images/Slicer2.png)

## Usage

1. Open the Slice Sprite Sheets window by navigating to `Tools` > `Slice Sprite Sheets` in the Unity editor menu.

    ![Open Project](Images/SlicerToolsImage.png)

2. Select the sprite sheet(s) you want to slice in your Project window. You can select folders or multiple individual texture assets.

3. Customize Slicing Options:
   - **Slice Mode**: Choose between slicing based on cell count or cell size.
   - **Lock Selection List**: Check this to freeze the list so you can edit individual configurations without clicking away and losing your queue.
   - **Auto-Detect**: Click this next to an item to scan for the ideal grid size (e.g., detecting `64x64` animations automatically).
   - **Ignore Empty Sprites**: Enabled by default; skips writing empty/fully transparent slices to your asset database.
   - **Pivot Preset**: Select from preset pivot positions or configure a custom pivot.

4. Click **Slice Only** for individual sheets, or **Slice All Enabled** to batch-process your entire queue.

## Contributing

Contributions are welcome! If you have any improvements, bug fixes, or additional features to suggest, please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
