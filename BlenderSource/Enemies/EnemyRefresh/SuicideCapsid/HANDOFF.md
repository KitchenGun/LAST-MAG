# Suicide Capsid Blender handoff

## Deliverables

- `Final/SuicideCapsid_Final.blend`
- `Final/SuicideCapsid_LOD0.fbx`
- `Final/SuicideCapsid_LOD1.fbx`
- `Final/Textures/`: 1K BaseColor, Normal, Emission
- `Final/Validation/`: Front, Side, Top, Turntable PNG, action MP4, QA JSON
- Action checkpoint PNGs cover each clip's start/middle/end frame.

Rebuild with Blender 5.2:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --python 'BlenderSource\Enemies\EnemyRefresh\SuicideCapsid\build_suicide_capsid.py'
```

## Rig and clips

- Generic bones: `root`, `body`, `upper`, then `leg_L/R_Front/Mid/Rear_01..03`.
- `Idle`: frames 0-59, loop, capsid breathing.
- `Move_LegsOnly`: frames 0-24, loop, in-place alternating tripod gait.
- `Warning_Explode`: frames 0-23, crouch plus capsid expansion. Normal duration is 0.8 s; runtime speed multiplier 4 gives 0.2 s.

## Materials

- Slot 0: `M_SuicideCapsid_Lower`, rebuilt legs.
- Slot 1: `M_SuicideCapsid_Upper`, Meshy capsid/body with localized red emission.
- Source BaseColor/Normal/Emission were relinked and resized to 1024x1024.

## Geometry decision and limits

Meshy's eight legs and body are one connected mesh, so one pair cannot be safely separated. The pipeline preserves and decimates the source capsid/body above the lower cut, removes the fused leg band, then builds exactly six clean three-segment legs. A low organic Lower-material base plate overlaps the retained torso and all six hip roots, hiding the cut and making the chains read as attached. This source asset is not remeshed, rigged, or written into Unity by this pipeline.

See `Final/Validation/SuicideCapsid_QA.json` for fresh-FBX-import evidence.
