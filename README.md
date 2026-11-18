# Flag Football Android Prototype

A Unity URP foundation for an offline 5v5 flag football prototype. Bootstrap prepares an explicitly bound QB, receiver and visible QB-held ball, with movement blocked until Start. Start/Restart controls drive repeatable attempts: the joystick controls the QB independently of an automatic one-cut receiver route. The running camera still follows only the QB. Automated route checks do not extend the historical Pixel evidence below to this candidate.

## Run the checks

1. Install Unity **6000.3.28f1** for Apple Silicon with Android Build Support, SDK/NDK Tools, and OpenJDK. Complete account and license prompts yourself.
2. Close any Editor using this project, then run from the repository root:

   ```bash
   python3 scripts/run_unity_tests.py
   ```

3. Both suites must report `Passed`, at least one passing test, and zero failures. The command also requires a successful Editor exit code.

Run one suite with `--platform EditMode` or `--platform PlayMode`. Use `--editor /path/to/Unity` or `UNITY_EDITOR` if the Editor is not in the default macOS Hub location. The runner reads the project's pinned Editor version and writes fresh XML reports and logs under ignored `TestResults/<timestamp>/` directories.

The runner uses Unity batch mode with Android as the active build target. It deliberately does **not** combine `-runTests` with `-quit`. A missing/empty/failed report, process failure, or timeout returns a nonzero exit code.

## Project layout

| Path | Responsibility |
| --- | --- |
| `Assets/Core` | Pure C# attempt lifecycle, one-cut routes, movement, bounds, joystick gesture rules and constrained camera smoothing; engine references disabled. |
| `Assets/Unity` | Scene initialization, movement/touch adapters, runtime visuals and camera projection/framing. May reference Core, never the reverse. |
| `Assets/Editor` | One-time baseline asset/scene setup, plus the repeatable Android development build action. Setup refuses to overwrite existing baseline assets. |
| `Assets/Tests/EditMode` | Movement behavior plus assembly identity and absence of Unity dependencies. |
| `Assets/Tests/PlayMode` | Actual Bootstrap movement/field adapter, lifecycle, camera/placeholder presence and URP assignment. |
| `Assets/Scenes/Bootstrap.unity` | Initial scene with a camera, directional light, ground, and an orange placeholder cube. |

Add this repository root as an existing project in Unity Hub and open it with the pinned Editor. The scene, URP assets, project settings, and `.meta` files were created by the Editor, not hand-authored serialized scene data. Keep assets together with their metadata.

## Bounded movement (#9)

`PlayerMovement.SetMoveInput(Vector2)` accepts screen-right/up input when `MovementAllowed`; blocked requests are discarded, including while the component is disabled. Magnitude maps linearly to speed, capped radially at 1 (including diagonals and over-range input). Zero stops on the next `Update`, without acceleration or momentum. The latest input persists until replaced. The touch joystick supplies this boundary and sends zero on release/cancellation or app interruption; no keyboard driver is installed.

| Internal setting | Initial value |
| --- | --- |
| Android orientation | Both landscape orientations only; portrait disabled |
| Maximum speed | 5 world units/second |
| Test field X/Z limits | -10 to +10 on both axes (the 20 × 20 ground) |
| Conservative footprint/framing inset | 2 units, retaining room for the elevated body and camera edge margin |
| Allowed character-center X/Z limits | -8 to +8 |
| View reference | Bootstrap's translating Main Camera with fixed orientation; horizontal forward/right, independent of character facing |

Change the serialized `PlayerMovement` fields on `Placeholder` in the Inspector before entering Play mode. Bounds are world-space X/Z; keep them aligned with the ground and retain a sufficient footprint radius if resizing visuals. Vertical position is unchanged. Motion uses `Update` time, not physics. Each axis is clamped independently, so tangential motion remains possible at an edge. Facing follows actual travel instantly, retaining the last direction when stopped or fully blocked. The dark `FacingMarker` on the orange body's local +Z makes forward distinguishable. This is a test-field constraint, not match out-of-bounds policy.

The scene was wired using `FlagFootball.Editor.ProjectSetup.ConfigurePlayerMovement` through Unity Editor APIs. It preserves existing baseline assets and can be rerun to reconnect the camera/marker; normally just edit the committed scene.

