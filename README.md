# Unity Essentials

This module is part of the Unity Essentials ecosystem and follows the same lightweight, editor-first approach.
Unity Essentials is a lightweight, modular set of editor utilities and helpers that streamline Unity development. It focuses on clean, dependency-free tools that work well together.

All utilities are under the `UnityEssentials` namespace.

```csharp
using UnityEssentials;
```

## Installation

Install the Unity Essentials entry package via Unity's Package Manager, then install modules from the Tools menu.

- Add the entry package (via Git URL)
    - Window → Package Manager
    - "+" → "Add package from git URL…"
    - Paste: `https://github.com/CanTalat-Yakan/UnityEssentials.git`

- Install or update Unity Essentials packages
    - Tools → Install & Update UnityEssentials
    - Install all or select individual modules; run again anytime to update

---

# Foldout Attribute

> Quick overview: Group serialized fields under collapsible foldouts in the Inspector. Start a group with `[Foldout("Group[/Sub]")]`, optionally add a background panel, nest groups via `Parent/Child`, and close levels with `[EndFoldout]`.

Organize inspectors without custom editors. Mark the first field of a group with `[Foldout]` and, when you’re done, place `[EndFoldout]` on the first field that should be outside the group. You can nest groups (e.g., `Settings/Advanced`) and optionally render a background panel around the foldout content.

![screenshot](Documentation/Screenshot.png)

## Features
- Simple grouping: add `[Foldout("Group")]` on the first field to start grouping subsequent fields
- Close groups explicitly with `[EndFoldout]` (you can place multiple to close multiple nested levels)
- Nested groups via path segments: `[Foldout("Parent/Child/Sub")]`
- Optional background panel: `[Foldout("Group", background: true)]`
- Respects property order; draws full properties (including children) within the group
- Per-object expand/collapse state cached while the object is selected
- Inspector-only; no runtime overhead

## Requirements
- Unity Editor 6000.0+ (Editor-only; attributes live in Runtime for convenience)
- Depends on the Unity Essentials Inspector Hooks module (integration and rendering pipeline)

Tip: If grouping doesn’t appear, ensure the Inspector Hooks package is installed and active.

## Usage
Basic group and close

```csharp
using UnityEngine;
using UnityEssentials;

public class PlayerConfig : MonoBehaviour
{
    [Foldout("Movement")]
    public float Speed;
    public float Acceleration;

    // Place EndFoldout on the first field that should be outside the group
    [EndFoldout]
    public float JumpHeight;
}
```

Nested groups

```csharp
public class AudioConfig : MonoBehaviour
{
    [Foldout("Audio/Volumes")]
    public float Master;
    public float Music;
    public float SFX;

    [Foldout("Audio/Advanced")]
    public bool Spatialize;
    public int MaxVoices;

    [EndFoldout] // closes "Audio/Advanced" only
    public bool MuteOnFocusLost;

    [EndFoldout] // closes "Audio" (the parent group)
    public bool DebugLogging;
}
```

Background panel

```csharp
public class UIConfig : MonoBehaviour
{
    [Foldout("HUD", background: true)]
    public bool ShowHealth;
    public bool ShowMinimap;

    [EndFoldout]
    public bool ShowFPS;
}
```

## How It Works
- The inspector pipeline (Inspector Hooks) scans serialized properties in order
- When it sees `[EndFoldout]`, it closes the current group level (one level per attribute occurrence)
- When it sees `[Foldout("A/B/C")]`, it creates or reuses nested groups A → B → C, then sets the current group to C
- Properties are appended to the current group, if any
- Each top-level group is drawn as a foldout; nested groups are drawn beneath their parent
- Background groups render a help-box style panel containing the foldout label and the group’s content
- Expanded state is cached per target instance and group path while selected in the inspector

## Notes and Limitations
- Attribute placement
  - Start a group by placing `[Foldout]` on the first grouped field
  - Close the current group by placing `[EndFoldout]` on the first field that should be outside the group
  - To close multiple nested levels, stack multiple `[EndFoldout]` attributes (or place them on consecutive fields)
- Reusing groups
  - Using the same segment name on a later field reuses that existing group level
  - The `background` flag applies to the last newly created level; if you reuse a level, its prior background setting remains
- Drawing behavior: Each grouped property is drawn including its children (arrays/structs)
- State persistence: Expansion state is cached for the active selection session; it’s not saved across editor sessions
- Multi-object editing: Groups render per-inspected object; actions apply to the current target

## Files in This Package
- `Runtime/FoldoutAttribute.cs` – `[Foldout]` and `[EndFoldout]` attribute markers
- `Editor/FoldoutEditor.cs` – Group detection, hierarchy, and rendering; integration with Inspector Hooks
- `Runtime/UnityEssentials.FoldoutAttribute.asmdef` – Runtime assembly definition
- `Editor/UnityEssentials.FoldoutAttribute.Editor.asmdef` – Editor assembly definition

## Tags
unity, unity-editor, attribute, propertydrawer, foldout, grouping, inspector, ui, tools, workflow
