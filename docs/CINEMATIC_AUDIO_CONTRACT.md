# Cinematic audio / spatial fleet integration — 2026-09-19

Base: saved FleetAssault_Enhanced; destination: Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity. Preserve existing scenes, assets, GUIDs and uncommitted work.

## Shared event contract and ownership

Reuse FleetCombatSimulation.EventRaised / CombatEvent, never a second gameplay bus. Root owns CombatEvents.cs, FleetCombatSimulation.cs, FleetLaserDirector.cs, MissionController.cs and NarrativeApproachController.cs if integration changes are needed. Add immutable `eventId` (long, unique within generation), `attackerId`, `targetId` (strings), `direction`, `normal`, `damageState`, `behaviorState`, `originOffset` alongside existing kind/speaker/subject/position/source/simulationTime/generation. IDs use generation + sequence. Capture position and states at emission. Preserve existing constructor and EmitEvent call compatibility. New enum values appended: WeaponFired, DropletContact. No enum reordering. Camera/audio never apply damage or change deadlines. Origin-relative event positions rebase by recorded versus current accumulated origin offset.

- A owns new Runtime/CinematicAudio/ShotDirector.cs and camera-specific files; ChaseCamera.cs and FleetRenderManager.cs (only bounded per-subject view pinning); camera tests and docs/CINEMATIC_SHOTS_REPORT.md. No shared Editor operations or scene writes.
- B owns audio generation exclusively; Tools/Audio cinematic tools/content, ArtSource/Audio cinematic masters/stems, runtime RadioController.cs / RadioLibrary.cs / RadioPresenter.cs and new audio components, new Editor/CinematicAudioAssets.cs integration helper, audio tests and docs/CINEMATIC_AUDIO_REPORT.md. No shared Editor invocation, scene, prefab or mixer saves; provide root a Configure method and staged accepted asset list. Use local waveform output and actual bounded Seed calls, never unsupported sound-effect prose spoken as audio.
- C owns Tools/Blender/CinematicFleet, new ArtSource/Blender and Exports/CinematicFleet, new Editor/CinematicFleetLayout.cs helper, optional new runtime initialization helper, layout tests and docs/CINEMATIC_FLEET_REPORT.md. No modification of shared simulation/mission/render files. Blender background process permitted, never overwrite interactive user scene.
- Root owns final import, new scene, Mixer, Timeline, shared event wiring, integration builder, integrated tests/player verification, STATUS/ENVIRONMENT/ASSET_LICENSES/acceptance report.

## Decisions

20×25×2 equals 1000, conflicting with explicit 2000 identities. Use 20×25×4, with 20×25 face facing the fixed combat-start droplet position. Tune physical layer separation for near-cubic bounds; never scale ship models/root. Blender remains source; JSON export plus shared low-detail preview instances. Existing stable IDs remain unchanged. Set initial fleet transforms at initialization/restart, never each frame; normal and skipped narrative share mission combat entry.

## Integration order

1. Preserve baseline and extend existing event payload/source emissions.
2. Parallel camera, actual audio production, Blender layout/export.
3. Root compiles, executes Editor helpers on safe copy, imports local clips, saves own Mixer/Timeline.
4. Actual current-version EditMode/PlayMode checks, complete 2000-ship rendered player, pause/skip/reset/origin/late explosion interruption checks.
5. Save evidence, listening page and accurate generated/mixed/listened/runtime-verified status. Subjective listening remains pending if no audio perception is available.
