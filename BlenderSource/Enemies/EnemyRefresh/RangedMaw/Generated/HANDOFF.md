# Ranged Maw Blender handoff

## Deliverables

- `RangedMaw_Master.blend`: packed 1K textures, hidden 137k-triangle Meshy source, LOD0/LOD1, 26-bone rig and five newly-authored Actions.
- `RangedMaw_LOD0.fbx`: animation FBX for the highest runtime LOD.
- `RangedMaw_LOD1.fbx`: animation FBX for the lower runtime LOD.
- `Textures/`: 1K BaseColor (sRGB), Normal (Non-Color), Emission (sRGB).
- `Renders/`: front, side, turntable, and one MP4 per Action.
- `Validation/`: machine-readable build and fresh-import evidence.

## Runtime geometry

- LOD0: 5,810 triangles, 2,907 vertices.
- LOD1: 2,954 triangles, 1,479 vertices.
- Both FBXs: 26 bones, one UV layer, Body slot 0, Head slot 1, and at most four bone influences per vertex.
- `ProjectileOrigin_Mouth` is a non-deforming child of `Head`, positioned inside the open maw. `RightHand` and `Head` retain their contract names.

## Animation contract (30 fps, newly keyed)

- `Idle`: 0-29, loop.
- `Walk_Heavy_Ranged`: 0-31, loop, left contact at 0 and right contact at 16; locomotion is in-place.
- `Attack_Ranged_MawDischarge`: 0-32, `ProjectileRelease` pose marker at frame 21 (0.7 s).
- `Hit_FullBody`: 0-6.
- `Death_Backward`: 0-35, settled ground pose from frame 30 through 35.
- No prior Action or keyframe data was imported or copied. Object transforms are not animated; only the death pose uses an authored pelvis offset to reach the ground.
- Every Action carries `authored_new_for_ranged_maw=true`; build QA asserts fresh authorship and records frame-21 Jaw-open/recoil deltas.

## Validation

- Blender 5.2 factory-startup fresh import: PASS for both FBXs.
- LOD0/LOD1 required deformation groups are asserted non-empty for both Arm/ForeArm/Hand chains and Jaw.
- `Renders/RangedMaw_ActionContactSheet.png` labels foot contacts, attack anticipation/release/recovery, hit and death poses.
- Textures are packed in the master blend and embedded in each FBX; no dependency on the broken Meshy relative paths remains.
- Validation videos: 6 MP4 files.

## Known limits

- LOD topology is deterministic collapse-decimation of the Meshy triangulated source, not hand-retopology.
- Skinning is deterministic anatomical distance weighting and requires gameplay deformation review before Unity adoption.
- The open maw is source geometry; `Jaw` deformation is intentionally modest because the source has no separated lower-jaw mesh.
- Unity import, prefab replacement, Animator wiring, colliders and Play Mode validation are explicitly outside this delivery.

Rebuild: `blender --background --factory-startup --python build_ranged_maw.py`
