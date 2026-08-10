from pathlib import Path
import math

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE_FBX = ROOT / "SM_Bacteriophage_MeshyHigh.fbx"
OUTPUT_BLEND = ROOT / "Bacteriophage_HighQuality_Setup.blend"
PREVIEW_PNG = ROOT / "Bacteriophage_BlenderSetup_Preview.png"
TARGET_HEIGHT_M = 2.0


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def make_material(name, color, metallic=0.0, roughness=0.55):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return material


def add_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def add_bone(armature, name, head, tail, parent=None):
    bone = armature.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.parent = parent
    bone.use_connect = False
    return bone


def insert_rotation(pose_bone, frame, degrees):
    pose_bone.rotation_mode = "XYZ"
    pose_bone.rotation_euler = (math.radians(degrees), 0.0, 0.0)
    pose_bone.keyframe_insert("rotation_euler", frame=frame, group=pose_bone.name)


bpy.ops.wm.read_factory_settings(use_empty=True)

source_collection = add_collection("SOURCE_HIGH")
working_collection = add_collection("WORKING_DECIMATE")
rig_collection = add_collection("RIG")
preview_collection = add_collection("PREVIEW")

bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if len(meshes) != 1:
    raise RuntimeError(f"Expected one Meshy mesh, found {len(meshes)}")

source = meshes[0]
source.name = "SOURCE_HIGH"
source.data.name = "SOURCE_HIGH_Mesh"

# Meshy FBX is Y-up. Convert it to Blender/Unity-friendly Z-up, then normalize to 2 m.
source.rotation_euler.x = math.radians(90.0)
bpy.context.view_layer.objects.active = source
source.select_set(True)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

scale = TARGET_HEIGHT_M / max(source.dimensions.z, 0.0001)
source.scale = (scale, scale, scale)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

world_corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
minimum = Vector((min(v.x for v in world_corners), min(v.y for v in world_corners), min(v.z for v in world_corners)))
maximum = Vector((max(v.x for v in world_corners), max(v.y for v in world_corners), max(v.z for v in world_corners)))
source.location -= Vector(((minimum.x + maximum.x) * 0.5, (minimum.y + maximum.y) * 0.5, minimum.z))
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
move_to_collection(source, source_collection)
source.hide_set(True)
source.hide_render = True

working = source.copy()
working.data = source.data.copy()
working.name = "WORKING_DECIMATE"
working.data.name = "Bacteriophage_Working_Mesh"
working.hide_set(False)
working.hide_render = False
working_collection.objects.link(working)

upper_material = make_material("M_Bacteriophage_Upper", (0.38, 0.42, 0.46), metallic=0.15, roughness=0.48)
lower_material = make_material("M_Bacteriophage_Lower", (0.12, 0.14, 0.16), metallic=0.25, roughness=0.52)
working.data.materials.clear()
working.data.materials.append(lower_material)
working.data.materials.append(upper_material)

# The capsid starts above the collar. Keep exactly two slots: Upper=head, Lower=body+legs.
head_start_z = TARGET_HEIGHT_M * 0.56
for polygon in working.data.polygons:
    polygon.material_index = 1 if polygon.center.z >= head_start_z else 0

decimate = working.modifiers.new("USER_POLY_CONTROL", "DECIMATE")
decimate.decimate_type = "COLLAPSE"
decimate.ratio = 1.0
decimate.use_collapse_triangulate = True
decimate.show_viewport = True
decimate.show_render = True

# Meshy resolved the four visible legs in each side view as eight physical legs.
# Drive them as four quadrant groups so paired supports move together.
low_vertices = [vertex.co for vertex in working.data.vertices if vertex.co.z < TARGET_HEIGHT_M * 0.24]
quadrants = {
    "FL": [v for v in low_vertices if v.x < 0.0 and v.y < 0.0],
    "FR": [v for v in low_vertices if v.x > 0.0 and v.y < 0.0],
    "BL": [v for v in low_vertices if v.x < 0.0 and v.y > 0.0],
    "BR": [v for v in low_vertices if v.x > 0.0 and v.y > 0.0],
}

half_x = working.dimensions.x * 0.34
half_y = working.dimensions.y * 0.34
fallback = {
    "FL": Vector((-half_x, -half_y, 0.0)),
    "FR": Vector((half_x, -half_y, 0.0)),
    "BL": Vector((-half_x, half_y, 0.0)),
    "BR": Vector((half_x, half_y, 0.0)),
}
centers = {}
for name, points in quadrants.items():
    if points:
        centers[name] = Vector((sum(v.x for v in points) / len(points), sum(v.y for v in points) / len(points), 0.0))
    else:
        centers[name] = fallback[name]

armature_data = bpy.data.armatures.new("Bacteriophage_Rig")
armature_object = bpy.data.objects.new("Bacteriophage_Rig", armature_data)
rig_collection.objects.link(armature_object)
armature_object.show_in_front = True
bpy.context.view_layer.objects.active = armature_object
armature_object.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")

root_bone = add_bone(armature_data, "Root", (0.0, 0.0, 0.0), (0.0, 0.0, TARGET_HEIGHT_M * 0.65))
leg_bones = {}
hip_z = TARGET_HEIGHT_M * 0.23
knee_z = TARGET_HEIGHT_M * 0.115
ankle_z = TARGET_HEIGHT_M * 0.025
for name, center in centers.items():
    upper = add_bone(
        armature_data,
        f"Leg_{name}_Upper",
        (center.x, center.y, hip_z),
        (center.x, center.y, knee_z),
        root_bone,
    )
    lower = add_bone(
        armature_data,
        f"Leg_{name}_Lower",
        (center.x, center.y, knee_z),
        (center.x, center.y, ankle_z),
        upper,
    )
    leg_bones[name] = (upper.name, lower.name)

