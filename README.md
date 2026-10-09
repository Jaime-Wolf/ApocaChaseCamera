# ApocaChaseCamera 0.2.4 — steadier driving camera and instrument HUD

A steadier third-person driving camera for Apocalypter, inspired by the chase-camera feel of Mad Max (2015).

Vertical smoothing softens bumps, and the view stays level when the vehicle pitches or rolls. Height lag is limited on steep climbs and drops so the camera does not remain far below or above the vehicle. Heading smoothing makes turns gentler. Mouse look responds immediately and can gently return behind the car after a delay. Height, distance, side offset, and viewing angle are adjustable in the MODS menu. There is no speed-dependent FOV effect.

Version 0.1.1 adds direct terrain-height checks and road-slope probes for ground clearance, including positions that ordinary collision casts miss. Ground clearance brings the camera closer to the vehicle instead of adding an artificial upward lift. Camera look direction survives floating-origin shifts, frame stalls, pauses, settings menus, and focus interruptions. Recenter motion now has its own smoothing setting, eases in, and is limited to 120 degrees per second.

Version 0.1.2 adds a compact driving display above the inventory slots: speed in km/h, engine RPM with a gauge that turns red near the limiter, and current gear / total forward gears (for example, `3 / 5`). Neutral displays `N`, and reverse displays `R`. The display reads the vehicle's actual speed, engine RPM, and transmission, including changes to its gear list. It uses the game's font and text effects and follows the visible inventory slots, including expanded slots. It appears while driving in third person and hides in first person, on foot, and during menus or aiming views.

Version 0.1.3 gives the HUD a rusted, bolted metal backing and separate dark gauge housings with inventory-style rims. It reuses the game's loaded inventory artwork, with sliced borders to preserve the rivets when resized. The existing placement, readings, font, RPM warning, size and height controls stay the same. No extracted game textures are included in the download.

Version 0.1.4 fixes weapon icons appearing in the HUD: item-slot textures change with the equipped weapon, so they are no longer used. Only the static rusty inventory plate is reused. Speed and RPM now have brass semicircular instrument faces, tick marks and live needles, with digital readings beneath them. The speed needle spans 0–240 km/h; the number still shows actual speed above that range. The RPM needle uses the current engine's rev limit, with a red zone and warning near redline. Gear has its own circular instrument rim. The default display is slightly taller to fit these faces and remains above the inventory with the same size and height controls.

## Changes in 0.2.4

Addresses three issues from the full-folder review while retaining the user-confirmed 0.2.3 crosshair auto-recenter correction:

- Native mouse-look caches now support the game's separate PlayerCameraHolder as well as direct eye targets. Each uses its own parent's coordinate frame. The holder follows passive recenter so new mouse input or returning to its view cannot restore stale angles. The player's body and steering targets are excluded. The native vertical cache stores actual local pitch; only new MouseY input is inverted.
- Camera discovery also checks the current vehicle seat when the native eye has already been reparented there, with a bounded, ownership-checked named fallback. Loading or refreshing while seated no longer requires finding the eye under its on-foot holder.
- Vehicles without the expected controller retry HUD discovery every half second instead of searching their hierarchy every frame. Changing vehicles resets the delay immediately; a late controller can still recover.

The cursor-time correction, camera ownership guards, compass, cutaway, zoom, collision and smoothing behavior are retained. Apocaplayer and Apocasetter are unchanged. Production compiles without warnings; 3,425 regression checks, 1,962 cross-mod checks and 47 installed-interface contracts pass. Tests covering each of the three issues fail against the preserved 0.2.3 source. Unity/physics/input services are simulated; 0.2.4 still needs the focused in-game checks in Verification/IN-GAME-CHECKLIST.md. These checks do not constitute a runtime performance benchmark.

The working pre-fix 0.2.3 ZIP remains in Releases/PluginFolder and has a verified backup. Source and verification material stay in the separate developer archive.

## Changes in 0.2.3

Revises the Apocaplayer auto-recenter correction after the user reported that 0.2.2 still left the crosshair at the last look direction. Eye aim now follows the valid cruising pose immediately before the cursor projection requests it, including repeated pose requests in the same frame. This repairs a native aim write that happens after the chase pose was first cached. The eye correction also runs when the native Look controller is inactive, in an empty state, or unavailable; finding matching active input actions no longer gates the correction. Existing eye-only input caches in normal and mouse-steering states are kept ready for resumed input.

