# ApocaChaseCamera 0.2.4 compatibility test

Build against your installed game references using `Source/build.ps1 -GameDir 'C:\path\to\Apocalypter'`.
Replace the single installed ChaseCamera DLL while the game is closed. Keep Apocaplayer unchanged.
The startup log should contain `Apocaplayer compatibility patch ready`. After auto-recenter first activates in a vehicle, check for `Driving aim recenter active` with the eye name, native Look state and matching camera-action count (eye or native holder). A zero count must not prevent eye recentering. A changed core interface should produce one disabled-bridge warning and keep Apocaplayer's own view.

1. With both mods enabled, enter a parked vehicle, switch to third person, and leave weapons stowed. Move the mouse horizontally and vertically. The cursor/dot should stay on the point reached by the game's eye ray, and the compass should match the visible heading.
2. With weapons stowed, look left, right, up and down, then stop moving the mouse. After the configured delay, the cursor and actual aim should return smoothly with the camera. Resume mouse movement midway and after settling: no snap back to an old angle. Try both normal driving and mouse steering, a tilted parked vehicle, and a turn after settling. Disable recenter to confirm the aim stays under manual control.
3. Orbit, draw a weapon, aim, stop aiming, and stow it. Repeat while holding Left Alt and with optional middle-mouse observation enabled. Check for unexpected rotation, doubled input, cursor drift, and compass jumps. Binocular and scoped views should retain Apocaplayer's behavior. Check the compass while aiming a non-scoped weapon and orbiting: it should still follow the rendered third-person heading.
4. Scroll while cruising. ChaseCamera's visible distance should change and its Distance setting should persist after one second. Apocaplayer's driving distance must stay unchanged. Scroll with a weapon drawn: Apocaplayer's usual zoom should work instead. A live ChaseCamera Distance slider edit should override a pending scroll save.
5. Test all four combinations of Apocaplayer's main culling and vehicle culling toggles. Only both enabled should request its cutaway effect. When either is off, walls should shorten the camera distance. With both on, fadeable walls should allow the chosen distance; collider-only blockers, terrain and rendered road surfaces must still keep the camera safe.
6. With both culling toggles on, drive into a cave and back out if a suitable entrance is available. No cutaway or bright light leaks should appear inside. Compare FPS and the BepInEx log with vehicle culling off to check for repeated effect recreation or rendering errors.
7. Drive over slopes and bumps, near walls, under roofs, and in vehicles with different body sizes. Check ground clearance, collision fallback, and camera recovery.
8. Switch first/third person repeatedly; leave/re-enter the vehicle; change vehicles; open/close ESC and MODS menus; lose/restore focus; reload a scene. Cursor and compass should recover without stale poses or error spam.
9. Disable ChaseCamera: Apocaplayer's own camera, orbit and scroll should work. Disable Apocaplayer: ChaseCamera should still work on the game's native driving camera. Verify normal on-foot movement, aiming, interaction, and climbing remain unchanged.

Record installed versions, vehicle, settings, reproduction steps, and the relevant `BepInEx/LogOutput.log` entries for any failure. Automated checks simulate Unity/physics/effect dependencies; they do not replace these game tests.

## Focused 0.2.4 checks

- With Apocaplayer enabled and weapons stowed, look in several directions, let recenter finish and then move the mouse a little. Check for yaw/pitch snaps, retained cursor alignment and a matching compass. Try normal and mouse steering, first/third person switching and a vehicle parked on a slope.
- Load a save while seated, then switch views and exit/re-enter. Confirm ChaseCamera takes over without needing to leave the vehicle first. Its native camera must still work with Apocaplayer disabled.
- If a modded vehicle lacks NWH VehicleController, its HUD should hide without warning spam. Switching to a supported vehicle should restore the HUD immediately.
- The 0.2.3 ZIP is preserved for rollback. This build has not been installed or published by the agent. Actual Unity frame time, visuals, interactions and input transitions require this live check.
