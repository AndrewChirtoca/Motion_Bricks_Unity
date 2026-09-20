# Inference benchmark tools and qualification protocol — plan

Status: proposed, 20 September 2026. Commands, schemas and new tools below are specifications for implementation, not commands that currently exist. Use the existing [quickstart](quickstart.md) for today's build. This protocol accompanies the [backend implementation plan](inference-backends-plan.md).

## 1. What a comparison must answer

For a specific platform, hardware setup and deployment objective: which model/backend/settings combination is correct, responsive, stable under rendering load, affordable in memory, and deployable without unacceptable dependencies? Report why a configuration is recommended or rejected. Never reduce the answer to isolated inference FPS.

Every row represents an immutable **model + backend + effective settings + workload + environment** combination. Separate backend speed from scheduling policy, rendering workload, power state and model changes. Publish the settings beside the metrics, with expandable details and raw evidence links. Rankings must not pool different GPUs, render resolutions, quality gates, scheduling policies or model variants without an explicit grouped comparison.

## 2. Tool surfaces

Implement one Python orchestration/reporting package, `Tools/motionbricks_benchmark`, with a PowerShell entry point `Tools/benchmark.ps1`. Keep report generation independent of Unity and heavyweight inference packages; document its minimal dependency lock. Unity's `MotionBricks → Inference → Benchmark` window uses the same suite/profile files and runner contract, not a second interpretation of settings.

| Operation | Human surface | Agent/CLI contract |
|---|---|---|
| Discover | Backend cards with availability/reasons and settings summary | `discover --json`; optional explicit model probe; discovery alone does not launch expensive tests |
| Validate | Profile errors with units, restart flags and effective values | `validate --profile PATH --json`; rejects unknown/inapplicable settings |
| Preview run | Show devices, processes, cases, configurations, duration estimate and output directory | `plan --suite PATH --json`; no model execution |
| Run | Start/cancel, progress, current configuration and live status | `run --suite PATH --output PATH`; JSONL progress on stdout, diagnostics on stderr |
| Inspect/compare | HTML report, settings diffs, plots and exclusion reasons | `report --run PATH`; `compare --runs PATHS --qualification PATH --json` |
| Resume | List incomplete run cells and reasons | `resume --run PATH`; rerun interrupted cells only after environment/hash match |
| Recommend | Explain eligible profiles and tradeoffs | `recommend --comparison PATH --objective responsiveness --json`; emits a candidate profile and evidence references |
| Verify | Evidence integrity and missing files | `verify --run PATH --json`; validates schema, hashes, completion and metric applicability |

Reports: standalone local HTML (no CDN requirement), Markdown summary, JSON result/comparison, CSV request/frame/process samples, JSONL events, and optional profiler captures/screenshots. No proprietary dashboard or UI automation is necessary to understand results. HTML offers filters, raw sample downloads, settings diffs, latency distributions, frame timelines and CPU-versus-response tradeoff plots. All plots label units, sample count, warmup exclusions and measurement method.

Schema-versioned stdout events contain run/configuration/scenario IDs, phase, completed/total units, state, output path and error code. Stable exit codes: 0 completed and gates passed; 1 valid measurements but qualification failed; 2 invalid configuration/schema; 3 unavailable/unsupported target; 4 execution/infrastructure error; 5 cancelled; 6 incomparable or insufficient evidence. Even nonzero runs must produce a manifest and reason. Comparisons retain failed/unavailable rows. Never fabricate zero timings for unavailable metrics.

### Proposed command workflow

These example files and commands must be shipped and tested during implementation:

```powershell
Set-Location D:\Projects\Motion_Bricks_Unity
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 discover --json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 validate --profile .\Config\Inference\unity-gpu-balanced.json --json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 plan --suite .\Config\Benchmarks\desktop-screen.json --json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 run --suite .\Config\Benchmarks\desktop-screen.json --output .\Artifacts\Benchmarks\desktop-screen-01
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 verify --run .\Artifacts\Benchmarks\desktop-screen-01 --json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\benchmark.ps1 compare --runs .\Artifacts\Benchmarks\desktop-screen-01 --qualification .\Config\Benchmarks\desktop-60fps.json --json
```

Offer an equivalent `python -m motionbricks_benchmark ...` interface through a documented installed package on non-Windows hosts. The backend runner communicates with a target player through the same versioned suite/result files. Platform-specific build/deploy adapters supply their own exact runbook and prerequisites before that platform is listed as supported. Do not claim that PowerShell scripts alone qualify mobile or console targets.

