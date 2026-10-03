# Cinematic fleet authoring — 2026-09-19

Open the integrated `Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity` after root integration. The retained ships are saved under `GeneratedFleet/Cubic_Layer_01` through `Cubic_Layer_04`; each depth plane has 500 targets. Use the Scene view's front, top, side and perspective views. The player starts outside the front at `(0,8,0)`, facing +Z; X=0/Y=8 is a real four-ship approach lane. Enter/Timeline completion and Tab skip use the existing `MissionController.StartCombat`, while R restarts directly into combat.

The count conflict was resolved explicitly: 20 × 25 × 2 is 1000, so the required 2000 independent identities use **20 × 25 × 4**. The front face is XY. Centre-to-centre spacings are 750/600/4800 Unity units; centre spans are **14250 × 14400 × 14400**, aspect ratio **1.010526**. The 100 metres/Unity-unit scale and 57.564-unit ship length remain unchanged. Formation centre is `(375,8,8200)`; existing 26000-unit escape radius and 32000-unit arena radius contain the complete formation with ample reserve.

All bows face the fixed combat-start droplet independently. The existing authored model has Blender +Y bow/+Z up and calibrated Unity +Z bow/+Y up; its main exhaust is at negative longitudinal position. No model, collider, prefab or fleet root is resized. Existing gameplay target IDs, mission array order, hull damage, delayed explosion and escape logic are preserved. No runtime formation Update exists: saved transforms enter the existing simulation's Configure cache and its ResetSimulation restores them. Fleeing ships therefore remain under the existing simulation's sole authority.

## Delivered files

- `Tools/Blender/CinematicFleet/author_layout.py`, `run.py`, `layout_config.json`, `README.md`: repeatable bpy authoring, guarded source saves, background process execution and layout validation.
- `ArtSource/Blender/CinematicFleet/FleetLayout_Cubic.blend`: editable 2000-instance preview sharing **one collection, 12 LOD2 meshes, 1396 prototype triangles**. No 2000-copy high-detail mesh expansion.
- `ArtSource/Exports/CinematicFleet/FleetLayout_Cubic.json`: byte-identical Unity-space pose export, preserving all old stable names/IDs.
- `ArtSource/Exports/CinematicFleet/review_{front,top,side,perspective}.png`: actual Blender workbench renders; perspective uses a perspective camera. These are layout reviews, not Unity screenshots.
- `Assets/_Project/Scripts/Editor/CinematicFleetLayout.cs`: explicit Undo-aware pose-only importer and independent actual-Unity-mesh audit. It only accepts the new scene, validates all IDs before mutation, never creates/deletes/renames ships, retains unrelated content and never saves scenes. Its caller owns final saving. New Flight/Scale assets isolate changed layout metadata from old scenes.
- `Assets/_Project/Tests/EditMode/CinematicFleetLayoutTests.cs`: four read-only tests for retained identities/volume, fixed bow directions, unsafe duplicate/reversed-bow rejection and authority/export parity.

## Executed Blender evidence

Real local Blender **5.2.1 LTS**, build `9e2066aef7ef` (2026-08-25); embedded Python **3.13.13**, run independently with `--background --factory-startup --python-exit-code 1`. No interactive Blender or Unity Editor session was opened/saved by the layout agent.

Generation, saved-file reopen verification, actual second generation/reopen, and four image renders all exited **0**. Evidence is `ArtSource/Exports/CinematicFleet/generation.json`, `verification.json`, `repeatability.json`, `generate.log`, `repeat-generation.log`, and `render.log`.

- Saved Blender instances/unique IDs: **2000/2000**; all 2000 old IDs retained.
- Saved instance position error: **0**; minimum actual Blender bow dot: **0.99999985**.
- Rotated full-hull conservative AABB pairs checked: **1,999,000**; intersections: **0**.
- Minimum rotated hull clearance: **540.190035 UU**; spawn clearance: **971.218 UU**.
- Repeated JSON export hash unchanged: `cc1f8812c48bcececb4c8e1f3e89c4a83e5847bcf595f9d427f59c6a074a91c4`.
- Original model `.blend`, model FBX and old authority JSON hashes unchanged; exact hashes are in generation receipt.
- Actual exported perspective/front/side images were inspected visually; the low-detail authored hulls show separate depth planes without changing hull size. Top image was generated, not claimed as individually inspected.

## Unity handoff and limits

Root must compile and invoke `CinematicFleetLayout.ApplyToScene(scene)` in the safe copy, save, repeat import, and execute `CinematicFleetLayoutTests` plus full-scene/runtime tests. The helper writes `ArtSource/Exports/CinematicFleet/unity-layout-audit.json` only after real Unity LOD0 bounds, bow/engine colliders, 18,000 query colliders, all-pairs clearance and membership checks pass. Do not treat Blender evidence as Unity/play-mode/player verification. The shared root acceptance report owns those results.

Read-only integration review confirmed normal/skipped narrative share the fixed spawn entry and cached poses. All DropletSettings references are set by the importer; root was advised to rebind the saved LaserBeamPool.scale and SolarSystemBackdrop.combatScale to the new identical physical-scale asset as well. This does not change sun placement or beam sizes. Full-scene ordinary collision tests should isolate lasers only if prior reflected damage resolves the intended motor target; the combined player check should keep weapons active.

No external assets or new third-party licenses were introduced. Subjective handling and final Unity rendering/escape/restart/origin behavior are assessed by root's actual integration evidence, not inferred from these geometric checks. No additional milestone is proposed.
