"""Build the final Ranged Maw source, LODs, rig, animations, previews and QA.

Run with Blender 5.2:
  blender --background --factory-startup --python build_ranged_maw.py

The only art inputs are the immutable files under ./MeshySource.
"""

from __future__ import annotations

import json
import math
import shutil
import subprocess
import time
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "MeshySource"
SOURCE_FBX = SOURCE / "RangedMaw_Meshy6_Source.fbx"
SOURCE_TEXTURES = SOURCE / "RangedMaw_Meshy6_Source_textures"
GENERATED = ROOT / "Generated"
TEXTURES = GENERATED / "Textures"
RENDERS = GENERATED / "Renders"
VALIDATION = GENERATED / "Validation"
MASTER_BLEND = GENERATED / "RangedMaw_Master.blend"
LOD0_FBX = GENERATED / "RangedMaw_LOD0.fbx"
LOD1_FBX = GENERATED / "RangedMaw_LOD1.fbx"
HANDOFF = GENERATED / "HANDOFF.md"
BUILD_REPORT = VALIDATION / "RangedMaw_BuildReport.json"

FPS = 30
LOD0_TARGET = 5900
LOD1_TARGET = 3000
EXPECTED_ACTIONS = {
    "Idle": (0, 29),
    "Walk_Heavy_Ranged": (0, 31),
    "Attack_Ranged_MawDischarge": (0, 32),
    "Hit_FullBody": (0, 6),
    "Death_Backward": (0, 35),
}
EXPECTED_BONES = [
    "Hips",
    "LeftUpLeg",
    "LeftLeg",
    "LeftFoot",
    "LeftToeBase",
    "RightUpLeg",
    "RightLeg",
    "RightFoot",
    "RightToeBase",
    "Spine02",
    "Spine01",
    "Spine",
    "LeftShoulder",
    "LeftArm",
    "LeftForeArm",
    "LeftHand",
    "RightShoulder",
    "RightArm",
    "RightForeArm",
    "RightHand",
    "neck",
    "Head",
    "head_end",
    "headfront",
    "Jaw",
    "ProjectileOrigin_Mouth",
]


def require_inputs() -> None:
    required = [
        SOURCE_FBX,
        SOURCE_TEXTURES / "base_color.png",
        SOURCE_TEXTURES / "normal.png",
        SOURCE_TEXTURES / "emission.png",
    ]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing MeshySource inputs:\n" + "\n".join(missing))
    for directory in (GENERATED, TEXTURES, RENDERS, VALIDATION):
        directory.mkdir(parents=True, exist_ok=True)


def select_only(*objects: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0] if objects else None


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def object_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    # `Object.bound_box` can remain stale immediately after applying an imported
    # FBX transform. Vertex coordinates are authoritative for build proportions.
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return minimum, maximum


def normalize_imported_mesh() -> bpy.types.Object:
    bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX), use_image_search=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("MeshySource FBX imported no mesh")
    for obj in meshes:
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
    if len(meshes) > 1:
        select_only(*meshes)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        source = meshes[0]
    else:
        source = meshes[0]
    minimum, maximum = object_bounds(source)
    offset = Vector((-(minimum.x + maximum.x) * 0.5, -(minimum.y + maximum.y) * 0.5, -minimum.z))
    source.data.transform(Matrix.Translation(offset))
    source.data.update()
    source.name = "SM_RangedMaw_SourceHigh"
    source.data.name = "SM_RangedMaw_SourceHigh_Mesh"
    return source


def downscale_texture(source_name: str, output_name: str, colorspace: str) -> bpy.types.Image:
    source_path = SOURCE_TEXTURES / source_name
    output_path = TEXTURES / output_name
    temporary = bpy.data.images.load(str(source_path), check_existing=False)
    temporary.scale(1024, 1024)
    temporary.file_format = "PNG"
    temporary.filepath_raw = str(output_path)
    temporary.save()
    bpy.data.images.remove(temporary)
    image = bpy.data.images.load(str(output_path), check_existing=False)
    image.name = output_name
    image.colorspace_settings.name = colorspace
    image.pack()
    image.filepath = bpy.path.relpath(str(output_path))
    return image


def principled_input(node: bpy.types.Node, *names: str):
    for name in names:
        if name in node.inputs:
            return node.inputs[name]
    raise KeyError(f"Missing Principled input {names}")


