# DropletPrototype — implementation outline

Prepared: 7 September 2026.

Status: planning only. No existing game repository was inspected for this outline. All scope, tuning values, counts, and performance budgets below are proposals, not completed features or measurements.

## 1. Game direction

Build a small, complete arcade game inspired by the user's premise: a droplet destroys an Earth fleet by physically passing through ships. The experience should contrast a small, seamless attacker with a large, organized formation.

The player fantasy is overwhelming power controlled precisely. The challenge comes from choosing a route, steering at speed, lining up several ships, and clearing the formation efficiently—not from a droplet health bar, ammunition, or conventional dogfighting.

Treat this as a gameplay adaptation, not literal spaceflight physics or a claim of exact novel chronology. The droplet is a rigid, polished object; do not implement fluid simulation. A timer, combo system, assisted camera, compressed distances, and stylized audio are deliberate game-design choices.

### Initial release

Proposed platform: Windows desktop, offline, keyboard and mouse. Camera: third-person chase camera. Player: one droplet. Content: one arena with 30–60 interactive ships built from two ordinary ship silhouettes and an optional visually distinct command ship. All targets remain stationary in the initial release.

First prove the mechanics with ten primitive ships. The first release adds actual Blender models, an authored fleet layout, reusable destruction effects, a readable HUD, pause, results, and restart. A sample mission lasts 180 seconds, adjustable in data. Destroying all valid targets wins; time expiring ends the attempt. An unfinished run still shows score and destroyed-target count.

A command ship is a composition and scoring variation, not a damage sponge. Every valid ship is destroyed by one registered droplet hit. Human weapons, if later added for atmosphere, must not secretly introduce droplet damage.

Defer multiplayer, multiple controllable droplets, a full campaign, seamless planetary travel, realistic relativistic speeds, runtime mesh slicing, advanced enemy AI, and a cinematic recreation of the whole novel.

### Core loop

Observe the formation → select a route → align and accelerate → penetrate ships → read impact/combo feedback → turn toward the next group → complete the mission → review results and restart.

This loop must work and feel understandable with primitive geometry before detailed production art begins.

## 2. Controls, camera, and mission rules

Proposed bindings:

| Input | Behavior |
|---|---|
| Mouse movement | Steer yaw and pitch |
| W / S | Increase / decrease cruise speed; S does not reverse flight |
| A / D | Lateral strafe to refine an approach |
| Left Shift | Hold boost; release returns toward selected cruise speed |
| Space | Brake while held |
| Escape | Pause/resume; unlock/lock cursor appropriately |
| R in pause/results | Restart the current mission |

Use assisted flight with automatic leveling; no manual roll in the first version. Clamp/tune steering so it remains legible. Boost should create a meaningful tradeoff between travel speed and turning precision. It does not consume ammunition or require an invented fuel economy.

Use a low-lag steering response and a separately smoothed camera. Interpolate presentation between simulation steps. The camera follows the visual pose in LateUpdate; do not have competing scripts write the same pose. Start with a stable horizon, modest speed-dependent field of view, minimal shake, and motion blur disabled. Expose sensitivity, invert-Y, FOV, and reduced camera-effects settings.

Early HUD: aiming reticle, speed, remaining targets, mission timer, score, and combo. Add arrows to remaining targets so the last ship is not a search chore. Do not make a complete 3D radar a prerequisite.

Suggested scoring: a hit has a data-defined base score. Consecutive kills within a configurable window increase a capped multiplier. Give a completion time bonus. Score only the single accepted destruction event, not every collision callback or visual effect. All scoring values are tuning data.

Mission state is explicit: Ready → Playing → Paused → Playing, and Playing → Results. Initialize the timer only when play begins. Resolve hits for a simulation step before evaluating its end condition; completion wins a tie with timer expiry. Clamp the final step to remaining mission time. Paused time does not advance mission or combo timers. A restart restores time scale, input, camera, targets, counters, event subscriptions, and transient effects.

A soft arena boundary warns the player and guides them back. If recovery is necessary, use a short fade/reposition, reset the previous sweep position, and never treat that teleport as a damaging traversal.

## 3. Technology and repository baseline