## 3. Configuration and evidence contracts

Implement JSON schemas for `InferenceProfile`, `BenchmarkSuite`, `RunManifest`, `MeasurementEvent`, `RunResult`, `Comparison` and `Recommendation`. Settings and suites have explicit schema versions and canonical hashes. Include executable CLI examples and golden report examples, clearly labeled synthetic; synthetic results test tooling but never count as model evidence.

Conceptual profile example (proposed schema):

```json
{
  "schemaVersion": 1,
  "id": "unity-gpu-balanced",
  "backend": "unity-gpu-compute",
  "modelId": "gear-sonic-6733128a3d8a523b1418b06bca3cdf61c8b0987f",
  "settings": {
    "scheduling": "time-sliced",
    "sliceBudgetMs": 1.0,
    "maxIteratorStepsPerFrame": 64,
    "readback": "async-awaitable",
    "warmupCalls": 30
  },
  "requestPolicy": {
    "mode": "on-change-or-low-buffer",
    "maxRequestHz": 10,
    "refillMinimumSeconds": 0.3,
    "requestDeadlineSeconds": 3
  }
}
```

Defaults omitted by an author are materialized in `resolved-profile.json` before running. Preserve both requested and effective configuration. If a requested setting cannot be applied, fail validation or record an explicitly approved override; no silent clamping. Adaptive policies include their version, initial state, bounds and every actual change, so a single starting profile does not conceal a varying configuration.

Each run directory contains:

```text
run-manifest.json             # identity, platform, status, build/model/suite hashes
requested-profile.json
resolved-profile.json
suite.json
result.json                  # gates, summaries, sample counts, metric methods
events.jsonl                 # lifecycle, commands, requests, settings, failures
requests.csv                 # per-request timings and dispositions
frames.csv                   # frame cost, inference-active flag, buffer state
process-samples.csv          # owned player/helper CPU, memory, threads, GPU observations
correctness.json             # fixture and motion-space errors
logs/                        # player, provider, build and runner output
captures/                    # separate visual/profiler evidence when enabled
report.md
report.html
checksums.json               # immutable artifact checksums, excluding itself
```

Manifest identity: UTC run time; monotonic timebase identifiers; repository commit plus dirty source snapshot/hash; exact player binary/content hashes; debug/release, Mono/IL2CPP, architecture; Unity and package/runtime versions; ONNX and converted-model hashes; fixture/suite versions and seed schedule; requested/actual backend and observable internal fallback; CPU/core topology/RAM; GPU/VRAM/driver/API/adapter; OS; resolution/render pipeline/settings/vSync/target frame rate; windowed/offscreen/headless mode; power plan, battery/AC/thermal observations when available; concurrent-load declarations; instrumentation level and cache state. Paths in portable reports should be relative; absolute local dependency paths remain available in a local environment manifest.

Build once per build-content configuration, then vary runtime profiles when valid. A build setting such as graphics API or global job-worker count requires its own recorded launch/build identity. Package source/model/license checks remain part of preflight. Native players must prove they use the included model, not an unnoticed repository copy.

## 4. Measurement semantics

Record monotonic timestamps with request ID, command ID and provider epoch. Unity and Python clocks are separate domains: compare durations within each process, and measure transport round trips on Unity's clock. Do not subtract unsynchronized absolute timestamps or add overlapping CPU/GPU durations and call the sum latency.