The previous takeover, compass, zoom, cutaway, smoothing and HUD changes are retained. This applies only to passive auto-recenter in the unarmed ChaseCamera driving view. A short `Driving aim recenter active` log entry records the native controller state without logging every frame. Apocaplayer and Apocasetter files are unchanged.

Production builds without warnings; all 3,419 existing regressions, 1,920 cross-mod simulations and 47 installed-interface contracts pass. New dormant-controller and late-native-write tests fail under their respective previous behavior. The exact runtime reason for the user's 0.2.2 failure was not observable in its log. The user subsequently confirmed that 0.2.3 fixes the crosshair auto-recenter issue in game; remaining review findings are addressed in 0.2.4.

## Changes in 0.2.2

Fixes the dynamic crosshair staying aimed in the last manually viewed direction while ChaseCamera automatically returns behind the vehicle with Apocaplayer enabled. Auto-recenter now brings the native eye's aim along with the visible camera and updates its mouse-look angle caches, so the cursor and interaction direction agree and new mouse input does not snap back to an old angle. The eye's position, player body and vehicle steering are left intact.

This applies only during passive auto-recenter in the unarmed ChaseCamera driving view, including following turns after settling. It respects the configured delay and disabled recenter setting; first person, weapons, on-foot views and blocked input retain their own control. The previous compatibility, compass, zoom, cutaway, smoothing and HUD fixes are retained. The user confirmed that the 0.2.1 bridge takes over correctly in game; the user subsequently reported this 0.2.2 recenter correction unsuccessful in game. See 0.2.3.

The production build compiles without warnings. All 3,419 existing regressions and 1,904 cross-mod simulation checks pass, plus 47 installed Apocaplayer/native aim interface contracts. The new aim regression fails against the prior 0.2.1 source as expected. These are automated checks, not a live Unity play-test.

## Changes in 0.2.1

Fixes the Apocaplayer compatibility bridge disabling itself on the game's Unity runtime. The 0.2.0 log reported “Operation is not supported on this platform”: its field accessors called Reflection.Emit.DynamicMethod, whose constructors are unsupported in the shipped game corlib. Accessors now use the Cecil backend supplied by BepInEx, explicitly avoiding that runtime API and preserving typed access without per-frame boxing. Bridge failures now include the full exception trace for diagnosis.

Production builds without warnings. The 3,419 regression checks, 85 cross-mod simulations and 42 installed interface checks pass. An added production check rejects the unsupported call in the original 0.2.0 DLL. The user subsequently confirmed in-game takeover with 0.2.1; see 0.2.2 for the remaining recenter correction. Check the startup log for `Apocaplayer compatibility patch ready`.

## Changes in 0.2.0

Adds an optional Apocaplayer compatibility patch entirely inside ChaseCamera. Unarmed vehicle cruising uses ChaseCamera's steady view, with Apocaplayer's cursor projected from that same pose. Drawing a weapon, aiming and binoculars retain Apocaplayer's camera controls. Its on-foot view is unchanged.

- Both Apocaplayer cutaway toggles, cave state, shader availability and current camera ownership are respected. Terrain, upward ground surfaces, named road meshes and blockers without fadeable renderers keep collision protection.
- Mouse-wheel zoom while cruising adjusts ChaseCamera's distance and saves it after the wheel rests. Armed views retain Apocaplayer's own zoom.
- Hidden orbit offsets are suppressed while ChaseCamera controls the view. Camera ownership changes transfer the visible orientation, clear stale pose caches and recover interrupted projection changes.
- The compass follows the active rendered heading, including Apocaplayer's armed and non-scoped aiming views. Scoped and binocular views retain native special-view behavior.
- Blocker renderer discovery is cached, with live visibility checks and bounded refreshes instead of repeated hierarchy-array searches every tenth of a second.

This is a locally built test version. Production compilation, regression checks and cross-mod simulations pass; actual gameplay compatibility still needs testing. Apocaplayer and Apocasetter files are unchanged.

## Changes in 0.1.9

