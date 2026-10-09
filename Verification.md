# Verification - 0.2.4

The user confirmed the 0.2.3 crosshair auto-recenter correction in game. 0.2.4 fixes native holder/cache synchronization, cold seated camera discovery and repeated missing-controller HUD searches, preserving cursor-time recenter and the existing compatibility hooks.

Production builds without warnings against installed dependencies. 3,425 regression checks, 1,962 cross-mod simulation checks and 47 installed-interface contracts pass. New scenarios reject the preserved 0.2.3 source for each of the three audit findings. See Verification/0.2.4-REVIEW.md and the accompanying result logs.

Run Verification/test.ps1, Verification/test-cross-mod.ps1 -ApocaplayerSource <unchanged separate source directory>, Source/build.ps1 and Verification/verify-production.ps1 -Dll <DLL>. Cross-mod tests also accept -Scenario holder or -Scenario discovery for focused checks. No other-mod code is bundled. Simulated Unity/physics/input services do not replace live tests or actual performance measurements. Use Verification/IN-GAME-CHECKLIST.md. The old ZIP is backed up; 0.2.4 has not been installed or published by the agent.
