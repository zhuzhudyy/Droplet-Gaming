# G05–G07 Blender delivery

Generated and inspected on 2026-09-07 with installed **Blender 5.2.1 LTS**, build `9e2066aef7ef`. All models and formation composition are original procedural authoring for this project; no downloaded models, textures, fonts or add-ons were used. Final game materials, lighting and presentation are owned by the Unity integration and are not validated by the Blender renders alone.

## Actual deliverables

- `calibration.blend` and `Exports/calibration.fbx`: one metre cube, asymmetric FRONT / UP / RIGHT geometry, a frigate and three rotated/translated spawn markers.
- `fleet_assets.blend`: editable model modules, seamless droplet mesh, three ship silhouettes, three pre-cut wreck sets and showcase instances. Authoring collections are excluded from the showcase view layer to avoid rendering their originals at the origin; enable the relevant `*_Source` collection in the Outliner to edit it.
- `fleet_layout.blend`: **40 editable Empty spawn markers** and linked ship collection preview instances. Marker IDs and preview links are explicit custom properties. Source models are still independently editable.
- `Exports/Droplet.fbx`, `Frigate.fbx`, `Cruiser.fbx`, `Command.fbx`: actual explicit model exports, one combined `VisualMesh` per intact model with shared material slots. The `.blend` files retain separate editable modules and modifier stacks.
- `Exports/FrigateWreck.fbx`, `CruiserWreck.fbx`, `CommandWreck.fbx`: six individually addressable pre-cut pieces per ship type. Cut surfaces are capped, assigned dark Trim material, and have individual piece pivots. No runtime fracture is needed.
- `Exports/FleetLayout.fbx`: 40 tiny named **pose-proxy meshes**, containing no ship preview geometry. Unity consumes their imported transforms and discards the proxy geometry when producing real Prefab instances.
- `Exports/asset_manifest.json`: actual triangle counts, dimensions and exporter settings.
- `Exports/FleetLayout_expected.json`: every stable ID, type, expected Unity world position, forward/up vectors, group and unit scale.
- `model_geometry_report.json`: actual evaluated module/piece bounds, pivots, signed volume and closed-mesh audit.
- `Previews/calibration-preview.png`, `fleet-assets-preview.png`, `droplet-preview.png`, `fleet-layout-preview.png`, `frigate-breakup-preview.png`: actual Blender renders, all opened and visually inspected during asset work. The breakup view shows the actual six closed pieces spread apart. These are authoring evidence, not Game-view acceptance.

## Validated coordinate contract

Authoring is metric, one Blender metre per Unity unit. Blender **+Y is forward**, **+Z is up**. Unity **+Z is forward**, **+Y is up**. Export uses Blender's bundled `io_scene_fbx`: `global_scale=1`, `apply_unit_scale=true`, `apply_scale_options=FBX_SCALE_UNITS`, `axis_forward=-Z`, `axis_up=Y`, `use_space_transform=true`, `bake_space_transform=true`; selection only, meshes/empties only, modifiers applied on export copies, no animation, no leaf bones.

Actual Unity calibration passed with **Unity 6000.5.10f1** and ModelImporter `globalScale=1`, `useFileScale=true`, `bakeAxisConversion=true`, imported normals, no generated colliders or animation. Evidence: `docs/verification/G05-G09/calibration-unity.txt`. The one-metre cube tolerance is 0.002 units; asymmetric +Z / +Y / +X and all three full marker poses were checked.

Two real calibration failures were fixed before bulk export:

1. Blender 5.2's `bake_space_transform` converted meshes correctly but left exported Empty transforms in the wrong axes. The contract's permitted tiny mesh proxy fallback now retains the source Empty while exporting a four-vertex asymmetric tetrahedron at its world pose. Each proxy is only 0.02 × 0.06 × 0.04 Blender metres and is not a gameplay visual.
2. Reading `matrix_world` before refreshing Blender's dependency graph copied stale zero positions. Export now calls `view_layer.update()` before copying poses. Proxy export hierarchy is flat; Unity still resolves imported world poses including the FBX import root.

No corrective scale or rotation is required at gameplay roots. Model pivot is the authored origin. The droplet has a rounded leading end at +Z and a pointed trailing end at -Z in Unity.

