# G05–G09 independent review

Review owner: regression_review sub-agent. Unity Editor operations and final evidence remain coordinated by the main agent. No pass is inferred from another agent's completion message.

## Findings before integration

- Existing `SceneAuthoringTests` recursively asserts exactly five materials below `Art/Materials`. Production materials need a separate folder or a carefully scoped preservation assertion; adding fleet materials there would produce a real regression failure. Reported to main agent.
- `ShipTarget.Destroyed` originally invokes all listeners directly. A faulty presentation listener can abort later mission listeners or the remaining multi-target sweep. Main agent agreed to isolate listener failures without changing destruction identity or hit traversal. A regression test will deliberately throw in a presentation listener and require remaining scoring and targets to resolve.
- Asset importer must validate every marker before changing any target, including duplicate IDs, unknown type, and non-unit inherited scale. Checking only during placement risks a partially updated scene. Tests include an invalid late marker after a valid changed pose.

## Integration review and tests

- Main-agent source fixes inspected directly: production materials use `Art/FleetMaterials`; `ShipTarget.Notify` isolates each event subscriber; duplicate-ID errors now name the offending marker; `FleetAssaultBuilder` records the existing owned hierarchy for Undo and assigns `DropletTrail.effects`, so the live Off quality setting also controls the trail.
- `Assets/_Project/Tests/EditMode/FleetAssetTests.cs`: twelve parameter-expanded cases. Parent/world transforms, stable instance identity, manual objects, duplicate IDs (including the same numeric ID across types), unknown types, inherited negative/non-unit scale, stale target removal; the actual forty-marker FBX is compared against independently exported source positions/forward/up/scale, then instantiated as actual production prefabs and saved/reopened. A copy of the formal scene is reopened twice for target/reference/material checks. Four real imported models are checked against Blender dimensions and usable mesh normals.
- `Assets/_Project/Tests/PlayMode/PresentationRegressionTests.cs`: nine parameter-expanded cases. Off/zero/exhausted budgets with real compound-thin-target sweeps and scoring; deliberate presentation subscriber failure; active-effect pause freeze; switching effects Off; three resets and repeated bindings; teleport/restart trail cleanup; the actual forty-target scene completing and restarting three times through the existing motor and detector.
- Temporary fixture cleanup deletes only the test-created `Assets/_Project/Tests/G09TemporaryFixture` folder, with an ownership flag. Existing folders cause refusal, not deletion. The test runner's unchanged untitled default Camera/Light scene is replaced with the fixture; any extra or altered content causes refusal. Named scenes use additive loading, and the user's actual scene is restored by Test Runner after the run.

## Issues found while running in Unity

- Initial compile rejected eight `GetInstanceID()` calls as obsolete errors in Unity 6000.5.10f1. Tests now use `GetEntityId()` with inferred typed collections. No assertions were removed.
- The initial new EditMode cases all failed during fixture setup because `EditorSceneManager.NewScene(Additive)` rejects the test runner's untitled scene. A subsequent `SceneManager.CreateScene` attempt was rejected because that API is PlayMode-only. The runner's actual injected scene was then identified as the unchanged Main Camera/Directional Light default. Fixture setup now uses the supported Editor Single-scene creation only for that verified default (names, component types, positions, rotations and scales) or an empty shell. Cleanup restores a clean default runner scene after removing owned temporary assets, avoiding dirty-shell residue that initially failed the older scene-authoring checks. The final full rerun passed all fifteen EditMode cases. No assertion was removed or test skipped.

## Actual verification

All Unity state operations are executed by the main agent. The review agent independently parsed the saved raw XML:

| Evidence | Actual result |
|---|---|
| `baseline-editmode.xml` | 3/3 passed, zero failed/skipped |
| `baseline-playmode.xml` | 18/18 passed, zero failed/skipped |
| `editmode-integrated.xml` | **15/15 passed, zero failed/skipped**, 7.8209927 seconds; all twelve new asset/import cases and the three existing checks together |
| `playmode-integrated-27.xml` | **27/27 passed, zero failed/skipped**, 1.6544611 seconds; all nine new presentation/real-fleet cases and eighteen existing checks together |

No test pass is inferred from compilation or source inspection. The regression suite is verified at this integration point; later changes must retain the relevant gates.