Use Unity 6 and URP. For a completely new project, Unity 6.3 LTS is a conservative supported option; Unity's support page lists support through December 2027 [S1]. This is not a claim that it is the newest feature release. If a valid project already exists, inspect its exact version and preserve it rather than automatically upgrading or downgrading.

Use the installed stable Blender version and record it. Select Unity Input System and Test Framework versions compatible with the actual Editor; do not copy arbitrary package version numbers. Use a straightforward camera initially; introduce Cinemachine only if a specific need justifies it, and use APIs matching the installed major version.

The game requires no backend, model API, or online service. Codex is a development assistant, not a runtime dependency. The workflow must remain usable through C# Editor tools and Blender scripts even without a desktop-control integration.

Suggested layout:

```text
DropletPrototype/
  AGENTS.md
  docs/
    IMPLEMENTATION_PLAN.md
    ASSET_PIPELINE.md
    TEST_PLAN.md
    STATUS.md
    ENVIRONMENT.md          # generated during G00
    ASSET_LICENSES.md       # populated as assets are added
  ArtSource/Blender/
  Tools/Blender/
  Assets/_Project/
    Art/Models/
    Art/Materials/
    Art/Textures/
    Audio/
    Data/
    Prefabs/Player/
    Prefabs/Ships/
    Prefabs/VFX/
    Scenes/TestRange.unity
    Scenes/FleetAssault.unity
    Scripts/Runtime/
    Scripts/Editor/
    Tests/EditMode/
    Tests/PlayMode/
  Packages/
  ProjectSettings/
```

Commit Assets with .meta files, Packages, ProjectSettings, documentation, and export scripts. Exclude generated caches such as Library and Temp and build output from ordinary source commits. Keep source-art history appropriate to repository storage limits. Do not put .blend sources into Assets as an implicit import pipeline: Unity recommends explicit FBX exchange instead of proprietary model files for production [S2].

## 4. Ownership and Blender-to-Unity pipeline

Blender is authoritative for model shape, UVs, authored textures, pre-broken mesh pieces, and fleet composition. Unity is authoritative for gameplay roots, component references, colliders, final materials/lighting, camera, UI, sound, and mission state.

Separate each Unity ship prefab into a stable gameplay root, a replaceable visual child, and a hit-volume child. Replace only the visual child when better art arrives. Never tie score identity or collision behavior to a renderer name.

Create the fleet composition in Blender using instances for previews and named Empty/object markers for spawn poses. Export individual ship models separately from the layout. Export a lightweight layout FBX containing the named markers, not a fused mesh of the whole fleet.

A Unity Editor importer maps marker types to ship prefabs, uses the already-imported marker transforms, and writes actual prefab instances into the scene under GeneratedFleet. The formation must be inspectable before Play. Run generation through an explicit menu/button with Undo support; rerunning it must not create duplicates or remove hand-authored objects outside its owned subtree.

Blender changes should update composition; Unity prefab changes should update gameplay and appearance. Do not let both tools independently become the authoritative source for the same layout transforms. Keep optional Unity-only set dressing under a separate, non-generated root.

Verify one model and an asymmetrical marker arrangement before bulk export. Size and import units matter to the asset contract; Unity documents preparing model scale for its import pipeline [S3]. Detailed asset requirements are in ASSET_PIPELINE.md.

## 5. High-speed collision architecture

This is the main correctness risk. A fast object can pass through a thin ship between sampled positions. For example, a proposed boost speed of 180 units/second travels 3.6 units in a 0.02-second simulation step. A thin target can lie entirely between the endpoints.

Use one movement authority, DropletMotor, with query-based collision. It computes a proposed fixed-step path, asks DropletHitDetector to process that path, and then applies the resulting pose. Do not mix this with an independently controlled dynamic Rigidbody. The motor is allowed to penetrate ships; it is not a bouncing physics projectile.

Conceptual order for each straight segment:

```text
old position → proposed new position
check valid targets overlapping the starting droplet volume
sweep the droplet's gameplay sphere across the complete displacement
collect every target hit, including several ships in one step
map compound colliders to a stable parent ship identity
sort accepted intersections by distance and deduplicate by ship identity
call ShipTarget.TryDestroy(hit context)
apply the proposed position and update presentation
```

For a bent path, sweep its actual piecewise segments. Do not test only the straight chord between the start and end of a sharp turn. A reset/teleport is not a traversed attack segment.

