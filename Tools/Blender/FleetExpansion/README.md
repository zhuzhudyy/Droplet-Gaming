# FusionFrigate expanded fleet authoring

`layout_config.json` is the single authoritative layout. It contains the fixed baseline count **40**, target count **120**, seed **908261**, all stable marker IDs and poses, six squadron assignments, measured spacing, and the mission-duration estimate. Ordinary export reads the stored markers and does not multiply the target count or regenerate placement.

The generated review file is `ArtSource/Blender/FleetExpansion/FleetLayout_Expanded.blend`: 120 Empty markers plus one root, organized into six collections. There are no ship meshes, duplicate high-detail previews, materials, textures, cameras or lights. Edit the JSON to change authoritative placement; the `.blend` is a disposable review/export result, not another independent layout source. The exporter nevertheless refuses to overwrite a changed `.blend` whose SHA-256 no longer matches its generation receipt.

The FBX is `ArtSource/Exports/FleetExpansion/FleetLayout_Expanded.fbx`. Export reuses the existing calibrated `build_fleet_assets.py` pose-proxy method because the installed Blender exporter does not bake Empty axes consistently. The original Empty objects are preserved; temporary four-vertex mesh proxies carry the exported poses. Unity's existing `FleetLayoutImporter` consumes only their world transforms and never instantiates proxy geometry. Import options must follow `docs/ASSET_PIPELINE.md`, including `globalScale=1`, `useFileScale=true`, `bakeAxisConversion=true`, no animation/colliders, and positive unit-scale gameplay roots.

## Explicit commands

Run from the project directory; none of these commands connects to a user's open Blender session or writes into Unity `Assets`.

```powershell
# Validate the stored authority poses, spacing and conservative rock separation.
python Tools/Blender/FleetExpansion/author_layout.py

# Only when intentionally rebuilding poses from edited formation parameters:
python Tools/Blender/FleetExpansion/author_layout.py --reflow

# Export the stored poses without altering them.
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/Blender/FleetExpansion/author_layout.py -- --export

# Independently reopen the source and round-trip the actual exported FBX.
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python Tools/Blender/FleetExpansion/inspect_layout.py

# Run two independent export + reopen/FBX rounds and compare semantic results.
python Tools/Blender/FleetExpansion/verify_repeatability.py
```

Each marker has `name` (the full `ShipTarget.targetId`, e.g. `SPAWN_Small_FF001`), short `id`, `group` (`Squadron_01`–`06`), zero-based `column`/`row`, Unity-space `position`, `forward`, `up`, unit `scale`, `yaw` and `pitch`. Export manifest/expected files duplicate these values as generated inspection receipts; they are not edited as authorities. Markers use the existing `Small` dispatch key to select the shared FusionFrigate gameplay prefab in this scene.

## Recorded design and checks

- Ship length **22.14 m**, based on measured final FusionFrigate LOD0 in the existing model report; Unity validates its own imported bounds separately.
- Same-column depth **110.7 m = 5 L**; lateral baseline **106.272 m = 4.8 L**; column height steps **55.35 m = 2.5 L**. Column depth staggering and small row offsets form three-dimensional groups. Each group's first column retains a straight five-target lane.
- Nearest-neighbour center distance: old median **51.778373718 m**, new minimum **110.7 m**, new median **110.888030310 m = 5.008492787 L**, ratio **2.141589670**. No gameplay-root or fleet-root scaling produces these distances.
- Old center range **335 × 135 × 420 m**; new center range **1424.351 × 437.692 × 1455.825 m**; new complete visual AABB **1431.731 × 443.903535 × 1477.965 m**.
- Minimum squadron visual-AABB clearance **216.987846 m = 9.800716 L**, above required **6 L = 132.84 m**. Pairwise ship AABB overlaps: **0**.
- Closest conservative ship/rock separation **404.760438 m**. Rock spheres use the maximum all-LOD full AABB diagonal times instance scale (deliberately larger than half-diagonal) plus the ship corner radius. This numerical test is not described as a Unity physics check.
- Fleet radius including visual hulls from solar local origin is at most **1519.787457 m**; adding the **250 m** turn allowance stays below the unchanged **4880 m** warning radius and **5840 m** boundary.
- First target remains **(0, 8, 100)** with +Z bow, player start **(0, 8, 0)**. No solar layout, ship model, movement tuning or existing scene is modified by authoring.
- The reproducible, deliberately non-optimal all-target snake route is **18377.248883 m**. At 60% route distance at 52 m/s and 40% at 156 m/s, effective speed is **70.909091 m/s**, requiring **259.166330 s**. Add **69 s** lane/group turns and **60 s** target alignment, multiply by **1.3**, and round **504.616230 s** upward to **540 s**. This is a documented design estimate, not a completed human playthrough.

Actual authoring evidence is in `docs/verification/FleetExpansion/layout-authoring-measurements.json`, `layout-source-roundtrip.json`, `layout-repeatability.json` and their logs. Blender **5.2.1 LTS / 9e2066aef7ef** executed these checks. Unity scene, combat, LOD/rendering, performance and user handling are verified and reported separately by the Unity integration.
