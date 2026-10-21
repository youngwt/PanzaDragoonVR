- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-toolchain-and-a-c-vr-scene-on-the-headset-plan.md`
  summary: Scaffold the Godot 4.7 C# project in `game/`, add the one command that builds and installs the APK on the Quest 3, and prove C# runs on the headset with a cube rotated by C# code and a label showing the .NET version. This is the rest of ticket 1.1; the ticket is not done until it is built.
  evidence: Split at the author's request on 2026-10-07 because the full plan was about 2,100 tokens against a 1,600 guideline. Decisions already made by the author for this part - package name `io.github.youngwt.panzadragoonvr`; app name `Panzer Dragoon Zwei VR`. Planned approach - no OpenXR vendor plugin and no Gradle build to start (fallback is plugin 5.1.0 with Gradle), Compatibility renderer, export preset keys `xr_features/xr_mode=1`, `architectures/arm64-v8a=true`, `screen/immersive_mode=true`, project settings `xr/openxr/enabled` and `xr/shaders/enabled`, headless `--import`, `--build-solutions --quit`, `--export-debug`, install through host `adb` via `flatpak-spawn --host`. Known risk - open Godot issues #99021 and #115678 report C# startup crashes on Android.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-decide-the-manifest-format-and-folder-layout-plan.md`
  summary: Stories 1.3 to 1.7 and the epic's wording predate the placeholder decision and need a pass with bmad-ticket - the game now ships built-in placeholders, there are no `test.` IDs, and story 1.4 proves external loading by supplying own images under `e03.floor` and `e03.canopy`, which needs a small PC script to write a manifest with exact byte sizes.
  evidence: The asset contract agreed on 2026-10-08 says the game always runs on placeholders and replaces them ID by ID. The epic file still quotes CAP-2 as "ships with no assets", and the entries for 1.4, 1.5 and 1.7 describe loading and a missing-file report without the fallback. The build workflow may not edit ticket files.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-test-scene-at-90-hz-plan.md`
  summary: The app runs at 72 Hz on the Quest 3, not the spec's 90 Hz; find why the headset returns an empty list of refresh rates and get 90 Hz requested.
  evidence: Headset screenshot of 2026-10-08 reads "Running at 72 Hz; 90 Hz could not be set", which the code shows only when `GetAvailableDisplayRefreshRates()` returns nothing, while `DisplayRefreshRate` returned 72 in the same call. Godot 4.7.2 has the refresh-rate extension built in with no project setting to enable it. Untested leads - ask again once the session is focused; the Meta vendor plugin; an Android manifest entry. The author accepted 72 Hz for now.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-test-scene-at-90-hz-plan.md`
  summary: No log output from the app reaches `adb logcat` on the Quest 3, so frame-rate figures and crash details cannot be read from the PC.
  evidence: After a run of about a minute on 2026-10-08, the device log held system lines about process 18919 (the app) but none written by it - no `PDZ-FPS` lines and none under Godot's `godot` tag. The log buffer reached back before the launch. `tools/build-quest.sh` relies on logcat for its crash capture, so that would be empty too.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-test-scene-at-90-hz-plan.md`
  summary: The Quest 3 display stays at 72 Hz although the app now asks for 90 Hz and the headset accepts the request; the spec's 90 Hz target is unmet. Supersedes the earlier entry about an empty rate list.
  evidence: On 2026-10-09, with the Meta vendor plugin, the headset offered four rates and logged `SetDisplayRefreshRate: app has requested a new display refresh rate: 90`, but every one of about 1,500 one-second readings was 72 and the kernel only logged `dsi_display_set_mode ... fps=72`. Worn and unworn gave the same result. The headset's own shell also requested 90 and stayed at 72. Battery saver was off. Untested leads - whether any store app reaches 90 on this headset; a headset display setting; Meta manifest metadata a store app would carry.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-test-scene-at-90-hz-plan.md`
  summary: The last changes to the frame-rate readout (reporting the rate in use, and pausing while the headset is not showing the app) have not been seen running on the Quest 3.
  evidence: They pass 32 unit tests and build cleanly, but on 2026-10-09 a Guardian boundary prompt blocked the launch that would have verified them. Check that the readout reads "Asked for 90 Hz; running at 72 Hz", that `target=72` appears in `adb logcat -s PDZ-FPS`, and that taking the headset off and on logs paused and resumed without a slow frame.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-decide-the-standard-model-format-plan.md`
  summary: Story 1.7's model loader must switch off vertex colour as albedo on loaded materials, because the asset contract says the game ignores `COLOR_0` in format 1 and Godot turns it on automatically when a file carries it.
  evidence: Read in the Godot 4.7.2 glTF importer source during ticket 1.6 research; not run. The contract's promise that `COLOR_0` can be added later without raising `format` depends on it. Story 1.7's entry does not mention it.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-decide-the-standard-model-format-plan.md`
  summary: Nothing in the asset contract's Models section has been run in Godot or on the Quest 3; thirteen of its rules are marked provisional until model geometry is decoded.
  evidence: The section was written from Godot's documentation and source and the glTF specification. Saturn VDP1 behaviour behind the transparency, shading and facing rules was stated from memory and not re-checked. Story 1.7 is the first load of a model; the dragon epic is where the provisional rules get confirmed or changed.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-launch-report-and-fallback-plan.md`
  summary: In debug builds the head-locked frame-rate readout covers the lower part of the launch report board in the starting view; move one of them.
  evidence: Seen in a headset screen capture on 2026-10-14. Release builds do not show the readout, so players are unaffected; it gets in the way while testing.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-grey-cube-loaded-from-a-model-file-plan.md`
  summary: The earlier item asking story 1.7 to switch off vertex colour as albedo is dealt with - the game does switch it off - but Godot 4.7.2 turned out never to turn it on, so the switch is a guard only.
  evidence: Ticket 1.7 loaded five models carrying `COLOR_0` (textured unlit, untextured unlit, untextured lit, no material, three-component colours) with the game's switch disabled; every one rendered without the vertex colours. The asset contract's rule that the game ignores `COLOR_0` in format 1 holds either way.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-grey-cube-loaded-from-a-model-file-plan.md`
  summary: The earlier item that ticket 1.3's last readout changes had not been seen on the headset is closed; the 72 Hz item stays open.
  evidence: Headset screen captures on 2026-10-15 show the readout reading "Asked for 90 Hz; running at 72 Hz (4 rates offered)" and "Measured 72.0 Hz".
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-find-why-the-display-stays-at-72-hz-plan.md`
  summary: The 72 Hz item is closed - the cause was the headset's battery saving mode, which needed a restart after being turned off; the game now runs at 90 Hz.
  evidence: Ticket 1.10, 2026-10-17 - with battery saving off and the headset restarted, the readout showed target 90 and the log averaged 89.9 to 90.3 Hz. Players with battery saving on will get 72 Hz; worth a line in any player instructions.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md`
  summary: The first item, the rest of ticket 1.1 (project, build command and a C# scene in the headset), is closed; it was built under `plan-1-1-csharp-vr-scene-on-headset.md`.
  evidence: That plan records the launch confirmed in the headset; it was marked done when the epic closed on 2026-10-16.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md`
  summary: The item asking for a pass over stories 1.3 to 1.7 and the epic's wording after the placeholder decision is closed.
  evidence: The re-slice of 2026-10-13 reworded the epic and the stories (epic Notes); the epic file quotes the reworded CAP-2.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md`
  summary: The item saying no log output from the app reaches `adb logcat` is closed.
  evidence: Plan 1.3's notes of 2026-10-09 say the `PDZ-FPS` tag works; logcat showed `PDZ-FPS` and `PDZ-ASSETS` lines on the headset on 2026-10-16.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md`
  summary: The item about the frame-rate readout covering the launch report board is closed.
  evidence: Fixed in commit 0146440 (ticket 1.8); headset captures on 2026-10-16 show the readout below the board.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md`
  summary: The epic's remaining open items moved to `_bmad-output/backlog/` as tickets 1 to 10, including the provisional Models rules (story 8) and the battery-saving line for players (story 9).
  evidence: Engine spike closed on 2026-10-16; the author asked for the outstanding items in a backlog.
- source_plan: `_bmad-output/backlog/spike-move-off-net-9-before-its-support-ends-plan.md`
  summary: Move to Godot 4.8 stable and target `net10.0` on Android, so the APK leaves .NET 9, which gets no security fixes after 2026-11-10. The toolchain, the unit tests and the desktop build are already on .NET 10.
  evidence: Godot 4.7.2's prebuilt Android export templates need exactly `net9.0`; a `net10.0` project fails to export (godot#118989). The fix, godot#123738, merged to master on 2026-09-26 for 4.8, makes `net10.0` the minimum and moves Android to .NET 10; nothing shows it backported to 4.7.x. Godot 4.8 beta 1 came out on 2026-10-15; stable is expected in Q4 2026 and may land after 2026-11-10. Microsoft ends .NET 9 support on 2026-11-10. Unknown - whether the Meta vendor plugin 5.1.0 works with Godot 4.8. On 2026-10-16 the APK built by the .NET 10 SDK showed ".NET 9.0.21" in the headset.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/story-every-accepted-disc-image-layout-is-read-plan.md`
  summary: A `disc/` folder holding a cue sheet and the `.iso` it names (`FILE "game.iso" BINARY`, `TRACK 01 MODE1/2048`) counts as two disc images and extracts nothing, though the cue path could read it; decide whether an `.iso` named by the one cue sheet belongs to it.
  evidence: Review of ticket 2.4, 2026-10-17. The asset contract accepts "a .cue with the .bin files it names, or one .iso", so this follows the contract as written; the README now tells the player to keep only the `.iso`. Some dumps ship this way.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/story-extraction-runs-behind-a-progress-board-and-falls-back-on-fa-plan.md`
  summary: The launch report and progress board can stand behind the player: it is placed at a fixed spot ahead of the play area's origin, not ahead of where the headset faces when the app starts. Place it in front of the head's starting direction.
  evidence: The author, 2026-10-18, on the Quest 3 during ticket 2.9's damaged-image check: "the board is behind me when I view it, can you move it to the front in future?"
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool.md`
  summary: The item about the board standing behind the player moved into the extraction epic as story 2.13.
  evidence: The author asked for it as its own entry in the epic, 2026-10-18.
- source_plan: `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/story-the-launch-report-board-stands-in-front-of-the-player-plan.md`
  summary: The item about the board standing behind the player is closed; the board now stands in front of where the head faces once the headset tracks it, and again after a recentre.
  evidence: Built as story 2.13 in commit f615b97, marked done on 2026-10-21 (commit 8cc9fa1).