| Metric | Definition and required presentation |
|---|---|
| Startup | Model load/deserialization, worker/session creation, first inference and ready time separately; process-cold vs cache-cold explicitly distinguished |
| Scheduling CPU | Sum of main-thread submission/preparation slices per request; each slice/iterator-step cost; p50/p95/p99/max; frames spanned |
| Completion/readback | Dispatch-to-ready wall time; nonblocking wait duration; CPU-visible output copy/validation cost; transport/serialization separately where measurable |
| GPU execution | Only claim kernel/device time with a supported instrument and explicit scope; otherwise `null` plus unavailable reason. Async readback duration is not GPU kernel duration |
| Feature CPU | All main-thread feature work, including coordinator, input/context construction, scheduling, completion processing, buffer, FK, retargeting, diagnostics/UI; avoid double-counting nested markers |
| Frame impact | CPU/render/GPU timings when available, rendered frame intervals, p50/p95/p99/max, missed-budget percentage and >33.3/>50 ms hitch counts; paced waits distinguished from active CPU work |
| Active-frame impact | Separate feature CPU and frame distribution for frames that prepare/schedule/complete inference; overall p95 alone can hide low-frequency spikes |
| Command response | Input observation → request admission → accepted result → source motion change → corresponding human pose update; each stage and deadline-miss count |
| Buffer/reliability | Minimum/percentile buffer lead, starvation count and duration, expired/stale/rejected/cancelled results, request and queue counts, rate-cap violations, errors and recovery time |
| Resources | Player/helper working set/private memory where available, GC bytes/collections, worker/thread counts; GPU memory source/scope; peak, warm ranges, growth slope and end-minus-start |
| Power/thermal | Energy or power/temperature with sampling source and precision if supported; unavailable is not zero; no energy ranking without comparable measurements |

