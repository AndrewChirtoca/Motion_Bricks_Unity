# Implementation status

Executed 2026-09-19 on the supplied RTX 3070 (8 GB). Baseline: `cbf2c69`, initially clean; no AGENTS.md found. The attached brief supplies the scope and defaults. The original FBX and Humanoid import settings are preserved.

## Milestones

| Phase | Delivered / result |
|---|---|
| 0 — Environment and contract | Complete: exact Unity editor, license, Windows empty build, supplied valid Humanoid Avatar, GPU and storage inventory; immutable source/weight provenance and actual tensor contracts recorded. |
| 1 — Independent reference | Complete: locked local Python environment, six real planner fixtures, deterministic seed checks, source renders, timings and independent MuJoCo FK comparisons. Full backbone checkpoints also exercised on CUDA. |
| 2 — Unity source playback | Complete: XML-derived source hierarchy, explicit basis conversion, 30 Hz pose interpolation, named diagnostic skeleton/root axes/trajectory, recorded playback and reproducible editor setup. |
| 3 — Live inference | Complete through the disclosed loopback Python ONNX Runtime CPU fallback. Native Unity import and execution work, but native scheduling cost and strict fixture parity do not meet the integration gate. |
| 4 — Locomotion and retargeting | Functional implementation delivered: generated idle/walk/run, camera-relative movement, independent facing, timestamped bounded buffering, capsule correction/rebasing, mapping asset and sole LateUpdate bone writer. Ten-minute player run: 682 accepted batches, zero underruns/rejections, stable memory and thread counts. Human motion quality remains an owner acceptance item. |
| 5 — Chair interaction | Incomplete by measured quality gate. Reusable source-pose authoring, masks/timing, three placed chair previews and real backbone trials delivered. All nine endpoint trials exceed 5 cm at human scale (6.81–13.47 cm). No completed chair sequence or interaction state machine is represented as working. |
| 6 — Validation and delivery | Windows player, scripts, focused tests, representative rendered frames, evidence and documentation delivered. See validation.md for final test counts and performance qualifications. |

## Completion levels and blockers

**Locomotion MVP: implemented and exercised in a Windows player.** Full acceptance remains qualified: robotic source-to-human motion needs artistic/contact review, whole-feature CPU including UI is not fully profiled, and automated offscreen timing is not a display-photon measurement. The selected runtime requires the local Python service; a native-only deployment is not delivered.

**Interaction extension: incomplete.** Target-pose interface access and weights are available and fit this GPU. The blocker is constraint-fit/model quality for the authored seated targets, not unavailable hardware or a missing interface. The smallest next step is better source-rig target/contact authoring with the existing backbone, followed by the same three-placement error and penetration checks. No training, dataset download or architecture substitution was performed.

Native follow-up: reduce the measured 22–25 ms main-thread GPU scheduling cost, resolve the left-turn fixture's 1.4046e-4 error against the original 1e-4 tolerance, and investigate the retained editor-exit allocation warning before replacing the working provider.

## Reproduction and evidence

Start with [quickstart.md](quickstart.md), [validation.md](validation.md), [model-contract.md](model-contract.md) and [limitations.md](limitations.md). Small reports are retained in `docs/evidence/`; builds, checkpoints, logs and generated fixtures remain local under ignored `Artifacts/`, `Models/` and `External/` directories. The committed fetcher and editor setup reproduce them.

Final checks: 7/7 Unity EditMode, 4/4 Unity PlayMode and 5/5 Python tests passed. The final two-minute Windows run accepted 138 generations without underruns; interpolated source-response p95 was 66.72 ms across 29 command changes. PowerShell scripts parse, Python sources compile, and Git diff whitespace checks pass. Incidental editor/build changes to four existing Unity settings files were restored to the original baseline; the inference package pins and new implementation remain. Changes are uncommitted.

Network/setup commands required sandbox escalation and were approved. Git reads used a per-command safe.directory override; no global Git setting changed. Background service and smoke-player ownership are recorded explicitly, and shutdown only targets the owned process. No publishing or external deployment was performed.
