# RV-SceneSelectorTool

## Project Overview
The **RV-SceneSelectorTool** is a Unity Editor extension designed to simplify and enhance scene management, navigation, and testing. This powerful toolset enables developers to quickly manage scenes, view and organize dependencies, manage scene history, favorites, generate automated tests, and improve overall project organization and productivity.

---

## Installation
Follow these steps to install **RV-SceneSelectorTool**:

1. **Clone the Repository**:
   - To use the tool directly as a Unity package, use the `upm` branch:
   ```bash
   https://github.com/RomanVitolo/RV-SceneSelectorTool.git#upm
   ```

2. **Manual Installation**:
   - Clone the repository, specifically the `main` branch, to obtain the complete Unity project.
   ```bash
   git clone https://github.com/RomanVitolo/RV-SceneSelectorTool.git
   ```
   - Open the Unity project directly, or copy the package contents into your project's `Assets` folder.

3. **Modify & Customize**:
   - Feel free to adapt and modify the toolset code to better suit your needs or integrate it with your existing workflow.

4. **Access the Tools**:
   - Navigate to `RV - Template Tool > Scenes Tools` in Unity's top menu.

---

## Usage
After installation, access and utilize each tool from the Unity menu:

- **Quick Access Panel**: Open scenes quickly and manage scene favorites and recent history.
- **Scene Selector**: Comprehensive management and organization tool for scenes.
- **Scenes Tests Tool**: Run and generate tests for the current scene.

---

## Implemented Tools

### Quick Access Panel

![Quick Access Panel](QuickAccess%20Panel%20Toolpng.png)

| Feature                    | Description                                                                  |
|----------------------------|------------------------------------------------------------------------------|
| Favorites                  | Mark scenes for quick access.                                                 |
| Recent Scenes              | Tracks and quickly reopens recently visited scenes.                           |
| Build Settings Scenes      | View scenes currently set in the project's Build Settings.                    |
| Export/Import Scene List   | Export/import your scene configuration for collaboration or backup purposes. |
| Rename Scenes (All)        | Batch rename scenes directly from the editor interface.                       |
| Customize Visibility       | Choose which sections to display for personalized workflow.                   |

---

## Scene Selector

![Scene Selector](Scene%20Selector%20Tool%20Sections.png)

| Feature                    | Description                                                                  |
|----------------------------|------------------------------------------------------------------------------|
| Scene History & Favorites  | Easily manage and access recently or frequently used scenes.                  |
| Most Used Scenes           | Displays most frequently used scenes sorted by usage.                         |
| Scene Previews             | Capture and store custom thumbnails for quick reference.                      |
| Scene Notes & Tags         | Annotate scenes with notes and tags for easy categorization and search.       |
| Scenes in Build Settings   | Add, remove, enable, disable, reorder, and preview scenes directly.           |
| Scenes Grouped by Folders  | Organize and access scenes based on their folder location in the project.     |
| Hotkeys for Favorites      | Assign hotkeys for rapid scene loading directly from the editor.              |

**Dependencies Tab**:

| Dependency Type             | Description                                                                  |
|-----------------------------|------------------------------------------------------------------------------|
| Prefabs Used (Assets)        | Lists all prefab assets used in the current scene.                           |
| Scripts Referenced          | Lists scripts attached to scene objects, with direct selection and removal.    |
| Assets (Textures, Materials, Audio) | View asset dependencies used in the scene.                           |
| Prefab Instances           | Manage prefab instances (select, rename, break prefab links).                  |
| Materials                  | Adjust material properties and rename material assets directly from the tool. |

---

## Scenes Tests Tool

![Scenes Tests Tool](Scene%20Tests%20tool.png)

| Feature                    | Description                                                                  |
|----------------------------|------------------------------------------------------------------------------|
| Test Coverage              | Shows current scene test coverage status.                                     |
| Custom Test Generator      | Generate custom test scripts for targeted GameObjects.                        |
| Intelligent Test Generator | Automatically create tests based on detected scene components and methods.     |
| Load and Run Test Lists    | Execute tests from external lists or discovered in scenes via Category tags.   |
| Test Results               | View, filter, and manage test outcomes effectively (filter by status or name).|
| Export Results             | Export test results for reporting in JSON or TXT formats.                     |
| Scene View & Hierarchy Indicators | Visual feedback directly in Scene view based on test outcomes.         |

---

## Contributing
Contributions are encouraged! Please:
- Submit feature requests or bugs via GitHub issues.
- Follow clear coding conventions.
- Document any new features or fixes clearly.

---

## License
Licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

© 2025 Roman Vitolo

