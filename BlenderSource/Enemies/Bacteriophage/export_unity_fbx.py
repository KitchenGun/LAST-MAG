from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parent
OUTPUT = ROOT / "Exports" / "SK_Bacteriophage.fbx"
OUTPUT.parent.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action="DESELECT")
working = bpy.data.objects["WORKING_DECIMATE"]
armature = bpy.data.objects["Bacteriophage_Rig"]
working.hide_set(False)
armature.hide_set(False)
working.select_set(True)
armature.select_set(True)
bpy.context.view_layer.objects.active = armature

bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT),
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
print(f"UNITY_FBX={OUTPUT}")