Both normal driving views now use the native on-foot compass rotation actions. ChaseCam supplies the visible camera heading during orbit, recentering and smoothed turns; first-person driving supplies the active camera's horizontal heading. The native compass keeps its own axis/layout settings instead of being replaced with a separately constructed rotation. The shared native heading variable is restored after each draw, and the adapter works when the vehicle has disabled the on-foot compass FSM without changing its enabled state. On foot, menus and special views retain native control.

3,418 simulated checks pass, including native action reuse, layout preservation, repeated callbacks and view switches. The production build compiles without warnings. The user confirmed that this version fixes the compass issue in game on October 9, 2026.

## Changes in 0.1.8

Corrects the compass handoff when switching between first-person and third-person driving. Both views now use their actual visible heading, so facing the same direction keeps the same bearing across repeated switches. The compass is synchronized just before the UI renders, after the game's camera-switch and compass controllers run. Mouse orbit, recentering and smoothed turns still follow ChaseCam; normal first-person driving reads the actual camera instead of a stale camera-holder direction. On-foot and special aiming views retain native behavior.

The earlier 0.1.7 fix was reported incorrect in game. This revision passes 3,405 simulated checks, including repeated and late-frame switches, but needs a new in-game test.

## Changes in 0.1.7

The stock compass now follows the actual ChaseCam view during mouse orbit, automatic recentering and smoothed turns. Looking East, recentering North, then looking East again no longer accumulates an incorrect compass offset. First person, aiming, menus, vehicle exit and collision fallback retain the native compass. Camera and player transforms are left untouched.

## Changes in 0.1.6

- Live viewing-angle changes stay within the camera's vertical limits. An interrupted render is restored before the next frame, preventing an old view from carrying forward.
- Vehicle cameras added or replaced after entry are rediscovered. Stable vehicle bindings reuse their hierarchy and health-variable references.
- Replaced or reparented inventory UI rebuilds the driving HUD and releases its owned sprite. Inventory graphics and bounds are cached, while the display continues following the inventory's position.
- Clear camera frames use four collision queries instead of six. Obstructed checks stop when the camera cannot move farther inward. Collision buffers retain grown capacity up to 4,096 hits, with a complete-results fallback beyond that limit.
- Gear text is formatted only when its displayed values change, and static gauge artwork is cached between needle updates. The optional Apocaplayer bridge validates its interface, uses typed accessors and disables itself with one warning if that interface fails.
- Camera tuning, the rusty gauge appearance, and saved settings are retained. In-game comfort and frame-time improvement still require a drive test.

## Changes in 0.1.5

- Optional Apocaplayer discovery now searches quietly once per scene when absent. It no longer performs Harmony's missing-type search/logging every two seconds. A loaded compatible optional mod still receives the same driving-camera bridge.
- Camera wall, road and overlap queries reuse 128-result buffers during ordinary driving. A full buffer falls back to the complete query so crowded armor/parts cannot hide a wall or ground surface. Terrain clearance, immediate obstacle contraction and smooth recovery retain their behavior.
- Camera position, smoothing, mouse look, recentering, stock vehicle support and rusty speed/RPM/gear gauges keep their tuning. Saved settings are preserved. ApocaDustStorm is a separate optional mod.
- These changes address identified sources of periodic work and allocations. Actual driving stutter/performance still requires comparison in the game.

## Install and use

Requires BepInEx 5 and Apocasetter, already used by ApocaGearbox. ApocaGearbox, Apocaplayer and ApocaDustStorm are not required.

1. Close the game.
2. Copy the ZIP's `ApocaChaseCamera` folder into `BepInEx/plugins` in the Apocalypter game folder, merging the existing mod folder. When updating from an earlier version, replace the existing `ApocaChaseCamera.dll`; do not keep another copy elsewhere in plugins. Existing settings are retained, and new settings receive their defaults.
3. Start the game and enter a vehicle. Use the game's normal **Change Camera** control to select third person.
4. Open **MODS → ApocaChaseCamera** to adjust the view. Settings apply to every supported vehicle, without installing a part on the car.

Under **Driving HUD**, use **Enabled**, **Size**, and **Height above inventory** to adjust the display. Defaults show it just above the inventory at normal size. The HUD does not require ApocaGearbox or a part installed on the vehicle.

