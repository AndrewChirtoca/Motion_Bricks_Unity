# Selectable inference backends — implementation plan

Status: proposed, 20 September 2026. This document plans the feature; the described new providers, settings UI and benchmark commands are not implemented yet. Existing runtime behavior and the stopped service are unchanged.

## 1. Outcome and scope

Make the inference backend an explicit, inspectable choice for a deployment platform and hardware configuration. Each backend owns a typed set of meaningful settings. A common benchmark system measures correctness, responsiveness, rendering impact, resource use and reliability, retaining the exact settings behind every result. Humans use an Inspector/window and readable reports; agents use the same configuration, CLI and structured evidence.

First-release backend IDs:

| ID | Execution and deployment | Current evidence | Planned support |
|---|---|---|---|
| `python-ort-cpu` | Local Python process, ONNX Runtime CPU, loopback transport | Working Windows runtime and endurance baseline | Preserve as default; make session/transport settings configurable |
| `unity-cpu` | In-process Unity Inference Engine CPU/Burst jobs | Initialization fixture executed; warm performance not established | Implement runtime provider and full benchmark coverage |
| `unity-gpu-compute` | In-process Unity Inference Engine GPUCompute | Graph executes; scheduling and strict parity gates currently fail | Implement asynchronous, optionally frame-sliced provider; retain experimental status until qualified |

Do not equate a device name with support or speed. Runtime capability checks, real model probes, and target-player evidence decide eligibility. Windows x64 is the first qualification target. Other desktop/mobile/consoles need their own build, package/operator, memory, graphics API and runtime tests; Python-process availability must not be assumed there. Publish `available`, `unsupported`, `unverified`, `failed-probe` and `qualified` separately. Qualification is attached to a model/build/profile/target combination, not a permanent backend label.

The registry must allow later separately packaged providers: in-process ONNX Runtime CPU, Python ORT CUDA, and platform-specific ORT execution providers. They are follow-on candidates, not promised implementations in this first feature. An in-process ORT provider could remove Python while retaining an independent CPU executor, but needs native-library packaging, thread/lifecycle and Mono/IL2CPP validation. CUDA/DirectML/CoreML/TensorRT are not interchangeable switches: each needs an adapter, dependency lock, settings schema and qualification suite before appearing as selectable.

Backend choice must preserve the selected model contract. The GEAR-SONIC locomotion model remains 4 context frames, 30 Hz, 36-value qpos and up to 64 output frames. Switching to GPU does not add arbitrary target-pose conditioning or solve the chair milestone. MotionBricks backbone/interaction experiments stay a different workload and capability track.

## 2. Starting evidence and optimization implications

The runtime currently constructs `PythonMotionGenerator` in `LocomotionDemo.Start`. `Planner` fixes ORT intra-op threads to 4. The native editor probe calls whole-model `Schedule` and blocking `ReadbackAndClone`; it is not an optimized native runtime.

Recorded RTX 3070 / Ryzen 5900X results: native GPU scheduling typically 22–25 ms (one warm sample about 28.5 ms); total schedule/completion/readback approximately 39–45 ms. One left-turn result differs by 1.4046e-4 against the original 1e-4 tensor tolerance. The Python endurance run accepted 682 batches in 600 seconds with zero underruns; its final focused source-response p95 was 66.72 ms. These historical measurements are baselines, not a controlled comparison of optimized providers. See [validation](validation.md) and `docs/evidence/`.

Normal request cadence is already capped at 10 Hz and driven by command changes/buffer depletion, not every rendered frame. Collision correction currently resets the timer; centralize admission control so collision recovery also respects the configured cap. Motion playback continues independently at render rate.

Optimization order:

1. Reuse workers and compatible input storage; warm up explicitly outside scored steady state. Validate buffer/tensor ownership before reusing memory.
2. Replace blocking output readback with asynchronous readback for both qpos and valid count. Do not reuse a worker or dispose inputs/outputs while work/readback is pending.
3. Compare whole scheduling with `ScheduleIterable`, driven by a main-thread pump and an elapsed-time slice budget. Record individual iterator-step cost, total scheduling CPU and number of frames to finish.
4. Profile costly operations, CPU fallback, dynamic shape dependencies, uploads and synchronization. The pinned Worker source yields after non-fallback layers; a `MoveNext` may encompass several operations. A requested slice budget is a soft limit, not a preemption guarantee.
5. Measure the native CPU route fairly. Unity CPU computation already uses jobs; do not assume a GPU wins for this graph or that arbitrary `Task.Run` around Unity worker APIs is valid.
6. Only after profiling, investigate validated graph simplification/constant folding or backend placement. Preserve provenance, seeds, valid-count behavior and fixture parity. Precision conversion is a separately hashed model variant with an explicit quality gate, never a silent performance setting.

