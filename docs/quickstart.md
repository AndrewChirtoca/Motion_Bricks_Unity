# Quickstart

The playable prototype uses a **local Python ONNX Runtime CPU provider** and Unity 6000.3.10f1. NVIDIA GPU inference was separately tested for the MotionBricks backbone and Unity native candidate. No model-host login is needed for the pinned public artifacts.

## Run the existing build

From PowerShell:

```powershell
Set-Location D:\Projects\Motion_Bricks_Unity
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\start-service.ps1
.\Artifacts\Windows\MotionBricks.exe
```

The service command waits for readiness and runs the owned provider without a terminal window. `-ExecutionPolicy Bypass` applies only to that PowerShell process; it does not change the saved user or machine execution policy and does not require administrator access. The final command opens the interactive Windows player. The scene shows the cyan source skeleton, colored root axes, recent trajectory, and the supplied human character. The on-screen panel identifies the Python provider and counts generations, underruns and rejected buffer responses.

Controls: **WASD** moves relative to the camera; **left Shift** runs; releasing movement selects idle. **Left Alt** keeps facing independent of movement; use **Q/E** to adjust facing (hold Alt while moving). **N** toggles source-joint names. **Escape** quits. The controller uses generated root motion; walls on layer 8 trigger capsule sweeps and context rebasing.

Stop/restart the owned provider with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\stop-service.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\start-service.ps1
```

The player holds its last pose while disconnected and reconnects automatically. The stop script checks the owned launcher's PID, creation ticks and executable path. It never searches for or terminates unrelated Python processes. An occupied port produces a startup error; inspect the log instead of killing an unknown process.

## Reproduce from a clean checkout

Install/activate the project's exact Unity version through Unity Hub, with Windows build support. `uv` must be available on PATH. Close this project's editor before batch commands. All other Python installs/downloads below stay inside this repository.

```powershell
Set-Location D:\Projects\Motion_Bricks_Unity
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\setup.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action environment
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\start-service.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action edit-tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action play-tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\run-smoke.ps1 -Seconds 600 -Name endurance
```

`setup.ps1` installs Python 3.11.15, the exact reference dependency lock, verifies the public planner's publisher SHA-256/size, downloads the pinned G1 XML, and generates real inference fixtures. It does not download a dataset. The minimal planner is approximately 774 MB. A fuller backbone setup downloads approximately 2.3 GB of additional checkpoints plus a CUDA PyTorch wheel and dependencies; disk usage is larger after installation.

For the separate target-pose experiment:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\setup.ps1 -Backbone
.\.venv\Scripts\python.exe .\Tools\motionbricks_reference\backbone_probe.py
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action chair-authoring
```

The official backbone checkpoint loading uses `weights_only=False` on verified, pinned NVIDIA checkpoints. No dataset is instantiated or training run. `Artifacts/backbone/placements.json` records the nine seated-pose trials and their failures against the 5 cm human-scale criterion.

## Project layout and editor use

Open **`D:\Projects\Motion_Bricks_Unity\Motion_Bricks_Unity`**, not the outer repository folder, in Unity 6000.3.10f1. Keep the FBX at `Assets/Models/SK_Player_01.fbx`; its original Humanoid import configuration is preserved.

- `Assets/MotionBricksUnity/Samples/LocomotionDemo.unity`: live service-based locomotion.
- `Assets/MotionBricksUnity/Samples/SourcePlayback.unity`: recorded reference playback, explicitly labeled; no service required.
- `Assets/MotionBricksUnity/Samples/SmartObjectAuthoring.unity`: chair constraints and preview gizmos, **not a completed generated interaction**.
- `SKPlayerMapping.asset`: generated rest-pose/chain mapping and human scale.
- `ChairDefinition.asset`: reusable source-rig entry/seated/exit poses, token timing, supported frame masks and recovery policy data.
- `Models/`: ignored local planner/native conversion cache.
- `External/motionbricks/`: ignored pinned backbone source/checkpoints, if requested.
- `Artifacts/`: ignored builds, fixtures, logs, test XML, captures and measurements.

The **Tools → MotionBricks** menu regenerates scenes and mappings. This intentionally replaces generated demo configuration; preserve custom authoring under a different asset name. The original character and renderer configuration are not authored by this tool. Regenerate after a clean checkout because recorded motion JSON is deliberately not committed. The generated scene references are recreated together with the fixtures.

The Windows build is a development Mono player, using the project's graphics configuration (D3D12 on this machine). The build script supplies its own scene list; it does not require manually changing the project's build-scene list.

## Additional validation

```powershell
# Provider framing, real inference and reconnect tests (provider must be running):
.\.venv\Scripts\python.exe -m unittest discover -s Tools/motionbricks_reference -p test_reference.py -v

# Native investigation, not the runtime provider:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action native-cpu
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\unity.ps1 -Action native-gpu

# Deliberately stop the owned provider after 8 s and restart after 14 s:
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\run-smoke.ps1 -Seconds 35 -InjectFailure -Name recovery
```

Smoke runs use hidden windows, `Application.runInBackground`, and explicit offscreen 1920×1080 render requests. This avoids the black/paused behavior of an occluded Windows swapchain. Captures and measurements go to `Artifacts/smoke-<Name>/`. Interactive player presentation is separate from this automated capture mode.

## Troubleshooting

- **Running scripts is disabled:** use the `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...` commands shown above instead of invoking `.ps1` files directly. No permanent policy change is needed.

- **Provider unavailable:** read `Artifacts/service-error.log`; confirm the planner exists and start the service. Connection/inference timeout is three seconds, retry interval one second. Startup readiness has a 15-second deadline.
- **Hash mismatch or LFS pointer:** do not use that file. Remove only the identified cached artifact and rerun `setup.ps1`; the fetcher verifies publisher hashes for planner/checkpoints.
- **Unity already open/license errors:** close this project's editor or complete Unity Hub activation, then rerun the failed action. Batch logs are under `Artifacts/unity-*.log`.
- **Python not on PATH:** setup uses `.venv\Scripts\python.exe`; no global `python` command is required after setup.
- **Git ownership error in a sandbox:** read-only commands can use `git -c safe.directory=D:/Projects/Motion_Bricks_Unity ...`; no global setting is necessary.
- **Chair does not run:** intentional capability gate. The authoring scene is a preview, and the released backbone's tested seated poses have not met the acceptance tolerance. See `model-contract.md`, `validation.md`, and `limitations.md`.