Verification: `python3 scripts/run_unity_tests.py` passed **30 EditMode + 17 PlayMode tests**, zero failures, Editor exits 0. `AndroidOrientationTests.PlayerSettings_AllowOnlyBothLandscapeOrientations` guards the landscape-only project settings, also enforced by the Android build action. `BoundedMovementTests` covers analog/capped and diagonal speed, view axes, immediate reversal/neutral, all four edges/four corners, tangential facing, custom speed/bounds and frame-size independence. `PlayerMovementTests.PublicInput_MovesAtAnalogAndCappedSpeedThenStops`, `CameraRotationAndDirectionChange_ApplyOnNextFrame` and `FieldEdgesAndCorners_ContainWholePlaceholderAndPermitReturn` drive the real scene adapter across frames, including the entire visual footprint. Baseline checks still pass. These are automated behavior checks, **not physical touch or rendered-visual verification**; parent #8 remains open for the remaining integrated device checks.

Rollback boundary: remove the movement core/adapter, their two test files and metadata, the `ConfigurePlayerMovement` method, and revert Bootstrap's movement component, body scale and marker plus this documentation. The existing bootstrap/build/test baseline is otherwise independent. Later touch/camera increments depend on this movement boundary and must be removed first.

## Touch joystick (#10)

Touch inside the bottom-left circle, drag to change direction/speed, and lift to stop. The original finger remains captured outside the circle, with speed capped and direction still updating. A second finger cannot steal the gesture or inherit it when the owner releases. A touch starting outside must be lifted and started inside to control movement.

| Internal setting on `Bootstrap` | Initial value |
| --- | --- |
| Joystick diameter | 240 reference units |
| Padding from safe-area bottom/left | 64 reference units |
| Sensitivity | 1 (linear; full speed at one radius, no dead zone) |
| Canvas scaling | 1920 × 1080 reference, matched to screen height |

Edit the serialized joystick fields on `SceneBootstrap` before Play mode. The circular pad and handle are created once at startup inside `Screen.safeArea`, alongside the attempt controls described below; no imported UI artwork is needed. At sensitivity 1, half-radius input requests half speed. The handle stays inside the circle while the controlling finger may travel beyond it.

Input uses **Input System 1.20.1 EnhancedTouch** and **uGUI 2.0.0**, with `activeInputHandler: 1` (new Input System only). Changing the backend requires restarting an open Editor. There are no legacy `Input.Get*` calls, keyboard controls, action assets or UI event modules: the adapter polls actual touch phases and screen positions, feeds the engine-free `JoystickGesture`, then calls `PlayerMovement.SetMoveInput` before movement updates. Device and touch IDs together identify the owner.

Release, cancellation, focus loss, pause, disabled controls or a disappearing owning touch neutralize input. Returning resets the acceptance timestamp; only a new `Began` touch starting after return can capture the pad. Focus and pause are tracked separately, so regaining focus while still paused does not enable controls. EnhancedTouch enable/disable calls are balanced and generated UI textures are destroyed with the scene.

`JoystickGestureTests` checks analog capping, overdrag direction/diagonals, ownership, cancellation and configurable sensitivity. `TouchJoystickTests` injects `TouchState` events into a virtual `Touchscreen`, through the real EnhancedTouch/scene/movement path:

- `ActualTouchPath_ControlsAnalogSpeedOverdragDirectionAndRelease`
- `OtherFinger_CannotStealOrAutomaticallyInheritTheGesture`
- `TouchCancellation_StopsWithoutRetainingInput`
- `OutsideTouch_DoesNotActivateWhenDraggedIntoThePad`
- `FocusPauseAndDisable_RequireFreshTouchesAfterReturn`
- `FocusReturningBeforeUnpause_DoesNotEnableHeldTouch`
- `JoystickLayout_StaysInBottomLeftSafeArea`

Tests temporarily route input to the headless game and ignore actual Editor focus, restoring both settings and removing their virtual device afterward. Lifecycle messages are simulated, not physical lock/unlock proof. The independent movement-boundary tests disable the joystick before supplying input directly.

### Public fresh-input reset (#14 foundation)

