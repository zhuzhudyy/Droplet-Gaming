# Acceptance and regression tests

## Current-version verification policy - 2026-09-29

Latest playable and default enabled build scene: `Assets/_Project/Scenes/FleetAssault_SeedAudio.unity` (2000 ships). Unity Editor API set it as the one enabled `EditorBuildSettings` scene; preceding `FleetAssault_CinematicAudio_Cubic.unity` remains listed but disabled. The released Seed player is `Builds/Windows-SeedAudio-20260929/DropletGaming.exe`, built with an explicit one-scene list. Rebuild through `DropletPrototype/Seed Audio/3 Build Windows` in Unity Editor or the current default Build Settings.

For Seed audio changes, run the focused `SeedAudioAuthoringTests` (EditMode), `SeedAudioRuntimeTests`, `SeedAudioIntegratedSceneTests`, `CinematicAudioTests` for shared world sources, and `SeedAudioValidationIteratorTests` (PlayMode), followed by a rendered full-2000-ship Seed player route. Test menu/ready, all four act music and cabin selections, direct combat, pause position/resume, skip, flight cruise/boost and edge cues, real penetration/reactor/delayed blast/interrupted communications, radio ducking, world source count ≤12, results and three continuous restarts. Verify decoded WAVs, hashes, independent source variants, long-loop seams, clipping and subtitle timing separately. The final 2026-09-29 run has EditMode **3/3**, runtime PlayMode **6/6**, scene integration PlayMode **3/3**, sustained-world PlayMode **6/6**, iterator **1/1**, and 1920×1080 release player **34 passed / 0 failed / 3 manual**; details and first-failure repair evidence are under `verification/SeedAudio-20260929/`.

The three manual gates remain a natural 90-minute incomplete and victory route, human listening to the finished music/ambience/voices/radio/loop seams, and natural four-act playback followed by actual keyboard/mouse collision. The player validator checks four-act routing at selected cue indices and an accelerated incomplete result; do not describe those as natural full-length play or subjective acceptance. Six representative sound categories were approved by the user, but their approval is not individual audition of the 131 final variants.

Use small fixtures only for focused diagnosis, never as a substitute for the saved full-fleet player. Run tests relevant to changed shared behavior and broader current-version checks at release. Historical cinematic/Enhanced fixtures remain available for targeted regression work; a broad unfiltered Test Runner can include them, so choose filters intentionally. Do not routinely rebuild or replay historical players.

Retention target: current `Windows-SeedAudio-20260929` player, preceding full `Windows-CinematicAudio-20260919` rollback, and compact `DropletPrototype-v0.2.2-Windows.zip`. Preserve source models, audio caches, GUIDs, test evidence and referenced authoring scenes. Eleven older generated player directories were identified for cleanup after path checks, but automatic review rejected the recursive deletion; all remain, and no directory deletion is claimed. They are not current acceptance targets. On 2026-10-03, the separate obsolete tracked NarrativeCombat release ZIP was removed and the current Seed ZIP archived for GitHub Release distribution. See `verification/VersionCleanup-20261003/REPORT.md` and the historical `verification/VersionCleanup-20260919/REPORT.md`. Packaging-only tasks verify archive contents against the existing tested player; do not routinely rebuild or rerun historical suites.


Status: specification only; every test below is initially NOT RUN. Use the actual Unity Editor, Test Framework, player build, and Blender export tools where required. References for Unity testing and physics are in IMPLEMENTATION_PLAN.md [S4, S5, S9].

A tool being unavailable is a limitation to record, not a passing result. Save actual test output paths, Unity logs, hardware, and reproduction steps in STATUS.md or a linked report. Do not invent screenshots or timing numbers.

## Test categories

Use EditMode tests for pure scoring/state logic, configuration validation, layout marker/type checks, and safe importer behavior. Use PlayMode/physics tests for actual collision queries, target events, restart, and integration. Use the standalone player for input, rendering, audio, scene startup, and measured performance. Keep editor code out of runtime assemblies.

## Functional and collision tests

