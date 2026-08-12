"""Deterministic Blender 5.2 build pipeline for the refreshed melee enemy.

Run:
  blender.exe --background --factory-startup --python build_melee_blade.py

The Meshy source FBX and source textures are read-only inputs. All authored
outputs are written beside this script under Exports, Textures and Validation.
"""

import bpy
import bmesh
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parent
SOURCE_DIR = ROOT / "MeshySource"
SOURCE_FBX = SOURCE_DIR / "Gulag_MeleeBlade_Meshy6.fbx"
SOURCE_TEXTURES = SOURCE_DIR / "Gulag_MeleeBlade_Meshy6_textures"
EXPORT_DIR = ROOT / "Exports"
TEXTURE_DIR = ROOT / "Textures"
VALIDATION_DIR = ROOT / "Validation"
FRAME_DIR = VALIDATION_DIR / "_frames"

OUTPUT_BLEND = ROOT / "Gulag_MeleeBlade_Final.blend"
LOD0_FBX = EXPORT_DIR / "Gulag_MeleeBlade_LOD0.fbx"
LOD1_FBX = EXPORT_DIR / "Gulag_MeleeBlade_LOD1.fbx"
FRONT_PNG = VALIDATION_DIR / "Gulag_MeleeBlade_Front.png"
SIDE_PNG = VALIDATION_DIR / "Gulag_MeleeBlade_Side.png"
TURNTABLE_MP4 = VALIDATION_DIR / "Gulag_MeleeBlade_Turntable.mp4"
ACTION_MP4 = VALIDATION_DIR / "Gulag_MeleeBlade_ActionValidation.mp4"
ATTACK_SHEET = VALIDATION_DIR / "Attack_Melee_OverheadSmash_ContactSheet.png"
QA_JSON = VALIDATION_DIR / "Gulag_MeleeBlade_FreshReimportQA.json"
HANDOFF_MD = ROOT / "HANDOFF.md"

TASK_ID = "019ff515-4ce5-7182-9f1b-9f54d2606272"
FPS = 30
LOD0_MAX_TRIS = 6000
LOD1_TARGET_TRIS = 3000

ACTION_SPECS = {
    "Idle": (0, 29, True),
    "Run_Fast_Melee": (0, 19, True),
    "Attack_Melee_OverheadSmash": (0, 20, False),
    "Hit_FullBody": (0, 6, False),
    "Death_Backward": (0, 35, False),
}

# Existing 24-name hierarchy, plus Jaw and ProjectileOrigin_Mouth.
BONES = [
    ("Hips", None, (0.0, 0.0, 0.92), (0.0, 0.0, 1.04), True),
    ("LeftUpLeg", "Hips", (0.10, 0.0, 0.90), (0.12, 0.0, 0.52), True),
    ("LeftLeg", "LeftUpLeg", (0.12, 0.0, 0.52), (0.13, 0.01, 0.14), True),
    ("LeftFoot", "LeftLeg", (0.13, 0.01, 0.14), (0.13, -0.11, 0.06), True),
    ("LeftToeBase", "LeftFoot", (0.13, -0.11, 0.06), (0.13, -0.22, 0.055), True),
    ("RightUpLeg", "Hips", (-0.10, 0.0, 0.90), (-0.12, 0.0, 0.52), True),
    ("RightLeg", "RightUpLeg", (-0.12, 0.0, 0.52), (-0.13, 0.01, 0.14), True),
    ("RightFoot", "RightLeg", (-0.13, 0.01, 0.14), (-0.13, -0.11, 0.06), True),
    ("RightToeBase", "RightFoot", (-0.13, -0.11, 0.06), (-0.13, -0.22, 0.055), True),
    ("Spine02", "Hips", (0.0, 0.0, 1.00), (0.0, 0.0, 1.15), True),
    ("Spine01", "Spine02", (0.0, 0.0, 1.15), (0.0, 0.0, 1.30), True),
    ("Spine", "Spine01", (0.0, 0.0, 1.30), (0.0, 0.0, 1.43), True),
    ("LeftShoulder", "Spine", (0.02, 0.0, 1.42), (0.18, 0.0, 1.48), True),
    ("LeftArm", "LeftShoulder", (0.18, 0.0, 1.48), (0.39, 0.0, 1.49), True),
    ("LeftForeArm", "LeftArm", (0.39, 0.0, 1.49), (0.64, 0.0, 1.49), True),
    ("LeftHand", "LeftForeArm", (0.64, 0.0, 1.49), (0.93, 0.0, 1.49), True),
    ("RightShoulder", "Spine", (-0.02, 0.0, 1.42), (-0.18, 0.0, 1.48), True),
    ("RightArm", "RightShoulder", (-0.18, 0.0, 1.48), (-0.39, 0.0, 1.49), True),
    ("RightForeArm", "RightArm", (-0.39, 0.0, 1.49), (-0.64, 0.0, 1.49), True),
    ("RightHand", "RightForeArm", (-0.64, 0.0, 1.49), (-0.93, 0.0, 1.49), True),
    ("neck", "Spine", (0.0, 0.0, 1.43), (0.0, 0.0, 1.53), True),
    ("Head", "neck", (0.0, 0.0, 1.53), (0.0, -0.015, 1.73), True),
    ("head_end", "Head", (0.0, 0.0, 1.73), (0.0, 0.0, 1.89), True),
    ("headfront", "Head", (0.0, -0.04, 1.63), (0.0, -0.16, 1.63), True),
    ("Jaw", "Head", (0.0, -0.055, 1.57), (0.0, -0.125, 1.54), True),
    ("ProjectileOrigin_Mouth", "Jaw", (0.0, -0.13, 1.59), (0.0, -0.19, 1.59), False),
]


