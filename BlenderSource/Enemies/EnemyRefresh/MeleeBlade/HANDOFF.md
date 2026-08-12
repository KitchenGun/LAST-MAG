# Gulag MeleeBlade handoff

## Outputs

- Blender source: `Gulag_MeleeBlade_Final.blend`
- Unity-ready candidates: `Exports/Gulag_MeleeBlade_LOD0.fbx`, `Exports/Gulag_MeleeBlade_LOD1.fbx`
- 1K textures: `Textures/BaseColor.png`, `Textures/Normal.png`, `Textures/Emission.png`
- Validation: `Validation/Gulag_MeleeBlade_Front.png`, `Validation/Gulag_MeleeBlade_Side.png`, `Validation/Gulag_MeleeBlade_Turntable.mp4`, `Validation/Gulag_MeleeBlade_ActionValidation.mp4`
- Per-action validation: `Validation/Idle.mp4`, `Validation/Run_Fast_Melee.mp4`, `Validation/Attack_Melee_OverheadSmash.mp4`, `Validation/Hit_FullBody.mp4`, `Validation/Death_Backward.mp4`.
- Attack contact sheet: `Validation/Attack_Melee_OverheadSmash_ContactSheet.png` (frames 0, 9, 13, 20).
- Fresh reimport QA: `Validation/Gulag_MeleeBlade_FreshReimportQA.json`

## Geometry and material contract

- Meshy source is preserved in `MeshySource/` and as hidden `SOURCE_HIGH_DO_NOT_EDIT` in the blend.
- Source: 117,608 tris. LOD0: 5,750 tris (limit 6,000). LOD1: 2,999 tris (target about 3,000).
- Meshy's finger-like auxiliary branches were removed with the source arms; deterministic closed blade prisms replace them and keep one long bilateral blade silhouette.
- One UV set, smooth normals, max three generated skin weights per vertex (contract max four). Blade weights use continuous Shoulder/Arm/ForeArm/Hand gradients.
- Material slot 0 `Body`; slot 1 `Head`. Head uses the weak source emission map; role green remains confined to the head sensor UV region.
- FBX source texture paths were relinked from Meshy's missing `model.fbm/texture_0*.png` paths to the downloaded PBR files before making the 1K set.

## Rig contract

- 26 bones: the existing 24 names/hierarchy plus `Jaw` and `ProjectileOrigin_Mouth`.
- `RightHand` and `Head` are retained. `ProjectileOrigin_Mouth` is non-deforming and parented to `Jaw`.
- This is a newly generated skin. No existing animation or keyframe was imported or copied.

## Actions at 30 fps

- `Idle`: frames 0-29, loop.
- `Run_Fast_Melee`: frames 0-19, loop; contacts frames 2 and 12.
- `Attack_Melee_OverheadSmash`: frames 0-20; `MeleeHit` at frame 13.5 (0.45 s), held across frames 13-14.
- `Hit_FullBody`: frames 0-6.
- `Death_Backward`: frames 0-35; knees release into a forward-left/side fall, blades remain spread, frames 28-35 are still.
- All clips are in-place: armature object is never keyed, Hips X/Y remain zero, and no exported root-motion curve is authored.

## Fresh Blender 5.2 reimport

- LOD0: 5,750 tris, 26 bones, max 2 weights, 1 UV layer, 2 material slots.
- LOD1: 2,999 tris, 26 bones, max 2 weights, 1 UV layer, 2 material slots.
- FBX is fresh-imported with Blender's `anim_offset=0.0`; raw imported action ranges exactly match the 0-based contract. Full machine-readable details are in `Gulag_MeleeBlade_FreshReimportQA.json`.

## Known limits

- Automated decimation and procedural replacement blades prioritize WebGL silhouette, triangle budget, and deterministic rebuild over hand-authored edge flow.
- The emission source delivered by Meshy is very dim; material strength is raised on `Head`, but the map itself remains visually subtle.
- FBX/Unity import and prefab replacement were intentionally not performed in this task.
