# Environment

Validated 19 September 2026 on Windows x64.

| Item | Observed |
|---|---|
| Repository baseline | `cbf2c69`, clean before execution |
| Unity project | `D:\Projects\Motion_Bricks_Unity\Motion_Bricks_Unity` |
| Editor | `6000.3.10f1 (e35f0c77bd8e)` |
| Editor executable | `C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe` |
| Build module | `windowsstandalonesupport`, Windows x64 build succeeded |
| Renderer | Existing URP `17.3.0` |
| Inference package | `com.unity.ai.inference` `2.6.1` |
| Test framework / input | `1.6.0` / `1.18.0` |
| CPU | AMD Ryzen 9 5900X, 12 cores |
| RAM | Unity reports 32,686 MiB |
| GPU | NVIDIA GeForce RTX 3070, 8,192 MiB VRAM, WDDM |
| NVIDIA driver | `610.74`, driver advertises CUDA UMD `13.3` |
| PyTorch runtime | `2.11.0+cu128`; CUDA available; no separate Toolkit installed |
| Python | Project-local CPython `3.11.15` |
| ONNX / ONNX Runtime | `1.23.0` / `1.30.0` |
| Disk at start | approximately 119.8 GB free on D:, 159.7 GB free on C: |
| Git LFS | `3.5.1` |
| Character | `Assets/Models/SK_Player_01.fbx` |
| Avatar | Exists, valid, Humanoid (engine checked) |
| Renderers | 74 modular/LOD renderers; generated demo selects `male_body1_noGloves_LOD0` plus non-body parts |

The headless `-nographics` inventory reports a Null Device; use `nvidia-smi` and graphical native/player runs for GPU observations. The exact runtime dependency closure is saved in the two requirements lock files. No dataset, Isaac Sim, ROS, robot SDK, or training job was installed or run.

Read-only inventory encountered sandbox restrictions on networking/CIM; approved execution supplied network and Unity access. Git reads use a per-command `safe.directory` override, without changing global configuration. Batch launches are hidden. Scripts identify and stop only their owned service process.

Raw evidence: `Artifacts/rig-validation.txt`, `Artifacts/empty-build.txt`, `Artifacts/unity-environment.log` and subsequent validation logs. These generated files are intentionally ignored by Git.
