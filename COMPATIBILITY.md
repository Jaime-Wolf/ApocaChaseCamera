# Compatibility development

This repository contains ApocaChaseCamera's original source and simulated verification suite. It does not distribute Apocalypter, Unity, NWH Vehicle Physics, BepInEx, Harmony, Apocasetter or Apocaplayer assemblies or extracted game code/assets. Build against your own local game installation with Source/build.ps1. Install the compiled mod from this repository's Releases page.

The plugin GUID is local.apocalypter.chasecamera. BepInEx 5 and Apocasetter are required; ApocaGearbox, ApocaDustStorm and Apocaplayer are optional and independently maintained.

The driving camera reads the standard Player/InCar and DriveTrigger/3rdCamera hierarchy. It changes worldToCameraMatrix and cullingMatrix only during its selected camera's render, then resets them after rendering or an interrupted frame. It does not move native camera/player/vehicle transforms or change vehicle physics. Other render hooks should use the active rendered matrices rather than assume transform.position is the rendered eye position.

The optional Apocaplayer bridge delegates the unarmed cruising view while preserving weapon, aiming and binocular views. Its reflected members are validated and accessed through typed delegates; a persistent interface failure disables that bridge with one warning. Other custom camera systems need an in-game compatibility test.

The HUD reads live vehicle speed, RPM, rev limit and transmission gear. It follows the current inventory UI, rebinds replacements, and supports expanded inventory slots. Changes to native UI hierarchy or custom driving seats should be tested after scene loading, vehicle changes and UI replacement.

Run Verification/test.ps1 for the simulated regressions. These checks do not replace in-game tests of native rendering, PhysX geometry, third-party mods or performance.

Separate compatibility patches are permitted under LICENSE.md. They must depend on the original mod and must not bundle its source or DLL. Copying, adaptation for another project, redistribution and reuploads require Jaime-Wolf's prior permission.