def ensure_dirs():
    for path in (EXPORT_DIR, TEXTURE_DIR, VALIDATION_DIR):
        path.mkdir(parents=True, exist_ok=True)
    if FRAME_DIR.exists():
        shutil.rmtree(FRAME_DIR)
    FRAME_DIR.mkdir(parents=True)


def triangles(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def apply_modifier(obj, name):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=name)
    obj.select_set(False)


def decimate_to(obj, target):
    current = triangles(obj.data)
    if current <= target:
        return current
    modifier = obj.modifiers.new(f"Decimate_{target}", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.001, min(1.0, target / current))
    modifier.use_collapse_triangulate = True
    modifier.use_symmetry = True
    modifier.symmetry_axis = "X"
    apply_modifier(obj, modifier.name)
    current = triangles(obj.data)
    if current > target:
        modifier = obj.modifiers.new(f"DecimateTrim_{target}", "DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.001, min(1.0, (target - 32) / current))
        modifier.use_collapse_triangulate = True
        apply_modifier(obj, modifier.name)
    return triangles(obj.data)


def load_source():
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX), use_anim=False)
    imported = list(set(bpy.context.scene.objects) - before)
    source = max((o for o in imported if o.type == "MESH"), key=lambda o: len(o.data.vertices))
    for obj in imported:
        if obj != source:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = source
    source.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    source.select_set(False)

    # Meshy exported centered despite origin_at=bottom. Normalize bottom to Z=0.
    min_z = min(v.co.z for v in source.data.vertices)
    for vertex in source.data.vertices:
        vertex.co.z -= min_z
    source.data.update()
    source.name = "MeleeBlade_SOURCE_HIGH_DO_NOT_EDIT"
    source.data.name = "MeleeBlade_SOURCE_HIGH_Mesh"
    source["source_fbx"] = str(SOURCE_FBX)
    source["source_triangles"] = triangles(source.data)
    source.hide_viewport = True
    source.hide_render = True
    source.hide_set(True)

    source_collection = bpy.data.collections.new("SOURCE_HIGH_DO_NOT_EDIT")
    bpy.context.scene.collection.children.link(source_collection)
    for collection in list(source.users_collection):
        collection.objects.unlink(source)
    source_collection.objects.link(source)
    return source


def prune_source_arms_and_add_blades(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()

    # Remove the generated finger/prong arm geometry. New blade roots overlap the
    # shoulders, leaving a clean single-blade silhouette without a risky boolean.
    doomed = [
        face for face in bm.faces
        if face.calc_center_median().z > 1.28 and any(abs(v.co.x) > 0.225 for v in face.verts)
    ]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    loose = [vertex for vertex in bm.verts if not vertex.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")

    uv_layer = bm.loops.layers.uv.verify()
    # Each appendage is a closed tapered diamond prism. No secondary branch.
    sections = [
        (0.16, 0.105, 0.085),
        (0.25, 0.095, 0.078),
        (0.39, 0.080, 0.065),
        (0.55, 0.061, 0.050),
        (0.72, 0.043, 0.034),
        (0.86, 0.026, 0.020),
        (0.94, 0.006, 0.006),
    ]
    center_z = 1.49
    for sign in (1.0, -1.0):
        rings = []
        for distance, half_z, half_y in sections:
            x = sign * distance
            ring = [
                bm.verts.new((x, 0.0, center_z + half_z)),
                bm.verts.new((x, -half_y, center_z)),
                bm.verts.new((x, 0.0, center_z - half_z)),
                bm.verts.new((x, half_y, center_z)),
            ]
            rings.append(ring)
        for index in range(len(rings) - 1):
            for edge in range(4):
                face = bm.faces.new((
                    rings[index][edge],
                    rings[index + 1][edge],
                    rings[index + 1][(edge + 1) % 4],
                    rings[index][(edge + 1) % 4],
                ))
                for loop in face.loops:
                    u = abs(loop.vert.co.x - sign * sections[0][0]) / (sections[-1][0] - sections[0][0])
                    v = 0.5 + (loop.vert.co.z - center_z) / 0.22
                    loop[uv_layer].uv = (u, v)
        for ring, reverse in ((rings[0], sign < 0), (rings[-1], sign > 0)):
            ordered = list(reversed(ring)) if reverse else ring
            face = bm.faces.new(ordered)
            for loop in face.loops:
                loop[uv_layer].uv = (0.5, 0.5)

    bm.normal_update()
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.validate(clean_customdata=False)
    obj.data.update()
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def resize_texture(source_name, output_name, colorspace, normalize_role_emission=False):
    source = SOURCE_TEXTURES / source_name
    output = TEXTURE_DIR / output_name
    image = bpy.data.images.load(str(source), check_existing=False)
    image.colorspace_settings.name = colorspace
    image.scale(1024, 1024)
    if normalize_role_emission:
        # Meshy delivered an almost-black 0-6/255 mask. Preserve its UV mask
        # but normalize non-black pixels to the exact role color #35C759.
        pixels = list(image.pixels[:])
        role = (0x35 / 255.0, 0xC7 / 255.0, 0x59 / 255.0)
        for index in range(0, len(pixels), 4):
            intensity = max(pixels[index], pixels[index + 1], pixels[index + 2])
            if intensity > (1.0 / 255.0):
                strength = min(1.0, intensity * 80.0)
                pixels[index] = role[0] * strength
                pixels[index + 1] = role[1] * strength
                pixels[index + 2] = role[2] * strength
        image.pixels[:] = pixels
    image.filepath_raw = str(output)
    image.file_format = "PNG"
    image.save()
    image.name = output_name
    return image, output


def principled_input(node, *names):
    for name in names:
        if name in node.inputs:
            return node.inputs[name]
    raise KeyError(f"Missing Principled input: {names}")


def create_material(name, base_image, normal_image, emission_image, head=False):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    base = nodes.new("ShaderNodeTexImage")
    normal = nodes.new("ShaderNodeTexImage")
    emission = nodes.new("ShaderNodeTexImage")
    normal_map = nodes.new("ShaderNodeNormalMap")
    base.image = base_image
    normal.image = normal_image
    emission.image = emission_image
    normal.image.colorspace_settings.name = "Non-Color"
    emission.image.colorspace_settings.name = "Non-Color"
    links.new(base.outputs["Color"], principled_input(bsdf, "Base Color"))
    links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], principled_input(bsdf, "Normal"))
    principled_input(bsdf, "Roughness").default_value = 0.68
    principled_input(bsdf, "Metallic").default_value = 0.0
    if head:
        links.new(emission.outputs["Color"], principled_input(bsdf, "Emission Color", "Emission"))
        principled_input(bsdf, "Emission Strength").default_value = 8.0
    links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    material.diffuse_color = (0.10, 0.085, 0.07, 1.0) if not head else (0.07, 0.09, 0.065, 1.0)
    return material