bpy.ops.object.mode_set(mode="OBJECT")

# Assign only low, nearby geometry to leg bones; the head and body stay on Root.
vertex_assignments = {bone: [] for pair in leg_bones.values() for bone in pair}
root_vertices = []
leg_radius = max(working.dimensions.x, working.dimensions.y) * 0.16
for vertex in working.data.vertices:
    position = vertex.co
    nearest_name, nearest_center = min(centers.items(), key=lambda item: (position.x - item[1].x) ** 2 + (position.y - item[1].y) ** 2)
    distance = math.hypot(position.x - nearest_center.x, position.y - nearest_center.y)
    if position.z <= hip_z * 1.08 and distance <= leg_radius:
        upper_name, lower_name = leg_bones[nearest_name]
        target = upper_name if position.z >= knee_z else lower_name
        vertex_assignments[target].append(vertex.index)
    else:
        root_vertices.append(vertex.index)

root_group = working.vertex_groups.new(name="Root")
root_group.add(root_vertices, 1.0, "REPLACE")
for bone_name, indices in vertex_assignments.items():
    group = working.vertex_groups.new(name=bone_name)
    if indices:
        group.add(indices, 1.0, "REPLACE")

working.parent = armature_object
armature_modifier = working.modifiers.new("Leg_Rig", "ARMATURE")
armature_modifier.object = armature_object

armature_object.animation_data_create()

# Idle is intentionally static. It is a separate action for Unity state switching.
idle_action = bpy.data.actions.new("Idle")
idle_action.use_fake_user = True
armature_object.animation_data.action = idle_action
for pose_bone in armature_object.pose.bones:
    if pose_bone.name.startswith("Leg_"):
        insert_rotation(pose_bone, 1, 0.0)
        insert_rotation(pose_bone, 60, 0.0)

# In-place gait: only leg bones receive keys. Root, body, neck, and head never move.
move_action = bpy.data.actions.new("Move_LegsOnly")
move_action.use_fake_user = True
armature_object.animation_data.action = move_action
diagonal_a = {"FL", "BR"}
for name, (upper_name, lower_name) in leg_bones.items():
    sign = 1.0 if name in diagonal_a else -1.0
    upper_pose = armature_object.pose.bones[upper_name]
    lower_pose = armature_object.pose.bones[lower_name]
    for frame, phase in ((1, 0.0), (7, 1.0), (13, 0.0), (19, -1.0), (25, 0.0)):
        insert_rotation(upper_pose, frame, sign * phase * 11.0)
        insert_rotation(lower_pose, frame, -sign * phase * 17.0)

armature_object.animation_data.action = move_action
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = 25
bpy.context.scene.render.fps = 30
bpy.context.scene.frame_set(1)

# Preview setup.
floor_material = make_material("M_PreviewFloor", (0.035, 0.04, 0.045), metallic=0.0, roughness=0.9)
bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, -0.012))
floor = bpy.context.object
floor.name = "Preview_Floor"
floor.data.materials.append(floor_material)
move_to_collection(floor, preview_collection)

bpy.ops.object.camera_add(location=(3.2, -5.0, 2.6))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.7
look_at(camera, (0.0, 0.0, 1.0))
move_to_collection(camera, preview_collection)
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="AREA", location=(2.5, -3.0, 4.0))
key_light = bpy.context.object
key_light.name = "Preview_Key"
key_light.data.energy = 950.0
key_light.data.shape = "DISK"
key_light.data.size = 3.0
look_at(key_light, (0.0, 0.0, 1.0))
move_to_collection(key_light, preview_collection)

bpy.ops.object.light_add(type="AREA", location=(-2.5, 1.0, 2.7))
fill_light = bpy.context.object
fill_light.name = "Preview_Fill"
fill_light.data.energy = 550.0
fill_light.data.size = 2.5
look_at(fill_light, (0.0, 0.0, 1.0))
move_to_collection(fill_light, preview_collection)

world = bpy.data.worlds.new("Preview_World")
world.color = (0.015, 0.018, 0.022)
bpy.context.scene.world = world
bpy.context.scene.render.engine = "BLENDER_EEVEE"
bpy.context.scene.render.resolution_x = 1024
bpy.context.scene.render.resolution_y = 1024
bpy.context.scene.render.resolution_percentage = 100
bpy.context.scene.render.image_settings.file_format = "PNG"
bpy.context.scene.render.filepath = str(PREVIEW_PNG)
bpy.context.scene.render.film_transparent = False

working["MeshyTaskId"] = "019fe691-d7e9-798f-a821-ef235a4da06f"
working["OriginalTriangles"] = sum(len(p.vertices) - 2 for p in working.data.polygons)
working["PolyControl"] = "Adjust USER_POLY_CONTROL > Ratio. Keep SOURCE_HIGH hidden as backup."
armature_object["AnimationNotes"] = "Eight supports are driven as four quadrant groups. Idle is static. Move_LegsOnly is in-place and keys leg bones only."

bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))
bpy.ops.render.render(write_still=True)
print(f"OUTPUT_BLEND={OUTPUT_BLEND}")
print(f"PREVIEW_PNG={PREVIEW_PNG}")
print(f"TRIANGLES={working['OriginalTriangles']}")
print(f"HEAD_START_Z={head_start_z:.4f}")
print("LEG_CENTERS=" + ",".join(f"{name}:{center.x:.4f}/{center.y:.4f}" for name, center in centers.items()))
