# Repository Guidelines

## Project Structure & Module Organization

This is a Unity project using editor version `6000.2.7f2`. Keep gameplay code, scenes, prefabs, ScriptableObjects, textures, audio, and editor tools under `Assets/`. Main areas include `Assets/Character`, `Assets/Inventory`, `Assets/Items`, `Assets/Crafting`, `Assets/WorldGen`, `Assets/Entities`, `Assets/Animals`, `Assets/Scenes`, `Assets/Textures`, `Assets/Audio`, and `Assets/Editor`.

Unity package state lives in `Packages/manifest.json` and `Packages/packages-lock.json`. Project-wide Unity settings live in `ProjectSettings/`. Do not edit or commit generated folders such as `Library/`, `Temp/`, `Logs/`, `obj/`, or IDE-generated solution files.

## Build, Test, and Development Commands

Open the project with Unity Hub or Unity Editor `6000.2.7f2`, using the repository root as the project path.

Useful batch-mode examples:

```powershell
%UNITY_EDITOR% -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
%UNITY_EDITOR% -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
%UNITY_EDITOR% -batchmode -quit -projectPath . -buildWindows64Player Builds/Isometric.exe
```

Use Unity's Test Runner for local Edit Mode and Play Mode runs when iterating inside the editor.

## Coding Style & Naming Conventions

Use C# with Unity conventions: 4-space indentation, braces on new lines, `PascalCase` for classes, methods, properties, and ScriptableObject types, and `camelCase` for private fields and locals. Prefer `[SerializeField] private` fields over public fields unless Unity inspector access and external mutation are both required. Keep one primary `MonoBehaviour` or `ScriptableObject` per file, with the file name matching the type name. Preserve Unity `.meta` files whenever adding, moving, or deleting assets.

## Testing Guidelines

The Unity Test Framework is installed. Add new automated tests under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode` and name files like `InventoryModelTests.cs`. Current inventory console helpers in `Assets/Inventory` are useful for manual checks but should not replace automated tests for new logic. Cover gameplay services and data models in Edit Mode where possible; use Play Mode for scene, physics, UI, and input behavior.

## Commit & Pull Request Guidelines

Recent commit messages are short Italian summaries such as `Aggiunta regola di despawn attorno al player` and `Aggiustato bug del main menu...`. Keep commits focused and describe the visible change or bug fixed.

For pull requests, include a concise description, affected scenes or prefabs, test evidence, and screenshots or short clips for UI, animation, rendering, or gameplay changes. Link related issues or tasks when available.

## Agent-Specific Instructions

Avoid broad asset reorganization unless requested. Do not modify generated Unity folders. Before changing serialized assets, inspect related scene or prefab references and keep changes scoped.