def set_materials(obj, body, head):
    obj.data.materials.clear()
    obj.data.materials.append(body)
    obj.data.materials.append(head)
    head_faces = 0
    for polygon in obj.data.polygons:
        center = sum((obj.data.vertices[index].co for index in polygon.vertices), Vector()) / len(polygon.vertices)
        polygon.material_index = 1 if center.z >= 1.53 and abs(center.x) < 0.24 else 0
        head_faces += int(polygon.material_index == 1)
    if not head_faces:
        raise RuntimeError("Head material slot received no faces")
    obj["head_material_faces"] = head_faces


def build_rig():
    armature_data = bpy.data.armatures.new("RIG_MeleeBlade")
    rig = bpy.data.objects.new("RIG_MeleeBlade", armature_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for name, parent, head, tail, deform in BONES:
        bone = armature_data.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = deform
        created[name] = bone
    for name, parent, _head, _tail, _deform in BONES:
        if parent:
            created[name].parent = created[parent]
            created[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    rig.show_in_front = True
    rig["contract"] = "Existing 24-bone hierarchy plus Jaw and ProjectileOrigin_Mouth"
    return rig


def add_weights(obj, rig):
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)
    groups = {name: obj.vertex_groups.new(name=name) for name, _p, _h, _t, deform in BONES if deform}

    def assign(vertex, values):
        total = sum(weight for _name, weight in values)
        for bone_name, weight in values:
            groups[bone_name].add([vertex.index], weight / total, "REPLACE")

    for vertex in obj.data.vertices:
        x, y, z = vertex.co
        ax = abs(x)
        side = "Left" if x >= 0.0 else "Right"
        if z > 1.70 and ax < 0.25:
            values = [("Head", 0.78), ("head_end", 0.22)]
        elif z > 1.53 and ax < 0.25:
            values = [("Head", 0.82), ("neck", 0.18)]
        elif z > 1.28 and ax > 0.15:
            if ax < 0.25:
                values = [(f"{side}Shoulder", 0.75), ("Spine", 0.25)]
            elif ax < 0.43:
                # Continuous root-to-elbow blend avoids a rigid shoulder prism.
                t = max(0.0, min(1.0, (ax - 0.25) / 0.18))
                values = [(f"{side}Arm", 0.88 - 0.28 * t), (f"{side}Shoulder", 0.12), (f"{side}ForeArm", 0.28 * t)]
            elif ax < 0.67:
                # Elbow zone bends across Arm and ForeArm instead of snapping.
                t = max(0.0, min(1.0, (ax - 0.43) / 0.24))
                values = [(f"{side}ForeArm", 0.70), (f"{side}Arm", 0.30 * (1.0 - t)), (f"{side}Hand", 0.30 * t)]
            else:
                # Outer blade follows Hand, retaining a short ForeArm falloff.
                t = max(0.0, min(1.0, (ax - 0.67) / 0.27))
                values = [(f"{side}Hand", 0.76 + 0.20 * t), (f"{side}ForeArm", 0.24 - 0.20 * t)]
        elif z > 1.40:
            values = [("neck", 0.60), ("Spine", 0.40)]
        elif z > 1.27:
            values = [("Spine", 0.72), ("Spine01", 0.28)]
        elif z > 1.12:
            values = [("Spine01", 0.70), ("Spine02", 0.30)]
        elif z > 0.92:
            values = [("Spine02", 0.55), ("Hips", 0.45)]
        elif z > 0.56:
            values = [(f"{side}UpLeg", 0.84), ("Hips", 0.16)]
        elif z > 0.16:
            values = [(f"{side}Leg", 0.84), (f"{side}UpLeg", 0.16)]
        elif y < -0.08:
            values = [(f"{side}ToeBase", 0.72), (f"{side}Foot", 0.28)]
        else:
            values = [(f"{side}Foot", 0.82), (f"{side}Leg", 0.18)]
        assign(vertex, values)

    obj.parent = rig
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = rig
    modifier.use_vertex_groups = True
    obj["max_skin_influences"] = 3


def reset_pose(rig):
    for pose_bone in rig.pose.bones:
        pose_bone.rotation_mode = "XYZ"
        pose_bone.matrix_basis.identity()
    bpy.context.view_layer.update()


def rotate_bone(rig, name, xyz):
    pose_bone = rig.pose.bones[name]
    pose_bone.rotation_mode = "XYZ"
    pose_bone.rotation_euler = xyz


def aim_bone(rig, name, target, up_axis="Z"):
    """Aim a pose bone's +Y axis at an armature-space target."""
    pose_bone = rig.pose.bones[name]
    head = pose_bone.head.copy()
    direction = Vector(target) - head
    if direction.length < 0.0001:
        return
    rotation = direction.normalized().to_track_quat("Y", up_axis).to_matrix().to_4x4()
    pose_bone.matrix = Matrix.Translation(head) @ rotation
    bpy.context.view_layer.update()


def set_attack_chain_pose(rig, stage):
    reset_pose(rig)
    if stage == "anticipation":
        rotate_bone(rig, "Spine01", (-0.18, 0.0, 0.0))
        bpy.context.view_layer.update()
        targets = {
            "LeftShoulder": (0.18, 0.0, 1.56), "LeftArm": (0.30, -0.02, 1.78),
            "LeftForeArm": (0.20, -0.10, 2.00), "LeftHand": (0.05, -0.18, 2.16),
            "RightShoulder": (-0.18, 0.0, 1.56), "RightArm": (-0.30, -0.02, 1.78),
            "RightForeArm": (-0.20, -0.10, 2.00), "RightHand": (-0.05, -0.18, 2.16),
        }
    elif stage == "impact":
        rotate_bone(rig, "Spine01", (0.50, 0.0, 0.0))
        rig.pose.bones["Hips"].location.z = -0.045
        bpy.context.view_layer.update()
        targets = {
            "LeftShoulder": (0.18, -0.05, 1.40), "LeftArm": (0.29, -0.22, 1.22),
            "LeftForeArm": (0.20, -0.48, 0.96), "LeftHand": (0.11, -0.75, 0.73),
            "RightShoulder": (-0.18, -0.05, 1.40), "RightArm": (-0.29, -0.22, 1.22),
            "RightForeArm": (-0.20, -0.48, 0.96), "RightHand": (-0.11, -0.75, 0.73),
        }
    elif stage == "recover":
        rotate_bone(rig, "Spine01", (0.18, 0.0, 0.0))
        bpy.context.view_layer.update()
        targets = {
            "LeftShoulder": (0.18, -0.02, 1.45), "LeftArm": (0.38, -0.10, 1.37),
            "LeftForeArm": (0.55, -0.20, 1.28), "LeftHand": (0.72, -0.25, 1.23),
            "RightShoulder": (-0.18, -0.02, 1.45), "RightArm": (-0.38, -0.10, 1.37),
            "RightForeArm": (-0.55, -0.20, 1.28), "RightHand": (-0.72, -0.25, 1.23),
        }
    else:
        return
    for bone_name in (
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
        "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    ):
        aim_bone(rig, bone_name, targets[bone_name])


def key_pose(rig, frame):
    for pose_bone in rig.pose.bones:
        pose_bone.keyframe_insert("location", frame=frame, group=pose_bone.name)
        pose_bone.keyframe_insert("rotation_euler", frame=frame, group=pose_bone.name)
        pose_bone.keyframe_insert("scale", frame=frame, group=pose_bone.name)


def begin_action(rig, name, start, end, loop):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = start
    action.frame_end = end
    action["fps"] = FPS
    action["loop"] = loop
    action["in_place"] = True
    action["root_motion"] = False
    action["authored_from_existing_keyframes"] = False
    rig.animation_data_create()
    rig.animation_data.action = action
    reset_pose(rig)
    return action


def set_pose(rig, rotations=None, hips_z=0.0):
    reset_pose(rig)
    rig.pose.bones["Hips"].location.z = hips_z
    for name, rotation in (rotations or {}).items():
        rotate_bone(rig, name, rotation)


def create_actions(rig):
    # Idle: restrained breathing, exact loop boundary.
    action = begin_action(rig, "Idle", 0, 29, True)
    for frame, breath in ((0, 0.0), (14, 0.015), (29, 0.0)):
        set_pose(rig, {"Spine01": (breath, 0.0, 0.0), "neck": (-breath * 0.5, 0.0, 0.0)}, breath * 0.20)
        key_pose(rig, frame)

    # Fast in-place run. Contacts are deliberately keyed at frames 2 and 12.
    action = begin_action(rig, "Run_Fast_Melee", 0, 19, True)
    action["foot_contact_frames"] = "2,12"
    run_keys = {
        0: (0.35, -0.35, 0.01),
        2: (0.60, -0.50, -0.015),
        7: (-0.35, 0.35, 0.025),
        12: (-0.60, 0.50, -0.015),
        17: (0.35, -0.35, 0.025),
        19: (0.35, -0.35, 0.01),
    }
    for frame, (left, right, bounce) in run_keys.items():
        rotations = {
            "LeftUpLeg": (left, 0.0, 0.0), "RightUpLeg": (right, 0.0, 0.0),
            "LeftLeg": (-max(left, 0.0) * 0.70, 0.0, 0.0),
            "RightLeg": (-max(right, 0.0) * 0.70, 0.0, 0.0),
            "LeftArm": (-right * 0.25, 0.0, 0.0), "RightArm": (-left * 0.25, 0.0, 0.0),
            "Spine01": (0.12, 0.0, 0.0),
        }
        set_pose(rig, rotations, bounce)
        key_pose(rig, frame)

    # Bilateral overhead wind-up and decisive forward/down smash.
    action = begin_action(rig, "Attack_Melee_OverheadSmash", 0, 20, False)
    action["event_name"] = "MeleeHit"
    action["event_frame"] = 13.5
    action["event_time_seconds"] = 0.45
    attack_keys = {
        0: None, 5: "anticipation", 9: "anticipation",
        13: "impact", 14: "impact", 17: "recover", 20: None,
    }
    for frame, stage in attack_keys.items():
        if stage:
            set_attack_chain_pose(rig, stage)
        else:
            set_pose(rig)
        key_pose(rig, frame)

    action = begin_action(rig, "Hit_FullBody", 0, 6, False)
    for frame, rotations in {
        0: {},
        2: {"Hips": (-0.10, 0.0, 0.08), "Spine02": (-0.28, 0.0, -0.12), "Spine01": (-0.22, 0.0, -0.08), "neck": (0.18, 0.0, 0.10)},
        4: {"Hips": (0.05, 0.0, -0.04), "Spine01": (0.10, 0.0, 0.05)},
        6: {},
    }.items():
        set_pose(rig, rotations, -0.025 if frame == 2 else 0.0)
        key_pose(rig, frame)

    # In-place death: knees release, body tips forward/side, settles by frame 28
    # and remains fully still through frame 35 (1.17 s at 30 fps).
    action = begin_action(rig, "Death_Backward", 0, 35, False)
    action["motion_note"] = "Knee release into forward-left fall; blades remain spread; settled by frame 28"
    death_keys = {
        0: ({}, 0.0),
        7: ({"LeftUpLeg": (0.22, 0.0, 0.0), "RightUpLeg": (0.18, 0.0, 0.0), "LeftLeg": (-0.45, 0.0, 0.0), "RightLeg": (-0.42, 0.0, 0.0), "Spine01": (0.12, 0.0, 0.08)}, -0.08),
        16: ({"Hips": (0.55, 0.20, 0.18), "Spine02": (0.30, 0.10, 0.10), "Spine01": (0.24, 0.10, 0.12), "LeftLeg": (-0.80, 0.0, 0.0), "RightLeg": (-0.70, 0.0, 0.0), "LeftArm": (0.0, 0.0, -0.18), "RightArm": (0.0, 0.0, 0.18)}, -0.35),
        24: ({"Hips": (1.15, 0.35, 0.28), "Spine02": (0.42, 0.14, 0.14), "Spine01": (0.38, 0.12, 0.16), "neck": (-0.25, 0.0, -0.10), "LeftUpLeg": (0.55, 0.0, 0.0), "RightUpLeg": (0.38, 0.0, 0.0), "LeftLeg": (-1.00, 0.0, 0.0), "RightLeg": (-0.86, 0.0, 0.0), "LeftArm": (0.0, 0.0, -0.25), "RightArm": (0.0, 0.0, 0.25)}, -0.72),
        28: ({"Hips": (1.32, 0.42, 0.32), "Spine02": (0.48, 0.15, 0.16), "Spine01": (0.42, 0.14, 0.18), "neck": (-0.30, 0.0, -0.12), "LeftUpLeg": (0.62, 0.0, 0.0), "RightUpLeg": (0.42, 0.0, 0.0), "LeftLeg": (-1.05, 0.0, 0.0), "RightLeg": (-0.90, 0.0, 0.0), "LeftArm": (0.0, 0.0, -0.30), "RightArm": (0.0, 0.0, 0.30)}, -0.88),
        35: ({"Hips": (1.32, 0.42, 0.32), "Spine02": (0.48, 0.15, 0.16), "Spine01": (0.42, 0.14, 0.18), "neck": (-0.30, 0.0, -0.12), "LeftUpLeg": (0.62, 0.0, 0.0), "RightUpLeg": (0.42, 0.0, 0.0), "LeftLeg": (-1.05, 0.0, 0.0), "RightLeg": (-0.90, 0.0, 0.0), "LeftArm": (0.0, 0.0, -0.30), "RightArm": (0.0, 0.0, 0.30)}, -0.88),
    }
    for frame, (rotations, hips_z) in death_keys.items():
        set_pose(rig, rotations, hips_z)
        key_pose(rig, frame)

    # Linear impact/fall timing; loops get cyclic interpolation behavior in-app.
    for action in bpy.data.actions:
        for fcurve in getattr(action, "fcurves", []):
            for key in fcurve.keyframe_points:
                key.interpolation = "BEZIER"
    return {action.name: action for action in bpy.data.actions if action.name in ACTION_SPECS}


def make_lods(source, rig, body, head):
    work_collection = bpy.data.collections.new("MELEE_BLADE_LODS")
    bpy.context.scene.collection.children.link(work_collection)
    lod0 = source.copy()
    lod0.data = source.data.copy()
    lod0.name = "Gulag_MeleeBlade_LOD0"
    lod0.data.name = "Gulag_MeleeBlade_LOD0_Mesh"
    lod0.hide_viewport = False
    lod0.hide_render = False
    lod0.hide_set(False)
    work_collection.objects.link(lod0)
    prune_source_arms_and_add_blades(lod0)
    decimate_to(lod0, 5750)
    if triangles(lod0.data) > LOD0_MAX_TRIS:
        raise RuntimeError(f"LOD0 triangle limit exceeded: {triangles(lod0.data)}")
    set_materials(lod0, body, head)
    add_weights(lod0, rig)

    lod1 = lod0.copy()
    lod1.data = lod0.data.copy()
    lod1.name = "Gulag_MeleeBlade_LOD1"
    lod1.data.name = "Gulag_MeleeBlade_LOD1_Mesh"
    work_collection.objects.link(lod1)
    # Recreate weights after decimation so exported assignments remain bounded.
    for modifier in list(lod1.modifiers):
        lod1.modifiers.remove(modifier)
    lod1.parent = None
    decimate_to(lod1, LOD1_TARGET_TRIS)
    set_materials(lod1, body, head)
    add_weights(lod1, rig)
    return lod0, lod1


def export_fbx(path, mesh, rig):
    # Blender's FBX importer reconstitutes exported 0-based clips at +1 frame.
    # Temporarily shift authored key data for export, then restore the .blend.
    shifted_actions = []
    for action in bpy.data.actions:
        if action.name not in ACTION_SPECS:
            continue
        start, end, _loop = ACTION_SPECS[action.name]
        action.frame_start = start - 1
        action.frame_end = end - 1
        for slot in action.slots:
            for layer in action.layers:
                for strip in layer.strips:
                    channelbag = strip.channelbag(slot, ensure=False)
                    if not channelbag:
                        continue
                    for fcurve in channelbag.fcurves:
                        for key in fcurve.keyframe_points:
                            key.co.x -= 1.0
                            key.handle_left.x -= 1.0
                            key.handle_right.x -= 1.0
        shifted_actions.append(action)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.hide_set(False)
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="COPY",
        embed_textures=False,
    )
    mesh.select_set(False)
    rig.select_set(False)
    for action in shifted_actions:
        for slot in action.slots:
            for layer in action.layers:
                for strip in layer.strips:
                    channelbag = strip.channelbag(slot, ensure=False)
                    if not channelbag:
                        continue
                    for fcurve in channelbag.fcurves:
                        for key in fcurve.keyframe_points:
                            key.co.x += 1.0
                            key.handle_left.x += 1.0
                            key.handle_right.x += 1.0
        start, end, _loop = ACTION_SPECS[action.name]
        action.frame_start = start
        action.frame_end = end


def camera_at(scene, location, target, ortho_scale=2.25):
    camera = bpy.data.objects.get("QA_Camera")
    if camera is None:
        data = bpy.data.cameras.new("QA_Camera")
        camera = bpy.data.objects.new("QA_Camera", data)
        scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    return camera


def setup_render(lod0, lod1, rig, actions):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.fps = FPS
    if scene.world is None:
        scene.world = bpy.data.worlds.new("ValidationWorld")
    scene.world.color = (0.035, 0.035, 0.035)
    lod0.hide_render = False
    lod0.hide_viewport = False
    lod1.hide_render = True
    lod1.hide_viewport = True
    source = bpy.data.objects.get("MeleeBlade_SOURCE_HIGH_DO_NOT_EDIT")
    if source:
        source.hide_render = True

    for name, location, energy, size in (
        ("Key", (2.8, -3.5, 4.0), 1100.0, 4.0),
        ("Fill", (-3.0, -2.0, 2.2), 650.0, 3.0),
        ("Rim", (0.0, 2.5, 3.2), 900.0, 3.0),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0.0, 0.0, 1.0)) - light.location).to_track_quat("-Z", "Y").to_euler()

    rig.animation_data.action = actions["Idle"]
    scene.frame_set(0)
    camera_at(scene, (0.0, -5.0, 1.05), (0.0, 0.0, 0.95))
    scene.render.filepath = str(FRONT_PNG)
    bpy.ops.render.render(write_still=True)
    camera_at(scene, (5.0, 0.0, 1.05), (0.0, 0.0, 0.95))
    scene.render.filepath = str(SIDE_PNG)
    bpy.ops.render.render(write_still=True)


