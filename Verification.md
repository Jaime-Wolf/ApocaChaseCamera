# ApocaChaseCamera 0.2.5 verification

Built on 2026-10-11. The user confirmed that the world-shift camera fix works in game and approved publication. The full previous 0.2.4 project and archives remain backed up locally.

- Production build: passed against installed game, BepInEx and Apocasetter references, without compiler warnings.
- Standalone: 3,607 checks, including 182 new origin checks. Simulates same-frame pose consumption before a world translation, all axes, two successive shifts, interrupted render matrices and next-frame continuity at 30/60/144 FPS. Ordinary origin callbacks retain the existing pose/query budget.
- Cross-mod: 2,001 checks using unchanged separate Apocaplayer ThirdPerson.cs and the installed real Harmony library. The 39 added checks execute all three observer hooks through real Harmony and verify cursor-cache invalidation, rendering, crosshair projection, compass and armed-to-cruising handoffs. Supporting Unity/input/physics services remain simulated. External source hash remained unchanged.
- Installed interfaces: 50 native/Apocaplayer contracts passed, including all three native origin LateUpdate methods. Production checks confirm observer startup/teardown, both camera-history notifications and absence of transform or rigidbody writes in the observer.
- Apocaplayer remains optional. Production assembly contains no Apocaplayer implementation or hard assembly reference.
- Player ZIP contains a root ApocaChaseCamera folder with DLL/icon/README only. Source and verification are separate, without game or third-party dependency DLLs.

Run Verification/test.ps1, Verification/test-cross-mod.ps1 -ApocaplayerSource <separate unchanged source directory>, Source/build.ps1, and Verification/verify-production.ps1 -Dll <built DLL>. No other-mod source is redistributed.

## Additional in-game regression checks

Drive across several 1 km world shifts in native third person, then with Apocaplayer enabled. Test while orbiting, while auto-recentering, near obstacles and immediately before/after drawing or stowing a weapon. Camera position, crosshair and compass should remain aligned without a void frame or jump. Check first-person switching and leaving/re-entering the vehicle. Startup log should include "World-origin camera synchronization ready." See Verification/IN-GAME-CHECKLIST.md for existing compatibility checks.

The user confirmed successful gameplay testing of the reported world-origin issue. The agent did not perform live gameplay or a runtime FPS benchmark. The full combinations above remain useful regression checks; the user confirmation does not establish coverage of every combination.
