# Third-party notices

The prototype consumes NVIDIA GEAR-SONIC planner weights and MotionBricks checkpoints, licensed under the NVIDIA Open Model License. Upstream source and the G1 XML-derived skeleton data are attributed to **NVIDIA CORPORATION & AFFILIATES**, copyright 2026, under Apache License 2.0. Full upstream notices are included here. Source/artifact revisions and hashes are recorded in `model-manifest.json`.

`Tools/motionbricks_reference/backbone_probe.py` adapts feature-construction semantics from the pinned MotionBricks `full_agent.py` and uses its unmodified neural modules. It is a new headless integration, not an upstream file. `Samples/G1Skeleton.json` is derived from the pinned G1 XML; body hierarchy and transforms retain source semantics.

ONNX Runtime, ONNX, NumPy, SciPy, Matplotlib, PyTorch and their dependencies retain their individual package licenses in the local Python environment. Unity packages retain their licenses in Package Manager. No Python package binaries or model binaries are included in ordinary Git tracking.

The owner supplied `SK_Player_01.fbx` and related materials/textures. No additional rights to redistribute that character are granted by this integration. No BONES-SEED data was downloaded.
