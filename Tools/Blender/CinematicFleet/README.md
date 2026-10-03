# Cinematic cubic formation

`python Tools/Blender/CinematicFleet/run.py generate` starts an independent background Blender 5.2 process, preserves the original model/FBX/old layout, authors the retained 2000 IDs, saves the `.blend`, exports schema-1 poses and reopens the source for validation. `render`, `verify` and `repeatability` are separate commands. No Unity or interactive Blender state is touched.

The explicit count correction is 20 × 25 × **4** = 2000; 20 × 25 × 2 would be 1000. The 20 × 25 face is XY, aimed toward combat-start droplet `(0,8,0)`. Four depth planes have equal 4800 UU spacing; X/Y spacings are 750/600 UU. Overall centre spans are 14250 × 14400 × 14400 UU. Existing 100 metres/UU and 57.564 UU ships remain unchanged. A lane at X=0/Y=8 supports ordinary straight approach into actual saved ships.

All targets use existing calibrated +Z bows aimed independently toward the fixed spawn. Blender uses +Y bow/+Z up and a shared LOD2 collection (12 meshes/1396 triangles); 2000 high-detail mesh copies are never made. The exporter stores Unity-space positions and basis vectors, so Unity must not swap axes again.

Authority: `layout_config.json`; byte-identical export: `ArtSource/Exports/CinematicFleet/FleetLayout_Cubic.json`. `.blend`: `ArtSource/Blender/CinematicFleet/FleetLayout_Cubic.blend`. The generation receipt guards against silently replacing a manually changed `.blend` and records original source hashes.

The root integration calls `CinematicFleetLayout.ApplyToScene(scene)` only on `FleetAssault_CinematicAudio_Cubic.unity`. It rejects differing identities, never creates/deletes/renames ships, preserves `mission.targets` order and existing colliders/visuals, uses Undo and four owned depth grouping transforms, updates only saved poses and the new scene's cloned settings, and leaves scene saving to the caller. The normal and skipped Timeline both already call `MissionController.StartCombat`. Existing `FleetCombatSimulation.Configure/ResetSimulation` caches/restores the saved initial poses; the layout importer has no runtime update component and cannot pull fleeing ships back.

Geometry checks use every pair of **rotated full-hull** conservative AABBs. Unity's helper independently checks actual LOD0 world bounds, nine colliders, bow-versus-engine centres, saved directions, full membership and spawn clearance. Blender checks do not count as Unity/player verification.