Use an explicit target layer mask. Exclude the player's visual/query representation, debris, effects, and non-gameplay geometry. Use simple box/capsule or small compound target volumes. Keep all initial-release targets stationary so moving-versus-moving collision is not an unacknowledged problem.

Physics.SphereCastNonAlloc can collect hits into a reusable buffer, but Unity documents that results are unordered and limited to the buffer capacity [S4]. Sort the populated range; if the buffer fills, grow and retry before processing, or use a deliberate complete fallback. A full buffer is not evidence that all targets were found. Apply the same completeness rule to overlap buffers. A correctness-first allocating query is acceptable during initial development, but instrument and remove regular gameplay allocations during profiling.

Add an explicit starting-overlap policy rather than assuming sweeps cover it; Unity documents an initial-overlap limitation for SphereCast and provides overlap queries [S5]. Spawn the player clear of targets in ordinary gameplay; the detector still needs a regression test for overlap.

ShipTarget.TryDestroy must return false after a ship has already been destroyed. On the first accepted hit, mark it destroyed, disable its hit colliders immediately, and emit one event. ScoreSystem, MissionController, and the effects presenter respond to that event. The droplet continues along its path. Missing VFX must not prevent the target from being counted.

Tests must cover thin ships, multiple targets in one segment, compound colliders, initial overlap, a full buffer, sharp turns, teleports, low frame rates, and repeated runs. At moving-fleet expansion time, revisit this design for relative target motion or adequately synchronized substeps; endpoint snapshots alone are not a guarantee for crossing trajectories.

## 6. Minimal runtime architecture

Prefer a few clear components over a large custom framework.

| Component | Sole responsibility |
|---|---|
| DropletInput | Read configured player actions and UI/gameplay input state |
| DropletMotor | Own simulation pose, speed, steering, and path segments |
| DropletHitDetector | Query a supplied path and resolve target intersections |
| ShipTarget | Own ship identity and idempotent alive/destroyed state |
| MissionController | Own mission state, valid-target count, timer, and result |
| ScoreSystem | Own score, combo, and completion bonus |
| ChaseCamera | Follow the presented droplet pose without owning movement |
| HudPresenter | Display state and remaining-target guidance |
| DestructionPresenter | Present pooled impact/debris/audio feedback |
| FleetLayoutImporter | Editor-only conversion of authored markers to prefabs |

DropletHitDetector is invoked explicitly by DropletMotor; do not rely on an accidental FixedUpdate ordering across components. Use serialized scene/prefab dependencies or a small explicit initialization step. Keep critical gameplay code separate from effects code.

Use DropletSettings, ShipDefinition, and MissionSettings data assets for speed, turning, collision radius, scoring, timer, and effects references. Do not embed configuration inside imported model files. Validate required references and layer assignments in the Editor.

Suggested scene structure:

```text
FleetAssault
  Systems
  Environment
  PlayerRoot
    VisualRoot
  GeneratedFleet
    Ship_001
      VisualRoot
      HitVolumes
  HandAuthoredDecor
  CameraRig
  UI
  RuntimeEffects
```

## 7. Art direction and destruction

Use contrast rather than sheer asset count. The droplet is smooth, compact, reflective, and readable. Human ships are larger and segmented, with clear bow/engine silhouettes and a small shared material set. Avoid making them so dark that targets disappear against space.

The authored fleet should offer a learning group, several lined-up targets for penetration chains, staggered groups that demand a turn, and a visually identifiable final target. These can all occupy one arena. The layout should reward intentional routes rather than random scattering.

Build the droplet's final material in URP. Start with a metallic material and a baked or custom reflection source. Reflection probes supply a cubemap representation of surroundings; they are not a guarantee of fully dynamic mirror-quality reflections [S6]. Do not update a full real-time reflection capture every frame in the first version.

Use a pre-authored destruction sequence: mark the target destroyed immediately, hide its intact visual, show a brief directional flash, reveal or spawn a small pre-broken piece set, then fade/recycle those pieces. Add extra glow and audio as presentation only. No runtime geometry cutting or hundreds of physical fragments per ship.