Defaults: 1.7 m additional height, 6.5 m distance, 8° downward view, 0.35 seconds of bump smoothing, and 0.25 seconds of turn smoothing. For a larger truck, try Height 2.2–2.8 and Distance 7–9. More bump smoothing softens movement further but follows hills more slowly.

New comfort defaults: **Maximum height lag 0.6 m**, **Ground clearance 0.6 m** plus the clipping radius, and **Recenter smoothing 0.7 seconds**. Lower maximum height lag if the camera still follows hills too slowly; increase recenter smoothing for a slower return behind the car.

Walls and obstacles push the camera inward immediately, with a gentle return afterward. If the selected camera position cannot clear a solid object, that frame uses the game's view.

To remove, close the game and delete only `BepInEx/plugins/ApocaChaseCamera`. Optional settings live in `BepInEx/config/local.apocalypter.chasecamera.cfg`.

## Vehicle support and other camera mods

The mod binds to the vehicle you enter through the standard Player/InCar and DriveTrigger camera setup. All 14 stock vehicle prefab setups were inspected and checked: Duke, Crossbreed, Vulture, TinyTyrant, Junker, Rustliner, Outrider, Rustallion, Rustcargo, PipeRat, Poloska, PigPen, Rustchief, and Halfbreed. Their shared settings work across vehicles; larger bodies may need a higher or more distant view.

Modded cars using the same driving setup should work. Custom camera or seating systems need testing.

The optional runtime bridge targets the installed Apocaplayer 2.3.2 interface. It validates reflected members before applying its own hooks and yields safely with one warning if the interface changes. No Apocaplayer assembly or source is included in the download. Other camera systems and future Apocaplayer interfaces need compatibility testing.

## Validation and limitations

Built without compiler warnings against this installation's real Unity, NWH, PlayMaker, BepInEx, Harmony and Apocasetter dependencies. 3,419 regression checks pass across 14 stock vehicle anchor fixtures. Another 1,920 checks pass using unchanged Apocaplayer ThirdPerson source and the installed Harmony 2.9.0 library, with Unity, physics, UI and cutaway services simulated. The compiled DLL also passes 47 installed Apocaplayer/native aim interface checks. Three negative controls confirm that cursor-pose, aiming-compass and road-collision tests detect the corresponding broken behavior.

These checks cover camera ownership, cursor projection, armed states, cutaway toggles, cave exclusion, zoom persistence, hidden orbit state, scene refresh, failure cleanup and bounded renderer scans. They do not measure real PhysX geometry, shader compositing or frame times. See Verification.md and Verification/IN-GAME-CHECKLIST.md for remaining in-game tests.

In a 3 Hz vertical-bump simulation, the default smoothing retained about 15% of the bump amplitude at 30, 60, and 144 FPS after the elevation fixes. These are simulation results, not measured in-game road tests. The user reported the driving HUD working in-game and identified the item-icon problem in 0.1.3. The user subsequently confirmed the corrected gauge appearance working. The user confirmed the 0.1.9 compass fix in game; real obstacle clearance, the actual MODS sliders, and driving comfort need further play-testing. This is not an exact reproduction of Mad Max's camera.

Designed for existing saves: the mod changes the rendered driving view, adds its own HUD, and saves its own settings, without writing vehicle tuning or save data.

The install ZIP contains only the plugin and README. Source and verification files are in the separate `ApocaChaseCamera-0.2.4-Developer.zip`; do not extract that archive into the game. Build with `Source/build.ps1 -GameDir <game folder>`.


Archive layout: the repackaged installation ZIP opens directly to the mod's folder. Copy that folder into BepInEx/plugins. For an older ZIP that opens to a BepInEx folder, merge that folder into the game folder instead. Source and verification files are supplied separately.

## Permissions

Copyright (c) 2026 Jaime-Wolf. You may install and use the official mod, inspect its source, and build/test it locally for evaluation and compatibility development. Separate compatibility patches are allowed when they depend on the original mod and do not bundle its DLL or source.

Prior permission from Jaime-Wolf is required to copy or adapt this source into another project, bundle or redistribute the mod, distribute a modified version, or reupload it. All other rights are reserved. These permissions apply to original ApocaChaseCamera code and documentation; game and third-party components retain their owners' terms. Full permissions are in LICENSE.md in the source repository. [Repository and releases](https://github.com/Jaime-Wolf/ApocaChaseCamera).

