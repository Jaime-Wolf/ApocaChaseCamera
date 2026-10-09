# ApocaChaseCamera 0.1.9 — steadier driving camera and instrument HUD

A steadier third-person driving camera for Apocalypter, inspired by the chase-camera feel of Mad Max (2015).

Vertical smoothing softens bumps, and the view stays level when the vehicle pitches or rolls. Height lag is limited on steep climbs and drops so the camera does not remain far below or above the vehicle. Heading smoothing makes turns gentler. Mouse look responds immediately and can gently return behind the car after a delay. Height, distance, side offset, and viewing angle are adjustable in the MODS menu. There is no speed-dependent FOV effect.

Version 0.1.1 adds direct terrain-height checks and road-slope probes for ground clearance, including positions that ordinary collision casts miss. Ground clearance brings the camera closer to the vehicle instead of adding an artificial upward lift. Camera look direction survives floating-origin shifts, frame stalls, pauses, settings menus, and focus interruptions. Recenter motion now has its own smoothing setting, eases in, and is limited to 120 degrees per second.

Version 0.1.2 adds a compact driving display above the inventory slots: speed in km/h, engine RPM with a gauge that turns red near the limiter, and current gear / total forward gears (for example, `3 / 5`). Neutral displays `N`, and reverse displays `R`. The display reads the vehicle's actual speed, engine RPM, and transmission, including changes to its gear list. It uses the game's font and text effects and follows the visible inventory slots, including expanded slots. It appears while driving in third person and hides in first person, on foot, and during menus or aiming views.

Version 0.1.3 gives the HUD a rusted, bolted metal backing and separate dark gauge housings with inventory-style rims. It reuses the game's loaded inventory artwork, with sliced borders to preserve the rivets when resized. The existing placement, readings, font, RPM warning, size and height controls stay the same. No extracted game textures are included in the download.

Version 0.1.4 fixes weapon icons appearing in the HUD: item-slot textures change with the equipped weapon, so they are no longer used. Only the static rusty inventory plate is reused. Speed and RPM now have brass semicircular instrument faces, tick marks and live needles, with digital readings beneath them. The speed needle spans 0–240 km/h; the number still shows actual speed above that range. The RPM needle uses the current engine's rev limit, with a red zone and warning near redline. Gear has its own circular instrument rim. The default display is slightly taller to fit these faces and remains above the inventory with the same size and height controls.

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

An optional runtime bridge targets Apocaplayer's current ThirdPerson interface. It uses this mod's cruising view while driving unarmed. Drawing a weapon, aiming, or using binoculars hands the view back to Apocaplayer. Its on-foot camera stays under its control. No Apocaplayer files or code are included or edited. The bridge was checked against an interface double; compatibility with an actual installed copy still needs an in-game test. Other camera mods may compete for the same view.

## Validation and limitations

Built successfully against this installation's Unity, NWH Vehicle Physics, PlayMaker, BepInEx, Harmony, and Apocasetter assemblies. 3,418 total checks passed using real stock prefab anchors with simulated rendering and physics. The existing 2,137 checks include 121 HUD checks and cover vehicle binding, camera switching, roll/pitch isolation, obstacle contraction and recovery, live height changes, pause/focus/input guards, death, teleport resets, and the optional bridge interface. Elevation regressions simulate 12 m/s climbs and drops at 30, 60, and 144 FPS, terrain surfaces missed by collision casts, and sloped road meshes. Recenter regressions check look direction across interruptions, gradual return, and return speed at different frame rates. HUD checks cover live readings, neutral/reverse and forward gear counts, RPM sizing and redline color, native style, weapon-texture isolation, live dial geometry, expanded inventory placement, adjustable size/height, screen bounds, visibility, sprite cleanup, and leaving vehicle physics unchanged. The additional 312 checks cover live pitch limits, missed render callbacks, collision-query budgets and retained buffer growth, late/replaced cameras, live safety guards, changed optional interfaces, persistent bridge failures, replaced inventory, bounds-cache cadence, in-place gear edits, delayed UI fonts and static gauge artwork reuse.

In a 3 Hz vertical-bump simulation, the default smoothing retained about 15% of the bump amplitude at 30, 60, and 144 FPS after the elevation fixes. These are simulation results, not measured in-game road tests. The user reported the driving HUD working in-game and identified the item-icon problem in 0.1.3. The user subsequently confirmed the corrected gauge appearance working. The user confirmed the 0.1.9 compass fix in game; real obstacle clearance, the actual MODS sliders, and driving comfort need further play-testing. This is not an exact reproduction of Mad Max's camera.

Designed for existing saves: the mod changes the rendered driving view, adds its own HUD, and saves its own settings, without writing vehicle tuning or save data.

The install ZIP contains only the plugin and README. Source and verification files are in the separate `ApocaChaseCamera-0.1.9-Developer.zip`; do not extract that archive into the game. Build with `Source/build.ps1 -GameDir <game folder>`.


Archive layout: the repackaged installation ZIP opens directly to the mod's folder. Copy that folder into BepInEx/plugins. For an older ZIP that opens to a BepInEx folder, merge that folder into the game folder instead. Source and verification files are supplied separately.

## Permissions

Copyright (c) 2026 Jaime-Wolf. You may install and use the official mod, inspect its source, and build/test it locally for evaluation and compatibility development. Separate compatibility patches are allowed when they depend on the original mod and do not bundle its DLL or source.

Prior permission from Jaime-Wolf is required to copy or adapt this source into another project, bundle or redistribute the mod, distribute a modified version, or reupload it. All other rights are reserved. These permissions apply to original ApocaChaseCamera code and documentation; game and third-party components retain their owners' terms. Full permissions are in LICENSE.md in the source repository. [Repository and releases](https://github.com/Jaime-Wolf/ApocaChaseCamera).