The review agent actually opened and observed `game-first-camera.png`: ships and the droplet are visible without pink or entirely black materials. Strong white reflection bands converge at the droplet's near tip; a side view was requested to distinguish intentional reflections from possible visible shading seams. This camera-only image contains no HUD, so it is not UI acceptance. No audio listening, standalone launch or performance measurement was performed by this review agent.

## Final regression and standalone evidence review

The review agent independently parsed the later `final-editor.xml` (**15/15 passed**, zero failed/skipped, 8.0584139 seconds) and `final-playmode.xml` (**27/27 passed**, zero failed/skipped, 1.6259678 seconds). These are separate actual Unity test runs.

The standalone evidence below is specifically the **v0.2.0** player run in `PlayerBenchmark/20260907-121841-799/benchmark.json`, started 2026-09-07 12:18:41 UTC. It does not by itself verify the subsequent v0.2.1 build. `player-benchmark.log` identifies the v0.2.0 executable data directory, Unity 6000.5.10f1, the RTX 4060 Laptop GPU, Direct3D12 and driver 32.0.16.1062. It records completed checks and normal shutdown. A D3D12 info-queue query message (`0x80004002`) occurs during startup; graphics initialization and all twenty benchmark checks subsequently complete successfully, so it was not a startup blocker in this run.

Conditions recorded by the player: Windows 11 build 26200, Ryzen 9 7940HX, RTX 4060 Laptop GPU with 7956 MB reported graphics memory, 15575 MB system memory, **1920 × 1080**, PC quality, High effect quality, VSync disabled, unlimited target frame rate, non-development WindowsPlayer, no batch mode. The scripted route uses the existing mission/motor, and excludes screenshot waits and pause waits from the timed flight phases. It is a deterministic verification route, not a human flight-feel test.

| Phase | Samples / focused frames | Measured duration | Mean frame interval | p95 | Maximum | Effects / audio peak |
|---|---:|---:|---:|---:|---:|---:|
| Normal flight | 7383 / 7383 | 8.000014 s | 1.083572 ms | 2.050900 ms | 5.467701 ms | 4 / 2 |
| Dense penetrations | 6755 / 6755 | 8.000821 s | 1.184429 ms | 2.133999 ms | 5.790505 ms | 16 / 6 |
| Three restart frames | 3 / 3 | 0.003813 s | 1.271067 ms | 1.644700 ms | 1.644700 ms | 0 / 0 |

All sampled frames report `Application.isFocused=true`; focused-frame counts equal sample counts in every phase. The means were independently cross-checked as measured seconds × 1000 / samples. The p95/max values above are the saved distributions. Inspection of `FleetBenchmark.Summarize` confirms sorted nearest-rank percentiles; individual frame sample arrays are not persisted, so those percentiles cannot be independently recalculated from the JSON alone. The restart p95 is just the maximum of three samples, not a statistically strong tail estimate.

These are Unity frame intervals from `Time.unscaledDeltaTime`, with an available Main Thread recorder as a separate CPU-side diagnostic. They are **not GPU timings or the monitor's actual displayed refresh FPS**. All captured intervals in these short phases are below the approximately 16.67 ms budget for 60 FPS. This supports the local machine's 1080p/PC/High target for this measured route; it does not establish long-session stability, thermal behavior, all possible camera angles or performance on other hardware. Startup and screenshot stalls are outside the reported timed flight phases.

| Snapshot after phase | Unity allocated memory | Unity reserved memory | Mono used memory | Pool objects |
|---|---:|---:|---:|---:|
| Normal flight | 178.417 MiB | 269.020 MiB | 4.105 MiB | 441 |
| Dense penetrations | 178.961 MiB | 269.020 MiB | 4.234 MiB | 441 |
| Three restart frames | 179.057 MiB | 269.020 MiB | 4.215 MiB | 441 |

Memory figures are Unity profiler snapshots, not process working-set or GPU-memory peaks. Allocated memory rises about 655.7 KiB between the first and final snapshot; this brief sample cannot establish or rule out a long-term memory leak. **Per-frame GC allocation was not measured:** the player reports `gcRecorderAvailable=false` and zero samples in every phase. The zero-valued empty GC summaries must not be presented as zero allocation.

