# MotionBricks in Unity: agent implementation brief

Prepared 19 September 2026. Execute this brief in the intended Unity project repository. This is a plan, not a report of a tested implementation.

## Owner pre-setup

Complete these before execution:

1. **Provide a writable project folder.** Prefer a separate prototype project or a Git branch of an existing project. Tell the agent the absolute path. Do not run this plan in a production checkout without a recoverable baseline.
2. **Install and activate Unity through Unity Hub.** For an existing project, install its exact editor version. For a new project, use a supported Unity 6 editor with a compatible Inference Engine package; the agent must verify and pin the exact versions. Open the project once, complete licensing/sign-in prompts, and confirm that Play mode works. Install the Windows build module if it is missing. Close the editor before the agent performs automated batch runs against that same project.
3. **Provide a machine on which the agent can run Unity and inference.** Windows x64 with an NVIDIA CUDA-capable GPU is the reference target. Record the GPU model and VRAM. Other GPUs can be evaluated through the ONNX route, but the original CUDA reference path cannot be assumed available. No published minimum VRAM has been verified for this plan. Suggested development provisioning is 32 GB RAM and 15–25 GB free SSD space, excluding a new Unity installation; these are estimates, not vendor minimums.
4. **Supply one licensed human character if human-character validation is required immediately.** Prefer a skinned FBX with a valid Unity Humanoid Avatar, recognizable rest pose, and no missing limbs. Provide its asset path. Without one, the agent can build a diagnostic skeleton and prove inference/playback, but must mark human retargeting validation incomplete. Do not buy an asset solely for this experiment before the source-skeleton test works.
5. **Complete any model-host authentication and terms acceptance yourself if required.** Use the host's normal login mechanism; do not paste tokens into the task. The agent should identify the exact official artifact and applicable terms before requesting anything from you.

The agent can install project-local Python dependencies, fetch source and permitted model artifacts, add Unity packages, generate scenes, and run validation. Git/Git LFS and Python 3.10+ should be available, or the agent can configure them using the environment's normal installation permissions. A separate CUDA Toolkit is not automatically necessary for a compatible PyTorch binary; verify driver/runtime requirements first. Native TensorRT work may require additional tooling later.

You do **not** need a training dataset, cloud GPUs, Isaac Sim, ROS, a physical robot, TensorRT, or a custom-trained model before starting the prototype. Do not download BONES-SEED for these phases.

Optional owner inputs; use the defaults below if omitted:

| Input | Default |
|---|---|
| Product target | Windows x64 desktop, one animated human |
| Project | New isolated Unity prototype; preserve an existing project's configuration if supplied |
| Rendering | Simple scene; use the project's renderer, or URP for a new project |
| Initial behavior | Idle, walking, turning, stopping, and running if supported by the selected model |
| Interaction | One chair sitting/standing interaction after the locomotion milestone |
| Final prototype runtime | Native Unity inference preferred; a separately launched local Python service is an acceptable disclosed fallback |
| Training | Out of scope |
| Paid services/assets | None |

## Agent assignment

Implement and validate a MotionBricks-based Unity prototype using released pretrained models. Produce working source, reproducible setup, playable scenes, and measured results. Work through the phases sequentially and continue through automatic technical gates without repeatedly requesting confirmation. Ask only for missing access, owner-only setup, spending, or a material scope decision that the defaults do not resolve.

Start by reading applicable repository instructions and inspecting existing project state. Preserve user changes. Record assumptions and progress in `docs/implementation-status.md` so another agent can resume.

**Completion has two explicit levels:**

- **Locomotion MVP:** verified inference, source-skeleton playback, human retargeting, camera-relative controls, robust buffering, and a Windows player build with reproducible startup.
- **Interaction extension:** one generated chair interaction, authored through reusable data, with interruption/completion handling. This requires verified target-pose conditioning; a locomotion-only planner is not sufficient proof of support.

Complete both when the released assets support them. If a required capability is unavailable, finish unaffected work and document the exact blocker. Never describe placeholder animation, recorded playback, or a locomotion-only wrapper as completion of unsupported generative interactions. Report each level independently.

## Authoritative starting references

Recheck these at execution time, then pin the source commit and artifact revisions used. A roadmap promise is not an available capability.

