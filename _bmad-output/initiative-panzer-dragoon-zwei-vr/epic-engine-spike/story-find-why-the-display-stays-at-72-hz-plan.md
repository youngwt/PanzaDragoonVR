---
title: 'Find why the display stays at 72 Hz'
type: 'chore'
ticket: '10'
created: '2026-10-16'
status: done
baseline_revision: 'f152b6a8fd75ffc312b7d31e37f1f4afd213a46a'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'none'
review_source: 'auto'
lenses_ran: []
review_loop_iteration: 0
context:
  - '{project-root}/tools/README.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/deferred-work.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The spec asks for a steady 90 Hz. With Meta's plugin the headset offers four rates and accepts the app's request for 90, yet the display stays at 72. The headset's own home screen also asks for 90 and stays at 72, and the kernel only ever sets 72. The cause is unknown.

**Approach:** Investigate on the headset in a fixed order, from the cheapest test to the most invasive, and stop at the first cause found. When the cause is a setting, manifest entry or export option the project controls, apply the fix and show 90 Hz in the readout. Otherwise write the cause down with its evidence for the author to decide on.

## Boundaries & Constraints

**Always:**
- Each step's evidence (command, output, what it shows) goes in Implementation Notes as it is gathered.
- Headset state changed for a test, such as a system property, is put back afterwards unless it is the fix and the author agrees to keep it.
- Device checks use `adb` on the host, screen capture and the `PDZ-FPS` log read while the app runs.
- The author is asked before anything is changed in the headset's own settings menus or anything is installed from the store.

**Never:**
- No change to the scene, the asset loader or the launch report.
- No factory reset, firmware change or developer setting that affects other apps without the author's agreement.
- No lowering of the spec's 90 Hz target here; that is the author's call if no fix is found.

</frozen-after-approval>

## Code Map

- `game/Main.cs` `RequestRefreshRate()` / `RequestRefreshRateAgain()` -- read the offered rates, request 90, read back the rate in use; the readout reports "Asked for 90 Hz; running at 72 Hz (4 rates offered)".
- `game/export_presets.cfg` -- Meta plugin on, `meta_xr_features/quest_3_support=true`, hand tracking optional. The merged manifest declares `com.oculus.supportedDevices` = `quest2|quest3|quest3s|questpro`.
- Evidence so far (2026-10-09, `deferred-work.md`): `VrRuntimeClient: SetDisplayRefreshRate: app has requested a new display refresh rate: 90`; `ShellApp: Requesting refresh rate change from 72 to 90`; kernel `dsi_display_set_mode ... fps=72` only; battery saver off; worn and unworn alike.
- `adb` runs on the host through `flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb"`; the app launches without a controller.

## Tasks & Acceptance

