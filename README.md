# QuestWwiseTest

A throwaway spike project. It exists to test Wwise audio integration on Oculus
Quest. It is not a product and was never developed beyond the initial import:
the repository has a single commit ("init", April 2020).

## Facts

| Item | Value |
| --- | --- |
| Engine | Unity 2019.2.9f1 |
| Product name | QuestWwiseTest |
| Wwise Unity integration | 2019.1.7.1611 (`Assets/Wwise/Version.txt`) |
| Wwise authoring install recorded in settings | Wwise 2019.1.7.7135, Windows |
| Scenes | one, `Assets/Scenes/SampleScene.unity` |
| Scripting runtime | .NET 4.x equivalent (`scriptingRuntimeVersion: 1`) |

## Contents

| Path | Contents |
| --- | --- |
| `Assets/Oculus/` | Vendored Oculus SDK: VR, Avatar, LipSync, Platform, Spatializer, AudioManager, SampleFramework. |
| `Assets/Wwise/` | Vendored Audiokinetic Wwise Unity integration. |
| `Assets/Scenes/` | `SampleScene.unity`. |
| `Assets/StreamingAssets/Audio/GeneratedSoundBanks/` | SoundBank output location used at runtime. |
| `Assets/WwiseSettings.xml` | Points the Unity integration at the authoring project and sets the SoundBank path. |
| `QuestWwiseTest_WwiseProject/` | The Wwise authoring project. |
| `Packages/com.sketchbox.logging/` | Logging package, see below. |
| `WwiseUnityIntegration_*_Src.zip` | Integration source archives for Android, Mac and Windows, as shipped by the Wwise Launcher. |
| `logRunSetup.txt` | Captured Unity editor log from the original integration run. |

## Code

There is effectively no first-party C# in this repository. All 715 `.cs` files
under `Assets/` belong to `Assets/Oculus` or `Assets/Wwise`, both vendored SDKs.
Do not edit them. The only first-party C# is the logging package under
`Packages/`.

## Wwise authoring project

`QuestWwiseTest_WwiseProject/QuestWwiseTest_WwiseProject.wproj` is the authoring
project that the Unity integration reads. `Assets/WwiseSettings.xml` references
it by relative path:

```
../../QuestWwiseTest/QuestWwiseTest_WwiseProject/QuestWwiseTest_WwiseProject.wproj
```

That path assumes the repository is checked out next to the Unity project
directory as the original author had it. If the Wwise picker reports a missing
project, re-point it via Unity's `Wwise > Settings` rather than editing the XML
by hand.

SoundBanks are generated into `QuestWwiseTest_WwiseProject/GeneratedSoundBanks/`
with `Android`, `Mac` and `Windows` platform folders.
`CopySoundBanksAsPreBuildStep` is enabled and
`GenerateSoundBanksAsPreBuildStep` is disabled, so banks are copied into
`StreamingAssets` on build but must be generated from the Wwise authoring tool
first.

## Opening the project

1. Install Unity 2019.2.9f1.
2. Open the repository root as the Unity project.
3. Install Wwise 2019.1.7 if you intend to regenerate SoundBanks. The Unity
   integration is already vendored into `Assets/Wwise`, so the Wwise Launcher
   does not need to re-integrate it.
4. Confirm the Wwise project path under `Wwise > Settings`.
5. Open `Assets/Scenes/SampleScene.unity`.

Android build target for Quest. `Assets/Oculus/OculusProjectConfig.asset` holds
the Oculus platform settings.

## Logging

The repository contains `Packages/com.sketchbox.logging`, a logging facade and
global exception handler. See `Packages/com.sketchbox.logging/README.md` for the
API.

- Runtime code: `Packages/com.sketchbox.logging/Runtime/` (`Log.cs`,
  `GlobalExceptionHandler.cs`).
- Editor tests: `Packages/com.sketchbox.logging/Tests/Editor/LogTests.cs`.
- The package is registered under `testables` in `Packages/manifest.json`, so
  its tests appear in the Unity Test Runner.

No first-party call sites were converted, because this repository has no
first-party logging code. Every `UnityEngine.Debug.Log*` call in the tree is
inside vendored SDK code, which is deliberately left unchanged. The package is
present here for consistency with the other repositories and to keep the
Editor tests running in CI, not because it is used by project code.

## CI

`.github/workflows/ci.yml` validates that all JSON, `.asmdef` and `.asmref`
files parse and that assembly names are unique.
`.github/workflows/tests.yml` runs Unity edit-mode tests via
`game-ci/unity-test-runner`; it is skipped unless Unity credentials are set in
repository secrets.
