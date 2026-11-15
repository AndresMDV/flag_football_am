# Flag Football Android Prototype

A minimal Unity URP baseline for an offline 5v5 flag football prototype. This increment proves project initialization, assembly boundaries, and automated tests. It does not implement gameplay.

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
| `Assets/Core` | Pure C# assembly with engine references disabled. Its marker supports architectural checks; no game rules exist yet. |
| `Assets/Unity` | Unity-facing components, currently scene initialization only. May reference Core, never the reverse. |
| `Assets/Editor` | One-time baseline asset/scene setup, plus the repeatable Android development build action. Setup refuses to overwrite existing baseline assets. |
| `Assets/Tests/EditMode` | Core assembly identity and absence of Unity engine/editor dependencies. |
| `Assets/Tests/PlayMode` | Real scene loading, `Awake` initialization, camera/placeholder presence, and URP assignment. |
| `Assets/Scenes/Bootstrap.unity` | Initial scene with a camera, directional light, ground, and an orange placeholder cube. |

Add this repository root as an existing project in Unity Hub and open it with the pinned Editor. The scene, URP assets, project settings, and `.meta` files were created by the Editor, not hand-authored serialized scene data. Keep assets together with their metadata.

## Build and launch the Android baseline

From the repository root, with the Editor closed:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity"
python3 scripts/run_unity_tests.py && \
mkdir -p TestResults/AndroidBuild && \
"$UNITY" -batchmode -quit -nographics -projectPath "$PWD" -buildTarget Android \
  -executeMethod FlagFootball.Editor.BuildAndroid.Run \
  -logFile "$PWD/TestResults/AndroidBuild/Editor.log"
```

The build action configures and persists a development-only ARM64/IL2CPP APK, OpenGL ES 3, minimum API 25 and target API 36. It selects only `Bootstrap.unity`, uses the default debug signer (not a custom release keystore), and rejects an unsuccessful `BuildReport` or missing APK. Outputs are ignored: `Builds/Android/FlagFootballPrototype.apk`, `build-report.json`, and the Editor log above. The report's total size includes other build artifacts, not just the compressed APK.

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
- Dependency versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

Headless PlayMode checks establish scene/lifecycle behavior and pipeline configuration, not rendered pixels, touch usability, FPS, or thermal performance. The runner's failure path was verified by temporarily breaking an assertion, observing a failed NUnit report and nonzero command exit, then restoring the assertion and rerunning both suites successfully.

GitHub Issues is the specification/task tracker: [prototype specification](https://github.com/AndresMDV/flag_football_am/issues/1). The [Pixel 6a build and visible-launch task](https://github.com/AndresMDV/flag_football_am/issues/7) remains open until human visual confirmation. No cloud services, player accounts, monetization, or optional pre-release automation are part of this baseline.