**Execution:**
- [x] Step 1 -- read what the system reports: `dumpsys display`, `dumpsys SurfaceFlinger` and the VR runtime's display services for supported modes and any refresh-rate cap or policy; all `getprop` and `settings` keys mentioning refresh, fps, display or power.
- [x] Step 2 -- ask the author to check in the headset whether any other app runs above 72 Hz (Meta's OVR Metrics Tool shows it, if installed) and whether the headset's settings have a refresh-rate or power option.
- [x] Step 3 -- try other rates from the app (80, 120) and a request after a delay, to tell "90 refused" from "any change refused".
- [x] Step 4 -- only with the author's agreement: try a developer property or setting that forces the rate, and put it back after.
- [x] Apply the fix if one is found in the project's control; otherwise write up the cause with evidence.
- [x] `tools/README.md` -- the outcome, in "Where this stands on the Quest 3".

**Acceptance Criteria:**
- Given the fix applied, when the app runs for a minute in the headset, then the readout shows target 90 and `adb logcat -s PDZ-FPS` shows averages of at least 89; or, given no fix in the project's control, the cause and its evidence are written down and the author has decided what to do about the target.

## Implementation Notes

Done in the main conversation, not by a subagent: steps 2 and 4 need the author's eyes and consent mid-way.

**Step 1, 2026-10-16 (read-only, over adb):**
- `dumpsys display` lists the panel's hardware modes: 4128x2208 at 90 Hz (default mode), a reduced 3104x1664 at 90 Hz, and many high-rate entries (186-207 Hz). The display is currently running a mode at `renderFrameRate 72.00001`.
- `dumpsys SurfaceFlinger`: `activeMode=72.00 Hz`, VSYNC period 13888888 ns (72 Hz), `debugDisplayModeSetByBackdoor=false`.
- `settings` (global, secure, system): nothing mentions refresh rate; `low_power=0`.
- `getprop`: no refresh-rate override property (`debug.oculus.refreshRate` absent).
- Reading: the panel itself can run 90 Hz and nothing at the Android settings level forces 72. The cap, if there is one, is inside the VR runtime's own policy, which these reports do not show.

**Step 2 (part), 2026-10-16:** with Meta's OVR Metrics Tool installed by the author, the home screen and other apps also read 72 FPS. The cap is headset-wide, not specific to this app.

**Step 3, 2026-10-16:** a throwaway build asking for 120 Hz logged `app has requested a new display refresh rate: 120`, the readout said "Asked for 120 Hz; running at 72 Hz (4 rates offered)", and Vsync stayed at 72.00 Hz. The 80 Hz build was installed but its launch was held back and gave no reading. Both throwaway builds were removed and the normal build reinstalled. Reading: the headset refuses any change from 72, for every app, so nothing in this project's code or export settings is the cause.

**Step 2 (settings), 2026-10-16:** the author found one related setting in the headset, a low graphics quality option, and turned it off. The home screen and apps still ran at 72 Hz afterwards.

**USB, 2026-10-16:** the author unplugged the headset; the home screen still read 72 Hz. The cable is not the cause.

**Step 4, 2026-10-16 (author agreed):** `adb shell setprop debug.oculus.refreshRate 90` (the property was empty before). The app then logged `status="Target 90 Hz (4 rates offered)" target=90`, Vsync read `2 x 11.11 ms (90.00 Hz)`, and SurfaceFlinger reported `activeMode=90.00 Hz`. The property was cleared straight afterwards (a first attempt to clear it failed on shell quoting; the second left it empty, confirmed with `getprop`). It is not persistent and a restart also clears it.

**Finding:** the headset runs every app at 72 Hz and ignores apps' requests for other rates (90 and 120 tried), yet the panel runs 90 Hz as soon as the developer property asks for it. So the cap is the headset's default policy, not this project. The property is a developer lever on one headset; it is not something a player's headset would have, so it does not meet the spec's 90 Hz for players. Why the runtime ignores apps' requests on this headset is still unknown.

**Power saving, 2026-10-16:** research pointed at Meta's Power Saving Mode (Settings, Device, Power), reworked and on by default after a Horizon OS update. The author turned battery saving off and restarted the headset; the home screen still read 72 Hz. Not the cause, or not the only one.

**Logs after power saving off and a restart, 2026-10-17:** Horizon OS build `ro.vros.build.version` 207, Android build `UP1A.231005.007.A1`, `debug.oculus.refreshRate` empty. For the first time without the developer property, the display switched: the shell (process 3073) requested 90, the runtime raised `XR_TYPE_EVENT_DATA_DISPLAY_REFRESH_RATE_CHANGED_FB fromRate 72 -> toRate 90`, `TrackingFidelityService: active refresh rate changed: 72 -> 90 Hz`, and Vsync read 90.00 Hz. On 2026-10-09 the same shell request had left Vsync at 72. A Guardian dialog then appeared, the shell requested 72 and the display went back to 72; our app's launch was held behind the dialog, so its own request has not yet been seen to take effect.

**Result, 2026-10-17:** with the Guardian prompt cleared, the author saw the readout at 90 Hz, and the log read `target=90 avg=90.3` and `target=90 avg=89.9`, with 6 slow frames in about 2,600. **Cause: the headset's battery saving mode, which holds every app at 72 Hz; turning it off took effect only after a restart.** Nothing in the project needed changing. `tools/README.md` now says so. The Guardian dialog also drops the display to 72 Hz while it shows.

## Plan Change Log

## Review Triage Log

No review lens was run: the change is two documentation edits (this plan and one paragraph of `tools/README.md`), with no code. The author ended the session after the result.

## Verification

**Manual checks (if no CLI):**
- In the headset: the readout reads "Target 90 Hz" and measures about 90.