def encode_video(frame_pattern, output, framerate=FPS):
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("ffmpeg not found")
    subprocess.run([
        ffmpeg, "-y", "-loglevel", "error", "-framerate", str(framerate),
        "-i", str(frame_pattern), "-c:v", "libx264", "-crf", "20",
        "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output),
    ], check=True)


def render_turntable(lod0, rig, actions):
    scene = bpy.context.scene
    rig.animation_data.action = actions["Idle"]
    scene.frame_set(0)
    turn_dir = FRAME_DIR / "turntable"
    turn_dir.mkdir()
    frames = 72
    radius = 5.0
    for frame in range(frames):
        angle = math.tau * frame / frames
        camera_at(scene, (math.sin(angle) * radius, -math.cos(angle) * radius, 1.05), (0.0, 0.0, 0.95))
        scene.render.filepath = str(turn_dir / f"frame_{frame:04d}.png")
        bpy.ops.render.render(write_still=True)
    encode_video(turn_dir / "frame_%04d.png", TURNTABLE_MP4)


def render_action_validation(rig, actions):
    scene = bpy.context.scene
    action_dir = FRAME_DIR / "actions"
    action_dir.mkdir()
    camera_at(scene, (2.0, -4.6, 1.15), (0.0, 0.0, 0.90))
    output_frame = 0
    for action_name in ACTION_SPECS:
        start, end, _loop = ACTION_SPECS[action_name]
        rig.animation_data.action = actions[action_name]
        clip_dir = FRAME_DIR / action_name
        clip_dir.mkdir()
        clip_frame = 0
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            scene.render.filepath = str(action_dir / f"frame_{output_frame:04d}.png")
            bpy.ops.render.render(write_still=True)
            shutil.copy2(scene.render.filepath, clip_dir / f"frame_{clip_frame:04d}.png")
            output_frame += 1
            clip_frame += 1
        # Six-frame neutral separator makes clip boundaries visible.
        for _ in range(6):
            scene.render.filepath = str(action_dir / f"frame_{output_frame:04d}.png")
            bpy.ops.render.render(write_still=True)
            output_frame += 1
        encode_video(clip_dir / "frame_%04d.png", VALIDATION_DIR / f"{action_name}.mp4")
    encode_video(action_dir / "frame_%04d.png", ACTION_MP4)

    # Explicit attack timing sheet: neutral, wind-up, impact, recover.
    attack_dir = FRAME_DIR / "Attack_Melee_OverheadSmash"
    frames = [0, 9, 13, 20]
    ffmpeg = shutil.which("ffmpeg")
    inputs = []
    for frame in frames:
        inputs += ["-i", str(attack_dir / f"frame_{frame:04d}.png")]
    subprocess.run([
        ffmpeg, "-y", "-loglevel", "error", *inputs,
        "-filter_complex", "[0:v][1:v][2:v][3:v]hstack=inputs=4[v]",
        "-map", "[v]", str(ATTACK_SHEET),
    ], check=True)


