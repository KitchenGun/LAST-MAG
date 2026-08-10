# Bacteriophage Blender setup

1. Open `Bacteriophage_HighQuality_Setup.blend`.
2. Select `WORKING_DECIMATE` in the `WORKING_DECIMATE` collection.
3. Open the wrench tab and change `USER_POLY_CONTROL > Ratio`.
   - `1.0`: original 435,518 triangles
   - target ratio formula: `wanted triangles / 435518`
   - example 100,000 triangles: about `0.23`
4. Keep the modifier unapplied while comparing silhouette and animation.
5. `SOURCE_HIGH` is the hidden untouched backup.

Materials:

- `M_Bacteriophage_Upper`: capsid head
- `M_Bacteriophage_Lower`: collar, sheath, base body, legs

Actions:

- `Idle`: static rest state
- `Move_LegsOnly`: 25-frame, 30 FPS, in-place loop; only leg groups are keyed

The eight physical support legs are controlled as four paired quadrant groups.
