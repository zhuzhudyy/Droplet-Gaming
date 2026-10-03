# Version cleanup - 2026-09-19

## Applied
- Unity Editor API successfully set EditorBuildSettings.scenes to only FleetAssault_Enhanced.unity. The open Enhanced scene was left untouched.
- AGENTS.md, TEST_PLAN.md and START_HERE.md now specify current-version verification and opt-in historical checks.
- Corrected stale voice authorization blocker in START_HERE: live key works, full English library remains incomplete.

## Retained
- Current Windows-Enhanced-20260919 player.
- Previous Windows-NarrativeCombat full player for rollback.
- DropletPrototype-v0.2.2-Windows.zip compact early-release reference.
- Historical authored scenes: referenced by editor generation pipelines, mesh calibration and targeted regression fixtures. No scene, prefab, GUID, source art or audio was removed.

## Blocked deletion
Nine superseded generated build directories total 1,200,529,955 bytes (1.118 GiB). Automatic approval review rejected recursive removal with `blocked by policy`; the command did not execute. Actual recovered space: 0 bytes. Exact pending paths are in pending-deletions.json. No alternate deletion route attempted.

## Checks
Actual Unity Editor 6000.5.10f1 connected to this project, stopped and not compiling. Editor API execution succeeded without diagnostics. Current scene/build entry verified. Remaining player executables checked on disk; ZIP integrity checked. No gameplay/code/assets changed, so no historical gameplay reruns or new player build were performed. This cleanup does not constitute new gameplay acceptance.
