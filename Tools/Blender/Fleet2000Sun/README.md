# Fleet2000Sun layout authoring

Open `ArtSource/Blender/Fleet2000Sun/FleetLayout_2000_Sun.blend` in Blender 5.2.1 LTS. The saved scene contains exactly 2,000 independently editable collection instances in two layer collections. Each instance has a stable `FS1C01R01`-style ID, `model_id=FusionFrigate`, and unit scale. The single shared LOD2 collection contains 12 mesh objects / 1,396 triangles per complete ship. The old ship source and exported FBX are unchanged.

`Tools/Blender/Fleet2000Sun/layout_config.json` is the sole authored pose authority, following the project's existing FleetExpansion schema 1. `ArtSource/Exports/Fleet2000Sun/FleetLayout_2000_Sun.json` is its identical, hash-traceable export, not another independently maintained layout. Unity reads that export and reuses the existing enlarged `FusionFrigate_VisualUpgrade` prefab. The source .blend never goes under `Assets`.

JSON positions are already in Unity metres: +X right, +Y up, +Z forward. Blender displays `(x,z,y)` from those position arrays, with ship local +Y forward / +Z up. All marker scales are `(1,1,1)`. The existing gameplay model's constant 2.6 enlargement is baked once into the shared Blender preview prototype, giving length 57.563997 m; it is not applied again to individual Unity markers. `forward`/`up` remain the existing schema's canonical orientation, and `rotationQuaternion` is Unity `[x,y,z,w]`.

From the project folder, these commands use only the existing Blender installation:

```powershell
$blenderExe = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
$layoutScript = 'Tools/Blender/Fleet2000Sun/author_layout.py'

# Explicitly recreate regular positions from the JSON parameters.
python $layoutScript --reflow

# Generate the editable review source. Refuses to overwrite unrecorded edits.
& $blenderExe --background --factory-startup --python-exit-code 1 --python $layoutScript -- --generate

# Independent actual reopen, counts, sharing, axes, scale, bounds and source hashes.
& $blenderExe --background --factory-startup --python-exit-code 1 --python $layoutScript -- --verify

# Actual Blender perspective renders (layout previews, not Unity gameplay proof).
& $blenderExe --background --factory-startup --python-exit-code 1 --python $layoutScript -- --render
```

To publish manual layout edits, preserve a copy, move the existing named ship instances, save the .blend, then explicitly run the same Blender command ending in `-- --export-saved`. This updates the same authority and export JSON from saved world positions. Keep all 2,000 unique IDs, unit scale, +Y forward / +Z up; publishing rejects deleted/duplicate instances, rotation, scale changes, and intersecting full-ship AABBs. It also refreshes the source receipt. Run `--verify` afterward, then use the Unity scene import tool to apply the exported layout. Regenerating an unrecorded edited .blend is deliberately rejected.

The equal centre intervals are 172.692 m sideways (3 L), 230.256 m longitudinally (4 L), and 115.128 m between layers (2 L). Layer 2 alone has a constant 14.391 m sideways / 28.782 m longitudinal offset. The 20-ship attack lane remains at Unity x=0/y=8, starting z=300, reached directly from the existing (0,8,0) spawn. Perspective comes from actual perspective cameras; no distance-dependent spacing or scale is used.

The conservative all-2,000-target snake route is approximately 458.7 km. At the preserved 52/156 m/s speeds, timing fields estimate 8,940 seconds including turns, aiming and margin. This is an explicit estimate, not a completed human playthrough or a measured speedrun.

Actual Blender evidence is under `docs/verification/Fleet2000Sun/blender-*`: generation receipt, independent reopen, complete pairwise layout checks, and near/mid/panorama renders. No FBX model was re-exported or duplicated in this task. Unity gameplay, lighting, collision, profiler and player-build validation belong to the root task's separate evidence.
