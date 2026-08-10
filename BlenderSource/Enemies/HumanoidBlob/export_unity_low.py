import bpy
from pathlib import Path


ROOT = Path(r"C:\Users\kang9\Documents\ChatGPT\Gulag-project")
OUTPUT_DIR = ROOT / "Assets" / "Art" / "Enemies" / "HumanoidBlob"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
OUTPUT_FBX = OUTPUT_DIR / "SM_HumanoidBlob_Low.fbx"
OUTPUT_BLEND = ROOT / "BlenderSource" / "Enemies" / "HumanoidBlob" / "HumanoidBlob_UnityExport.blend"
TARGET_TRIANGLES = 2400
MAX_TRIANGLES = 2500


def triangle_count(mesh):
    return sum(len(poly.vertices) - 2 for poly in mesh.polygons)


working = bpy.data.objects["HumanoidBlob_WORKING"]
armature = bpy.data.objects["RIG_HumanoidBlob"]
source = bpy.data.objects["HumanoidBlob_SOURCE_HIGH"]
source.hide_viewport = True
source.hide_render = True

armature.data.pose_position = "REST"
bpy.context.scene.frame_set(1)

decimate = working.modifiers.get("USER_POLY_CONTROL")
if decimate is None:
    raise RuntimeError("USER_POLY_CONTROL modifier not found")

base_triangles = triangle_count(working.data)
decimate.ratio = min(1.0, TARGET_TRIANGLES / base_triangles)

armature_modifier = next((modifier for modifier in working.modifiers if modifier.type == "ARMATURE"), None)
if armature_modifier:
    armature_modifier.show_viewport = False
    armature_modifier.show_render = False

bpy.context.view_layer.objects.active = working
working.select_set(True)
bpy.ops.object.modifier_apply(modifier=decimate.name)

if armature_modifier:
    armature_modifier.show_viewport = True
    armature_modifier.show_render = True

final_triangles = triangle_count(working.data)
if final_triangles > MAX_TRIANGLES:
    raise RuntimeError(f"Triangle limit exceeded: {final_triangles} > {MAX_TRIANGLES}")

body = bpy.data.materials.get("M_HumanoidBlob_Body") or bpy.data.materials.new("M_HumanoidBlob_Body")
body.diffuse_color = (0.14, 0.15, 0.17, 1.0)
body.metallic = 0.0
body.roughness = 0.78

head = bpy.data.materials.get("M_HumanoidBlob_Head_Default") or bpy.data.materials.new("M_HumanoidBlob_Head_Default")
head.diffuse_color = (0.42, 0.43, 0.45, 1.0)
head.metallic = 0.0
head.roughness = 0.68

working.data.materials.clear()
working.data.materials.append(body)
working.data.materials.append(head)

head_group_names = {"Head", "head_end", "headfront", "neck"}
head_group_indices = {
    working.vertex_groups[name].index
    for name in head_group_names
    if working.vertex_groups.get(name) is not None
}
if not head_group_indices:
    raise RuntimeError("Head rig vertex groups not found")

def head_weight(vertex_index):
    return sum(
        assignment.weight
        for assignment in working.data.vertices[vertex_index].groups
        if assignment.group in head_group_indices
    )

head_faces = 0
for polygon in working.data.polygons:
    average_head_weight = sum(head_weight(index) for index in polygon.vertices) / len(polygon.vertices)
    polygon.material_index = 1 if average_head_weight >= 0.35 else 0
    head_faces += int(polygon.material_index == 1)

if head_faces == 0:
    raise RuntimeError("Head material assignment produced no faces")

armature.data.pose_position = "POSE"
armature.animation_data.action = bpy.data.actions.get("Idle")
bpy.context.scene.frame_set(1)

bpy.context.scene["unity_export_triangles"] = final_triangles
bpy.context.scene["head_material_selection"] = "Head rig weights >= 0.35"
bpy.context.scene["head_material_faces"] = head_faces

bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))

bpy.ops.object.select_all(action="DESELECT")
working.select_set(True)
armature.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=str(OUTPUT_FBX),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
)

print(f"OUTPUT_FBX={OUTPUT_FBX}")
print(f"FINAL_TRIANGLES={final_triangles}")
print(f"HEAD_FACES={head_faces}")