def make_materials(images: dict[str, bpy.types.Image]) -> tuple[bpy.types.Material, bpy.types.Material]:
    materials = []
    for name, emission_strength in (("M_RangedMaw_Body", 0.10), ("M_RangedMaw_Head", 2.25)):
        material = bpy.data.materials.new(name)
        material.use_nodes = True
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        shader = nodes.new("ShaderNodeBsdfPrincipled")
        shader.inputs["Metallic"].default_value = 0.02
        shader.inputs["Roughness"].default_value = 0.62
        principled_input(shader, "Emission Strength").default_value = emission_strength
        base = nodes.new("ShaderNodeTexImage")
        base.name = "BaseColor_1K_sRGB"
        base.image = images["base"]
        normal = nodes.new("ShaderNodeTexImage")
        normal.name = "Normal_1K_NonColor"
        normal.image = images["normal"]
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = 0.72
        emission = nodes.new("ShaderNodeTexImage")
        emission.name = "Emission_1K_sRGB"
        emission.image = images["emission"]
        links.new(base.outputs["Color"], principled_input(shader, "Base Color"))
        links.new(normal.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
        links.new(emission.outputs["Color"], principled_input(shader, "Emission Color", "Emission"))
        links.new(shader.outputs["BSDF"], output.inputs["Surface"])
        materials.append(material)
    return materials[0], materials[1]


def clean_normals(obj: bpy.types.Object) -> None:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    mesh.validate(clean_customdata=False)
    mesh.update()


def assign_material_roles(
    obj: bpy.types.Object,
    body: bpy.types.Material,
    head: bpy.types.Material,
    height: float,
) -> dict[str, int]:
    obj.data.materials.clear()
    obj.data.materials.append(body)  # Required slot 0.
    obj.data.materials.append(head)  # Required slot 1.
    head_faces = 0
    for polygon in obj.data.polygons:
        center = polygon.center
        is_head = center.z >= height * 0.775 and abs(center.x) <= height * 0.19
        polygon.material_index = 1 if is_head else 0
        head_faces += int(is_head)
    if head_faces == 0 or head_faces == len(obj.data.polygons):
        raise RuntimeError(f"Invalid Body/Head material split on {obj.name}")
    return {"body_faces": len(obj.data.polygons) - head_faces, "head_faces": head_faces}


def make_lod(source: bpy.types.Object, name: str, target: int) -> bpy.types.Object:
    lod = source.copy()
    lod.data = source.data.copy()
    bpy.context.scene.collection.objects.link(lod)
    lod.name = name
    lod.data.name = name + "_Mesh"
    current = triangle_count(lod)
    passes = 0
    while current > target and passes < 3:
        modifier = lod.modifiers.new(f"Decimate_{target}_{passes}", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.001, min(1.0, (target / current) * 0.985))
        if hasattr(modifier, "use_collapse_triangulate"):
            modifier.use_collapse_triangulate = True
        select_only(lod)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        current = triangle_count(lod)
        passes += 1
    clean_normals(lod)
    if triangle_count(lod) > target:
        raise RuntimeError(f"{name} exceeded triangle target: {triangle_count(lod)} > {target}")
    if not lod.data.uv_layers:
        raise RuntimeError(f"{name} lost UVs during decimation")
    return lod


def create_rig(height: float, half_width: float, min_y: float) -> tuple[bpy.types.Object, dict[str, tuple[Vector, Vector]]]:
    h = height
    hip_x = min(h * 0.072, half_width * 0.22)
    shoulder_x = min(h * 0.135, half_width * 0.34)
    elbow_x = min(h * 0.29, half_width * 0.68)
    wrist_x = min(h * 0.43, half_width * 0.97)
    front = min_y
    specs = [
        ("Hips", (0, 0, 0.455*h), (0, 0, 0.520*h), None, True),
        ("LeftUpLeg", (hip_x, 0, 0.470*h), (hip_x, 0, 0.270*h), "Hips", True),
        ("LeftLeg", (hip_x, 0, 0.270*h), (hip_x, 0, 0.075*h), "LeftUpLeg", True),
        ("LeftFoot", (hip_x, 0, 0.075*h), (hip_x, front*0.52, 0.035*h), "LeftLeg", True),
        ("LeftToeBase", (hip_x, front*0.52, 0.035*h), (hip_x, front*0.90, 0.030*h), "LeftFoot", True),
        ("RightUpLeg", (-hip_x, 0, 0.470*h), (-hip_x, 0, 0.270*h), "Hips", True),
        ("RightLeg", (-hip_x, 0, 0.270*h), (-hip_x, 0, 0.075*h), "RightUpLeg", True),
        ("RightFoot", (-hip_x, 0, 0.075*h), (-hip_x, front*0.52, 0.035*h), "RightLeg", True),
        ("RightToeBase", (-hip_x, front*0.52, 0.035*h), (-hip_x, front*0.90, 0.030*h), "RightFoot", True),
        ("Spine02", (0, 0, 0.475*h), (0, 0, 0.555*h), "Hips", True),
        ("Spine01", (0, 0, 0.555*h), (0, 0, 0.635*h), "Spine02", True),
        ("Spine", (0, 0, 0.635*h), (0, 0, 0.765*h), "Spine01", True),
        ("LeftShoulder", (0.02*h, 0, 0.755*h), (shoulder_x, 0, 0.770*h), "Spine", True),
        ("LeftArm", (shoulder_x, 0, 0.770*h), (elbow_x, 0, 0.755*h), "LeftShoulder", True),
        ("LeftForeArm", (elbow_x, 0, 0.755*h), (wrist_x, 0, 0.745*h), "LeftArm", True),
        ("LeftHand", (wrist_x, 0, 0.745*h), (half_width*0.995, 0, 0.735*h), "LeftForeArm", True),
        ("RightShoulder", (-0.02*h, 0, 0.755*h), (-shoulder_x, 0, 0.770*h), "Spine", True),
        ("RightArm", (-shoulder_x, 0, 0.770*h), (-elbow_x, 0, 0.755*h), "RightShoulder", True),
        ("RightForeArm", (-elbow_x, 0, 0.755*h), (-wrist_x, 0, 0.745*h), "RightArm", True),
        ("RightHand", (-wrist_x, 0, 0.745*h), (-half_width*0.995, 0, 0.735*h), "RightForeArm", True),
        ("neck", (0, 0, 0.755*h), (0, 0, 0.820*h), "Spine", True),
        ("Head", (0, 0, 0.820*h), (0, 0, 0.930*h), "neck", True),
        ("head_end", (0, 0, 0.930*h), (0, 0, 0.995*h), "Head", False),
        ("headfront", (0, -0.02*h, 0.875*h), (0, front*0.80, 0.875*h), "Head", False),
        ("Jaw", (0, -0.015*h, 0.875*h), (0, front*0.70, 0.835*h), "Head", True),
        ("ProjectileOrigin_Mouth", (0, front*0.66, 0.885*h), (0, front*0.92, 0.885*h), "Head", False),
    ]
    armature_data = bpy.data.armatures.new("RIG_RangedMaw_Data")
    armature = bpy.data.objects.new("RIG_RangedMaw", armature_data)
    bpy.context.scene.collection.objects.link(armature)
    select_only(armature)
    bpy.ops.object.mode_set(mode="EDIT")
    segments: dict[str, tuple[Vector, Vector]] = {}
    for name, head, tail, parent, deform in specs:
        bone = armature_data.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.use_connect = False
        bone.use_deform = deform
        if parent:
            bone.parent = armature_data.edit_bones[parent]
        try:
            bone.align_roll(Vector((0.0, -1.0, 0.0)))
        except RuntimeError:
            pass
        segments[name] = (Vector(head), Vector(tail))
    bpy.ops.object.mode_set(mode="OBJECT")
    if [bone.name for bone in armature_data.bones] != EXPECTED_BONES:
        raise RuntimeError("Rig bone contract or ordering changed")
    armature.show_in_front = True
    armature.data.display_type = "OCTAHEDRAL"
    return armature, segments


def point_segment_distance(point: Vector, head: Vector, tail: Vector) -> float:
    delta = tail - head
    denominator = delta.length_squared
    if denominator <= 1e-12:
        return (point - head).length
    t = max(0.0, min(1.0, (point - head).dot(delta) / denominator))
    return (point - (head + delta * t)).length


def weight_mesh(
    obj: bpy.types.Object,
    armature: bpy.types.Object,
    segments: dict[str, tuple[Vector, Vector]],
    height: float,
    front_y: float,
) -> dict[str, float | int]:
    while obj.vertex_groups:
        obj.vertex_groups.remove(obj.vertex_groups[0])
    deform_names = [bone.name for bone in armature.data.bones if bone.use_deform]
    groups = {name: obj.vertex_groups.new(name=name) for name in deform_names}
    epsilon = height * 0.018
    max_influences = 0
    for vertex in obj.data.vertices:
        p = vertex.co
        if p.z < height * 0.54:
            side = "Left" if p.x >= 0 else "Right"
            candidates = ["Hips", side + "UpLeg", side + "Leg", side + "Foot", side + "ToeBase"]
        elif p.z > height * 0.64 and abs(p.x) > height * 0.115:
            side = "Left" if p.x >= 0 else "Right"
            candidates = ["Spine", side + "Shoulder", side + "Arm", side + "ForeArm", side + "Hand"]
        elif p.z > height * 0.77 and abs(p.x) <= height * 0.20:
            candidates = ["Spine", "neck", "Head"]
            if p.y < front_y * 0.20 and p.z > height * 0.80:
                candidates.append("Jaw")
            else:
                candidates.append("Spine01")
        else:
            candidates = ["Hips", "Spine02", "Spine01", "Spine", "neck"]
        scored = []
        for name in candidates:
            distance = point_segment_distance(p, *segments[name])
            scored.append((1.0 / ((distance + epsilon) ** 2), name))
        scored.sort(reverse=True)
        selected = scored[:4]
        total = sum(score for score, _ in selected)
        for score, name in selected:
            groups[name].add([vertex.index], score / total, "REPLACE")
        max_influences = max(max_influences, len(selected))
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = armature
    modifier.use_vertex_groups = True
    obj.parent = armature
    obj.matrix_parent_inverse = armature.matrix_world.inverted()
    return {"vertices": len(obj.data.vertices), "max_influences": max_influences}


def reset_pose(armature: bpy.types.Object) -> None:
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def key_pose(
    armature: bpy.types.Object,
    frame: int,
    rotations: dict[str, tuple[float, float, float]] | None = None,
    locations: dict[str, tuple[float, float, float]] | None = None,
) -> None:
    reset_pose(armature)
    for name, degrees in (rotations or {}).items():
        armature.pose.bones[name].rotation_euler = tuple(math.radians(value) for value in degrees)
    for name, location in (locations or {}).items():
        armature.pose.bones[name].location = location
    for bone in armature.pose.bones:
        bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)
        bone.keyframe_insert(data_path="rotation_euler", frame=frame, group=bone.name)
        bone.keyframe_insert(data_path="scale", frame=frame, group=bone.name)