The scene attempt owner calls `TouchJoystick.ResetInput()` on its joystick reference. The call cancels gesture ownership, immediately clears the player's movement request and centers the handle, then accepts only a new `Began` touch whose start time is strictly after the reset. Existing held touches, their subsequent moves and their release cannot resume movement. Repeated calls, including while disabled, are safe; re-enabling does not revive the held gesture.

This action alone does not enable/disable controls, freeze movement, reposition the player or create an attempt lifecycle. Unmanaged movement components still default to allowed; Bootstrap now owns preparation gating. The joystick still supplies input before movement updates; focus, pause and disable handling use the same reset. The caller owns when to reset, while the joystick owns gesture cancellation and freshness.

Verification through actual virtual `Touchscreen` event dispatch:

- `TouchJoystickTests.PublicReset_StopsHeldMovementAndRequiresFreshTouch`: active movement in the field interior, synchronous handle centering, no drift while held/moved/released, then fresh-touch movement.
- `TouchJoystickTests.PublicReset_RepeatedAndDisabledResetsDoNotReviveHeldTouch`: repeated idle/held/disabled resets, disable/re-enable, stale move/release rejection and fresh recovery.

`python3 scripts/run_unity_tests.py --platform PlayMode --test-filter 'TouchJoystickTests.PublicReset_'` passed **2/2**. Before exposure, the same command failed compilation with CS1061 (no accessible `ResetInput`), runner exit 1 and no XML. After exposure, `python3 scripts/run_unity_tests.py` passed **30 EditMode + 19 PlayMode**, zero failures/skips, Editor exits 0. Ignored XML/log artifacts are under `TestResults/20261117T020328819680Z` (focused) and `TestResults/20261117T020410209579Z` (full); the RED log is under `TestResults/20261117T020259526024Z`.