Slicing trades frame spikes for response latency. If 22 ms scheduling cost remained, 1 ms slices could need roughly 22 frames (367 ms at 60 FPS). Async readback also does not remove synchronization inside graph scheduling. If both frame and response budgets cannot be met, report an infeasible configuration instead of hiding the tradeoff with a larger buffer.

API basis: Unity documents [asynchronous readback](https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/read-output-async.html), [split scheduling](https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/split-inference-over-multiple-frames.html), and [backend execution/CPU fallback](https://docs.unity.cn/Packages/com.unity.ai.inference%402.6/manual/how-sentis-runs-a-model.html). Implementation must use the pinned 2.6.1 APIs and validate any package upgrade separately.

## 3. Configuration and selection design

Create `InferenceProfile` ScriptableObjects with a versioned JSON import/export equivalent. Use a discriminated settings union keyed by backend ID, not a flat object full of irrelevant fields. The Inspector displays only the selected backend's settings, units, restart requirements, validation messages and effective values. CLI and UI share one schema validator/resolver.

Separate four concerns:

- `ModelDescriptor`: immutable source/model hash, conversion hash, skeleton/units/dtypes, fixed FPS and supported capabilities. The native build uses a serialized model asset generated at editor/build time; no runtime reflection into the editor-only ONNX converter or dependence on a repository-relative file path.
- `InferenceProfile`: one backend and its settings plus shared request/buffer policy. Stable ID, version and canonical content hash.
- `BackendSelectionPolicy`: explicit profile, or an ordered allowlist of profiles for the current target. Default remains explicit Python for existing scenes. Explicit selection fails visibly unless a fallback is expressly configured. No silent fallback.
- `QualificationProfile`: performance/quality budgets and evidence requirements for a deployment, such as desktop-60fps or a separately defined mobile target. Different budgets are different comparisons, not altered results.

Resolution order: explicit benchmark/command-line override, scene-assigned profile, project default. An optional platform policy chooses among approved profiles; the result shows the source of every override. Do not mutate source assets when applying runtime overrides. Unknown or irrelevant settings are errors; supported defaults must be resolved and serialized. Never silently accept an unimplemented GPU/thread/precision knob.

Capabilities include model compatibility, locomotion/target-pose support, cancellation semantics, external-process requirements, graphics API requirements and supported settings. A readiness result includes requested backend, actual backend, any internal fallback knowledge, model hash, provider/runtime version and effective configuration hash. If actual per-operator placement cannot be observed, mark it unknown rather than claiming all-GPU execution.

### Shared scheduling and buffer settings

| Setting | Initial/default behavior | Rules |
|---|---|---|
| `requestPolicy` | `on-change-or-low-buffer` | Also allow `fixed-interval` for controlled tests and `adaptive` after separate validation |
| `maxRequestHz` | 10 | Initial supported tuning range 1–30 Hz; hard cap applies to all request causes; maximum one in flight |
| `commandThresholds` | Preserve existing 5-degree / direction-delta thresholds | Units explicit; semantic idle/walk/run changes always considered |
| `refillMinimumSeconds` | 0.30 | Report actual buffer lead; fixed model horizon limits useful tuning |
| `latencyEstimator` | Preserve current EMA initially; add rolling-p95 option | Specify window, warm-start, safety margin, bounds and minimum samples |
| `starvationPolicy` | Hold last pose with visible counter | No unbounded extrapolation; duration and recovery are measured |
| `requestDeadlineSeconds` | 3 | Deadline rejects late delivery; it does not guarantee physical GPU cancellation |
| `adaptiveBounds` | Disabled initially | Versioned policy, min/max cadence and slice budget, update window, hysteresis and dwell time |

Maintain canonical source-rig history independently of presentation. Four-frame context, timestamp origin, lookahead, history retention and future-buffer capacity must be consistent with measured end-to-end completion time. The current 150 ms lookahead clamp/200 ms history retention cannot simply be assumed adequate for a slower sliced provider. Validate feasible bounds against history and model horizon; reject impossible profiles. Coalesce input changes to the newest desired command while keeping at most one active request; count superseded commands explicitly. Rebase source context and invalidate old generations on collision.

### Backend-specific settings

**`python-ort-cpu`**

- Intra-op threads: default 4; 0 means runtime-selected and must be reported as such. Inter-op threads are configurable only with parallel graph execution. Graph execution: sequential by default; optimization level: all by default. Thread spinning: explicit on/off, initially preserve runtime baseline. Record CPU arena/memory-pattern options if exposed and supported by the pinned ORT version.
- Transport: loopback port 17861, connect/request timeouts, readiness deadline 15 seconds, retry backoff; bounded protocol messages. Separate service launch mode `external` (default for existing use) from optional `managed` owned lifecycle, with explicit Python executable/environment/model path.
- Session settings take effect only on service/session creation. An existing service is reusable only if its readiness configuration hash matches. A mismatch produces actionable diagnostics or a deliberate restart of an owned service; never report requested threads while actually using a different session.
- Service handshake must expose actual ORT providers/session settings and owner identity. Add a versioned protocol transition with legacy-profile compatibility only for the exact known legacy defaults. Preserve bounded framing and loopback-only binding.

ORT exposes intra/inter-op parallelism, execution mode, optimization level and spinning, with CPU/power tradeoffs; see [thread management](https://onnxruntime.ai/docs/performance/tune-performance/threading.html). Record explicit settings instead of relying on future library defaults. Avoid adding newer tuning keys unless the pinned runtime confirms support.

**`unity-cpu`**

- Scheduling: `whole` or `time-sliced`; default candidate `whole` with nonblocking completion. Slice budget and max iterator steps are active only for sliced mode. A step count is not advertised as an exact layer count.
- Completion: async copy/poll through supported APIs; blocking mode is a benchmark-only historical control. Persistent worker and warmup policy are explicit; buffers are reused only after completion.
- CPU job-worker count is a Unity process-wide property, not an invented per-provider thread count. Initially observe it read-only. An advanced benchmark launcher may vary supported global worker settings in isolated processes, recording the effect on the whole game; never change it silently from an asset.

**`unity-gpu-compute`**

- Scheduling: `whole` or `time-sliced`; initial sliced candidate 1 ms per frame with a safety cap of 64 iterator advances per frame. It is an experimental starting point, not a claim that latency will pass. Sweep other budgets during qualification.
- Readback: `async-awaitable` or supported polling; blocking only for baseline diagnosis. Measure qpos and count readiness together, including CPU copies used by animation.
- Warmup: model import/deserialization, worker creation and first inference separately tracked. Initial policy: 30 warm calls, with shader-cache state explicitly identified.
- GPU adapter/graphics API: observed by default, selected through supported Unity player/build launch configuration where available. No fictitious per-worker GPU index, VRAM limit, FP16 switch or shader-thread count. Required compute/async-readback support is probed; unsupported combinations fail eligibility. Async GPU execution still competes with rendering for GPU time and memory.

Future adapters must declare their own settings rather than inherit unrelated options. For example, an ORT CUDA adapter would validate device selection, supported provider options, memory-arena policy and CPU fallback; an in-process ORT CPU adapter would own session thread settings and native-library identity, but have no TCP port or Python path. Unsupported settings are unavailable in both UI and schema.

## 4. Runtime structure and lifecycle

Keep canonical `MotionRequest`/`MotionResponse` and the `IMotionGenerator` abstraction; extend through explicit contracts rather than backend conditionals in gameplay. Proposed modules:

| Module | Responsibility |
|---|---|
| `InferenceProfile`, validators, JSON schema | Typed settings, migrations, effective settings, deterministic hashes |
| `BackendRegistry` / `MotionGeneratorFactory` | Register adapters, discover compiled/platform capabilities, construct selected provider |
| `MotionGenerationCoordinator` | Command admission, timestamps, context, coalescing, buffer/refill policy and epochs |
| `InferenceRunner` | Main-thread lifecycle/tick for Unity scheduling and resource ownership |
| `UnityCpuMotionGenerator`, `UnityGpuMotionGenerator` | Shared tensor adapter with backend-specific scheduling/completion |
| Updated `PythonMotionGenerator` / service | Configuration-aware startup, readiness, transport and timings |
| `InferenceTelemetry` | Provider-neutral timestamped events and backend-specific diagnostic fields |
| Editor configuration/benchmark window | Profile editing, capability checks, run preview and report access |

Lifecycle: `Uninitialized → Probing → Loading → Warming → Ready → Generating → ReadingBack → Ready`, with explicit `Draining`, `Faulted`, `Stopping` and `Disposed` states. Make graceful asynchronous shutdown a first-class operation; retain `Dispose` only as a safe owner-controlled cleanup path. Cancellation invalidates logical results immediately, but already submitted GPU/jobs must complete/drain before resources are destroyed. Iterator abandonment and partial scheduling require dedicated tests. Call Unity object/tensor disposal on the appropriate thread. Pure serialization, transport and independent math may run on worker threads; Unity APIs are not moved wholesale to `Task.Run`.

Changing backend or restart-required settings uses drain → invalidate epoch → dispose old provider → initialize/warm new → replan. Keep a bounded last-pose hold during transition. Default to one resident model/worker to avoid doubling VRAM on an 8 GB card. No per-frame backend switching. Responses from earlier provider epochs cannot splice into the new timeline even if request IDs collide.

Adaptive scheduling is opt-in and deterministic from logged inputs. It can adjust only within a validated profile envelope, using buffer lead, recent completion/frame cost and, where available, thermal/power signals. Apply hysteresis and a minimum dwell period; log each change and reason. It must not lower update frequency until input responsiveness becomes unmeasurable. Benchmark fixed settings first and adaptive policies separately. Unity Adaptive Performance integration is optional and capability-gated; do not assume a usable thermal provider because a module appears in the manifest.

## 5. Evidence-based platform selection

An auto policy filters compiled availability, deployment restrictions (including external-process allowance), model capabilities, memory constraints and qualifying evidence. Rank only eligible profiles, using a named objective: responsiveness, frame stability, memory or measured energy. Show tradeoffs and uncertainty rather than one universal performance score. Absence of power/GPU timing data cannot be interpreted as zero consumption.

Cache recommendations by hardware/OS/driver/graphics API, Unity/runtime/build/model/profile and benchmark-schema identity. A mismatch makes evidence stale. An automatic mode may use a configured conservative fallback but must report the reason and lack of qualification; it must not quietly install Python, download weights or run a long benchmark during startup. Explicit selection remains available for testing an unqualified candidate and carries that status into diagnostics/reports.

Benchmarks disable selection fallback by default. A fallback run is evidence for the actual backend and transition, never a performance success for the requested one. Deployment recommendations require comparable workloads and measurements from built players on the target device.

## 6. Implementation sequence and acceptance

| Milestone | Changes | Completion evidence |
|---|---|---|
| A — Contracts and measurement foundation | Schemas, profile resolver, registry, telemetry events, benchmark manifest; preserve current Python behavior | Configuration round-trip/migration tests; invalid/inapplicable settings rejected; report generator tested on clearly synthetic report fixtures |
| B — Selectable Python baseline | Factory/coordinator extraction, scene profile, session options/handshake, configuration-aware service scripts and UI | Existing real fixtures/tests and ten-minute behavior pass; requested/effective settings agree; mismatch and owned restart tested |
| C — Native CPU provider | Build-time model asset, persistent worker, async completion, safe lifecycle | All real fixtures, warm backend-only and rendered-player runs; no Python process required; teardown/cancel/scene reload verified |
| D — Native GPU provider and optimization | Async readback, scheduling slices, per-step markers, reuse, fallback diagnostics | Whole vs sliced comparison; strict numerical report including known left-turn difference; frame/response tradeoff and GPU/render contention measured |
| E — Benchmark product | CLI/window, presets, raw evidence, reports/comparisons and runbooks from companion protocol | One documented command chain completes on this Windows machine; agent can validate/compare without scraping prose; failure cases generate complete manifests |
| F — Selection policies and adaptive scheduling | Platform rules, qualified-profile ranking, transition handling, optional adaptation | Explicit/fallback/auto behavior tests; stale evidence detection; adaptive trace reproducibility; no cap violations on collision or command spam |
| G — Qualification and handoff | Repeatable hardware matrix, finalist endurance/failure runs, clean-machine packaging instructions | At least all available first-release backends measured on this machine, failures retained; target-specific recommendations or explicit no-qualified-candidate result |

Dependencies: A precedes B/C; collect benchmark evidence incrementally rather than waiting for E. D uses C's lifecycle/tensor plumbing. F follows stable fixed-profile measurements. GPU budget failures do not block selectable CPU/Python delivery or the tools. Do not relax correctness thresholds to force a recommendation; any revised tolerance requires documented joint/root visual and numerical justification and a separately versioned gate.

Tests include profile resolution and override precedence; request rate cap including collision; epochs/stale results; buffer/context timing with multi-frame completion; queue bounds; cancellation during each lifecycle state; dual-output readback; settings changes while busy; graceful process shutdown; external-service mismatch; missing model/compute/readback support; build without Python; model inclusion; and stripping/Mono/IL2CPP on each claimed target. Real inference tests remain distinct from deterministic fault-injection tests.

Deliver implementation under `Assets/MotionBricksUnity/{Runtime/Inference,Runtime/Diagnostics,Editor,Tests,Samples}`, profile assets and benchmark suites under `Config/Inference/` and `Config/Benchmarks/`, orchestration/report code under `Tools/motionbricks_benchmark/`, and versioned schemas under `Tools/schemas/`. Runtime asmdefs must not depend on editor assemblies or optional providers absent from a target build.

Update quickstart, limitations, model manifest, notices, implementation status and validation after implementation. Retain the historical evidence unchanged and label the new qualification series with a new schema/version. Full procedures, outputs, settings sweeps and acceptance rules are specified in the [benchmark protocol](inference-benchmark-protocol-plan.md).
