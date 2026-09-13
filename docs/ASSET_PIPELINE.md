# Blender and Unity asset contract

Status: G05 calibration and explicit exports actually validated in Unity 6000.5.10f1 on 2026-09-07. Blender 5.2.1 LTS; source and exact export settings are in ArtSource/Blender/DELIVERY.md. Calibration evidence: docs/verification/G05-G09/calibration-unity.txt. The specifications below remain the ongoing contract.

Batch implementation: source Empty markers are exported as temporary four-vertex mesh pose proxies because the installed FBX exporter produced incorrect Empty axes with bake_space_transform. Unity consumes the imported full transform and never instantiates marker geometry into the fleet. ModelImporter uses globalScale=1, useFileScale=true, bakeAxisConversion=true, imported normals, no animation or colliders. No corrective gameplay-root transform is required. Export staging is ArtSource/Blender/Exports; FleetAssetPipeline copies explicit FBX into Assets/_Project/Art/Models/Fleet. Final production prefabs use independent VisualRoot / HitVolumes / EffectsAnchor. See BATCH_G05_G09.md for validation status.

## 1. Ownership

Keep `.blend` sources under ArtSource/Blender, not Assets. Keep export automation under Tools/Blender. Export explicit FBX and texture files to Assets/_Project/Art. Models are independent exports; the fleet layout is a separate marker export.

Blender owns mesh shape, UVs, texture sources, breakup meshes, and fleet composition. Unity owns stable gameplay prefabs, component references, hit volumes, final URP materials, lights, camera, UI, and effects. Do not expect arbitrary Blender shader graphs, camera post-processing, or rigid-body simulations to transfer as game systems.

## 2. Minimum art inventory

| Asset | Initial content | Suggested initial budget |
|---|---|---|
| Droplet | One seamless polished silhouette | Approximately 2,000–6,000 triangles |
| Small ship | One recognizable silhouette | Approximately 1,000–3,000 triangles |
| Large ship | One modular variation | Approximately 3,000–6,000 triangles |
| Command ship | Optional visual/scoring variation | Reuse modules before making a unique high-detail asset |
| Breakup set | Four to eight large pieces or a reusable debris set | No hundreds-of-shards fracture |
| Background | Simple star backdrop and reflection source | No individually simulated star objects |
| Fleet layout | Named pose markers plus Blender preview instances | 30–60 interactive targets for initial release |

These counts are starting budgets to revise after profiling, not performance guarantees. One or two shared ship materials and modest texture sizes are enough for the first pass. Detail silhouette first, then large surface panels, then small decoration only when it remains visible from the game camera.

## 3. Scale and coordinate contract

Use metric authoring with a calibration cube intended to measure one Unity unit on each side. The gameplay prefab's root scale must be (1,1,1). Unity gameplay convention for this project is +Z forward and +Y up. Identify the intended leading end on a labeled orientation test asset.

Suggested compressed gameplay proportions: droplet length approximately 1 unit; ordinary ship lengths 12–30 units; fleet separations in tens of units. All are design choices, not novel dimensions. Keep the combat arena close to the origin rather than building astronomical distances.

Apply intended model rotation and scale on an export copy. Keep the original editable source and modifier stack. Align the pivot to the agreed model/gameplay origin. Avoid negative and nonuniform scales on spawn markers. Bake or apply necessary export geometry modifiers on a copy and validate triangulation/shading in Unity.

Record the actual Blender version, exporter implementation, axis settings, unit/scaling settings, and Unity importer settings after they pass calibration. Do not copy a generic axis preset and assume it is correct. Do not repair every ship by introducing arbitrary scale/rotation corrections at its gameplay root.

## 4. Round-trip gate

Before producing the full asset library, export a one-unit cube, an asymmetrical forward/up marker, one ship, and a small marker layout with different rotations and off-origin positions.

Unity validation must establish all of the following:

- Cube dimensions are one unit on each side within a small documented tolerance.
- Forward/up orientation matches the agreed +Z/+Y gameplay convention.
- The model pivot and visible mesh line up with hit volumes.
- No unintended negative/nonuniform scales are required at the gameplay root.
- Normals face out, shading is acceptable, and textures/material references exist.
- Rotated/translated layout markers preserve the intended visual arrangement.
- Reimport changes only the intended assets and does not break prefab references.

Blender authoring and Unity importer axis conventions must be tested together. Do not manually swap Euler components as a universal coordinate conversion.

## 5. Prefab contract

```text
PF_Ship_Small                 # stable root, identity and ShipTarget
  VisualRoot                 # replaceable imported visual
  HitVolumes                 # one or a few simple colliders
  EffectsAnchor              # optional effect reference pose
```

The ship's definition holds base score and presentation references. A visual replacement must not recreate its gameplay root. Mesh colliders are not the default for high-poly visual geometry. Compound child colliders all resolve to the same ShipTarget and produce only one kill.

The player follows the same stable-root/visual-child pattern. Its gameplay query radius is explicit tuning data and need not equal the complete silhouette exactly. Show it with a debug gizmo so art and gameplay can be compared.

Broken pieces are presentation assets. Put them on a layer excluded from droplet hit queries; they never contribute to mission target count. Disable physics on distant pieces or use simple animated drift where suitable.

## 6. Fleet layout contract

In Blender, use preview instances of ship assets to compose the formation. Place a corresponding named Empty/object marker for each interactive ship in a dedicated export collection. Names follow:

```text
SPAWN_Small_001
SPAWN_Large_002
SPAWN_Command_003
```

Type tokens map to a Unity prefab registry. IDs must be unique and stable. Keep preview ship geometry and decorative backgrounds out of the marker export. Verify that the installed exporter/importer preserves the marker hierarchy; a minimal marker mesh is an acceptable deliberate fallback if Empty export is not preserved, provided it is discarded during conversion and the contract is documented.

Export the lightweight marker layout separately from the ship meshes. The Unity Editor importer loads the reference layout, preserves the imported hierarchy/transform conversions, maps each marker to a prefab, and places actual scene instances under GeneratedFleet. It must not independently guess source-coordinate conversions.

Account for parent/import-root transforms when resolving poses. Do not just read a nested marker's local position and assume it is a world position. Validate unit scale and reject unexpected transforms with an actionable message before writing the scene.

Importer behavior:

1. Validate all IDs, type mappings, and transform constraints before changing the scene.
2. Preview or report the number of creates/updates/removals.
3. Change only GeneratedFleet, with Undo and proper scene dirty/save handling.
4. Keep hand-authored set dressing and lighting outside that generated subtree.
5. Ensure rerunning the same layout produces the same target count, not duplicates.
6. Keep targets visible and editable in the saved scene before Play.

Do not join the whole fleet into one mesh. Distant decorative ships, if added later, must be labeled separately, have no interactive colliders, and never count toward victory.

## 7. Materials, destruction, and licensing

Build final materials in Unity using the selected URP workflow. Export reusable texture maps as needed, not a claim that the Blender node graph is portable. For the droplet, validate a metallic surface and a baked/custom reflection source in the actual gameplay camera. A highly metallic object needs a useful reflection environment; do not try to repair poor readability only by pushing a material slider.

First destruction pass: disable intact target visuals and colliders, trigger a directional flash, reveal a small piece set, drift/fade it, and return it to a pool. Geometry breakup is authored in Blender, but its timing, budget, and cleanup are Unity behavior.

Maintain docs/ASSET_LICENSES.md with asset name, creator/source, license, required attribution, permitted use, and any unresolved distribution restriction. Use original or licensed models, music, sounds, and UI. Do not treat assets from a fan video or adaptation as automatically reusable.