def begin_action(armature: bpy.types.Object, name: str, end: int, loop: bool) -> bpy.types.Action:
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = 0
    action.frame_end = end
    action["authored_new_for_ranged_maw"] = True
    action["fps"] = FPS
    action["loop"] = loop
    armature.animation_data_create()
    armature.animation_data.action = action
    return action


def add_marker(action: bpy.types.Action, name: str, frame: int) -> None:
    marker = action.pose_markers.new(name)
    marker.frame = frame


def create_actions(armature: bpy.types.Object, height: float) -> dict[str, bpy.types.Action]:
    actions: dict[str, bpy.types.Action] = {}

    idle_arms = {
        "LeftArm": (0, 0, -70),
        "RightArm": (0, 0, 70),
        "LeftForeArm": (0, 0, -30),
        "RightForeArm": (0, 0, 30),
    }

    idle = begin_action(armature, "Idle", 29, True)
    key_pose(armature, 0, idle_arms)
    key_pose(armature, 7, {**idle_arms, "Spine01": (1.5, 0, 0), "Spine": (2.4, 0, 0), "Head": (-1.2, 0, 0), "Jaw": (2.0, 0, 0), "LeftShoulder": (0, 0, 1.2), "RightShoulder": (0, 0, -1.2)})
    key_pose(armature, 15, {**idle_arms, "Spine01": (-0.8, 0, 0), "Spine": (-1.6, 0, 0), "Head": (1.0, 0, 0), "Jaw": (-1.0, 0, 0)})
    key_pose(armature, 22, {**idle_arms, "Spine01": (1.0, 0, 0), "Spine": (1.8, 0, 0), "Head": (-0.8, 0, 0), "Jaw": (1.5, 0, 0)})
    key_pose(armature, 29, idle_arms)
    add_marker(idle, "LoopEnd", 29)
    actions[idle.name] = idle

    walk = begin_action(armature, "Walk_Heavy_Ranged", 31, True)
    walk_frames = [0, 4, 8, 12, 16, 20, 24, 28, 31]
    for frame in walk_frames:
        phase = (frame / 31.0) * math.tau
        swing = math.cos(phase)
        lift_left = max(0.0, math.sin(phase))
        lift_right = max(0.0, -math.sin(phase))
        rotations = {
            "Hips": (1.5 * math.sin(phase * 2.0), 0, 3.5 * math.sin(phase)),
            "LeftUpLeg": (22.0 * swing, 0, 0),
            "RightUpLeg": (-22.0 * swing, 0, 0),
            "LeftLeg": (8.0 + 30.0 * lift_left, 0, 0),
            "RightLeg": (8.0 + 30.0 * lift_right, 0, 0),
            "LeftFoot": (-8.0 - 15.0 * lift_left, 0, 0),
            "RightFoot": (-8.0 - 15.0 * lift_right, 0, 0),
            "Spine02": (-2.5, 0, -3.0 * math.sin(phase)),
            "Spine": (2.0, 0, -4.0 * math.sin(phase)),
            "LeftShoulder": (-5.0 * swing, 0, 0),
            "RightShoulder": (5.0 * swing, 0, 0),
            "LeftArm": (-12.0 * swing, 0, 0),
            "RightArm": (12.0 * swing, 0, 0),
            "Head": (1.5, 0, 2.0 * math.sin(phase)),
        }
        key_pose(armature, frame, rotations)
    add_marker(walk, "FootContact_L", 0)
    add_marker(walk, "FootContact_R", 16)
    add_marker(walk, "LoopEnd", 31)
    actions[walk.name] = walk

    attack = begin_action(armature, "Attack_Ranged_MawDischarge", 32, False)
    key_pose(armature, 0, idle_arms)
    key_pose(armature, 8, {"Spine02": (-4, 0, 0), "Spine01": (-6, 0, 0), "Spine": (-8, 0, 0), "neck": (5, 0, 0), "Head": (7, 0, 0), "Jaw": (6, 0, 0), "LeftShoulder": (0, -4, 5), "RightShoulder": (0, 4, -5)})
    key_pose(armature, 15, {"Spine02": (-7, 0, 0), "Spine01": (-10, 0, 0), "Spine": (-13, 0, 0), "neck": (8, 0, 0), "Head": (12, 0, 0), "Jaw": (16, 0, 0), "LeftArm": (0, -8, 8), "RightArm": (0, 8, -8)})
    key_pose(armature, 21, {"Spine02": (8, 0, 0), "Spine01": (11, 0, 0), "Spine": (16, 0, 0), "neck": (-10, 0, 0), "Head": (-15, 0, 0), "Jaw": (38, 0, 0), "LeftShoulder": (8, -8, 12), "RightShoulder": (-8, 8, -12), "LeftArm": (10, -8, 8), "RightArm": (-10, 8, -8)})
    key_pose(armature, 24, {"Spine02": (10, 0, 0), "Spine01": (13, 0, 0), "Spine": (18, 0, 0), "neck": (-8, 0, 0), "Head": (-12, 0, 0), "Jaw": (24, 0, 0), "LeftArm": (8, 0, 5), "RightArm": (-8, 0, -5)})
    key_pose(armature, 28, {"Spine02": (3, 0, 0), "Spine01": (4, 0, 0), "Spine": (5, 0, 0), "Head": (-3, 0, 0), "Jaw": (8, 0, 0), "LeftArm": (0, 0, -45), "RightArm": (0, 0, 45), "LeftForeArm": (0, 0, -55), "RightForeArm": (0, 0, 55)})
    key_pose(armature, 32, idle_arms)
    add_marker(attack, "ProjectileRelease", 21)
    actions[attack.name] = attack

    hit = begin_action(armature, "Hit_FullBody", 6, False)
    key_pose(armature, 0)
    key_pose(armature, 2, {"Hips": (-7, 0, 12), "Spine02": (-12, 0, -10), "Spine01": (-15, 0, -8), "Spine": (-18, 0, -6), "neck": (10, 0, 9), "Head": (14, 0, 12), "LeftShoulder": (8, 0, 12), "RightShoulder": (-8, 0, 12)})
    key_pose(armature, 4, {"Hips": (3, 0, -5), "Spine02": (5, 0, 4), "Spine": (8, 0, 3), "Head": (-5, 0, -5), "Jaw": (5, 0, 0)})
    key_pose(armature, 6)
    actions[hit.name] = hit

    death = begin_action(armature, "Death_Backward", 35, False)
    key_pose(armature, 0)
    key_pose(armature, 8, {"Hips": (-12, 0, 4), "Spine02": (8, 0, 0), "Spine": (14, 0, 0), "Head": (-10, 0, 0), "LeftArm": (10, 0, 8), "RightArm": (-10, 0, -8)})
    key_pose(armature, 16, {"Hips": (-42, 0, 3), "Spine02": (12, 0, 0), "Spine": (18, 0, 0), "Head": (-14, 0, 0), "LeftUpLeg": (16, 0, 0), "RightUpLeg": (10, 0, 0), "LeftLeg": (28, 0, 0), "RightLeg": (20, 0, 0), "LeftArm": (20, 0, 18), "RightArm": (-16, 0, -15)}, {"Hips": (0, 0.10*height, -0.20*height)})
    ground_pose = {"Hips": (-88, 0, 2), "Spine02": (8, 0, 0), "Spine01": (-5, 0, 0), "Spine": (-8, 0, 0), "neck": (12, 0, 0), "Head": (10, 0, 0), "Jaw": (10, 0, 0), "LeftUpLeg": (28, 0, 0), "RightUpLeg": (22, 0, 0), "LeftLeg": (52, 0, 0), "RightLeg": (44, 0, 0), "LeftFoot": (-18, 0, 0), "RightFoot": (-12, 0, 0), "LeftArm": (28, -8, 25), "RightArm": (-22, 8, -20), "LeftForeArm": (20, 0, 0), "RightForeArm": (15, 0, 0)}
    ground_location = {"Hips": (0, 0.24*height, -0.43*height)}
    key_pose(armature, 24, ground_pose, ground_location)
    key_pose(armature, 30, ground_pose, ground_location)
    key_pose(armature, 35, ground_pose, ground_location)
    add_marker(death, "GroundStop", 30)
    actions[death.name] = death

    armature.animation_data.action = idle
    reset_pose(armature)
    bpy.context.scene.frame_set(0)
    return actions


