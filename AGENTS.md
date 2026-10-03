# DropletPrototype — agent instructions

## Product and scope

Build the game described in docs/IMPLEMENTATION_PLAN.md. The initial release is an offline, single-player, third-person arcade collision game: one indestructible droplet, one bounded arena, individually destructible stationary ships, a mission timer, score, pause, results, and restart.

Blender owns mesh creation and authored formation layout. Unity owns runtime behavior, prefabs, final lighting/materials, camera, UI, audio, and builds. No multiplayer, backend, language-model API, runtime mesh fracture, full solar-system simulation, or moving target AI in the initial release.

## Work method

### Volcengine voice authoring (2026-09-19)

For future game voice generation, read docs/VOLCENGINE_VOICE.md and use Tools/Audio/volc_voice.py. Use only Seed-TTS 2.0 for voice generation, as explicitly requested by the user. Do not add video generation providers. Keep cloud calls in development tools, never in the offline Unity player. Preview without --execute first; execute generation within the user's requested scope. Credentials belong in the documented local environment variables, never chat, Assets, source or logs. Preserve existing voices and .meta GUIDs; importing/replacing accepted audio is a separate Unity authoring step. Do not count mocked tests as live API verification.

### Authorized G05–G09 batch exception (2026-09-07)

The user authorizes consecutive G05–G09 implementation, validation and repairs without per-stage confirmation. Agents create and execute Blender model/layout scripts in this batch; the user reviews the final art and handling together. Preserve TestRange, existing gameplay, old builds and all safety/quality constraints. Root coordinates all Unity state and final asset writes. Stop after G09.

### Authorized G01–G04 batch exception (2026-09-07)

The user explicitly authorized consecutive implementation of G01, G02, G03 and G04 in this batch. Validate and repair each stage, update STATUS.md, then proceed without per-stage approval. Deliver the first playable graybox and Windows build where supported. Stop after G04. All preservation, safety, quality and scope constraints still apply. The batch mission duration is 120 seconds, overriding the earlier proposed 180 seconds.

Read docs/STATUS.md before starting. Implement only the requested milestone. Inspect existing files before changing them. Record design changes rather than silently expanding scope. Do not alter unrelated projects or revert user work.

Inspect ProjectSettings/ProjectVersion.txt and Packages/manifest.json. Preserve the current supported Editor and pipeline; do not upgrade automatically. Use documentation and APIs matching installed versions. Record exact Unity, Blender, package, and test versions in docs/ENVIRONMENT.md.

## Unity rules

- Prefer small C# components, serialized dependencies, and ScriptableObject tuning data. No all-purpose GameManager containing every system.
- MonoBehaviour class names must match their .cs filenames. Keep editor code in an Editor-only assembly/folder; do not leak UnityEditor into player code.
- Create/change scenes and prefabs through Unity Editor APIs or the actual Editor, not guessed serialized YAML. Preserve existing .meta files and GUIDs. Do not edit generated .csproj files as source configuration.
- Main scene objects and the fleet must be saved and visible before Play. Runtime VFX and temporary debris are allowed; hidden runtime-only construction of the entire authored level is not.
- Editor generation must be explicit, repeatable, Undo-aware, and confined to an owned generated subtree. Never erase authored content outside that subtree. Never overwrite unsaved scene work silently.
- DropletMotor is the sole movement authority. Use a fixed-step proposed path and call the hit detector before applying each segment. Do not rely solely on OnCollisionEnter or OnTriggerEnter at high speed.
- Sweep the whole traveled path, process all hit ships, handle initial overlap, deduplicate compound colliders by ship identity, and resolve destruction once. Handle full query buffers without dropping hits silently.
- Physics/query geometry and visual meshes are separate. A model replacement must not remove scripts, colliders, identity, or score behavior. Debris must not block the droplet or become a scoring target.
- Reset pause/time scale, input state, score, targets, events, camera, and temporary effects on restart. Avoid stale static state across Play-mode entries.
- Keep target gameplay independent of optional visuals and audio. Pool recurring effects during the effects milestone and profile before changing architecture.

## Blender and assets

Follow docs/ASSET_PIPELINE.md. Keep .blend source outside Assets; import explicit FBX and texture exports. Validate size, forward/up axes, pivots, normals, and marker transforms using a calibration asset before bulk export. Do not assume arbitrary Blender shader graphs become Unity shaders.

Retain individually addressable ships. Do not join the fleet into one mesh. Keep original sources; export copies for destructive modifier application or triangulation. Track third-party asset licenses in docs/ASSET_LICENSES.md.

## Verification and reporting

Follow docs/TEST_PLAN.md. Unity compilation, EditMode/PlayMode tests, and a standalone build are distinct checks. A successful dotnet build is not proof that the scene or player works. Use the actual Editor/test runner when available. If a required executable, license, or Editor session is unavailable, report the limitation and exact manual steps; never report an unrun check as passed.

After every milestone, report: changed files; executed checks and evidence; unrun checks; manual verification steps; known issues; and next milestone. Update docs/STATUS.md. Fix regressions before moving on. Stop at the requested milestone gate. Do not install unapproved third-party tools or use unrelated personal services.

## Version retention and validation override (2026-09-19)

The user now authorizes removal of obsolete versions with no remaining value; this supersedes blanket old-build preservation above. Keep one current player, one preceding full rollback and the compact v0.2.2 release ZIP. Consult docs/TEST_PLAN.md current-version policy before testing. Historical scene/authoring suites are opt-in for relevant changes, not mandatory on every task. Preserve referenced source assets and unrelated uncommitted work. Deletion remains subject to actual tool permissions; do not report blocked deletions as completed.
