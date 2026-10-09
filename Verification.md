# ApocaChaseCamera 0.1.9 verification

October 9, 2026. The user requested the native on-foot compass behavior in both vehicle views, following the camera rather than keeping the player facing direction.

Production compilation against installed game dependencies succeeds without warnings. All 3,418 simulated assertions pass across fourteen stock camera fixtures, including thirteen new native-compass adapter assertions. The new layout-preservation reproduction fails against the previous 0.1.8 source and passes with this update.

New cases verify the on-foot action pipeline is invoked, its multiplier and untouched layout axes remain authoritative, its disabled FSM is not re-enabled, a late-update rotation action is supported, the shared yaw value is restored, repeated callbacks do not accumulate rotation, unfamiliar actions/uninitialized FSMs yield, and late initialization retries. Existing orbit/recenter, first/third-person switch and late-switch, collision, HUD, pause and lifecycle checks remain passing. A fresh clear frame still uses four buffered collision queries, and multiple canvas/render callbacks reuse its pose.

Read-only inspection confirms the native on-foot compass state consists of GetRotation, FloatMultiply and SetRotation. The adapter invokes the installed multiply/rotation actions with a temporary visible-heading input and restores the original variable immediately. It does not re-enable the FSM or modify native transforms. The game implementation is not copied or redistributed; handwritten test doubles exercise the public interface only.

The user confirmed the compass issue fixed in game on October 9, 2026. Automated results remain simulation, compilation and interface checks; this confirmation does not establish compatibility with every third-party camera/UI mod. Test on foot first, then enter a parked vehicle, toggle first/third person while looking at the same landmark, orbit the chase view and wait for recentering, and exit. Equal camera bearings should produce equal compass readings.

Evidence: Verification/0.1.9-check-results.txt. The expected old-version failure remains locally in work/chase-compass-0.1.9/baseline-regression.txt, outside distributed archives. The source and DLL from 0.1.8 are backed up separately. Player ZIP contains only ApocaChaseCamera/ApocaChaseCamera.dll and README.md. The user tested the build and authorized GitHub publication. No installation was performed by the agent.