Pool recurring effects and cap active debris. Unity provides ObjectPool<T> for object reuse [S7]. Prototype caps might be 16 simultaneous bursts and 64 active debris pieces, with low/medium/high quality presets. These are adjustable budgets, not measured engine limits. Score and hit registration must remain correct even when an effect is skipped because its pool budget is exhausted.

## 8. Milestones and Codex task prompts

Do not request every milestone in one implementation turn. Each milestone ends with a verifiable state and a saved record in STATUS.md. Fix its failures before adding the next layer.

### G00 — Establish the actual project baseline

Outcome: a valid Unity project opens cleanly, the existing Editor/pipeline is known, and work can be reproduced.

Codex work: inspect the repository, Editor version, package manifest, installed tools, and current compile status. Create ENVIRONMENT.md. Establish the agreed folder structure, ignore rules, test assembly layout, and stage status without importing unrelated project code. Identify required manual Hub actions when no valid project exists. Do not fabricate a project or silently switch pipelines.

User verification: open the project in its recorded Editor and confirm there are no red Console errors.

Acceptance: actual Unity baseline compile evidence, or an explicit blocked status with precise prerequisites. No unrun test is marked passed.

Task prompt:

```text
Implement G00 only. Audit the actual Unity project and tooling, preserve
its Editor and render pipeline, prepare the folder/docs/test baseline,
and report what you actually verified. Do not add gameplay or art yet.
```

### G01 — Save a visible graybox test scene

Outcome: TestRange contains a droplet placeholder, ten target placeholders, lighting, a camera, and clear spatial references before Play.

Codex work: create stable placeholder player/ship prefabs and an Editor-only scene setup tool. Save the scene. Tool reruns must be repeatable and must not overwrite unrelated or unsaved content. Add scene/prefab validation.

User verification: inspect the Hierarchy and Scene view without pressing Play; close/reopen the scene and confirm objects remain.

Acceptance: ten individually addressable target roots, no missing script references, scene saved, and no runtime-only construction of the whole test range.

Task prompt:

```text
Implement G01 only. Create and save TestRange with a placeholder droplet,
ten separate target prefabs, camera, and lighting. Keep it visible in Edit
mode. Make setup repeatable without erasing hand-authored content.
```

### G02 — Make flight and camera usable

Outcome: the player can approach, pass, turn, brake, and line up another pass.

Codex work: implement DropletInput, DropletMotor, a minimal chase camera, and DropletSettings. Begin with modest speeds; expose controls in the Inspector. Resolve cursor capture and pause input state. Do not add target damage yet.

User verification: fly around the placeholders and test stopping/turning after a boost. Judge clarity and discomfort, not model beauty.

Acceptance: no unintended roll or camera instability; speed/turn settings visibly change behavior; frame-rate tests reveal no obvious input-speed coupling.

Task prompt:

```text
Implement G02 only. Add assisted 3D flight, boost, braking, and a readable
third-person camera. Keep movement authoritative in one component and
all tuning in data. Do not implement destruction or cinematics yet.
```

### G03 — Prove collision correctness

Outcome: every ship intersected by the droplet is destroyed once, including several in one step.

Codex work: implement DropletHitDetector, ShipTarget, layer filtering, swept-path visualization, starting-overlap handling, query-buffer completeness, and compound-collider deduplication. Use a color change or disappearance instead of elaborate VFX. Add the collision regression tests from TEST_PLAN.md.

User verification: boost through a thin ship and then a line of ships. Inspect debug paths when a hit looks wrong.

Acceptance: all required collision tests pass in Unity, including saturated-buffer and duplicate-collider cases. No double scoring/destruction events or invented hits through sharp-turn chords.

Task prompt:

```text
Implement G03 only. Make high-speed, multi-target penetration reliable
using full-path queries. Handle initial overlap, compound colliders,
full buffers, and reset teleports. Add real Unity regression tests;
placeholder destruction is sufficient.
```

### G04 — Complete the graybox game loop

Outcome: an ugly but complete, replayable game and an initial standalone build.

Codex work: add MissionController, ScoreSystem, HUD, Ready/Playing/Paused/Results states, countdown, combo, remaining-target guidance, arena recovery, and restart. Define timer/hit ordering and reset behavior. Build a desktop player using the actual Editor.

User verification: win, run out of time, pause/resume, leave the arena, and restart several times.

