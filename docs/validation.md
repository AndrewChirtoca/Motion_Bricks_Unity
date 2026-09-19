# Validation

Machine and exact versions: [environment.md](environment.md). Reproduction commands: [quickstart.md](quickstart.md). All results below use real published weights unless a test is explicitly a mathematical/protocol boundary test.

## Acceptance ledger

| Criterion | Result and evidence |
|---|---|
| Exact Unity editor/license/Windows build support | PASS: 6000.3.10f1 compiled and built the empty scene, then the locomotion player |
| Supplied character | PASS: FBX imported; Avatar valid and Humanoid; modular renderer selection applied in generated scenes |
| Artifact provenance/integrity | PASS: immutable source/artifact revisions; actual ONNX metadata; planner and all four checkpoint files match publisher LFS SHA-256 and size |
| Real reference locomotion | PASS: idle, walk, left, right, stop, run; finite outputs; 36/44 valid frames; fixed-seed repetitions identical |
| Source geometry/basis | PASS: 7 EditMode tests, including matrix basis conversion, quaternion round trip, SciPy/Unity FK, frame truncation, delayed/stale buffer results, starvation/rebase, smart-object placement and timestamp roundoff |
| Independent FK | PASS: 12 randomized poses match actual MuJoCo body positions/rotation matrices to 1e-7 after removing only unused rendering geometry/assets |
| Live Unity integration | PASS: 4/4 PlayMode tests covering service readiness, generated motion on the supplied Avatar, camera-relative keyboard movement and rapid reversals, recorded playback, cancellation/reconnect and sole bone writer |
| Python boundaries and live provider | PASS: 5/5 tests, including actual loopback inference/reconnect and independent MuJoCo FK |
| Native Unity route | IMPORT/EXECUTION PASS, INTEGRATION GATE NOT PASSED: strict full-fixture parity and main-thread budget fail; runtime uses disclosed Python fallback |
| Human playback visual QA | Source/human representative frames inspected; movement/turning and side-by-side correspondence visible. Artistic motion quality, foot locking and owner acceptance remain open |
| Windows player | PASS: development Windows x64 Mono player starts, generates and renders actual 1920×1080 offscreen frames |
| Service interruption | PASS on verified 35-second test: provider absent for six seconds, two counted starvation events, generation recovered; no unrelated process terminated |
| Ten-minute nominal run | PASS: 600 seconds of live generation, 682 accepted batches, zero underruns/rejections; stable process memory/thread counts; see measurements below |
| Chair extension | FAIL/INCOMPLETE: real pose-conditioned backbone output in three placements, but every tested endpoint exceeds 5 cm at human scale; authoring delivered, complete generated chair sequence not accepted |

## Reference and native timings

Initial independent ONNX Runtime CPU run: model load **1.404 s**, first inference **32.63 ms**, warm p50 **30.13 ms**, warm p95 **32.18 ms** over 30 warm calls (five repetitions per command). Harness RSS was **876.9 MiB**. Root motion and valid counts were checked; no frame padding was played.

Native Unity initialization: CPU first schedule/readback **136.1 ms**, maximum absolute error **8.97e-5**; GPU first uncached execution/readback **18.17 s**, error **7.08e-5**. A later full GPU probe with cached shaders took roughly **22–25 ms just to schedule**, and approximately **40–44 ms including readback** on warm calls. This is not a separately measured GPU kernel duration. The left-turn fixture exceeded the original 1e-4 tolerance (about **1.4046e-4** maximum absolute error). The tolerance was not relaxed to claim full parity. An editor-exit persistent-allocation warning is retained in the native investigation logs.

The native importer warnings default LayerNormalization axis and some Resize attributes; this graph did execute. The blocker is the unoptimized scheduling cost and incomplete numerical/resource certification, not a claim that Unity cannot import ONNX. The reference service is a measured, functional fallback.

## Player timing method and findings

Normal smoke runs execute real rendering through a URP offscreen render request. The initial hidden-swapchain screenshots were black and were **discarded as rendering evidence**. `Application.runInBackground` keeps tests advancing when the application lacks focus.

The ten-minute run accepted **682** batches with **zero underruns and zero rejections**. Update+LateUpdate CPU p95 was **0.0696 ms**, total frame interval p95 **16.6674 ms**, and request completion p95 **66.71 ms**. After the first 30 seconds, player working set stayed within **659.5–665.9 MiB**, provider within **870.92–870.97 MiB**, player threads within **129–133**, and provider threads within **50–53**. These observations show no increasing worker/memory trend over this run; they do not prove indefinite absence of leaks.

