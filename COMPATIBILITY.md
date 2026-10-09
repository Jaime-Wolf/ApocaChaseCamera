# Compatibility development

This repository contains ApocaChaseCamera's original source and simulated verification suite. It does not distribute Apocalypter, Unity, NWH Vehicle Physics, BepInEx, Harmony, Apocasetter or Apocaplayer assemblies or extracted game code/assets. Build against your own local game installation with Source/build.ps1. Install the compiled mod from this repository's Releases page.

The plugin GUID is local.apocalypter.chasecamera. BepInEx 5 and Apocasetter are required; ApocaGearbox, ApocaDustStorm and Apocaplayer are optional and independently maintained.

The driving camera reads the standard Player/InCar and DriveTrigger/3rdCamera hierarchy. It changes worldToCameraMatrix and cullingMatrix only during its selected camera's render, then resets them after rendering or an interrupted frame. It does not move native camera/player/vehicle transforms or change vehicle physics. Other render hooks should use the active rendered matrices rather than assume transform.position is the rendered eye position.

The optional Apocaplayer bridge delegates the unarmed cruising view while preserving weapon, aiming and binocular views. Its reflected members are validated and accessed through typed delegates; a persistent interface failure disables that bridge with one warning. Other custom camera systems need an in-game compatibility test.

Version 0.1.9 uses the installed Player/Compass UI state's native FloatMultiply and SetRotation actions for both normal driving views. It validates the three-action compass fps state and its y variable/world-axis interface, then temporarily supplies the camera bearing through the native yaw input. The native multiplier and axis settings remain authoritative; the shared yaw value is restored in a finally block. The adapter does not change the FSM enabled flag or native camera/player transforms. If the expected actions are missing or uninitialized, it yields without substituting a guessed rotation. Missing bindings retry once per second; stable bindings are cached. A successful native binding is recorded in the log.

Synchronization still runs before Canvas.willRenderCanvases geometry preparation, after the game's vehicle compass update. ChaseCam's once-per-frame pose is reused for the camera and compass. Normal first-person and unresolved collision fallback use the visible native camera's horizontal forward direction. On-foot, disabled and paused views yield to native behavior. Scoped/binocular views yield too; non-scoped Apocaplayer aiming keeps its actual third-person bearing. Custom matrix camera owners and replacement compass UI require live compatibility testing. Source/build.ps1 now references the locally installed Assembly-CSharp.dll to invoke native compass actions; no game assembly or implementation is redistributed.

The HUD reads live vehicle speed, RPM, rev limit and transmission gear. It follows the current inventory UI, rebinds replacements, and supports expanded inventory slots. Changes to native UI hierarchy or custom driving seats should be tested after scene loading, vehicle changes and UI replacement.

Run Verification/test.ps1 for the simulated regressions. These checks do not replace in-game tests of native rendering, PhysX geometry, third-party mods or performance.

Separate compatibility patches are permitted under LICENSE.md. They must depend on the original mod and must not bundle its source or DLL. Copying, adaptation for another project, redistribution and reuploads require Jaime-Wolf's prior permission.

## Apocaplayer adapter in 0.2.0

The bridge patches only its own compatibility methods into ThirdPerson.PreCull, ComputeView, Zoom and Tick at runtime. It synchronizes the private once-per-frame cursor cache as well as the published view, routes cruising wheel input to ChaseCamera, and suppresses hidden native orbit offsets while driving unarmed. Armed/aiming views stay with Apocaplayer and supply their computed rotation to the existing native compass adapter. No Apocaplayer or Apocasetter files are edited.

Cutaway requires both OcclusionPrototype and OcclusionInVehicle, an available effect, and Cave.Inside=false. Renderer ownership mirrors the external effect conservatively; renderer discovery is cached for ten seconds (or a root change), visibility is checked live, and the cache is bounded to 1,024 colliders. Ground-facing sweep hits, TerrainCollider and named road geometry stay solid. Road-name recognition is conservative; unlabelled ground still uses hit normals, terrain sampling and road-slope probes.

Reference source reviewed: https://github.com/DeonUrist/Apocaplayer/tree/623d02f6c0e9bcec9fd44b252526b3c18fc4d20b
Installed Apocaplayer interface contracts were checked directly against its DLL. Current Unity shader behavior, scene changes and other camera owners still require in-game tests. The separate test source is not redistributed.

Windows cross-mod checks: Verification/test-cross-mod.ps1 -ApocaplayerSource <separate source directory>. This uses unchanged ThirdPerson.cs and the installed Harmony library; Unity and supporting services remain simulated. The .NET 8 project from the proposal remains available as an alternative, but the local verification used the Windows runner. Production build and metadata checks use the installed game references and never redistribute them.

## Unity runtime correction in 0.2.1