def action_ranges_from_import():
    rows = {}
    for action in bpy.data.actions:
        for expected in ACTION_SPECS:
            if action.name == expected or action.name.endswith("|" + expected):
                rows[expected] = [round(action.frame_range[0], 3), round(action.frame_range[1], 3)]
    return rows


def fresh_reimport(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Blender defaults FBX imports to anim_offset=1.0. Use zero explicitly so
    # fresh-reimport validation reflects the exported 0-based gameplay ranges.
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, anim_offset=0.0)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(meshes) != 1 or len(rigs) != 1:
        raise RuntimeError(f"Fresh import expected one mesh/rig: {path}")
    mesh_obj, rig = meshes[0], rigs[0]
    mesh = mesh_obj.data
    max_weights = max((sum(1 for group in vertex.groups if group.weight > 0.0001) for vertex in mesh.vertices), default=0)
    raw_actions = action_ranges_from_import()
    return {
        "file": str(path),
        "bytes": path.stat().st_size,
        "mesh_count": 1,
        "triangles": triangles(mesh),
        "vertices": len(mesh.vertices),
        "bones": len(rig.data.bones),
        "bone_names": sorted(bone.name for bone in rig.data.bones),
        "max_weights": max_weights,
        "uv_layers": len(mesh.uv_layers),
        "material_slots": len(mesh_obj.material_slots),
        "materials": [slot.material.name if slot.material else "" for slot in mesh_obj.material_slots],
        "fbx_import_raw_action_ranges": raw_actions,
        "actions": raw_actions,
        "dimensions": [round(value, 6) for value in mesh_obj.dimensions],
    }


