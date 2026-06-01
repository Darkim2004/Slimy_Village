# Slimy Village

Slimy Village is a Unity 6 2D isometric game project focused on survival, crafting, exploration, combat, and village-style progression. The project includes gameplay systems for inventory, crafting stations, building placement, world generation, animals, enemy slimes, boss encounters, UI, audio, and saving.

## Requirements

- Unity Editor `6000.2.7f2`
- Unity Hub, or a direct Unity Editor install matching the project version
- Packages restored from `Packages/manifest.json`

Key Unity packages include URP, the Input System, Cinemachine, Aseprite support, UGUI, Timeline, Visual Scripting, and the Unity Test Framework.

## Getting Started

1. Clone the repository.
2. Open Unity Hub.
3. Add this repository root as an existing Unity project.
4. Open it with Unity Editor `6000.2.7f2`.
5. Let Unity restore packages and import assets.

Main scenes are in `Assets/Scenes`:

- `MainMenu.unity`
- `Game.unity`
- `BossBattle.unity`
- `SampleScene.unity`

## Project Structure

Most project content lives under `Assets/`.

- `Assets/Character` - player movement, combat, and character behavior
- `Assets/Inventory` - inventory data model, item stacks, hotbar, and inventory UI
- `Assets/Items` - item definitions, world drops, and placeable items
- `Assets/Crafting` - crafting recipes, stations, services, and output queues
- `Assets/WorldGen` - world generation and area spawning
- `Assets/Entities` - health, loot, spawn rules, and shared entity definitions
- `Assets/Animals` - passive animal AI and definitions
- `Assets/Slimes` - slime enemy definitions and AI
- `Assets/Scripts` - shared gameplay systems, UI, audio, saving, building, boss logic, and debugging
- `Assets/Scenes` - Unity scenes
- `Assets/Textures`, `Assets/Audio`, `Assets/Animations`, `Assets/Rendering` - content and presentation assets
- `Assets/Editor` - editor utilities

Unity package state is stored in `Packages/`. Project-wide Unity settings are stored in `ProjectSettings/`.

## Testing

The Unity Test Framework is installed. Prefer Edit Mode tests for gameplay services and data models, and Play Mode tests for scene, physics, UI, and input behavior.

Example batch-mode commands:

```powershell
%UNITY_EDITOR% -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
%UNITY_EDITOR% -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

## Building

Example Windows build command:

```powershell
%UNITY_EDITOR% -batchmode -quit -projectPath . -buildWindows64Player Builds/Isometric.exe
```

Build output should stay in ignored build folders such as `Build/` or `Builds/`.

## Development Notes

- Keep gameplay code, scenes, prefabs, ScriptableObjects, textures, audio, and editor tools under `Assets/`.
- Preserve Unity `.meta` files when adding, moving, or deleting assets.
- Do not commit generated Unity folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `Builds/`, or IDE-generated project files.
- Before changing serialized assets such as scenes or prefabs, inspect related references and keep the change scoped.