| ID | Stage | Scenario | Expected outcome |
|---|---|---|---|
| BASE-01 | G00 | Open with recorded Editor and packages | Valid project; actual baseline compile result recorded |
| SCENE-01 | G01 | Open scene without Play | Player, ten targets, camera, and lighting already exist |
| SCENE-02 | G01 | Run setup twice | Same generated objects; no duplicates or unrelated deletions |
| SCENE-03 | G01 | Close and reopen saved scene | Intended objects and references persist |
| MOVE-01 | G02 | Cruise, boost, release, and brake | Speed transitions match settings and return to the correct mode |
| MOVE-02 | G02 | Render at 30, 60, and 144 FPS where possible | No unintended frame-rate-dependent travel for a fixed simulation input sequence |
| MOVE-03 | G02 | Pause, lose focus, and resume | No stuck boost; cursor/input state restored |
| HIT-01 | G03 | Traverse one normal ship | One destruction event |
| HIT-02 | G03 | Cross a thin target with both endpoints outside it | Hit registers at maximum configured boost |
| HIT-03 | G03 | Pass through several aligned ships in one simulation step | Every intersected ship destroyed once |
| HIT-04 | G03 | Hit a ship with several child colliders | One ship event and one score award |
| HIT-05 | G03 | Revisit a destroyed ship | No new hit, score, or effect event |
| HIT-06 | G03 | Start a test step overlapping a valid target | Explicit overlap policy resolves without missed or duplicate events |
| HIT-07 | G03 | More colliders than the initial query buffer size | Retry/fallback finds all valid targets; none silently dropped |
| HIT-08 | G03 | Bend a path around a non-intersected target | No false hit along the straight start/end chord |
| HIT-09 | G03 | Travel through debris and effect volumes | No target count change and no blocking |
| HIT-10 | G03 | Zero displacement | No invalid normalization; overlap policy remains well-defined |
| HIT-11 | G03/G04 | Teleport/reset across a target line | No attacks along the reset displacement |
| HIT-12 | G03 | Speed at 4x normal configured boost in a stress scene | Detector still accounts for all supplied swept segments |
| LOOP-01 | G04 | Destroy all mission targets | Exactly one completed result |
| LOOP-02 | G04 | Let timer expire with targets left | Exactly one incomplete result |
| LOOP-03 | G04 | Last kill on the timer's final simulation step | Documented tie rule; no duplicate/conflicting results |
| LOOP-04 | G04 | Pause for a while | Mission and combo timers do not advance |
| LOOP-05 | G04 | Restart after pause, failure, and completion | Fresh timer/score/targets/input/camera; time scale restored |
| LOOP-06 | G04 | Repeat restart ten times | No duplicated event subscribers, stale targets, or growing transient state |
| LOOP-07 | G04 | Trigger arena recovery | Safe reposition; no phantom collision path or permanent loss of control |
| LOOP-08 | G04 | Launch initial standalone build | Same playable graybox loop without the Editor |
| ART-01 | G05 | Import calibration assets | Size, +Z forward/+Y up, pivot, normals, and references correct |
| ART-02 | G05 | Replace ship and player visuals | Existing movement/hit/state tests still pass |
| ART-03 | G05 | Reimport modified FBX | Stable gameplay roots and component references |
| FX-01 | G06 | Destroy many ships rapidly | Bounded effects/debris and responsive play |
| FX-02 | G06 | Disable effects or exhaust their pool budget | Kill/score/mission results remain correct |
| FX-03 | G06 | Restart with active effects | Effects are cleared/reset and cannot leak into a new run |
| LAYOUT-01 | G07 | Import marker layout | Interactive count, poses, and prefab types match source |
| LAYOUT-02 | G07 | Import same layout twice | Same count, stable identities, no duplicates |
| LAYOUT-03 | G07 | Unknown type or duplicate ID | Actionable validation error before partial scene mutation |
| LAYOUT-04 | G07 | Imported markers under transformed parents | Resolved scene poses match intended reference layout |
| LAYOUT-05 | G07 | Add unrelated hand-authored content then regenerate | Unrelated content preserved |
| LAYOUT-06 | G07 | Reopen saved fleet scene without Play | Interactive fleet visible; decorative objects excluded from target count |
| PRESENT-01 | G08 | Different supported display resolutions | Readable HUD and target arrows, correct cursor behavior |
| PRESENT-02 | G08 | Reduce shake/motion effects | Comfort settings respected without gameplay changes |
| BUILD-01 | G09 | Launch final player outside Editor | Correct first scene, controls, materials, audio, and results |
| BUILD-02 | G09 | Run on machine without Blender | Exported game assets load; no Blender runtime dependency |

## Performance protocol

Record OS, CPU, GPU, RAM, screen resolution, graphics settings, Editor version, build configuration, target count, effect caps, and test route. Test a standalone build as well as the Editor. If a development/profiling build is used, identify it; do not present its numbers as a shipping-build measurement without qualification.

The initial goal is 1080p at approximately 60 FPS on the agreed target PC. It is a target, not a promise. Measure frame-time distribution and spikes during multi-ship hits, active effects, allocation behavior, temporary-object counts, and memory across restarts—not only a quiet-scene average FPS.

Test at 10 graybox targets, then the authored 30–60 interactive targets, then an optional larger stress configuration. A decorative fleet is not equivalent to the same number of interactive physics targets; report them separately. Document changed quality settings when comparing tests.

Investigate the measured bottleneck before introducing ECS, jobs, custom rendering, or a new engine architecture. Candidate low-complexity fixes include shared materials, simpler distant models, capped/pool-reused effects, fewer expensive lights, no per-frame reflection capture, and avoiding unnecessary per-frame target searches. Re-run correctness tests after optimization.

## Release gate

G05–G09实际执行结果映射：校准报告 `verification/G05-G09/calibration-unity.txt`；最终真实Unity结果 `release-editor.json`(15/15)、`release-playmode.json`(27/27)，此前NUnit XML也保留。FleetAssetTests覆盖ART/LAYOUT资产契约与保存重复导入；PresentationRegressionTests覆盖新模型下实际40目标、FX禁用/容量/异常隔离/暂停/3次重开/瞬移尾迹；既有碰撞和任务测试原样保留。BUILD与性能为独立玩家CLI实际渲染采样，说明/局限在PERFORMANCE_G09.md。物理键盘、个人听感和舒适度未用自动检查替代。

A build can be described as tested only with actual results. Mandatory blockers include missed/double-counted hits, scene startup failure, missing required references, broken restart, gameplay affected by missing VFX, and corrupted/import-duplicated fleets. Performance failure against the chosen target requires a measured report and a scope/quality decision, not an invented success.

User acceptance remains necessary for flight feel, scene readability, and camera comfort; automated tests cannot establish those aesthetic judgments.