Aggregate metrics include counts and denominators. Use a defined percentile estimator (linear interpolation over sorted samples, matching NumPy's `method="linear"`) across report tools. Report every repetition individually plus median/range of repetition summaries; do not average p95 values and label that a pooled p95. Report frame-rate percentiles as frame durations first. Separate profiling/capture overhead runs from minimally instrumented scoring; measure telemetry overhead with an on/off control.

Source response retains the corrected comparison to the previous predicted trajectory interpolated at actual playback time: first >1 cm root translation, >0.02 rad hinge difference, or >1 degree root rotation after accepting the relevant command response. Exclude initialization, but retain every scheduled meaningful command in the denominator. Record responded, no measurable change, superseded and timed-out outcomes. A 2-second timeout is censored failure, never omitted to improve latency. For a 100 ms gate, report the proportion of eligible commands responding within 100 ms; at least 95% must do so. Superseded rapid-reversal commands are reported separately and the final stable command must respond. Label this source metric separately from human pose application and from unmeasured display-photon/perceptual latency.

Publish human/source pose traces and representative visual captures. Tensor equality alone cannot certify retargeting quality or foot contact. Existing screenshots and offscreen frames are evidence of rendering, not evidence of visible-window latency or UI rendering cost.

## 5. Test tracks and repeatable procedure

### Preflight

1. Validate schemas, model/fixture hashes, dependencies, backend availability, graphics device and adequate disk/memory. Print the resolved plan, process ownership and bounded run estimate.
2. Detect an existing service and compare its effective configuration. Reuse only an exact match; restart only a service owned by this tooling. Record service lifecycle cost separately. Do not change the user's power plan, driver or global job settings automatically.
3. Freeze scene, model, seed schedule and render settings. Record user-declared background work and observed thermal/power state; do not silently terminate unrelated applications. Run competing configurations sequentially on a device to avoid mutual interference.
4. GPU tests require an actual graphics device; never use `-nographics` and label the Null Device as GPU evidence. Identify hidden/offscreen versus visible-window mode. Performance qualification uses built players; Editor results are exploratory and grouped separately.

### Track A — Contract and correctness (before ranking)

Replay immutable initialization, walk, left/right turn, stop and run fixtures using identical contexts/inputs/seeds across providers. Check dtypes, shapes, finite values, valid-frame counts, ignored padding, quaternion normalization, deterministic repetition where supported, source FK and basis. Report root translation error in metres, hinge error in radians, root orientation angular error, and joint-position error at source and human scale in addition to raw tensor error.

Retain the original 1e-4 raw FP32 fixture gate and exact valid counts initially. Define separate dimension-aware gates in the schema; until validated, absence of such a gate is `unqualified`, not a pass. The known native left-turn difference must appear as a failure under the current strict gate. A proposed tolerance revision requires a versioned rationale and motion-space inspection. Performance may still be collected for a failed candidate, labeled ineligible. Never replace difficult fixtures or change seeds to make a backend pass.

### Track B — Backend-only timing

Use fixed input fixtures, a persistent worker/session and serial requests without rendering pressure. Measure five fresh-process startups with cache state recorded; do not delete global shader/driver caches to claim a cold test. For warm timing, complete 30 warmup calls and then 100 measured calls per fixture (600 calls total), with three independent repetitions for qualification. Report timeouts and failures. For sliced execution, provide a controlled 60 Hz pump and include idle time between slices; report separately from a throughput-oriented whole-schedule test.

Backend-only scores isolate inference behavior; they do not determine gameplay suitability. Do not impose the gameplay cadence cap on a throughput test and then label the result maximum backend throughput.

### Track C — Rendered gameplay

Replay deterministic semantic commands using the production command/coordinator path, with fixed camera/scene rules. Cover steady idle/walk/run; starts/stops; left/right turns; independent facing; rapid reversal bursts; wall correction and replan; low-buffer deadlines. Validate keyboard/controller mapping separately so scripted commands cannot mask a broken input reader.

Use one supplied character, 1920×1080, fixed render settings, 60 FPS target, 30-second warmup excluded from scoring, and at least 120 scored seconds per screening run. Measure both a minimal scene and a fixed, versioned representative render/CPU-load scene. Include a no-live-inference recorded-motion control for rendering/retargeting cost; it is a control, not a competing generative backend. Closed-loop contexts will diverge across backends; identical command scripts and open-loop Track A/B fixtures address different comparison questions.

Schedule qualifying response-test commands with sufficient stable dwell (initially four seconds) so each can be observed; use a separately labeled rapid-reversal scenario for coalescing and robustness. Three repetitions use a recorded shuffled configuration order and consistent warmup. Declare whether each repetition reuses a warm service or starts a new one; do not mix these within a comparison group.

### Track D — Endurance, failure and transitions

For shortlisted configurations, run three independent ten-minute nominal trials, plus distinct fault trials. Observe bounded queues, workers, memory, NaNs, source/human offset, request caps and starvation. Source and human world positions must be checked numerically, not only by camera-following screenshots.

Fault tests: owned Python service absent/restart, configuration mismatch, late response, mid-request cancellation, native readback failure where deterministically injectable, missing/invalid model, unavailable compute, scene unload, quit, profile/backend switch and logical epoch rejection. Never intentionally trigger a GPU driver reset to test recovery. Mark simulated failures as injected and distinguish them from actual process interruption. Verify all owned resources/processes are released; preserve unrelated helpers.

Native cancellation cannot guarantee interrupting already submitted work. Validate immediate logical invalidation and eventual safe drain/disposal separately. Switching tests measure initialization stall and peak memory, ensuring two models are not unintentionally resident.

## 6. Settings sweeps and comparison fairness

Default invocation is one explicit smoke profile. Full sweeps require an explicit suite and preview of run counts/time. Initial desktop screening suite explores **16 unique configurations**, not a Cartesian product:

| Family | Initial sweep | Fixed controls |
|---|---|---|
| Python ORT CPU (8) | Intra-op threads 1, 2, 4, 8 × spinning off/on | Sequential graph execution, optimization all, same transport/settings |
| Unity CPU (3) | Whole scheduling; sliced 1 and 2 ms | Async completion, identical model, observed global worker count |
| Unity GPUCompute (5) | Whole scheduling; sliced 0.5, 1, 2 and 4 ms | Async readback, same adapter/API, iterator cap 64 |

All 16 initially use the same on-change/low-buffer policy, 10 Hz cap, deadlines, context/lead policy and nonadaptive behavior. Check whether the hardware can meaningfully run a thread count; unsupported configurations remain recorded exclusions. Whole/blocking historical readback is an optional diagnostic control outside the recommended sweep, not a production default.

Only after screening, use a second suite for finalists: cadence caps 5/10/20 Hz; lower/standard/larger refill lead; validated latency-estimator variants. Vary one factor at a time or use an explicitly defined small factorial experiment. Label a 5 Hz setting's possible 200 ms admission wait and let responsiveness gates reject it. Then test adaptation against the best fixed profile. Settings changes must be visible in report rows and differences; no hiding a favorable refill policy behind a backend name.

Advanced separate suites may vary Python parallel execution/inter-op counts, Unity process-wide job-worker count, model conversion variants or graphics APIs. Each uses a separate group and corresponding correctness/build checks. Never enable FP16/quantization without a supported conversion/runtime path and a separately identified model artifact.

Screening: one repetition for exploration, three for a decision. At 120 scored seconds plus warmup, 16 configurations × 3 repetitions is roughly two hours before all supplementary tests; the runner computes a fuller estimate. Suite configuration includes maximum unique configurations, maximum execution count and wall-clock limit. When a limit is reached, checkpoint and report partial evidence. A partial sweep must not claim an exhaustive winner. Ten-minute finalist qualification is an additional explicitly previewed suite.

## 7. Qualification and report rules

Initial desktop gate profile, versioned independently from metrics:

| Gate | Rule |
|---|---|
| Correctness | Required fixture/model-contract gates pass; known failures remain disqualifying until a justified versioned revision |
| Frame target | 60 FPS target; provisional rendered interval p95 ≤16.8 ms (allows timer/pacer rounding); report strict 16.667 ms miss rate, p99/max, and require >33.3 ms hitches ≤0.1% |
| Feature CPU | p95 <1 ms across all measured frames **and** inference-active frames; report p99/max and total scheduling per request |
| Responsiveness | At least 95% of eligible meaningful commands produce measured source response within 100 ms; no-response/timeouts included in failures; human-pose application separately reported |
| Reliability | No nominal underruns, NaNs, unbounded queues or leaked active workers; fault trials reported separately |
| Memory | Fit explicit platform budget including rendering, weights, staging and helpers; on the 8 GB target propose ≤6.5 GiB total observed dedicated GPU use as a conservative candidate budget, not an allocator guarantee |
| Evidence | Three complete comparable repetitions and finalist endurance; required metrics present; visual/source-human inspection recorded |

Resource growth flags require investigation, not an automatic leak claim: compare warm plateau segments, slope, end-minus-start, thread/worker counts and repeat runs. A proposed default flag is >20 MiB warm working-set growth over ten minutes or an increasing worker/thread plateau; record allocator behavior and measurement uncertainty. GPU memory availability varies by platform/tool; missing authoritative total-use data makes that particular budget unverified. CPU-only inference has no inference GPU duration, but the rendered player still has GPU use.

Report gate results as `pass`, `fail`, `unverified`, `not-applicable` or `not-run`, with reasons. A requested profile can be available and executable but unqualified. An incomplete metric cannot pass a strict gate. Show deployment requirements beside performance: Python dependency, build size/model memory, startup cost, supported platform/API and known correctness limitations.

Every comparison row includes backend/profile/config hash, key tuning values (threads/spinning or scheduling/slice/readback), cadence/refill/adaptive status, actual fallback, correctness, response-within-budget count, source-response distribution, active-frame CPU, frame hitches, memory, startup and evidence completeness. Full effective settings are linked from the same row. Adaptive rows show settings time-series and occupancy ranges, not just initial values.

If several profiles pass, present a Pareto comparison and a named objective with deterministic tie-breaking. Treat overlapping/noisy repetition results as inconclusive rather than declaring a small difference a win. If none pass, report the nearest candidates, failing gates and next profiling steps; do not automatically weaken budgets or promote an unavailable provider.

## 8. Human and agent runbooks to deliver

- **First measurement:** prerequisites, exact commands, expected progress/files, how to recognize real GPU execution, how to stop safely, and which profile/settings were used.
- **Choose a backend:** discover → validate → fixed-profile screening → compare → finalist qualification → export candidate selection policy with evidence references.
- **Tune one setting:** clone a profile, change one supported field, run paired trials, inspect settings diff, correctness and frame/response tradeoff; retain both configs.
- **Add a platform:** identify build/deploy adapter and runtime availability; freeze qualification budgets; collect actual target evidence; document missing instruments and unqualified cases.
- **Add a backend:** implement registry/capabilities/schema/lifecycle/telemetry, reference fixtures, optional dependency/build packaging, settings-specific sweeps, faults and teardown, then qualify. Passing an interface mock is insufficient.
- **Investigate a regression:** compare environment/model/build/config fingerprints before attributing a slowdown to the backend; use separate detailed profiler runs and retain failing evidence.
- **Agent continuation:** read manifest status and structured errors; verify checksums; never overwrite a completed run, invent missing metrics, silently restart an external service, relax a gate, or count a fallback under the requested backend. Resume only with matching identities or start a new linked run.

Archive small final reports, schemas, suites and chosen profiles in the repository; keep bulky raw traces/builds under ignored `Artifacts/Benchmarks/` with checksums and documented archive location/retention. Reports must explicitly say when raw evidence is no longer available. Retain historical baseline reports without rewriting their measurement method. Tool tests cover missing metrics, censored responses, percentile calculation, adaptive setting traces, incomplete runs, fallback attribution, incompatible comparisons and settings-hash differences.