Acceptance: a standalone graybox build is playable; counters reset; all targets lead to one result; timer expiry leads to one result; pausing cannot advance score or time. This is the first complete playable milestone.

Task prompt:

```text
Implement G04 only. Add the complete timed mission, score/combo, HUD,
pause, results, target guidance, boundary recovery, and restart. Produce
and smoke-test the first graybox desktop build where tools permit.
```

### G05 — Validate the Blender asset pipeline

Outcome: the actual droplet and first ship replace placeholders without altering gameplay.

Codex work: provide version-matched Blender export scripts/checks and a Unity model-validation tool. Use a calibration cube, a forward/up marker, and a simple model to validate the round trip. Create prefab visual children and URP material assets. Document the known-working export/import settings.

User work: create/refine the model shapes in Blender, inspect exported size/orientation, and approve their in-game silhouettes. Do not produce the whole fleet library before this test passes.

Acceptance: unit-scale gameplay roots; correct facing and dimensions; valid normals; no missing materials; collision regressions still pass after swapping visuals; ordinary runtime builds require no Blender installation.

Task prompt:

```text
Implement G05 only. Establish and document a tested Blender-to-FBX-to-
Unity pipeline, validate scale/axes/pivots, and swap in the first droplet
and ship visual assets without modifying their gameplay contracts.
```

### G06 — Add bounded destruction feedback

Outcome: collisions are satisfying but do not depend on expensive fracture simulation.

Codex work: add DestructionPresenter, reusable flashes/trails/debris/audio, pool limits, quality settings, and cleanup. Use a small manually pre-broken model or a generic debris set. Cap camera impulses and preserve target visibility.

User work: create/approve a small wreck or piece set and judge whether impacts read clearly at normal play speed.

Acceptance: each target still counts once; disabled VFX does not change results; debris cannot block or score; active effect counts remain bounded; effects clean up on restart.

Task prompt:

```text
Implement G06 only. Present destruction with pooled, capped effects and
pre-authored pieces. Keep gameplay independent of presentation. Do not
add runtime mesh fracture or uncontrolled Rigidbody debris.
```

### G07 — Import the authored fleet arena

Outcome: a 30–60-target FleetAssault scene reflects the formation built in Blender.

Codex work: implement marker-based FleetLayoutImporter, type-to-prefab mapping, stable IDs, transform validation, and saved scene generation. Populate only GeneratedFleet. Keep all gameplay ships independently addressable. Reject duplicate marker IDs and unknown ship types with actionable errors.

User work: compose approach lanes, target chains, turns, and focal points in Blender. Export layout markers and inspect the resulting Unity scene.

Acceptance: marker count and target count match; orientation/spacing match the reference; rerunning import adds no duplicates; manual scene content survives; fleet is visible before Play; victory counts only interactive targets.

Task prompt:

```text
Implement G07 only. Convert the Blender-authored marker layout into
saved Unity prefab instances under GeneratedFleet. Preserve individual
ships, stable identities, imported transforms, and hand-authored scene
content. Validate reruns, unknown types, and duplicate IDs.
```

### G08 — Polish readability, atmosphere, and accessibility

Outcome: a coherent, legible scene with usable feedback and adjustable camera effects.

Codex work: finalize URP materials/reflection source, lighting, sky background, trails, audio mixing, HUD readability, target arrows, sensitivity/invert-Y/FOV controls, and reduced camera effects. An optional short skippable opening must reset input/camera state correctly. Do not add an extra game mode or moving-ship AI under the name of polish.

User work: judge silhouettes, lighting, sound intensity, target visibility, and comfort in the actual player.

Acceptance: the droplet reads as polished metal, ships are findable against space, UI scales at the chosen resolutions, and disabling shake/motion effects does not change gameplay.

Task prompt:

```text
Implement G08 only. Polish materials, reflections, lighting, UI, sound,
and camera comfort. Preserve the established controls and collision
rules. Keep optional cinematic elements skippable and independently
removable.
```

### G09 — Profile, regress, and package

Outcome: a tested, documented desktop build with honest hardware-specific performance evidence.

Codex work: profile the recorded target PC at the agreed resolution; capture frame-time spikes, effect counts, memory, and allocations. Optimize the measured bottleneck first. Add simpler distant meshes or decorative fleets only within verified budgets. Run the complete regression suite and build smoke tests. Produce README controls, version info, and known issues.

