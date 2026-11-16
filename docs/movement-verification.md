# Integrated Pixel movement verification

Implementation evidence for [#12](https://github.com/AndresMDV/flag_football_am/issues/12) and its [parent #8](https://github.com/AndresMDV/flag_football_am/issues/8). Automated integration and the development build pass. **The user confirmed the requested integrated physical checks on the matching APK and accepted the existing calibration values.** This report is evidence, not a replacement issue tracker or permission to close issues.

## Build identity

| Item | Verified value |
| --- | --- |
| Unity | 6000.3.28f1, revision 8f082d2fe476 |
| Packages | URP 17.3.0; Test Framework 1.6.0; Input System 1.20.1; uGUI 2.0.0 |
| Build | Development APK; ARM64/IL2CPP; OpenGL ES 3; minimal stripping; default debug signer |
| Android APIs | Minimum 25, target 36 |
| Explicit scene list | Only `Assets/Scenes/Bootstrap.unity` |
| BuildReport | `Succeeded`, 0 errors, 0 warnings |
| APK size | 61,685,694 bytes (not the BuildReport's aggregate artifact size) |
| APK SHA-256 | `1d08108849ee42a2150a1803ae0afeed6df054e5b1584f3a7d93d80c0e9f8067` |
| Unity source/config snapshot | `d01a8011a032627be06dfe17c60bf1e18b91c975622b99c08465d0f6b35a5ad6` |
| Physical target | Pixel 6a; Android 16 / API 36 |
| Deployment | Known project APK verified before replacement; `install -r` succeeded; cold launch `Status: ok` |
| Runtime startup | Process running; `BASELINE_SCENE_READY`; no Unity error / Android fatal entries in scoped startup log |
| Orientation | `userLandscape`; active application bounds 2400 × 1080 |
| Installed output | Installed base APK SHA-256 equals the final APK above |

The source/config snapshot covers hashes and modes of 94 intended Unity project files under `Assets`, `Packages` and `ProjectSettings`, including test sources and metadata. It was unchanged across the build and the human checks. After the manual run, the installed APK still matched the final hash, the app remained in 2400 × 1080 landscape, and the scoped current-process log contained no Unity error or Android fatal entries. The source snapshot and APK hashes identify evidence; they are not independent review receipts. The implementation baseline is `fcc10c1`; the source/config snapshot identifies the verified candidate independently of commit metadata.

Raw manifests, BuildReport, XML results, device logs and binaries remain in ignored `TestResults/IntegratedPixelBuild`, `TestResults/20261116T172927010915Z` and `Builds/Android`. Do not attach raw device identifiers, logs or signing materials to public issues.

## Automated results

`python3 scripts/run_unity_tests.py`: **30 EditMode + 17 PlayMode passed**, 0 failures, 0 skipped, both Editor exits 0. The metadata checker reports 0 errors and 0 warnings across 70 checked files; `git diff --check` is clean.

| Coverage | Named evidence |
| --- | --- |
| Architecture/baseline | `CoreBoundaryTests`, `BootstrapSceneTests`, `AndroidOrientationTests` |
| Movement and facing | `BoundedMovementTests`, `PlayerMovementTests` |
| Gesture ownership and interruption | `JoystickGestureTests`, `TouchJoystickTests.FocusPauseAndDisable_RequireFreshTouchesAfterReturn`, `FocusReturningBeforeUnpause_DoesNotEnableHeldTouch` |
| Camera smoothing and full framing | `BoundedCameraFollowTests`, `RunningCameraTests` (center, edges, corners, three landscape aspects) |
| Touch → movement → camera | `TouchJoystickTests.IntegratedTouch_MovesFacesAndDrivesCameraThroughTurnAndRelease` |
| Touch → field/camera bounds → interruption recovery | `TouchJoystickTests.IntegratedTouch_BoundsFramingAndFreshInputSurviveInterruption` |

Both integrated tests use virtual touch events, not direct movement commands. They inspect automatic facing, real camera translation/fixed orientation, full visual bounds and viewport-ground containment. The interruption test moves inward before interruption so a field boundary cannot conceal stale movement. Test-only fixed timestep and input-routing settings are restored afterward.

Negative control: temporarily disabling the camera in the first new test produced the expected camera-follow failure and runner exit 2. The temporary line was removed before the successful full run and build. Lifecycle simulations do not prove physical lock/app-switch handling.

## Parent acceptance and matching-build human status

Earlier [joystick and camera observations](../README.md#physical-pixel-checks) belong to their explicitly recorded APK hashes. They are historical evidence, not automatically promoted to this final output.

| Parent #8 requirement | Automated / technical evidence | Human result on final APK |
| --- | --- | --- |
| Landscape, one character, fixed bottom-left joystick | Scene, orientation and safe-area tests; installed landscape configuration | Visible movement and joystick operation confirmed; Android measured 2400 × 1080 |
| Screen-relative direction, analog speed and maximum | Movement and actual-touch tests | Upward/lateral movement and near-center slow versus edge fast confirmed |
| Captured overdrag and direction changes | Actual touch path and gesture tests | Continued control and direction changes outside the circle confirmed |
| Immediate start/turn/release and automatic facing | Movement and integrated touch/turn/release test | Immediate release stop and forward marker alignment with travel confirmed |
| Field boundary containment | Movement tests plus integrated right/top boundary route | Perimeter traversal through all four corners remained inside the field |
| Smooth fixed camera, visibility and no outside empty areas | Camera routes and integrated full-framing assertions | Perimeter visibility/no joystick obstruction/no outside empty area confirmed; smooth framing accepted without bothersome rotation or jumps |
| Interruption neutralization and fresh-touch recovery | Focus/pause/disable simulations and integrated recovery | Lock/unlock and Android Home/app-return stayed neutral; a fresh touch restored normal control in both cases |
| Internally configurable, calibrated values; no settings UI | Serialized movement, joystick and camera values; no runtime parameter changes in #12 | User explicitly accepted speed, sensitivity and camera as comfortable; existing values retained |
| Public-behavior checks preserve baseline | 47 tests above, including two new cross-boundary checks | Automated result recorded |
| Updated physical development build | Successful explicit-scene build, install/launch and installed hash | Human checks above performed on this final installed APK |

## Calibration and limits

Accepted values remain 5 units/second, joystick sensitivity 1, diameter 240 reference units, padding 64, camera pitch 60°, height 7, orthographic half-height 2.2, response 8/second and 5% viewport margin. The 20 × 20 field has a conservative 2-unit movement/framing inset, giving center limits -8 to +8. See [configuration details](../README.md#bounded-running-camera-11).

No production parameter change was needed in this verification unit. After the matching-build physical checks, the user explicitly accepted the current speed, joystick sensitivity and camera comfort, including no bothersome rotation or jumps. The calibration decision is to retain these values, not make an arbitrary change. A future reported issue requires scoped tuning, affected test reruns, a new matching APK and renewed relevant physical checks.

This is one-device functional evidence, not exhaustive interruption/trajectory coverage, stable FPS, thermal readiness, production readiness, match-rule coverage or final art. Passing-wide framing and passing/running transitions remain deferred. The parent acceptance matrix now records the selected functional checks and remaining general limitations. Physical cancellation via disabling the component, outside-start gestures and secondary-finger ownership were not separately repeated in this final phone pass; the final automated suite covers them. Delivery and current issue status are tracked on GitHub; this functional evidence does not imply production readiness.

Rollback for #12 alone: remove the two integrated tests and their framing helper, restore the previous touch-test timestep setup and remove this report/its README link. Production movement, touch and camera behavior are unchanged by this verification unit.