The actual 0.2.0 game log showed the bridge disabled with NotSupportedException. The shipped game corlib's Reflection.Emit.DynamicMethod constructors call Unity.ThrowStub.ThrowNotSupportedException. Source field access now creates Cecil IL through MonoMod.Utils.DynamicMethodDefinition and explicitly selects DMDCecilGenerator, rather than the unsupported Reflection.Emit path. This uses MonoMod.Utils/Mono.Cecil from the installed BepInEx core; these DLLs are build references and are not bundled. Typed readers/writers still avoid per-frame boxing. Bridge error logs now include the complete exception.

Both Windows test runners use the installed Cecil backend. For the optional .NET 8 projects, set GameCore to a compatible BepInEx core directory containing MonoMod.Utils.dll and Mono.Cecil.dll; the supported local verification path is the Windows runner. Production metadata verification rejects direct Reflection.Emit calls and requires explicit Cecil generation. The old 0.2.0 DLL fails this new regression check as expected. In-game confirmation remains required.

## Aim during automatic recenter (0.2.2)

Apocaplayer's LateCrosshair projects a point on the native eye's ray. Sharing the chase view matrix alone cannot move that aim during passive recenter. ChaseCamera now synchronizes the eye rotation and the active native Player/Look FSM's MouseLook angle accumulators when auto-recenter is active and the cruising pose is valid. Only enabled actions targeting the actual eye are updated. The player/steering Direction target is excluded, and the camera position is unchanged. Local angles account for the eye's vehicle-seat parent; separate MouseY and combined-axis actions use their respective pitch signs. Private float writes use the same explicit BepInEx Cecil backend as the working 0.2.1 bridge, with cached typed delegates and bounded FSM discovery.

No recenter synchronization runs in first person, armed/special views, on foot, during paused/blocked input, or with recenter disabled. Missing or inactive native look states are left alone. No additional hooks or changes to Apocaplayer/Apocasetter are required. Production checks validate the installed native MouseLook field and axis contracts. Simulation passes do not establish live Unity behavior.

## Cursor-time aim synchronization (0.2.3)

The user tested the exact 0.2.2 DLL hash, confirmed in the current BepInEx log and installed file. The bridge initialized without an aim warning, but the cursor issue remained. Inspection of the installed Apocaplayer DLL confirms LateCrosshair calls ComputeView before reading the native eye position/forward vector. The prior simulation always ran native input before pose calculation and did not cover dormant look states, so it missed two ways the correction could be skipped or overwritten.

The corrected TryPose reapplies passive recenter aim after retrieving a valid pose, rather than only when BuildPose first computes the frame. This runs before Apocaplayer reads its cursor ray even if another native controller has since written the eye rotation. Eye correction is independent of Look FSM discovery, enabled state and matching active actions. When available, enabled eye-target MouseLook caches in the active state and standard Look/MouseSteering states are synchronized for resume. No player/steering targets are written. A cache writer failure warns once but no longer disables the eye correction. One brief recenter activation log per binding records controller state and matched actions. Discovery remains bounded and private writers remain cached typed Cecil delegates.

This does not establish which of the two conditions occurred in the user's game: 0.2.2 did not log those details. Both failure modes are reproduced and caught by separate negative controls. Live validation is pending; prior camera-owner and input guards remain in force. No third-party file, game dependency or installed configuration is changed by this build.

## Native holder, seated discovery and HUD retry fixes (0.2.4)

The user confirmed the 0.2.3 auto-recenter cursor correction in game. That correction still runs on every valid recenter TryPose, including a cached pose requested after a native aim overwrite. The compatibility hooks and their ownership guards are unchanged.

The full-folder review found that the native Look FSM targets PlayerCameraHolder, while the original test fixture targeted the eye directly. The stock holder is a separate scene root, not necessarily a player descendant. CameraBinding now caches that known holder; DrivingAim accepts only the actual eye or that exact holder when targeted by the player's own Look actions. The holder orientation and its input caches use its own parent frame, while cursor-time eye orientation remains authoritative even if the eye is still attached to it. Direction/player targets are excluded. Current, normal Look and MouseSteering states remain supported, including dormant controllers. Installed MouseLook IL confirms that GetYRotation(-1) negates the new input delta and returns the stored local angle; the prior test double inverted the stored angle incorrectly. The double and synchronization now follow the installed contract.

When the eye has moved to the vehicle seat before binding begins, discovery checks that seat and then an exact-name fallback restricted to the player/current seat hierarchy. Searches remain rate limited to half-second retries. Native/on-foot holder discovery remains supported. The HUD similarly negatively caches a missing VehicleController for half a second, but changing vehicles bypasses the old delay and a delayed controller can recover.

Tests use unchanged separately supplied Apocaplayer ThirdPerson.cs and real installed Harmony/Cecil, with handwritten Unity/physics/input services. They cover a scene-root and parented holder, attached and seat-reparented eyes, tilted parents, resumed input, player/steering exclusions, armed/disabled-recenter guards, cold/replaced seated eyes, rejected unrelated cameras, stable lookup reuse and missing/late/replaced HUD controllers. Preserved 0.2.3 fails the holder, seated discovery and HUD retry scenarios independently. No actual frame-time profiling has been performed. No third-party source or binaries are shipped in either archive.