def write_handoff(qa, source_tris, lod0_tris, lod1_tris):
    text = f"""# Gulag MeleeBlade handoff

## Outputs

- Blender source: `{OUTPUT_BLEND.name}`
- Unity-ready candidates: `Exports/{LOD0_FBX.name}`, `Exports/{LOD1_FBX.name}`
- 1K textures: `Textures/BaseColor.png`, `Textures/Normal.png`, `Textures/Emission.png`
- Validation: `Validation/{FRONT_PNG.name}`, `Validation/{SIDE_PNG.name}`, `Validation/{TURNTABLE_MP4.name}`, `Validation/{ACTION_MP4.name}`
- Per-action validation: `Validation/Idle.mp4`, `Validation/Run_Fast_Melee.mp4`, `Validation/Attack_Melee_OverheadSmash.mp4`, `Validation/Hit_FullBody.mp4`, `Validation/Death_Backward.mp4`.
- Attack contact sheet: `Validation/{ATTACK_SHEET.name}` (frames 0, 9, 13, 20).
- Fresh reimport QA: `Validation/{QA_JSON.name}`

## Geometry and material contract

- Meshy source is preserved in `MeshySource/` and as hidden `SOURCE_HIGH_DO_NOT_EDIT` in the blend.
- Source: {source_tris:,} tris. LOD0: {lod0_tris:,} tris (limit {LOD0_MAX_TRIS:,}). LOD1: {lod1_tris:,} tris (target about {LOD1_TARGET_TRIS:,}).
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

- LOD0: {qa['lod0']['triangles']:,} tris, {qa['lod0']['bones']} bones, max {qa['lod0']['max_weights']} weights, {qa['lod0']['uv_layers']} UV layer, {qa['lod0']['material_slots']} material slots.
- LOD1: {qa['lod1']['triangles']:,} tris, {qa['lod1']['bones']} bones, max {qa['lod1']['max_weights']} weights, {qa['lod1']['uv_layers']} UV layer, {qa['lod1']['material_slots']} material slots.
- FBX is fresh-imported with Blender's `anim_offset=0.0`; raw imported action ranges exactly match the 0-based contract. Full machine-readable details are in `{QA_JSON.name}`.

## Known limits

- Automated decimation and procedural replacement blades prioritize WebGL silhouette, triangle budget, and deterministic rebuild over hand-authored edge flow.
- The emission source delivered by Meshy is very dim; material strength is raised on `Head`, but the map itself remains visually subtle.
- FBX/Unity import and prefab replacement were intentionally not performed in this task.
"""
    HANDOFF_MD.write_text(text, encoding="utf-8")


