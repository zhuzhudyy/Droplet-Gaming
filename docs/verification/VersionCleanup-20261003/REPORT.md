# Version cleanup and remote delivery — 2026-10-03

User requested obsolete version cleanup and pushing the latest project to the existing remote repository. Read STATUS.md and the current TEST_PLAN.md retention policy; confirmed Unity 6000.5.10f1 and installed packages unchanged.

## Completed

- Removed the tracked obsolete `Releases/Droplet-Gaming-Windows-NarrativeCombat.zip` through `git rm`; 56,067,483 bytes removed from the working tree. Git history retains recovery. No history rewrite or force push.
- Archived the current `Builds/Windows-SeedAudio-20260929` player: 197 files, 353,405,270 uncompressed bytes, 176,407,481 ZIP bytes. ZIP CRC and every entry's SHA-256 match the source; see `archive-verification.json`.
- Updated repository/download instructions to the Seed scene and current player. Full player distribution uses GitHub Release `seed-audio-20260929`; the local ZIP is excluded from normal Git.
- Preserved preceding full `Builds/Windows-CinematicAudio-20260919`, compact `Builds/DropletPrototype-v0.2.2-Windows.zip`, referenced scenes, source assets, .meta GUIDs and unrelated root screenshot.
- Git's global proxy points to an inactive port 7890. Remote access works through the active system proxy on port 7994, supplied per command without altering global settings.

## Blocked cleanup

Automatic approval review rejected the path-checked recursive deletion of 11 obsolete generated build directories with `blocked by policy`. The command did not execute. All 11 remain on disk, totaling **1,629,439,691 bytes**; exact paths and sizes are in `pending-deletions.json`. Recovered bytes from these directories: **0**. No alternative deletion route attempted.

The prior 2026-09-19 and 2026-09-29 blocked-deletion records are historical and remain accurate. Removal of the separate tracked ZIP succeeded; it does not imply the directories were removed.

## Verification scope

This task changes packaging, documentation and version-control delivery. It includes existing uncommitted Seed/Cinematic/Enhanced implementation and asset work in the remote delivery without changing its gameplay behavior. Existing 2026-09-29 Unity and player verification remains the release evidence: 34 player automatic checks passed, zero failures and three manual gates outstanding. This task did not rerun Unity compilation, EditMode/PlayMode, Blender, player rendering or natural 90-minute play; archive checking is not new gameplay acceptance.

Pre-push inspection checked 4,943 staged files: 2,104 text files scanned, no matches to locally configured cloud credential values or known GitHub/AWS token formats, no individual Git file at or over 100 MiB and no missing .meta companion for staged Unity assets. Source takes referenced by shipped Seed, Cinematic and Enhanced audio are explicitly retained despite the general cloud-experiment ignore rule. Code and edited delivery documentation pass focused Git whitespace checking. The broad check also reports Unity-authored YAML empty-field trailing spaces and historical downloaded documentation whitespace; those artifacts are preserved without manual YAML edits. See `prepush-check.json`.

Manual verification: download the release ZIP, verify SHA-256, fully extract, launch `Windows-SeedAudio-20260929/DropletGaming.exe`. Complete the three manual routes in `docs/SEED_AUDIO_ACCEPTANCE.md` separately. Next work remains those acceptance routes and the blocked directory cleanup; no new gameplay milestone was started.