- [MotionBricks project](https://nvlabs.github.io/motionbricks/)
- [MotionBricks paper, searchable HTML](https://arxiv.org/html/2604.24833v1)
- [MotionBricks subproject and setup](https://github.com/NVlabs/GR00T-WholeBodyControl/tree/main/motionbricks)
- [Motion representation](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/motionbricks/docs/motion_representation.md)
- [Core inference implementation](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/motionbricks/motionbricks/motion_backbone/inference/motion_inference.py)
- [GEAR-SONIC kinematic planner ONNX interface](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/docs/source/references/planner_onnx.md)
- [Repository artifact manifest](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/config.json)
- [Unity Inference Engine documentation](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html)
- [Unity supported operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html)
- [Code and weight licenses](https://github.com/NVlabs/GR00T-WholeBodyControl/blob/main/LICENSE)

Current evidence: MotionBricks documents G1-oriented pretrained checkpoints and a preview training path. The parent project separately documents a deployable G1 kinematic planner ONNX. Do not conflate that wrapper, the MotionBricks Python backbone, and the UE5 human-animation demonstration. Establish the selected artifact's provenance and capabilities explicitly.

## Phase 0 — Establish the environment and artifact contract

1. Inventory Unity editor/build support, Git/LFS, Python, GPU, drivers, free disk, and the supplied rig. Save versions and the planned target hardware in `docs/environment.md`.
2. Build an empty scene before adding inference. Confirm the agent can compile, run EditMode/PlayMode tests, and build the player. If the editor/license is unavailable, continue source/reference work and report Unity execution as blocked rather than claiming success.
3. Inspect official download instructions. Fetch only needed artifacts and source, avoiding a full dataset or unrelated robot deployment stack. Verify that downloaded weights are real files rather than Git LFS pointers. Record URL, revision, SHA-256, size, and license in `model-manifest.json`; exclude large binaries, credentials, and generated caches from ordinary Git commits.
4. Compare two candidates: the published G1 planner ONNX for the quickest locomotion path, and the MotionBricks Python checkpoints for backbone access. Prefer an official suitable human checkpoint if one has appeared, after verifying its contract.
5. For each candidate, record inputs, dtypes, dimensions, coordinate basis, units, skeleton, frame rate, valid output length, normalization, reference-pose library, randomness, and conditioning capabilities. Inspect the actual model metadata and pinned code; do not assume documentation for a different version applies.
6. Probe full target-pose conditioning now. Record whether custom interaction keyframes are accessible and which module/export would be needed to expose them. Root waypoints and a mode selector alone do not establish arbitrary body-pose conditioning.

**Gate:** Select a verifiable model path and produce `docs/model-contract.md`. If a GEAR-SONIC wrapper is used, label it accurately and document what remains necessary to exercise the MotionBricks backbone. No custom training or architecture substitution is authorized by this brief.

## Phase 1 — Create an independent inference reference

1. Create an isolated Python environment and lock the versions that actually work.
2. Run the selected artifact through its reference implementation: ONNX Runtime for the supplied ONNX, or PyTorch for the MotionBricks checkpoints. Avoid installing the whole robotics stack merely to produce poses.
3. Build a headless command-line harness that accepts a context and commands, generates motion, and saves reference inputs/outputs with a manifest. Include initialization, straight walking, both turn directions, stop, and supported running/style commands.
4. Validate finite outputs, valid frame count, frame rate, continuity, and plausible root displacement. Render or visualize the source skeleton to catch joint-order mistakes that tensor checks cannot detect.
5. Control randomness. Fix a seed where supported; for cross-backend comparisons supply the same sampled tokens/noise or use a documented deterministic mode. Do not demand identical sampled motion from different random-number implementations.
6. Record cold start, warm p50/p95 inference latency, memory, and the exact hardware. Keep cold and warm figures separate.

**Gate:** Real pretrained motion is generated reproducibly and visually inspected on the source skeleton. Save fixtures locally with applicable license handling; provide regeneration scripts instead of publishing restricted assets.

## Phase 2 — Prove Unity playback without live inference

1. Import recorded reference output into a diagnostic scene. Draw the source skeleton, joint names, root axes, ground plane, and trajectory.
2. Implement the exact source hierarchy and forward kinematics. For G1 hinge outputs, compose serial hinge rotations in the correct order and include model offsets; do not map each hinge directly onto a human bone.
3. Implement explicit basis conversion, quaternion ordering, unit conversion, and rest-pose compensation. Use a basis-change matrix for rotation conversion and test it; a quaternion component shuffle is insufficient for a handedness change.
4. Maintain model state in model coordinates. Use explicit conversion at the Unity boundary rather than round-tripping every presentation pose into model features.
5. Sample by time using the verified model frame rate. Interpolate translations and slerp rotations for 60 Hz rendering. Do not duplicate animation advancement in both Update and FixedUpdate.
6. Add a small Editor setup command that generates the scene and references reproducibly; avoid depending on undocumented manual inspector wiring.

**Gate:** Unity playback matches the reference source skeleton for identity, translation, left/right turns, and rest pose. No mirrored motion, inverted axes, limb swapping, or accumulating root drift. This phase alone is not generative-runtime completion.

## Phase 3 — Add live inference behind a replaceable provider

Use a small interface such as `IMotionGenerator` with initialization, capability reporting, asynchronous generation, and disposal. Its request/response types must include request ID, context timestamp, time origin, model version, frame rate, and valid frame count. Represent target-pose constraints only where the backend actually supports them.

1. Try native Unity inference with the existing suitable ONNX. Pin a package version compatible with the installed editor. Record graph import errors, unsupported operators, CPU fallback, and backend execution.
2. Compare outputs against Phase 1 fixtures before integrating controls. Derive numerical tolerances from FP32 reference results, then document any relaxed FP16 tolerances. Compare root positions and joint angles/transforms as well as tensor errors.
3. If export is needed, separate tensor-only neural modules from Python scheduling, sampling, feature transforms, and state. Preserve semantics in C# or exported graphs. Do not assume a checkpoint imports directly into Unity.
4. Use a **two-working-day investigation budget** for native import/export compatibility. If unresolved, implement a local Python provider and preserve the findings for a later native port. This is a scope limit, not a requirement to idle for two days.
5. For the service fallback, bind to loopback, use a versioned message format with bounded lengths, and provide readiness, timeout, shutdown, and restart behavior. Start with a simple framed protocol; optimize serialization only if profiling warrants it. Run owned background helpers without visible terminal windows, capture their logs, and never terminate unrelated processes.
6. Move communication and CPU work off Unity's main thread. Schedule GPU inference according to the selected Unity backend's supported API; asynchronous scheduling does not guarantee no rendering contention. Bound outstanding requests and manage tensor lifetimes.

**Gate:** Live commands produce generated source-skeleton animation inside Unity. Provider mode is visible in diagnostics. A fallback service must be identified as such, including its installation/startup requirements.

## Phase 4 — Implement buffered locomotion and human retargeting

1. Build a motion buffer keyed by time. Use four context frames and 30 Hz only if confirmed for the selected artifact. Replan when meaningful commands change or the remaining buffer crosses a latency-aware threshold.
2. Use a bounded update cadence, initially up to 10 Hz, and avoid triggering on camera/input noise. Timestamp requests, account for inference lookahead, and splice at the intended context time. Reject out-of-order or obsolete results; never reset playback to the beginning of a delayed response.
3. Handle buffer starvation with a short hold or explicit fallback, exposing a diagnostic counter. Never extrapolate arbitrary motion indefinitely. Test service interruption and recovery if using a service.
4. Add camera-relative movement and independent facing, with commands limited to supported model ranges. Implement idle according to the selected model's documented mode/direction behavior; zero speed is not necessarily an idle request.
5. Retarget a hidden source skeleton onto the human rig. Create a mapping asset with rest-pose transforms, chain correspondence, scale, pelvis/foot handling, and optional corrective IK. Confirm a valid Humanoid Avatar if that route is used. Have exactly one system write the final bones at a defined animation stage.
6. Use generated root motion as the initial locomotion authority. Add simple capsule sweeps against walls and a documented correction/replanning strategy. Avoid competing NavMeshAgent, CharacterController, Animator root-motion, and generator writes. Leave slopes, stairs, and arbitrary traversal outside the initial acceptance scene.
7. Preserve a coherent generator context when gameplay corrects the root. Translate/rebase future poses for suitable rigid corrections, or invalidate and regenerate; do not feed a visibly retargeted human pose directly into the G1 feature interface without an explicit inverse mapping.

**Gate:** One human character supports the available idle/walk/run modes, left/right turns, starts/stops, and rapid direction changes. Run for ten minutes with no NaNs, unbounded queues, leaked workers, or accumulating world-offset errors. Compare source and human playback so retargeting artifacts are not mistaken for inference errors.

## Phase 5 — Add one authored smart-object interaction

1. Use the verified target-pose-capable backbone/provider. If the selected locomotion wrapper lacks this interface, expose it through the Python backbone or a new validated export. Keep locomotion working while doing this.
2. Create a reusable `SmartObjectDefinition` asset with object-relative entry/interaction/exit poses, constraint masks/timing supported by the model, approach region, and completion/interruption rules.
3. Implement one chair sequence: approach, turn, sit, remain seated, and stand/return to locomotion. Obtain target poses from licensed reference motion or author them against the source rig. Start with a chair height appropriate to the source morphology.
4. Provide gizmos showing root targets, pose previews, timing, and interaction bounds. Moving or rotating the chair must transform its constraints consistently.
5. Implement a small gameplay state machine: locomotion, approach, interaction, seated, recovery. Bind events to the generated timeline or verified contact/completion criteria, not to guessed wall-clock delays.
6. Validate approach clearance, target feasibility, and interruption policy. A generative model is not a collision-free planner. Document any IK correction applied and retain a source-skeleton comparison.

**Gate:** The character completes the generated interaction with the chair in at least three translated/rotated placements. Height generalization is a separate test, not an assumed capability. Measure foot/seat penetration and pose error against an initial 5 cm tolerance at human scale; report failures rather than hiding them. If the model cannot generate an acceptable sequence, deliver the authoring implementation and concrete failure evidence, and mark this milestone incomplete.

## Phase 6 — Validate and deliver

Use focused tests around the failure-prone boundaries:

- Basis/quaternion round trips, forward kinematics, rig mapping, normalization, and valid-frame truncation.
- Model reference comparisons with controlled randomness.
- Buffer time indexing, stale requests, context alignment, starvation, cancellation, and restart recovery.
- PlayMode controls, scene loading, and interaction events where implemented.
- A built Windows player smoke test; an Editor-only demonstration is insufficient.

Performance targets are provisional engineering budgets for the recorded test machine, not NVIDIA guarantees:

- One character in a simple 1080p scene, targeting 60 rendered FPS after warmup.
- Under 1 ms p95 for this feature's main-thread CPU work; separately report inference GPU time and total frame time.
- Under 100 ms p95 from a meaningful input change to an observable motion response, accounting for buffering rather than measuring inference alone.
- No steady-state buffer underruns during the ten-minute nominal test; separately document injected failures.

If targets fail, profile first. Try scheduling, avoiding allocations/readback stalls, validated precision changes, and backend selection before proposing retraining. Do not infer crowd capacity by dividing NVIDIA's published throughput figure.

Record visual QA with a video when tooling permits; otherwise capture representative frames and document the inspection performed. Do not claim subjective human-motion quality is proven by passing tensor tests. Distinguish implementation correctness, measured performance, visual findings, and remaining owner acceptance.

Deliver:

1. `LocomotionDemo` and, if successful, `SmartObjectDemo` scenes.
2. Runtime components, editor setup/mapping tools, model manifest, and focused tests.
3. Python reference harness and optional service with locked dependencies.
4. Scripts or documented commands to fetch permitted model artifacts and reproduce setup.
5. `docs/quickstart.md` with exact commands, editor/build versions, controls, model placement, startup, and troubleshooting.
6. `docs/validation.md` with hardware, fixture results, profiling, visual evidence, and pass/fail per acceptance criterion.
7. `docs/limitations.md` covering source model provenance, supported rigs/behaviors, platform restrictions, missing interaction support, and any external-process requirement.
8. Appropriate third-party notices, a Windows player build, and a short final status identifying achieved milestones and blockers.

Suggested organization; adapt to repository conventions:

```text
Assets/MotionBricksUnity/
  Runtime/{Inference,Motion,Skeleton,Retargeting,Gameplay,Diagnostics}/
  Editor/
  Tests/{EditMode,PlayMode}/
  Samples/
Tools/motionbricks_reference/
Models/                         # local artifacts; excluded from normal Git tracking
docs/
model-manifest.json
```

## Boundaries and blocker handling

- Do not train or fine-tune models, buy assets, rent GPUs, publish a package, or deploy a service externally as part of this brief.
- Do not install Isaac Sim, ROS, the robot SDK, or the complete WBC deployment environment unless a demonstrated dependency makes it necessary; pose generation should be isolated first.
- Do not use robot hardware deployment commands as setup shortcuts.
- Do not download BONES-SEED or assume its data license follows the weights license. Custom training is a separate proposal requiring a suitable dataset and confirmed rights. See the [dataset license](https://bones.studio/info/seed-license).
- If authentication, Unity activation, or a missing rig blocks a phase, ask for the specific missing item and continue independent work. Never replace a required real-model test with a mock and mark it passed.
- If a model lacks an essential behavior, save a minimal reproducible failing case and report whether the issue is missing interface access, missing weights, retargeting, or model quality. Propose the smallest next step without silently expanding into an open-ended training project.