The effect peak reaches its configured High cap of 16; audio peaks at 6, below the configured cap of 8. The prewarmed pool remains 441 objects through all three resets. All twenty player checks passed, including a single actual swept movement destroying three practice-lane ships, pause freezing gameplay and effects, victory at 40/40 with one results transition, three restarts each restoring forty live/actual scene targets and an unchanged pool, and 41 safe reposition checks with zero path-damage failures. These checks establish the recorded scripted standalone flow; they do not replace keyboard/mouse interaction or listening acceptance.

### Images actually observed

The review agent opened the actual v0.2.0 standalone `03-impact-inspection-flash.png`, `03-impact-inspection-wreck.png`, and `04-results.png` with the image viewer:

- The flash frame shows a localized warm impact at the ship's rear contact region, directional orange sparks and a blue droplet trail. The effect remains localized, with no full-screen washout or missing-material pink. HUD values show three destroyed targets, 600 points and a ×3 chain.
- The subsequent wreck frame shows separate displaced hull-shaped pieces retaining the frigate silhouette, with the intact bright hull replaced by darker wreck material. Large pieces remain distinguishable. These two images deliberately use the documented temporary inspection camera; they do not establish how noticeable the brief effect feels from the normal chase camera.
- The results image clearly shows the victory title, 21110 points, 40/40 cleared, 28.14 seconds elapsed, and restart/settings/quit controls. English glyphs are legible and the panel text/buttons do not overlap. The droplet remains visible against the dark background. Button operation is established by the other flow evidence only where actually exercised, not by this static image.

The distant target's orange label has weaker contrast against the bright planet in the inspection shots; it is a minor readability observation, not a demonstrated gameplay blocker. This review did not listen to audio or personally operate the standalone controls. The images prove observed rendered states, while the XML and player checks supply the separate behavioral evidence.

## v0.2.1 independent player recheck

The review agent directly read `windows-final-build.json`, `player-final-benchmark.log`, and `PlayerBenchmark/20260907-122720-511/benchmark.json`. The build summary identifies **Windows-v0.2.1-G09**, StandaloneWindows64, Succeeded, zero errors, one warning, 118856072 bytes and 195 files. The warning concerns the optional Pipeline tooling configuration being absent in the player. The log independently identifies the v0.2.1 player data directory and records the benchmark completing with `checks=True`. This is evidence of the actual final player running, beyond build success alone. The later v0.2.2 UI readability update must retain its own build/run evidence and is not covered by this v0.2.1 result.

The v0.2.1 run again uses 1920 × 1080, PC/High, Direct3D12 on the same recorded Ryzen/RTX 4060 machine, VSync off, unlimited target frame rate, and a non-development standalone player. All twenty checks pass: forty targets, three ships swept in one movement, pause, 40/40 victory with one results transition, three restarts restoring all forty targets and the 441-object pool, and 41 reposition checks with zero damage failures.

| Phase | Samples / focused | Duration | Mean interval | p95 | Maximum | Effects / audio peak |
|---|---:|---:|---:|---:|---:|---:|
| Normal flight | 7062 / 7062 | 8.001458 s | 1.133030 ms | 2.173895 ms | 8.276098 ms | 4 / 2 |
| Dense penetrations | 6779 / 6779 | 8.001077 s | 1.180274 ms | 2.185105 ms | 6.279903 ms | 16 / 6 |
| Three restart frames | 3 / 3 | 0.003442 s | 1.147234 ms | 1.545699 ms | 1.545699 ms | 0 / 0 |

Focus counts again equal every sample count, and mean intervals were cross-checked against duration/sample count. All measured intervals remain below 16.67 ms, supporting approximately 60 FPS for this short local route. The same limits apply: frame intervals are not GPU timings or display refresh FPS; three restart frames are a small sample; screenshot/startup waits are excluded; no long-session stability is established. Per-frame GC allocation remains **unavailable, not zero allocation**.

Unity allocated-memory snapshots are 178.564, 179.827 and 180.234 MiB; reserved memory remains 269.020 MiB; Mono-used memory is 4.164, 4.223 and 4.434 MiB. The allocation snapshot grows approximately 1710.3 KiB across the sampled phases, while pool objects remain 441. This is not a process/GPU peak-memory measurement or sufficient evidence of long-term leak behavior.