User verification: run the packaged build outside the Editor and complete the mission repeatedly.

Acceptance: recorded hardware and settings; a proposed 1080p/60-fps target evaluated rather than asserted; no recurring severe frame spikes during chain kills; no steadily growing temporary-object count across restarts; all mandatory functional tests passed or clearly listed as blocking.

Task prompt:

```text
Implement G09 only. Profile the standalone game on documented hardware,
fix measured bottlenecks and regressions, and package a build with
controls, known issues, asset-license records, and actual test evidence.
Do not claim performance numbers without measurements.
```

## 9. Reusable task and report format

Codex supports repository guidance through AGENTS.md, and its official guidance recommends explicit testing and review [S8]. Keep stable rules in AGENTS.md, detailed requirements in this document, and current progress in STATUS.md.

Use this template after G00:

```text
Read AGENTS.md, docs/IMPLEMENTATION_PLAN.md, docs/TEST_PLAN.md, and
current docs/STATUS.md. Implement milestone Gxx only.

First inspect the existing implementation and identify the smallest
change that satisfies this milestone. Preserve accepted behavior.

Use the actual Unity and Blender versions recorded for this project.
Save authoring assets through the proper Editor/export APIs. Add and
run the milestone's tests where the required tools are available.

At completion report:
1. Files created or changed and their purpose.
2. Checks actually run and evidence/results.
3. Checks not run and the precise limitation.
4. Exact manual verification steps, including scene and menu names.
5. Known defects and remaining acceptance criteria.

Update docs/STATUS.md and stop at this milestone. Do not implement
later stages merely because time remains.
```

A milestone is not complete because code was generated or dotnet compiled. Unity's Test Framework supports EditMode and PlayMode testing; use those checks for the project where appropriate, plus real player-build verification [S9]. Record passed, failed, and not-run outcomes separately. Version-control accepted milestones so regressions have a known comparison point.

## 10. Risks, deferred expansions, and rights

The main risks are unreliable high-speed hits, camera discomfort, a model/export mismatch, a giant fused fleet mesh, unbounded debris, runtime-only scene generation, and scope expanding before the basic loop is playable. Each has an explicit milestone or regression test above.

After G09, consider moving/escaping ships, a focus-turn action, selectable formations, a photo mode, or additional missions one feature at a time. Moving targets require additional collision validation, and any slow-motion feature must define whether score windows and mission time use scaled or real time. Do not add them implicitly.

Keep models, sound, music, and interface assets original or appropriately licensed. Before distributing a game using the Three-Body setting or branding, resolve adaptation and asset rights for the intended markets. Calling a project a fan game does not itself establish permission. The U.S. Copyright Office describes adaptation rights and applicable exceptions, but that is not a jurisdiction-specific clearance for this project [S10].

## Sources

These support technical/tool facts, not the proposed game balance or fictional adaptation choices. Documentation checked 7 September 2026. At implementation time, use the version matching the actual installed software.

[S1] Unity, Unity 6 release support: `https://unity.com/releases/unity-6/support`

[S2] Unity, Model file formats reference: `https://docs.unity3d.com/6000.5/Documentation/Manual/3D-formats.html`

[S3] Unity, Preparing models for import: `https://docs.unity3d.com/6000.0/Documentation/Manual/models-preparing.html`

[S4] Unity, Physics.SphereCastNonAlloc: `https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.SphereCastNonAlloc.html`

[S5] Unity, Physics.SphereCast and Physics.OverlapSphere: `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.SphereCast.html` and `https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Physics.OverlapSphere.html`

[S6] Unity, Reflection Probes and Reflection Probes in URP: `https://docs.unity3d.com/6000.5/Documentation/Manual/ReflectionProbes.html` and `https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/reflection-probes-introduction.html`

[S7] Unity, ObjectPool<T>: `https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Pool.ObjectPool_1.html`

[S8] OpenAI, Custom instructions with AGENTS.md and Best practices: `https://developers.openai.com/codex/agent-configuration/agents-md` and `https://developers.openai.com/codex/learn/best-practices`

[S9] Unity Test Framework manual: `https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html`

[S10] U.S. Copyright Office, What is Copyright?: `https://www.copyright.gov/what-is-copyright/`
