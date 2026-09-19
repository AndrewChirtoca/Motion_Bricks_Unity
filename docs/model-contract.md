# Pinned model contract

Source: [NVlabs/GR00T-WholeBodyControl at 7f151314](https://github.com/NVlabs/GR00T-WholeBodyControl/tree/7f151314d4d1606544bf249d2a7a1cb754c64582/motionbricks).
Planner artifact: [nvidia/GEAR-SONIC at 6733128a](https://huggingface.co/nvidia/GEAR-SONIC/tree/6733128a3d8a523b1418b06bca3cdf61c8b0987f).
All downloaded artifacts, sizes and SHA-256 values are in `model-manifest.json`. Large files are local, ignored artifacts. Code: Apache 2.0; weights: NVIDIA Open Model License. Full notices are in `ThirdPartyNotices/`.

## Selected locomotion route

The runtime uses the released **GEAR-SONIC G1 kinematic planner ONNX** through a loopback Python ONNX Runtime CPU service. This is not the MotionBricks Python backbone or a human checkpoint. No suitable released human checkpoint was found among the inspected official artifacts. The independent backbone experiment below establishes its separate provenance and actual pose interface.

Actual graph inspection: ONNX opset 17, 773,952,989 bytes, no custom metadata. Batch size is one. Exact tensor contract:

| Input | Shape | dtype |
|---|---|---|
| context_mujoco_qpos | 1×4×36 | float32 |
| target_vel | 1 | float32 |
| mode | 1 | int64 |
| movement_direction, facing_direction | 1×3 each | float32 |
| random_seed | 1 | int64 |
| has_specific_target | 1×1 | int64 |
| specific_target_positions | 1×4×3 | float32 |
| specific_target_headings | 1×4 | float32 |
| allowed_pred_num_tokens | 1×11 | int64 |
| height | 1 | float32 |
| Output mujoco_qpos | 1×64×36 | float32 |
| Output num_pred_frames | **1** | **int32** |

The last output differs from the documentation's scalar/int64 description. Only the first valid count is consumed. Frames run at 30 Hz; four context frames also occupy the first blended frames of the returned timeline. Output frame zero is at the request's context timestamp, not the arrival time.

The 36 pose values are XYZ root metres, root quaternion **wxyz**, and 29 radians in the official MuJoCo body-tree order. Model space is right-handed X forward, Y left, Z up. Unity presentation uses `(-y,z,x)` and rotation conjugation `B R B^-1` (determinant -1). Model history always stays in original model coordinates. FK includes XML body offsets, rest quaternions, serial hinge axes and joint pivots. The mesh-bearing head is fixed to the torso; there is no independent head hinge in this output.

Initialization uses the pinned C++ default root height 0.788740 m and identity heading with zero hinges, repeated four times; this neutral hinge bootstrap is an explicit integration choice. Subsequent contexts come from generated motion. The reference case uses seed 1234 and default token mask `[1,1,1,1,1,1,0,0,0,0,0]`. This artifact produced 36 or 44 valid frames in the tested cases. Repeat runs on ONNX Runtime CPU were identical. Cross-backend identity is not assumed.

Validated/exposed modes: idle 0, slow walk 1 (interface exposed; not separately quality-tested), walk 2, run 3. The harness tests idle/walk/run, turns and stopping. `target_vel=-1` selects mode defaults; zero does not mean stop. Idle uses mode 0 and zero movement. Facing is independently supplied. Height remains -1. Other library styles are not exposed in the demo. Reference clips and normalization are embedded in this graph. There is no external normalization of qpos.

Root waypoint inputs and selection among embedded clips do **not** expose arbitrary target body poses. The loopback service reports target-pose support false. It accepts only finite, bounded messages on 127.0.0.1:17861, little-endian 4-byte length plus UTF-8 JSON, version 1, at most 256 KiB, one processed request at a time. Envelopes include request ID, model revision, session time origin, context timestamp, frame rate and valid count. Timeouts, cancellation, reconnection and process ownership are explicit.

## Native investigation

Unity Inference Engine 2.6.1 imports the graph and executes on CPU and GPUCompute. The importer warns about defaulted LayerNormalization/Resize attributes. No unsupported-operator import failure was observed. The actual GPU test includes backend fallback/readback costs; it is not a pure neural GPU kernel timing.

Initialization error was below 0.0001; the full suite includes a left-turn maximum error about 0.00014046, exceeding the initial strict 0.0001 tolerance. Tolerance has not been relaxed to mark the suite passed. More consequentially, warm GPU scheduling consumes roughly 22–25 ms on the main thread, and total execution/readback roughly 40–44 ms. This exceeds the 1 ms feature budget and motivates the disclosed service route. An iterable scheduling/export optimization and wider numerical certification remain future native-port work. The native probe also reports one persistent allocation on editor exit; no claim of a leak-free native runtime is made.

## MotionBricks backbone / interaction experiment

All three official G1 checkpoints (pose, root, VQ-VAE), their configurations, skeleton/statistics and G1 clip library were downloaded from the same source commit. The loader bypasses dataset construction (`return_dataloader=False`), loads actual state dictionaries, and uses the official inference module on CUDA. For its converter use **g1.xml**, not g1_29dof.xml: the latter contains a floating-base name not present in the converter skeleton and produces a reproducible KeyError.

The backbone uses G1Skeleton34, a 418-dimensional dual motion representation (414 global, 413 local). Its motion-space basis is right-handed Y up / Z forward and differs from both MuJoCo and Unity. The official converter handles this. `predict` consumes unnormalized global-root values [1,8,5], local-root values [1,8,4], body features [1,8,303], and per-frame Boolean masks. Body features comprise 33 relative joint positions and 34 world 6D rotations. The first four frames are context; the final four supply the target. Mean/std arrays and epsilon 1e-5 are applied inside the official module. Text conditioning is not exercised. Token count is 8, 12 or 16 in the probe, four frames per token; argmax pose-token sampling and seed 1234 control randomness.

Custom sitting poses are authored on the source rig and converted through that interface. Three translated/rotated placements and three durations generate finite output, proving actual full-pose access. Every tested final pose exceeds 5 cm after the mapping's human scale of 1.4193771 (best observed about 6.8 cm; others up to 13.5 cm). This is a **quality/constraint-fit failure**, not missing weights, missing GPU capacity or absent pose-conditioning access. Joint-height checks are geometric proxies, not mesh penetration measurements. A complete generated chair interaction is not accepted or substituted with playback.
