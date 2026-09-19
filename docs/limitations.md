# Limitations

- Runtime generation uses the GEAR-SONIC G1 planner, with a separately launched local Python CPU service. Native import/execution was tested but is not the shipping provider due to scheduling cost and incomplete numerical certification.
- The source is a 29-DOF robot. Human retargeting is an automatically calibrated chain mapping, not a human-trained model. Fingers and unsupported articulation retain authored rest behavior. Humanoid validity does not certify animation quality.
- The chair authoring types expose source-rig entry/seated/exit poses and placement gizmos. The actual backbone was tested independently, but its authored seated endpoints fail the human-scale pose tolerance. No SmartObjectDemo is claimed, no complete seated/stand timeline is wired into gameplay, and interruption/completion events for generated interactions remain incomplete.
- Ground/seat penetration is not proven by joint-point minima. The chair failure report includes source pose error and three placement tests; human mesh/seat contact, stand-up quality and clearance acceptance are outstanding.
- Walls use simple horizontal capsule sweeps on layer 8. Corrections translate generated future poses and invalidate outstanding responses. Slopes, stairs, moving platforms, arbitrary traversal, navigation planning and multiple characters are outside this prototype.
- Source playback is explicitly recorded playback. It is useful for comparison but cannot establish live generation.
- A disconnected provider produces a stationary hold and visible diagnostics. It reconnects after restart. There is no indefinite extrapolation, and no claim that holding is natural animation.
- Performance numbers distinguish ONNX CPU, backbone CUDA, native scheduling, total frames and request completion. Request completion is not an input-to-visible-motion measurement.
- The supplied FBX and its material/texture licenses remain the owner's responsibility for redistribution. This task does not publish any assets or weights.