“Observable source response” compares played qpos with the previous predicted timeline sampled at 60 Hz and interpolated to the actual playback time after a meaningful command change. It records the first root difference over 1 cm, hinge difference over 0.02 rad, or root rotation difference over 1 degree, only after the response for that command has been accepted. It excludes initialization. It is a source-pose instrumentation measure, not a human perception study or display-photon latency. Requests that never create that difference within two seconds do not produce a sample; sample count is reported. Request completion is reported separately. The earlier short-run and endurance reports used floor sampling of the prior timeline; their observable-response fields are retained as raw history but excluded from the final latency claim.

The final build changes following endurance only interpolate this measurement, exclude initialization, add its rotation criterion, and enlarge the visible ground plane. Generation, buffering and retargeting remain the endurance-tested implementation. A focused two-minute final-build timing run validates the revised measurement separately.

**Final two-minute result:** 138 accepted batches, zero underruns/rejections, Update+LateUpdate CPU p95 **0.0747 ms**, frame interval p95 **16.6674 ms**, request completion p95 **66.74 ms**, and observable source-pose response p95 **66.72 ms** across **29** command changes. The source-pose metric meets the provisional 100 ms budget under the stated method; displayed human response still requires owner review. The final player log contains no reported exception, NaN or infinity.

The final context window ends near expected inference completion instead of starting there, removing approximately 100 ms of unnecessary known future context. Playback time never rewinds. JSON timestamp matching allows at most one microsecond of serialization roundoff while request IDs and model revisions remain exact.

Feature CPU instrumentation surrounds Update and LateUpdate, including retargeting and diagnostic transform updates; it excludes OnGUI and explicit rendering/capture. Therefore the <1 ms **entire-feature** CPU requirement is not fully certified by this timer alone. Separate inference GPU timing is not available for the selected CPU provider. Frame intervals include normal scheduling and rendering; capture frames are rare outliers. A displayed, user-driven 1080p profiler capture remains useful owner acceptance work.

## Chair conditioning results

The headless backbone loaded on CUDA and generated finite 32/48/64-frame seated transitions from custom source-rig targets. Peak allocated PyTorch memory was about **762.3 MiB** (allocated tensors, not total driver-reserved VRAM). After the first approximately 327 ms inference, calls in the placement probe took roughly **40–53 ms**, including synchronization.

| Placement (source XY metres, yaw) | 8 tokens | 12 tokens | 16 tokens |
|---|---:|---:|---:|
| (0, 0), 0° | 13.47 cm | 10.46 cm | 10.61 cm |
| (2, 1), +90° | 9.09 cm | 6.98 cm | 6.81 cm |
| (-1, 2), -45° | 13.01 cm | 12.09 cm | 12.27 cm |

Values are maximum final joint-position error scaled by the generated human root scale, 1.4193771. Every cell fails 5 cm. Minimum source joint heights remained positive, but these joint points do not measure foot-surface or seat penetration. There is no validated complete approach/sit/seated/stand/interruption sequence. The smallest next step is a better source-rig target/contact authoring and constraint-fit pass on this existing backbone, preserving locomotion; no training is proposed or performed.

## Evidence and exclusions

- `Artifacts/reference/report.json`, NPZ/JSON fixtures and `source-skeleton.png`: independent planner results.
- `Artifacts/native-cpu.txt`, `native-gpu.txt`, and `unity-native-*.log`: native investigation. The first CPU initialization result also appears in its log.
- `Artifacts/edit-tests.xml`, `play-tests.xml`: exact Unity test outcomes.
- `docs/evidence/`: retained small reference reports, 7/7 EditMode and 4/4 PlayMode XML, 5/5 Python test output, build result, endurance samples/summary, and final build/source hashes. The headless controls test uses InputState.Change because queued virtual keyboard events were discarded in batch mode; it tests the actual gameplay input reader and generated movement, not OS keyboard event routing.
- `Artifacts/backbone/placements.json`, `sit-<placement>-<tokens>.npz`, `chair-pose-comparison.png`: real backbone conditioning failures.
- `Artifacts/smoke-recovery-verified/`: actual outage/recovery run. The earlier `smoke-recovery` attempted a duplicate service and did not interrupt the real provider; it is **not** accepted outage evidence. Launcher ownership now uses creation ticks and waits for readiness.
- `Artifacts/smoke-endurance/`: final ten-minute build run, process memory/thread samples, captures and timing report.
- `Artifacts/smoke-final-latency/`: focused final-build check and representative rendered frames. The offscreen camera capture excludes the OnGUI panel; the interactive window displays that panel.
- The earlier `smoke-nominal` was deliberately stopped to investigate nearly static captures and exact timestamp comparison; it is not accepted endurance evidence.

No placeholder animation or recorded fixture is counted as live inference. No claim of native runtime completion, human-motion artistic quality, collision-free generative interaction, height generalization or crowd capacity is made.