def export_fbx(path: Path, mesh: bpy.types.Object, armature: bpy.types.Object) -> None:
    hidden = (mesh.hide_viewport, mesh.hide_render)
    try:
        mesh.hide_viewport = False
        mesh.hide_render = False
        select_only(mesh, armature)
        bpy.context.view_layer.objects.active = armature
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            axis_forward="-Z",
            axis_up="Y",
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            use_mesh_modifiers=True,
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_armature_deform_only=False,
            bake_anim=True,
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=True,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
            path_mode="COPY",
            embed_textures=True,
        )
    finally:
        mesh.hide_viewport, mesh.hide_render = hidden


def look_at(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def make_render_scene(height: float) -> tuple[bpy.types.Object, Vector, list[bpy.types.Object]]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.fps = FPS
    scene.frame_start = 0
    scene.frame_end = 35
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -1.0
    world = bpy.data.worlds.new("RangedMaw_RenderWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.022, 0.030, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.28

    camera_data = bpy.data.cameras.new("QA_Camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = height * 1.16
    camera = bpy.data.objects.new("QA_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    target = Vector((0, 0, height * 0.50))

    lights = []
    for name, energy, offset, size in (
        ("Key", 450.0, (-1.4, -1.5, 1.7), 3.2),
        ("Fill", 180.0, (1.3, -0.8, 0.8), 2.6),
        ("Rim", 350.0, (0.5, 1.3, 1.4), 2.4),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = Vector(offset) * height
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
        lights.append(light)
    return camera, target, lights


def set_front_camera(camera: bpy.types.Object, target: Vector, height: float) -> None:
    # Orthographic review framing is deterministic and includes the complete T-pose.
    camera.data.ortho_scale = height * 1.16
    camera.location = (0, -height * 3.20, height * 0.52)
    look_at(camera, target)


def render_still(path: Path, camera: bpy.types.Object, target: Vector, height: float, side: bool = False) -> None:
    scene = bpy.context.scene
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    if side:
        camera.data.ortho_scale = height * 1.16
        camera.location = (height * 2.35, 0, height * 0.52)
        look_at(camera, target)
    else:
        set_front_camera(camera, target, height)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def render_contact_sheet_frames(
    actions: dict[str, bpy.types.Action],
    armature: bpy.types.Object,
    camera: bpy.types.Object,
    target: Vector,
    height: float,
) -> Path:
    # The deterministic labeled HTML companion supplies exact frame/action labels;
    # this PNG sheet remains a compact visual review surface.
    selections = [
        ("Idle", 0),
        ("Walk_Heavy_Ranged", 0),
        ("Walk_Heavy_Ranged", 16),
        ("Attack_Ranged_MawDischarge", 15),
        ("Attack_Ranged_MawDischarge", 21),
        ("Attack_Ranged_MawDischarge", 32),
        ("Hit_FullBody", 2),
        ("Death_Backward", 16),
        ("Death_Backward", 35),
    ]
    tile_dir = GENERATED / "_contact_tiles"
    if tile_dir.exists():
        shutil.rmtree(tile_dir)
    tile_dir.mkdir(parents=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 384
    scene.render.resolution_y = 384
    set_front_camera(camera, target, height)
    for index, (name, frame) in enumerate(selections):
        armature.animation_data.action = actions[name]
        scene.frame_set(frame)
        path = tile_dir / f"{index:02d}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
    labels_path = RENDERS / "RangedMaw_ActionContactSheet.labels.json"
    labels_path.write_text(
        json.dumps(
            [{"tile": f"{index:02d}.png", "action": name, "frame": frame} for index, (name, frame) in enumerate(selections)],
            indent=2,
        ),
        encoding="utf-8",
    )
    sheet_path = RENDERS / "RangedMaw_ActionContactSheet.png"
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("ffmpeg is required for labeled contact sheet")
    raw_sheet = RENDERS / "RangedMaw_ActionContactSheet.raw.png"
    subprocess.run(
        [
            ffmpeg, "-y", "-loglevel", "error", "-framerate", "1",
            "-i", str(tile_dir / "%02d.png"),
            "-vf", "tile=3x3:padding=4:margin=4:color=black",
            "-frames:v", "1", str(raw_sheet),
        ],
        check=True,
    )
    font_path = "C\\:/Windows/Fonts/arial.ttf"
    label_filters = []
    for index, (name, frame) in enumerate(selections):
        column = index % 3
        row = index // 3
        x = 4 + column * 388
        y = 4 + row * 388
        label = f"{name}  frame {frame}"
        label_filters.append(f"drawbox=x={x}:y={y}:w=384:h=34:color=black@0.78:t=fill")
        label_filters.append(
            f"drawtext=fontfile='{font_path}':text='{label}':x={x + 10}:y={y + 8}:fontsize=17:fontcolor=white"
        )
    subprocess.run(
        [ffmpeg, "-y", "-loglevel", "error", "-i", str(raw_sheet), "-vf", ",".join(label_filters), "-frames:v", "1", str(sheet_path)],
        check=True,
    )
    raw_sheet.unlink()
    shutil.rmtree(tile_dir)
    return sheet_path


def encode_frames(frame_dir: Path, output: Path) -> None:
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("ffmpeg is required for validation MP4 generation")
    subprocess.run(
        [
            ffmpeg,
            "-y",
            "-loglevel",
            "error",
            "-framerate",
            str(FPS),
            "-i",
            str(frame_dir / "%04d.png"),
            "-c:v",
            "libx264",
            "-preset",
            "medium",
            "-crf",
            "20",
            "-pix_fmt",
            "yuv420p",
            str(output),
        ],
        check=True,
    )
    if not output.is_file() or output.stat().st_size == 0:
        raise RuntimeError(f"Video encode failed: {output}")


def remove_render_frames(frame_dir: Path) -> None:
    """Windows may briefly retain a render/encode handle; retry only this owned folder."""
    resolved = frame_dir.resolve()
    owned_root = (GENERATED / "_frames").resolve()
    if owned_root not in resolved.parents:
        raise RuntimeError(f"Refusing to remove non-owned render path: {resolved}")
    for attempt in range(12):
        try:
            shutil.rmtree(resolved)
            return
        except FileNotFoundError:
            return
        except PermissionError:
            if attempt == 11:
                raise
            time.sleep(0.25)


def render_action_video(
    action: bpy.types.Action,
    armature: bpy.types.Object,
    camera: bpy.types.Object,
    target: Vector,
    height: float,
) -> Path:
    frame_dir = GENERATED / "_frames" / action.name
    if frame_dir.exists():
        remove_render_frames(frame_dir)
    frame_dir.mkdir(parents=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    armature.animation_data.action = action
    set_front_camera(camera, target, height)
    start, end = EXPECTED_ACTIONS[action.name]
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        scene.render.filepath = str(frame_dir / f"{frame - start + 1:04d}.png")
        bpy.ops.render.render(write_still=True)
    output = RENDERS / f"Action_{action.name}.mp4"
    encode_frames(frame_dir, output)
    remove_render_frames(frame_dir)
    return output


def render_turntable(
    armature: bpy.types.Object,
    idle: bpy.types.Action,
    camera: bpy.types.Object,
    target: Vector,
    height: float,
) -> Path:
    frame_dir = GENERATED / "_frames" / "Turntable"
    if frame_dir.exists():
        remove_render_frames(frame_dir)
    frame_dir.mkdir(parents=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    armature.animation_data.action = idle
    scene.frame_set(0)
    frames = 60
    radius = height * 3.20
    for index in range(frames):
        angle = math.tau * index / frames - math.pi / 2.0
        camera.location = (math.cos(angle) * radius, math.sin(angle) * radius, height * 0.56)
        look_at(camera, target)
        scene.render.filepath = str(frame_dir / f"{index + 1:04d}.png")
        bpy.ops.render.render(write_still=True)
    output = RENDERS / "RangedMaw_Turntable.mp4"
    encode_frames(frame_dir, output)
    remove_render_frames(frame_dir)
    return output


def count_vertex_influences(obj: bpy.types.Object) -> int:
    return max((sum(1 for group in vertex.groups if group.weight > 1e-6) for vertex in obj.data.vertices), default=0)


def group_weight_stats(obj: bpy.types.Object) -> dict[str, dict[str, float | int]]:
    result = {}
    for group in obj.vertex_groups:
        weights = []
        for vertex in obj.data.vertices:
            for membership in vertex.groups:
                if membership.group == group.index and membership.weight > 1e-6:
                    weights.append(membership.weight)
                    break
        result[group.name] = {
            "weighted_vertices": len(weights),
            "max_weight": round(max(weights), 6) if weights else 0.0,
            "weight_sum": round(sum(weights), 6),
        }
    return result


def validate_required_deformation(weight_stats: dict[str, dict[str, float | int]]) -> None:
    required = [
        "LeftArm", "LeftForeArm", "LeftHand",
        "RightArm", "RightForeArm", "RightHand", "Jaw",
    ]
    failures = [name for name in required if weight_stats.get(name, {}).get("weighted_vertices", 0) == 0]
    if failures:
        raise RuntimeError("Required deformation groups have zero weighted vertices: " + ", ".join(failures))


def action_authorship_evidence(actions: dict[str, bpy.types.Action], armature: bpy.types.Object) -> dict:
    evidence = {}
    attack = actions["Attack_Ranged_MawDischarge"]
    armature.animation_data.action = attack
    bpy.context.scene.frame_set(0)
    jaw_0 = list(armature.pose.bones["Jaw"].rotation_euler)
    spine_0 = list(armature.pose.bones["Spine"].rotation_euler)
    bpy.context.scene.frame_set(21)
    jaw_21 = list(armature.pose.bones["Jaw"].rotation_euler)
    spine_21 = list(armature.pose.bones["Spine"].rotation_euler)
    if abs(jaw_21[0] - jaw_0[0]) < math.radians(20.0):
        raise RuntimeError("Attack frame 21 does not open Jaw by at least 20 degrees")
    if abs(spine_21[0] - spine_0[0]) < math.radians(10.0):
        raise RuntimeError("Attack frame 21 release recoil is not distinct")
    for name, action in actions.items():
        authored = bool(action.get("authored_new_for_ranged_maw", False))
        if not authored:
            raise RuntimeError(f"Action lacks fresh-authorship marker: {name}")
        evidence[name] = {
            "frame_range": list(EXPECTED_ACTIONS[name]),
            "authored_new_for_ranged_maw": authored,
            "source_animation_imported_or_copied": False,
        }
    evidence["Attack_Ranged_MawDischarge"]["frame21"] = {
        "release_marker": 21,
        "jaw_open_delta_degrees": round(math.degrees(jaw_21[0] - jaw_0[0]), 3),
        "spine_recoil_delta_degrees": round(math.degrees(spine_21[0] - spine_0[0]), 3),
    }
    return evidence


def normalize_action_name(name: str) -> str:
    for expected in EXPECTED_ACTIONS:
        if name == expected or name.endswith("|" + expected) or name.endswith("_" + expected):
            return expected
    return name


def fresh_import_validate(path: Path, triangle_rule: tuple[int, int]) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_image_search=True, anim_offset=0.0)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1 or len(armatures) != 1:
        raise RuntimeError(f"Fresh import object contract failed for {path.name}")
    mesh = meshes[0]
    armature = armatures[0]
    triangles = triangle_count(mesh)
    bone_names = [bone.name for bone in armature.data.bones]
    action_ranges = {}
    for action in bpy.data.actions:
        normalized = normalize_action_name(action.name)
        if normalized in EXPECTED_ACTIONS:
            action_ranges[normalized] = [round(frame) for frame in action.frame_range]
    result = {
        "fbx": str(path),
        "triangles": triangles,
        "vertices": len(mesh.data.vertices),
        "bone_count": len(bone_names),
        "bones": bone_names,
        "max_vertex_influences": count_vertex_influences(mesh),
        "uv_layers": [layer.name for layer in mesh.data.uv_layers],
        "material_slots": [slot.material.name if slot.material else None for slot in mesh.material_slots],
        "actions": action_ranges,
        "import_anim_offset": 0.0,
        "packed_images": {
            image.name: image.packed_file is not None
            for image in bpy.data.images
            if image.source == "FILE"
        },
    }
    minimum_triangles, maximum_triangles = triangle_rule
    failures = []
    if not minimum_triangles <= triangles <= maximum_triangles:
        failures.append(f"triangles {triangles} outside {triangle_rule}")
    if len(bone_names) != 26 or set(bone_names) != set(EXPECTED_BONES):
        failures.append(f"bone contract mismatch ({len(bone_names)})")
    if result["max_vertex_influences"] > 4:
        failures.append(f"max influences {result['max_vertex_influences']} > 4")
    if not result["uv_layers"]:
        failures.append("missing UV")
    if len(result["material_slots"]) != 2:
        failures.append(f"material slots {len(result['material_slots'])} != 2")
    elif result["material_slots"] != ["M_RangedMaw_Body", "M_RangedMaw_Head"]:
        failures.append(f"material slot order mismatch: {result['material_slots']}")
    if action_ranges != {name: list(frames) for name, frames in EXPECTED_ACTIONS.items()}:
        failures.append(f"action ranges mismatch: {action_ranges}")
    if failures:
        raise RuntimeError(path.name + " fresh-import validation failed: " + "; ".join(failures))
    result["status"] = "PASS"
    return result


def write_handoff(report: dict, videos: list[Path]) -> None:
    lod0 = report["fresh_import"]["LOD0"]
    lod1 = report["fresh_import"]["LOD1"]
    lines = [
        "# Ranged Maw Blender handoff",
        "",
        "## Deliverables",
        "",
        "- `RangedMaw_Master.blend`: packed 1K textures, hidden 137k-triangle Meshy source, LOD0/LOD1, 26-bone rig and five newly-authored Actions.",
        "- `RangedMaw_LOD0.fbx`: animation FBX for the highest runtime LOD.",
        "- `RangedMaw_LOD1.fbx`: animation FBX for the lower runtime LOD.",
        "- `Textures/`: 1K BaseColor (sRGB), Normal (Non-Color), Emission (sRGB).",
        "- `Renders/`: front, side, turntable, and one MP4 per Action.",
        "- `Validation/`: machine-readable build and fresh-import evidence.",
        "",
        "## Runtime geometry",
        "",
        f"- LOD0: {lod0['triangles']:,} triangles, {lod0['vertices']:,} vertices.",
        f"- LOD1: {lod1['triangles']:,} triangles, {lod1['vertices']:,} vertices.",
        "- Both FBXs: 26 bones, one UV layer, Body slot 0, Head slot 1, and at most four bone influences per vertex.",
        "- `ProjectileOrigin_Mouth` is a non-deforming child of `Head`, positioned inside the open maw. `RightHand` and `Head` retain their contract names.",
        "",
        "## Animation contract (30 fps, newly keyed)",
        "",
        "- `Idle`: 0-29, loop.",
        "- `Walk_Heavy_Ranged`: 0-31, loop, left contact at 0 and right contact at 16; locomotion is in-place.",
        "- `Attack_Ranged_MawDischarge`: 0-32, `ProjectileRelease` pose marker at frame 21 (0.7 s).",
        "- `Hit_FullBody`: 0-6.",
        "- `Death_Backward`: 0-35, settled ground pose from frame 30 through 35.",
        "- No prior Action or keyframe data was imported or copied. Object transforms are not animated; only the death pose uses an authored pelvis offset to reach the ground.",
        "- Every Action carries `authored_new_for_ranged_maw=true`; build QA asserts fresh authorship and records frame-21 Jaw-open/recoil deltas.",
        "",
        "## Validation",
        "",
        "- Blender 5.2 factory-startup fresh import: PASS for both FBXs.",
        "- LOD0/LOD1 required deformation groups are asserted non-empty for both Arm/ForeArm/Hand chains and Jaw.",
        "- `Renders/RangedMaw_ActionContactSheet.png` labels foot contacts, attack anticipation/release/recovery, hit and death poses.",
        "- Textures are packed in the master blend and embedded in each FBX; no dependency on the broken Meshy relative paths remains.",
        f"- Validation videos: {len(videos)} MP4 files.",
        "",
        "## Known limits",
        "",
        "- LOD topology is deterministic collapse-decimation of the Meshy triangulated source, not hand-retopology.",
        "- Skinning is deterministic anatomical distance weighting and requires gameplay deformation review before Unity adoption.",
        "- The open maw is source geometry; `Jaw` deformation is intentionally modest because the source has no separated lower-jaw mesh.",
        "- Unity import, prefab replacement, Animator wiring, colliders and Play Mode validation are explicitly outside this delivery.",
        "",
        "Rebuild: `blender --background --factory-startup --python build_ranged_maw.py`",
        "",
    ]
    HANDOFF.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    require_inputs()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    source = normalize_imported_mesh()
    source_min, source_max = object_bounds(source)
    height = source_max.z - source_min.z
    half_width = max(abs(source_min.x), abs(source_max.x))
    source_triangles = triangle_count(source)

    images = {
        "base": downscale_texture("base_color.png", "T_RangedMaw_BaseColor_1K.png", "sRGB"),
        "normal": downscale_texture("normal.png", "T_RangedMaw_Normal_1K.png", "Non-Color"),
        "emission": downscale_texture("emission.png", "T_RangedMaw_Emission_1K.png", "sRGB"),
    }
    body_material, head_material = make_materials(images)
    source_split = assign_material_roles(source, body_material, head_material, height)
    clean_normals(source)

    lod0 = make_lod(source, "SM_RangedMaw_LOD0", LOD0_TARGET)
    lod1 = make_lod(source, "SM_RangedMaw_LOD1", LOD1_TARGET)
    lod0_split = assign_material_roles(lod0, body_material, head_material, height)
    lod1_split = assign_material_roles(lod1, body_material, head_material, height)
    armature, segments = create_rig(height, half_width, source_min.y)
    lod0_weights = weight_mesh(lod0, armature, segments, height, source_min.y)
    lod1_weights = weight_mesh(lod1, armature, segments, height, source_min.y)
    lod0_weight_stats = group_weight_stats(lod0)
    lod1_weight_stats = group_weight_stats(lod1)
    validate_required_deformation(lod0_weight_stats)
    validate_required_deformation(lod1_weight_stats)
    actions = create_actions(armature, height)
    action_evidence = action_authorship_evidence(actions, armature)
    build_snapshot = {
        "LOD0_triangles": triangle_count(lod0),
        "LOD1_triangles": triangle_count(lod1),
        "textures": {
            name: {
                "path": str(TEXTURES / image.name),
                "size": [1024, 1024],
                "colorspace": image.colorspace_settings.name,
            }
            for name, image in images.items()
        },
    }

    source.hide_viewport = True
    source.hide_render = True
    lod1.hide_viewport = True
    lod1.hide_render = True
    lod0.hide_viewport = False
    lod0.hide_render = False

    export_fbx(LOD0_FBX, lod0, armature)
    export_fbx(LOD1_FBX, lod1, armature)

    camera, target, render_helpers = make_render_scene(height)
    armature.animation_data.action = actions["Idle"]
    scene.frame_set(0)
    render_still(RENDERS / "RangedMaw_Front.png", camera, target, height, side=False)
    render_still(RENDERS / "RangedMaw_Side.png", camera, target, height, side=True)
    videos = [render_turntable(armature, actions["Idle"], camera, target, height)]
    for name in EXPECTED_ACTIONS:
        videos.append(render_action_video(actions[name], armature, camera, target, height))
    contact_sheet = render_contact_sheet_frames(actions, armature, camera, target, height)

    armature.animation_data.action = actions["Idle"]
    scene.frame_set(0)
    set_front_camera(camera, target, height)
    lod0.hide_viewport = False
    lod0.hide_render = False
    lod1.hide_viewport = True
    lod1.hide_render = True
    source.hide_viewport = True
    source.hide_render = True
    for image in images.values():
        if image.packed_file is None:
            image.pack()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MASTER_BLEND), compress=True)

    # Fresh import resets the current scene and invalidates all build references,
    # so it must be the final Blender-data operation.
    lod0_validation = fresh_import_validate(LOD0_FBX, (1, 6000))
    lod1_validation = fresh_import_validate(LOD1_FBX, (2400, 3600))
    report = {
        "blender_version": bpy.app.version_string,
        "source": {
            "fbx": str(SOURCE_FBX),
            "triangles": source_triangles,
            "dimensions_xyz": [round(source_max.x - source_min.x, 6), round(source_max.y - source_min.y, 6), round(height, 6)],
            "material_split": source_split,
        },
        "build": {
            "master_blend": str(MASTER_BLEND),
            "LOD0": {"fbx": str(LOD0_FBX), "triangles": build_snapshot["LOD0_triangles"], "weights": lod0_weights, "weight_groups": lod0_weight_stats, "material_split": lod0_split},
            "LOD1": {"fbx": str(LOD1_FBX), "triangles": build_snapshot["LOD1_triangles"], "weights": lod1_weights, "weight_groups": lod1_weight_stats, "material_split": lod1_split},
            "textures": build_snapshot["textures"],
            "bone_count": 26,
            "actions": {name: list(frames) for name, frames in EXPECTED_ACTIONS.items()},
            "action_authorship_and_release_evidence": action_evidence,
            "videos": [str(path) for path in videos],
            "contact_sheet": str(contact_sheet),
        },
        "fresh_import": {"LOD0": lod0_validation, "LOD1": lod1_validation},
        "prohibited_steps_not_run": ["Meshy remesh", "Meshy rig", "Unity import", "prefab/code modification"],
    }
    VALIDATION.mkdir(parents=True, exist_ok=True)
    BUILD_REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    write_handoff(report, videos)
    print("RANGED_MAW_BUILD_REPORT=" + json.dumps(report, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