These are headless input simulations, not new physical touch, Android, rendered-visual or lock/unlock evidence. This is the input foundation for [#14](https://github.com/AndresMDV/flag_football_am/issues/14); the prepared lifecycle below builds on it. Receiver/held-ball preparation and the Start/Restart controls below now use this foundation. Existing physical evidence below applies only to its named APK.

Rollback this unit independently by restoring `ResetInput` to private, removing its new API comment and the two `PublicReset_` tests, and removing this subsection. No scene, metadata, core, movement or startup changes need reverting.

### Prepared attempts (#14)

`SceneBootstrap.Quarterback` explicitly references the existing orange `Placeholder`; runtime role selection never scans for an actor. The pinned Editor action `ProjectSetup.ConfigureAttemptQuarterback` saved this binding without regenerating baseline assets. The initial world position `(0, 0.5, 0)` and identity rotation are captured once in `Awake`, before movement, not recaptured on restart.

After `IsInitialized` becomes true in `Start`, the touch controls and external callers share these public synchronous actions:

- `StartAttempt()` returns true only for Preparation → Active. It clears joystick ownership/freshness before allowing movement. Duplicate Active Start returns false without disturbing a held gesture.
- `RestartAttempt()` returns true on successful completion, including while already prepared. It closes the movement gate, publishes Preparation and cancels the joystick before invoking `Resetting` subscribers in registration order, then restores both original actor poses and QB possession. Start and nested Restart are rejected during callbacks. A new explicit Start and a new `Began` gesture are required afterward.
- `Resetting` is the small integration boundary for future charge/flight/control/camera adapters. The owner resets its receiver route before these callbacks. Subscribers must reset synchronously/idempotently and unsubscribe when disposed. Exceptions propagate immediately (later callbacks are skipped); pose restoration and reentrancy cleanup still run, and Start stays blocked until a successful Restart retry.

The engine-free `AttemptLifecycle.State` exposes Preparation/Active; its action booleans report whether state changed. The scene's Restart boolean instead reports transaction completion. Preparation rejects both touch and direct movement requests; disabling/re-enabling movement cannot bypass it. Focus/pause still neutralize input only: interruption-to-attempt reset belongs to #21.

Focused verification: `AttemptLifecycleTests` exercises public transitions; `TouchJoystickTests.Attempt_` exercises actual scene preparation, fresh Start, duplicate Start, reset ordering/reentrancy/failure, pose restoration and stale held/moved/released touch rejection. Existing movement/touch/camera fixtures explicitly resolve the QB and Start when needed; complete renderer-edge framing assertions remain. These are headless virtual-touch checks, not physical touch, usability, Android-player or lock/unlock evidence. Historical APK evidence below is unchanged.

Earlier single-QB delivery regression: `python3 scripts/run_unity_tests.py --platform EditMode` passed **33/33** and `--platform PlayMode` passed **23/23**, retaining all 49 earlier cases plus seven new cases, zero failures/skips and Editor exits 0. Reports: ignored `TestResults/20261117T023723664570Z/EditMode.xml` and `TestResults/20261117T023733818599Z/PlayMode.xml`.

The earlier lifecycle unit can be rolled back separately after removing dependent receiver/ball preparation: restore its bootstrap/gate/binding, lifecycle, tests and docs against its pre-unit snapshot; preserve the public joystick reset and both `PublicReset_` tests.

#### Receiver and held-ball preparation

`SceneBootstrap` owns two runtime primitives and their cloned URP materials. Public `Receiver` and `Ball` expose the returned transforms; roles never depend on a first-actor search. `BallOwner` reports the explicitly bound QB transform only while the ball is directly parented to it, otherwise null. This is an attachment observation, not a transfer/flight/catch API.

| Visual | Initial pose and appearance |
| --- | --- |
| QB | Existing orange `Placeholder`; world `(0, 0.5, 0)`, identity rotation, scale `(0.6, 1, 1)` |
| Receiver | Blue cube named `Receiver`; world `(1.5, 0.5, 1)`, identity rotation, scale `(0.6, 1, 1)` |
| Ball | Brown ellipsoid named `Ball`; QB-local position `(0.8, 0.1, 0.2)`, identity local rotation; world size `(0.22, 0.18, 0.36)` at the initial pose, local scale approximately `(0.366667, 0.18, 0.36)` compensating QB scale |

The receiver has no joystick movement adapter. Neither added visual has a collider or rigidbody. Preparation keeps both actors still under joystick input; Active now runs the independent receiver route below while the joystick moves the QB and its attached ball. The initial poses and ball parent-relative pose/scale are captured once. Every Restart, including repeated prepared resets and callback failures, restores them in the existing `finally` block; detached/mutated balls are reattached. Callback ordering, blocked reentrancy and fail-closed retry behavior are unchanged.

After composition, Bootstrap explicitly refreshes the running camera's cached QB-child renderers so the ball participates in existing full-renderer edge framing. The nearby receiver is initially framed at 16:9, 20:9 and 2.5 aspect. Keeping the receiver visible throughout unrestricted QB movement is not promised; passing-camera behavior belongs to #16. Renderer-corner projection checks establish geometric containment, not rendered pixels or physical readability.

Scene-owner destruction releases both primitives (even a detached ball) and their materials. The surviving running camera skips destroyed cached visuals and keeps framing the QB; `PreparedActors_OwnerOnlyDestructionKeepsCameraUpdating` verifies owner-only cleanup across subsequent camera frames. Repeated Restart reuses the same objects. `BootstrapSceneTests.PreparedActors_ExposeDistinctRolesHeldBallAndInitialFraming` and `PreparedActors_RepeatedSceneUnloadDestroysOwnedVisualsAndMaterials` cover roles, initial geometry and repeated scene/reset cleanup. Existing `TouchJoystickTests.Attempt_` cases also disturb both actor poses and ball attachment/local pose/scale, verify reset/failure recovery and exercise fresh virtual touch independently of receiver route progress; prepared pose checks remain exact.

Actor-composition full regression: `python3 scripts/run_unity_tests.py --platform EditMode` passed **33/33** and `--platform PlayMode` passed **25/25**, zero failures/skips, Editor exits 0. All 56 previous case fullnames remain passing; both new actor checks pass. Ignored reports: `TestResults/20261117T125003194727Z/EditMode.xml` and `TestResults/20261117T125016992114Z/PlayMode.xml`. These are automated scene/virtual-touch checks, not new Android or physical-device evidence.

The #14 delivery added the Start/Restart controls below; #15 adds the receiver route. Charge, flight, catch, control transfer, passing camera and interruption-to-attempt policy remain absent. Roll back only receiver/ball composition/reset/cleanup, the camera refresh/lifetime guard, the actor tests, added actor assertions in the touch fixture and this documentation against this unit's pre-unit snapshot; preserve both earlier lifecycle/input deliveries.

#### Start/Restart touch controls

1. Press **Start | Preparation** at bottom right, then begin a fresh bottom-left joystick gesture. The QB and its held ball follow the joystick while the receiver runs its route; the label becomes **Start | Active**.
2. Press **Restart** to restore both original poses and QB possession/ball-local pose/scale. The label returns to Preparation. A new Start and a new joystick gesture are required.
3. Repeated Start while Active preserves held movement; repeated Restart while prepared repairs disturbances without duplicating actors or UI.

Actions activate on a fresh **press (`Began`)**, never release. A touch begun outside cannot drag in to activate. `AttemptControls` receives its explicit `SceneBootstrap` owner and the existing joystick safe-area transform; no actor lookup, EventSystem, UI event module, new action asset or keyboard feature is involved. Restart takes priority over every Start in the same input batch. The synchronous `Resetting` subscription invalidates all earlier action-touch start timestamps, including pending Began; held/moved/released old Start or Restart pointers cannot act after reset. Public actions update the label synchronously, including fail-closed callback failures.

Both 360 × 128 reference-unit regions sit at bottom right with a 32-unit vertical gap and 64-unit edge padding; the joystick remains bottom left. Height-matched canvas scaling and the shared `Screen.safeArea` anchors preserve placement across landscape aspects. White 30-unit labels sit on dark backgrounds. Layout tests exercise actual RectTransforms at 16:9, 20:9 and 2.5 with a representative inset safe area; batch Screen resolution itself remains 4:3. Tests check region separation, label fit and prepared actor/ball renderer corners, not rendered pixels or physical target size.

Disabled, paused or unfocused controls accept no actions; return/re-enable invalidates pending gestures. This neutralizes input only, not #21's full interruption-to-attempt policy. Each adapter balances its own EnhancedTouch enable/disable reference without clearing shared state. Disabling controls unsubscribes Resetting; owner destruction disables/destroys the complete owned touch canvas, clears movement permission and releases its controls alongside the prepared visuals. No orphan active buttons remain; the surviving camera/QB owner-only regression is retained.

Verification: `python3 scripts/run_unity_tests.py --platform PlayMode --test-filter 'TouchJoystickTests.Actions_'` exercises `Actions_StartRestartRestoreAndRequireFreshMovement`, `Actions_ResetRejectsPendingSameFrameAndOutsideGestures`, `Actions_InterruptionsNeutralizePendingGesturesAndExternalState` and `Actions_LayoutIsReadableDisjointAndSafeAcrossLandscapeAspects`. These use actual virtual Touchscreen event dispatch, not direct UI listener calls. The full EditMode/PlayMode suites retain all 59 previous named cases alongside these four additions. `PreparedActors_OwnerOnlyDestructionKeepsCameraUpdating` also verifies complete touch-UI teardown.

The scoped physical results below cover six user-confirmed scenarios on this candidate. Lock/unlock and app return were not accepted in this round. Automated virtual input alone does not establish physical usability, accessibility, FPS or thermal performance; historical APK evidence remains separately scoped.

Rollback only the final control delivery against its saved pre-unit snapshot: remove `AttemptControls.cs` with its Unity-generated metadata, restore only its Bootstrap/joystick ownership wiring, added action tests/teardown assertions, PlayMode uGUI test reference and this documentation. Preserve the previous lifecycle/input/actor/camera implementations and all 59 prior cases; do not restore from HEAD.

### One-cut receiver route (#15)

Start launches one internally configured route, independent of QB input. The existing `SceneBootstrap` owns the explicit receiver and advances the engine-free `OneCutRoute` in `Update` only while Active. No extra movement component, input binding, coroutine or event subscription controls the receiver.

| Internal setting on `SceneBootstrap` | Default |
| --- | --- |
| Start world X/Y/Z | `(1.5, 0.5, 1)`; initial forward +Z |
| Cut offset X/Z from start | `(0, 3)` → world `(1.5, 0.5, 4)` |
| End offset X/Z from start | `(3, 3)` → world `(4.5, 0.5, 4)` |
| Speed / timing | 3 world units/second; cut at 1 second, end at 2 seconds |
| Direction change | +Z to +X, one 90-degree right cut |

These points are inside the existing test area. Tune the serialized offsets/speed before Play mode, keeping both legs nonzero, noncollinear and inside that area. Core rejects nonfinite coordinates/time, negative time and nonpositive/nonfinite speed. Accumulated path distance consumes frame overshoot across the cut and clamps at the endpoint; different time-step partitions preserve progress. Facing follows the current travel leg, retains the incoming direction exactly at the cut, then retains +X while waiting at the end. Duplicate Start never rewinds or loops, even when the attempt remains Active at the endpoint.

Restart stops the model before reset callbacks and restores the original receiver pose in the existing `finally`; a callback failure cannot cause late route movement and still blocks Start until a successful reset. Preparation never advances the route. The next Start repeats the same route. Disabling the owner pauses its route Update; destruction leaves no independent route worker. This is not #21's full interruption policy.

`OneCutRouteTests` covers public start/cut/end/wait/reset/repeat, cut overshoot/time partitions, custom geometry/speed/facing and invalid inputs. `TouchJoystickTests.Route_PublicAttemptsAndTouch_ProgressCutWaitAndRepeatIndependently` drives actual public attempts and virtual QB touches through a moving reset, two complete runs, the cut, endpoint waiting and independent QB turns. Existing prepared/reset/ball-ownership, touch freshness, camera bounds and owner-cleanup checks remain.

This is **lab scaffolding**, not any of the six final playbook concepts or final receiver AI. No chase, catch, ball flight or control transfer is implemented. The unchanged QB-only `RunningCamera` may lose the receiver along this route or during QB movement; full passing framing belongs to #16. Headless position/facing checks are not rendered visibility, a new Android build or physical #15 acceptance; the historical #14 APK below contains no route.

Rollback only this uncommitted #15 unit against `7df3a4c`: remove the new Core route/test and their Unity-generated metadata; restore its owner wiring, route-specific touch tests/semantic migrations and these README changes. Preserve the complete published #14 lifecycle, controls, actors, input and camera implementation.

### Issue #14 physical acceptance

The user confirmed all six checks on a Pixel 6a, Android API 36, using the installed ARM64/IL2CPP/OpenGL ES 3 development APK with SHA-256 `8e532743a4ed9ea7f3bec29777062800aecaf29a51ed17e57d9f889a1a597e8d`.

| Scenario | User-confirmed result |
| --- | --- |
| Active joystick movement | QB moves; receiver stays stationary |
| Restart an active attempt | Both initial poses and QB possession restored; new Start required |
| Hold joystick through Restart and Start | No movement until the old finger lifts and a fresh gesture begins |
| Hold Start, press Restart with another finger, release old Start | Remains in Preparation; old release cannot activate |
| Restart twice, then fresh Start and joystick | Normal movement without unexpected pose/state changes |
| Both landscape orientations | Controls fully visible, disjoint and responsive |

Existing final automated evidence is **33/33 EditMode + 30/30 PlayMode**, with zero failures; documentation-only delivery does not rerun these suites. Physical results are user reports, not automated device input, cold-start proof, exhaustive interruption/accessibility coverage or performance/thermal measurements. Routes (#15), passing-camera framing (#16) and the remaining #13/#1 lab/product scope are not completed here.

### Physical Pixel checks

The user confirmed these incremental checks on the Pixel 6a using the development APK with SHA-256 `cb8265b105e4c1c0a5441eac26abfe52ea7585827627aa16910fa774f7f0d359`:

| Scenario | Observed result |
| --- | --- |
| Scene and orientation | Field, placeholder and landscape presentation visible |
| Short drag and release | Character moves and stops on finger lift |
| Near-center versus edge drag | Slower movement near center, faster at the edge |
| Drag beyond circle and change direction | Movement continues and follows the direction change |
| Second-finger interference | Original finger retains control |
| Owner lifts while second finger remains held | Character stops; held finger does not inherit control |
| Lock/unlock during movement | No stale movement; a fresh touch restores control |
| Android Home and app return during movement | No stale movement; a fresh touch restores control |

Disabled-component cancellation and outside-start gestures were tested automatically, not separately on the phone. These observations do not establish exhaustive interruption coverage, frame-rate/thermal performance or completed camera tuning. Those checks used the previous static-camera APK; they are not evidence for the new running camera. Integrated field/camera checks remain for #12.

Rollback boundary for #10: remove `JoystickGesture`, `TouchJoystick` and their tests/metadata, restore the bootstrap and test input wiring, remove the added assembly/package references and restore the previous input backend. Keep #9 movement and the landscape configuration intact.

## Bounded running camera (#11)

The orthographic running view follows in `LateUpdate` after player movement. Pitch is fixed; yaw and roll remain zero regardless of character facing. Orthographic projection keeps apparent size constant across the field, trading perspective depth scaling for predictable edge framing. There is no passing view, camera-control UI or automatic view transition.

| Internal setting on Main Camera's `RunningCamera` | Initial value |
| --- | --- |
| Height above character center | 7 units |
| Downward pitch | 60 degrees |
| Orthographic half-height | 2.2 units (4.4-unit vertical view) |
| Follow response | 8/second exponential convergence |
| Player viewport margin | 5% on every side |
| Test field / center limits | 20 × 20 units / -8 to +8 X/Z |
| Field grid | 2-unit spacing, visual reference only |

The original 10 × 10 field and 0.75 movement inset were retuned to give the running view room to translate and to contain the projection of the elevated body/marker. The 2-unit inset is conservative camera clearance, not the character's physical radius or final match out-of-bounds policy. Read-only `PlayerMovement.MinimumPosition`/`MaximumPosition` expose the configured center limits.

`RunningCamera` projects all viewport corners and each QB-child renderer's full AABB onto the flat, axis-aligned ground. It intersects the camera-position intervals that keep the entire viewport on the field and the entire actor inside the inner frame. `BoundedCameraFollow` applies exponential smoothing, then clamps to that intersection. Bounds and visibility win over smoothing during abrupt relocation or clipping risk; the character may be off-center near edges. The view shrinks if needed to fit the field at a changed aspect ratio. Impossible framing is rejected explicitly: tune view size/margin/pitch together with movement inset rather than hiding outside areas.

Edit serialized camera values before Play mode; keep the field flat and world-axis aligned and retain feasible framing. `ProjectSetup.ConfigureRunningCamera` wires the scene through Editor APIs and restores the initial field dimensions/inset and camera preview. The subtle runtime grid makes camera translation observable without final art; its owned material/texture are destroyed with the bootstrap.

`BoundedCameraFollowTests` covers smooth convergence, time-step partition independence, hard safety bounds and impossible intersections. These scene tests drive the public movement input and inspect observable projection, not just private settings:

- `RunningCameraTests.Tracking_IsSmoothAndConvergesAfterMovementStops`
- `CenterEdgesAndCorners_KeepWholePlayerAndGroundFramedAcrossLandscapeAspects` (16:9, 20:9 and 2.5 aspect; checks every renderer corner and viewport-ground corner along the route)
- `DirectionChanges_PreserveFixedViewAndScreenRelativeMovement`
- `SuddenRelocation_UsesVisibilitySafetyInsteadOfLosingThePlayer`

Camera tests use a temporary 1/60-second simulation step and restore it afterward. The independent movement test that deliberately rotates the view disables the camera driver first. Headless geometry checks are not Pixel comfort, rendered-pixel, FPS or thermal evidence.

### Physical Pixel camera checks

The user confirmed these incremental checks on the Pixel 6a using the camera development APK with SHA-256 `4b609d998f9eade5849fee6b57df39b9e000c365c8c7ddba02f2cba82733b1cd`:

| Scenario | Observed result |
| --- | --- |
| Short rightward movement and release | Grid scrolls smoothly; camera follows without rotating |
| Right movement limit and upward traversal to upper-right corner | Character remains visible; no empty outside-field area |
| Upper-edge traversal to upper-left corner | Character remains visible throughout; no empty outside-field area |
| Left-edge descent to lower-left corner | Character remains visible, not hidden by joystick; no empty outside-field area |
| Bottom-edge traversal to lower-right corner | Character remains visible throughout; no empty outside-field area |
| Diagonal return into the interior and release | Smooth settling without camera rotation or jumps |

All four corners were included in these selected human-reported checks. They are not exhaustive trajectories, frame-by-frame rendered-pixel checks or FPS/thermal measurements. Final integrated input/interruption regression and calibration evidence remain owned by #12.

Rollback boundary for #11: remove the camera core/adapter/tests and metadata, the scene's camera component/preview, grid setup, setup method and camera-specific test wiring; restore the previous field dimensions/inset and documented values. Preserve #9/#10 movement and touch behavior.

## Integrated verification (#12)

The #12 integration delivery passed **47 tests**, including two cross-boundary touch → movement → camera checks in `TouchJoystickTests`: `IntegratedTouch_MovesFacesAndDrivesCameraThroughTurnAndRelease` and `IntegratedTouch_BoundsFramingAndFreshInputSurviveInterruption`. The new development APK was built, installed and hash-verified on the Pixel without changing production control/camera values.

See the [integration evidence report](docs/movement-verification.md) for exact source/build identity, named results, calibration values and the parent acceptance matrix. The user confirmed the matching-build touch, automatic-facing, perimeter/four-corner, camera-visibility and lock/app-return checks, and accepted the current control/camera values without further tuning. Earlier APK observations remain separate. Delivery and issue status are tracked on GitHub.

## Build and launch the Android prototype

The current scene waits in Preparation with Start/Restart touch controls. The earlier #14 candidate was built, installed and checked in the scoped physical acceptance round above; this #15 route candidate has not been built or installed. The commands below remain build infrastructure; each historical device report applies only to its named APK.

From the repository root, with the Editor closed:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity"
python3 scripts/run_unity_tests.py && \
mkdir -p TestResults/AndroidBuild && \
"$UNITY" -batchmode -quit -nographics -projectPath "$PWD" -buildTarget Android \
  -executeMethod FlagFootball.Editor.BuildAndroid.Run \
  -logFile "$PWD/TestResults/AndroidBuild/Editor.log"
```

The build action enforces landscape-only orientation and configures a development-only ARM64/IL2CPP APK, OpenGL ES 3, minimum API 25 and target API 36. Landscape-only orientation is also stored in `ProjectSettings/ProjectSettings.asset`. It selects only `Bootstrap.unity`, uses the default debug signer (not a custom release keystore), and rejects an unsuccessful `BuildReport` or missing APK. Outputs are ignored: `Builds/Android/FlagFootballPrototype.apk`, `build-report.json`, and the Editor log above. The report's total size includes other build artifacts, not just the compressed APK.

For deployment, connect only the intended physical Pixel 6a over USB, unlock it, and approve any phone prompts yourself. Confirm the model before installing; do not replace an unrelated app or uninstall anything to bypass a signing conflict:

```bash
ADB="/Applications/Unity/Hub/Editor/6000.3.28f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
"$ADB" -d shell getprop ro.product.model
"$ADB" -d install Builds/Android/FlagFootballPrototype.apk
"$ADB" -d shell am start -W -n \
  com.andresmdv.flagfootballam.prototype/com.unity3d.player.UnityPlayerGameActivity
```

The expected model is `Pixel 6a`. A first install must report `Success`; use `install -r` for subsequent updates only after verifying that the installed prototype belongs to this project. The label is **Flag Football Prototype**. Startup logs include `BASELINE_SCENE_READY`, but only a person looking at the phone can confirm the orange cube and ground are visible. Keep device identifiers, screenshots, logs, binaries and signing materials out of public issue attachments.

## Versions and verification limits

- Editor: **6000.3.28f1**, revision **8f082d2fe476**.
- Universal Render Pipeline: **17.3.0**.
- Unity Test Framework: **1.6.0**.
- Input System: **1.20.1** (EnhancedTouch).
- uGUI: **2.0.0**.
- Dependency versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Headless PlayMode checks establish scene/lifecycle behavior and pipeline configuration, not rendered pixels, touch usability, FPS, or thermal performance. The runner's failure path was verified by temporarily breaking an assertion, observing a failed NUnit report and nonzero command exit, then restoring the assertion and rerunning both suites successfully.

GitHub Issues is the specification/task tracker: [prototype specification](https://github.com/AndresMDV/flag_football_am/issues/1). See the [Pixel 6a build and visible-launch evidence](https://github.com/AndresMDV/flag_football_am/issues/7) for the completed baseline; it does not verify this movement increment. No cloud services, player accounts, monetization, or optional pre-release automation are part of this baseline.