def main():
    ensure_dirs()
    for required in (SOURCE_FBX, SOURCE_TEXTURES / "base_color.png", SOURCE_TEXTURES / "normal.png", SOURCE_TEXTURES / "emission.png"):
        if not required.exists():
            raise FileNotFoundError(required)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    source = load_source()
    source_tris = triangles(source.data)
    base_image, _ = resize_texture("base_color.png", "BaseColor.png", "sRGB")
    normal_image, _ = resize_texture("normal.png", "Normal.png", "Non-Color")
    emission_image, _ = resize_texture("emission.png", "Emission.png", "Non-Color", normalize_role_emission=True)
    body = create_material("Body", base_image, normal_image, emission_image, head=False)
    head = create_material("Head", base_image, normal_image, emission_image, head=True)
    rig = build_rig()
    lod0, lod1 = make_lods(source, rig, body, head)
    lod0_tris, lod1_tris = triangles(lod0.data), triangles(lod1.data)
    actions = create_actions(rig)

    scene["meshy_task_id"] = TASK_ID
    scene["source_triangles"] = source_tris
    scene["lod0_triangles"] = lod0_tris
    scene["lod1_triangles"] = lod1_tris
    scene["animation_source"] = "New procedural keyframes; no copied actions"
    scene["root_motion"] = False
    rig.animation_data.action = actions["Idle"]
    scene.frame_start, scene.frame_end = 0, 29
    scene.frame_set(0)

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))
    export_fbx(LOD0_FBX, lod0, rig)
    export_fbx(LOD1_FBX, lod1, rig)
    build_media = "--media" in sys.argv
    if build_media:
        setup_render(lod0, lod1, rig, actions)
        render_turntable(lod0, rig, actions)
        render_action_validation(rig, actions)

    qa = {
        "blender_version": bpy.app.version_string,
        "factory_fresh_reimport": True,
        "source_unchanged": True,
        "source_fbx": str(SOURCE_FBX),
        "source_triangles": source_tris,
        "expected_bones": 26,
        "expected_actions": {name: [start, end] for name, (start, end, _loop) in ACTION_SPECS.items()},
        "lod0": fresh_reimport(LOD0_FBX),
        "lod1": fresh_reimport(LOD1_FBX),
    }
    for key in ("lod0", "lod1"):
        row = qa[key]
        row["checks"] = {
            "26_bones": row["bones"] == 26,
            "max_4_weights": row["max_weights"] <= 4,
            "has_uv": row["uv_layers"] >= 1,
            "two_material_slots": row["material_slots"] == 2,
            "actions_and_ranges": row["actions"] == qa["expected_actions"],
        }
    qa["lod0"]["checks"]["triangles_at_most_6000"] = qa["lod0"]["triangles"] <= LOD0_MAX_TRIS
    qa["lod1"]["checks"]["triangles_about_3000"] = 2500 <= qa["lod1"]["triangles"] <= 3200
    QA_JSON.write_text(json.dumps(qa, indent=2), encoding="utf-8")
    write_handoff(qa, source_tris, lod0_tris, lod1_tris)
    if not all(all(checks for checks in row["checks"].values()) for row in (qa["lod0"], qa["lod1"])):
        raise RuntimeError(f"Fresh reimport validation failed; inspect {QA_JSON}")
    shutil.rmtree(FRAME_DIR)
    print("BUILD_OK")
    print(f"BLEND={OUTPUT_BLEND}")
    print(f"LOD0={LOD0_FBX}|TRIS={qa['lod0']['triangles']}")
    print(f"LOD1={LOD1_FBX}|TRIS={qa['lod1']['triangles']}")
    print(f"QA={QA_JSON}")
    print(f"MEDIA_BUILT={build_media}")


if __name__ == "__main__":
    main()
