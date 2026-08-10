from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
BLEND_OUTPUT = ROOT / "SuicideBacteriophage_Low.blend"
FBX_OUTPUT = PROJECT / "Assets" / "Art" / "Enemies" / "SuicideBacteriophage" / "SK_SuicideBacteriophage_Low.fbx"
TARGET_RATIO = 0.005


working = bpy.data.objects["WORKING_DECIMATE"]
rig = bpy.data.objects["Bacteriophage_Rig"]

bpy.context.scene.frame_set(1)
bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
bpy.ops.object.select_all(action="DESELECT")
working.hide_set(False)
rig.hide_set(False)
working.select_set(True)
bpy.context.view_layer.objects.active = working

decimate = next(modifier for modifier in working.modifiers if modifier.type == "DECIMATE")
armature = next((modifier for modifier in working.modifiers if modifier.type == "ARMATURE"), None)
decimate.ratio = TARGET_RATIO
if armature is not None:
    armature.show_viewport = False
bpy.ops.object.modifier_apply(modifier=decimate.name)
if armature is not None:
    armature.show_viewport = True

working.name = "SK_SuicideBacteriophage_Low"
working.data.name = "SK_SuicideBacteriophage_Low_Mesh"
rig.name = "SuicideBacteriophage_Rig"

lower = working.data.materials[0]
upper = working.data.materials[1]
lower.name = "M_SuicideBacteriophage_Lower"
upper.name = "M_SuicideBacteriophage_Upper"
lower.diffuse_color = (0.08, 0.10, 0.12, 1.0)
upper.diffuse_color = (0.32, 0.37, 0.42, 1.0)

for obj in list(bpy.context.scene.objects):
    if obj not in {working, rig}:
        bpy.data.objects.remove(obj, do_unlink=True)

working["material_0_role"] = "Lower_Body_And_Legs"
working["material_1_role"] = "Upper_Head"
working["source_decimate_ratio"] = TARGET_RATIO

ROOT.mkdir(parents=True, exist_ok=True)
FBX_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_OUTPUT))

bpy.ops.object.select_all(action="DESELECT")
working.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=str(FBX_OUTPUT),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_ALL",
    use_space_transform=True,
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    use_armature_deform_only=True,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
)

material_counts = [0, 0]
for polygon in working.data.polygons:
    if polygon.material_index < len(material_counts):
        material_counts[polygon.material_index] += 1

print(f"BLEND_OUTPUT={BLEND_OUTPUT}")
print(f"FBX_OUTPUT={FBX_OUTPUT}")
print(f"TRIANGLES={len(working.data.polygons)}")
print(f"MATERIAL_COUNTS={material_counts}")