The review agent actually viewed v0.2.1's `02-flight.png`, impact flash/wreck images and `04-results.png`, plus `final-player-settings.png` and `final-player-ready-native.png`. The settings screenshot visibly shows 1.00 sensitivity, 65.00° FOV, 0.65 volume, explicit `[OFF] Invert Y`, `[ON] Reduce camera motion`, selected High effects and a labeled Reset Defaults button. The native Ready screenshot shows the 240-second/40-target briefing and readable start/settings/quit controls. These native window images are separate from the 1080p benchmark captures; they establish the displayed state, not an independent replay of the main agent's Reset Defaults click.

The v0.2.1 flash remains local and warm, wreck pieces are separated and recognizable, and the final results display remains legible at 40/40 with 21110 points and 28.14 seconds elapsed. The normal-flight capture shows visible angular/knot-like shaping in the near-camera blue trail; this is retained as a minor art limitation rather than a collision or restart failure. Target labels remained relatively dark in these v0.2.1 images. The main agent has prepared a separate gold-text/background-contrast fix for v0.2.2; this review does not pre-approve its unobserved final render. No source files or Unity state were changed by this recheck.

## v0.2.2 final release verification

The final review directly checked `windows-v0.2.2-build.json`, `player-v0.2.2-benchmark.log`, `PlayerBenchmark/20260907-123115-512/benchmark.json`, `release-editor.json`, and `release-playmode.json`. Earlier version evidence remains separate and has not been used as a substitute for the final run.

- **Build:** Windows-v0.2.2-G09, StandaloneWindows64, Succeeded, 195 files, 118856072 bytes, zero errors, one optional Pipeline-tooling runtime configuration warning. Build duration is 16.237 seconds.
- **Unity release regressions:** every result row was checked, not just the summary: EditMode **15/15 Passed**, PlayMode **27/27 Passed**, zero failed, skipped or inconclusive rows.
- **Actual final standalone:** the player log identifies the v0.2.2 data directory and records benchmark completion with `checks=True`; its JSON reports all **20/20 check records passed**. Seven of those records check screenshot output, so they are not represented as seven additional gameplay tests. The remaining records retain the forty-target scene, three-ship sweep, pause, once-only victory, three restored fleets/pools, and safe reposition evidence described above.

| Final player phase | Samples / focused | Duration | Mean interval | p95 | Maximum | Effects / audio peak |
|---|---:|---:|---:|---:|---:|---:|
| Normal flight | 7132 / 7132 | 8.000118 s | 1.121722 ms | 2.125702 ms | 5.989803 ms | 4 / 2 |
| Dense penetrations | 6587 / 6587 | 8.000588 s | 1.214603 ms | 2.191596 ms | 6.690802 ms | 16 / 6 |
| Three restart frames | 3 / 3 | 0.003981 s | 1.326868 ms | 1.715200 ms | 1.715200 ms | 0 / 0 |

All samples are focused, and means again agree with duration/sample arithmetic. The final non-development WindowsPlayer runs at **1920 × 1080, PC/High, Direct3D12** on the recorded Ryzen 9 7940HX / RTX 4060 Laptop GPU. These short-route frame intervals support the local approximately 60 FPS target; every measured interval is below 16.67 ms. They remain distinct from GPU timings and the display's delivered refresh rate, and do not establish long-term thermal/stability performance. The three restart samples are a functional/per-frame check rather than a strong percentile study.

Final Unity allocated-memory snapshots are **179.147 → 179.694 → 180.040 MiB**; reserved memory is **269.020 → 271.020 → 271.020 MiB**; Mono used is **4.113 → 4.473 → 4.648 MiB**. The effect pool remains **441 objects** in every phase. These are snapshots rather than peak process/GPU memory or proof of leak-free long sessions. The GC recorder remains unavailable with zero samples, therefore **per-frame GC allocation is unmeasured**.

The review agent actually opened final `02-flight.png` and `03-impact-inspection-flash.png`. The previously dark target text is now bright orange, with a dark backing separating the marker and distance text from the bright planet. The behind-target indication is clearly legible against space; the on-target label is clearly legible over the planet. This confirms the contrast fix in final player pixels, not merely in code. The local warm impact and readable HUD remain present. The earlier angular near-camera trail appearance persists as the documented minor art limitation; it is not hidden or claimed corrected. No Unity state, sources, builds or other documents were changed by this final review.