## Actual model measurements

Dimensions below are Unity X / Y / Z in metres, rounded. Full precision is in the JSON reports.

| Model | Dimensions | Triangles | Exported meshes |
|---|---|---:|---:|
| Droplet | 1.2 / 1.2 / 2.4 | 5,280 | 1 |
| Frigate | 9 / 4.456 / 22.14 | 2,040 | 1 |
| Cruiser | 18.7 / 8.01 / 33.275 | 2,964 | 1 |
| Command | 22.393 / 10.85 / 37.275 | 3,792 | 1 |
| FrigateWreck | 9 / 4.439 / 22.14 | 2,760 | 6 |
| CruiserWreck | 18.7 / 8.01 / 33.275 | 3,904 | 6 |
| CommandWreck | 22.393 / 10.85 / 37.275 | 5,248 | 6 |

Frigate intact bounds centre is `(0, 0.528, -0.070)`, cruiser `(0, 0.705, 0.3625)`, command `(0, 2.125, 2.3625)`. Their gameplay origins remain `(0,0,0)` rather than being moved to the visual bounds centre. Hull, Armor, Trim and Engine are shared ship material names; DropletMetal is the droplet slot. Materials use plain colour/metal/emission inputs and do not rely on external textures.

The evaluated source-geometry audit found **zero boundary edges, zero non-manifold edges and positive signed volume for every intact source mesh and every wreck piece**. The droplet is one closed, smooth mesh with a single vertex at each end, not disconnected overlapping spheres. Wrecks are cut from the actual evaluated hull modules with six spatial regions (port/starboard and aft/middle/fore); they keep the intact profile and have dark closed break faces.

## Fleet composition

Final count: **29 Small / 10 Large / 1 Command = 40**. IDs are stable `001`–`040`. Six authored groups create a learnable formation:

1. Four frigates along `(0,8,z)` at z = 100, 145, 190, 235 for the initial approach and chain-hit lane.
2. Eight port diagonal targets in two staggered lances.
3. Eight starboard diverging targets with height variation.
4. Eight targets on an upper screening arc.
5. Eight targets on a lower defensive crescent.
6. Two cruisers, one high escort and one central command ship as the final visual focus.

The final independent position audit found all 40 IDs unique and a minimum centre-to-centre spacing of **37.483 metres**. A first draft placed 020 and 036 only 18.166 metres apart; marker 036 was moved to `(165,-40,390)` and the actual FBX, JSON, source and preview were regenerated. Extents remain within the agreed approximate x ±180, y -40..100, z 100..520 area. Layout is deterministic and contains no random scatter or merged fleet geometry.

## Reproduce without touching an open Blender session

From the project directory:

```powershell
python Tools/Blender/run_assets.py --stage calibration
# Revalidate calibration in Unity before changing exporter/importer conventions.
python Tools/Blender/run_assets.py --stage full
# Layout-only FBX refresh, retaining existing model exports:
python Tools/Blender/run_assets.py --stage layout
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/Blender/inspect_sources.py
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python Tools/Blender/preview_breakup.py
```

`run_assets.py` starts a separate factory-startup background process, captures the real Blender exit code and logs to this directory. `full_generation.log` and `layout_generation.log` record **exit-code-0** production runs. No open Blender UI file is closed or overwritten. The scripts only write their owned `ArtSource/Blender` outputs. Re-running intentionally regenerates these authored outputs; manually edited source copies should be saved under another filename before regeneration.

`build_fleet_assets.py --stage source` also supports generating source/preview files without exporting the production FBX library, used while waiting for calibration acceptance. Blender 5.2 reports a non-fatal future-deprecation notice for explicit `Material.use_nodes` / `World.use_nodes`; these are supported in the installed version and caused no asset-generation failure.

## Acceptance boundary

Blender model generation, explicit exports, source editability, source mesh topology, deterministic layout and preview rendering were performed. Unity calibration was actually run by the integration owner and its evidence was read. Final imported materials/reflections, hit-volume fit, gameplay effects, complete formal scene, performance and Windows player acceptance remain the main integration/test records' responsibility; these Blender outputs alone do not mark those checks passed.
