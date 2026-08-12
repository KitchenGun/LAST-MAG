"""Rebuild the Suicide Capsid as a deterministic low-poly Blender asset.

Run with Blender 5.2:
  blender --background --factory-startup --python build_suicide_capsid.py

The MeshySource directory is read-only input. All generated files go to Final/.
"""

from pathlib import Path
import json
import math
import shutil
import subprocess

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "MeshySource"
SOURCE_TEXTURES = SOURCE / "SuicideCapsid_Meshy6_textures"
OUTPUT = ROOT / "Final"
TEXTURES = OUTPUT / "Textures"
VALIDATION = OUTPUT / "Validation"
BLEND_PATH = OUTPUT / "SuicideCapsid_Final.blend"
LOD0_FBX = OUTPUT / "SuicideCapsid_LOD0.fbx"
LOD1_FBX = OUTPUT / "SuicideCapsid_LOD1.fbx"
QA_PATH = VALIDATION / "SuicideCapsid_QA.json"
MP4_PATH = VALIDATION / "SuicideCapsid_ActionValidation.mp4"
FPS = 30
TARGET_HEIGHT = 1.80
UPPER_START_Z = 0.78
SOURCE_BODY_CUT_Z = 0.37

LEG_SPECS = {
    "L_Front": ((-0.33, -0.18, 0.36), (-0.49, -0.34, 0.22), (-0.68, -0.49, 0.09), (-0.86, -0.58, 0.02)),
    "R_Front": ((0.33, -0.18, 0.36), (0.49, -0.34, 0.22), (0.68, -0.49, 0.09), (0.86, -0.58, 0.02)),
    "L_Mid": ((-0.29, 0.00, 0.35), (-0.42, 0.00, 0.21), (-0.54, 0.00, 0.085), (-0.64, 0.00, 0.02)),
    "R_Mid": ((0.29, 0.00, 0.35), (0.42, 0.00, 0.21), (0.54, 0.00, 0.085), (0.64, 0.00, 0.02)),
    "L_Rear": ((-0.23, 0.20, 0.34), (-0.31, 0.35, 0.20), (-0.39, 0.48, 0.08), (-0.46, 0.57, 0.02)),
    "R_Rear": ((0.23, 0.20, 0.34), (0.31, 0.35, 0.20), (0.39, 0.48, 0.08), (0.46, 0.57, 0.02)),
}


def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    OUTPUT.mkdir(parents=True, exist_ok=True)
    TEXTURES.mkdir(parents=True, exist_ok=True)
    VALIDATION.mkdir(parents=True, exist_ok=True)


def make_materials():
    lower = bpy.data.materials.new("M_SuicideCapsid_Lower")
    lower.use_nodes = True
    lower.diffuse_color = (0.055, 0.018, 0.022, 1.0)
    lower_bsdf = lower.node_tree.nodes.get("Principled BSDF")
    lower_bsdf.inputs["Base Color"].default_value = (0.055, 0.018, 0.022, 1.0)
    lower_bsdf.inputs["Roughness"].default_value = 0.48
    lower_bsdf.inputs["Metallic"].default_value = 0.05

    upper = bpy.data.materials.new("M_SuicideCapsid_Upper")
    upper.use_nodes = True
    upper.diffuse_color = (0.34, 0.035, 0.05, 1.0)
    upper_bsdf = upper.node_tree.nodes.get("Principled BSDF")
    upper_bsdf.inputs["Base Color"].default_value = (0.34, 0.035, 0.05, 1.0)
    upper_bsdf.inputs["Roughness"].default_value = 0.38
    upper_bsdf.inputs["Metallic"].default_value = 0.02
    emission_input = upper_bsdf.inputs.get("Emission Color") or upper_bsdf.inputs.get("Emission")
    if emission_input:
        emission_input.default_value = (0.82, 0.012, 0.025, 1.0)
    if upper_bsdf.inputs.get("Emission Strength"):
        upper_bsdf.inputs["Emission Strength"].default_value = 0.28
    return lower, upper


def create_ellipsoid(name, location, scale, segments, rings, material, material_index):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = material_index
        polygon.use_smooth = True
    return obj


def create_lobe(name, location, scale, subdivisions, material):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 1
        polygon.use_smooth = True
    return obj


def create_capsid(lower, upper, lod):
    segments, rings = ((32, 20) if lod == 0 else (30, 18))
    body = create_ellipsoid(
        f"SuicideCapsid_LOD{lod}_Body",
        (0.0, 0.03, 0.57),
        (0.31, 0.35, 0.48),
        segments,
        rings,
        lower,
        0,
    )
    upper_obj = create_ellipsoid(
        f"SuicideCapsid_LOD{lod}_Upper",
        (0.0, 0.0, 1.25),
        (0.57, 0.54, 0.55),
        segments,
        rings,
        upper,
        1,
    )
    lobe_specs = (
        ((0.0, -0.38, 1.36), (0.27, 0.17, 0.29)),
        ((-0.34, -0.20, 1.31), (0.24, 0.18, 0.25)),
        ((0.34, -0.20, 1.31), (0.24, 0.18, 0.25)),
        ((-0.32, 0.20, 1.34), (0.23, 0.19, 0.27)),
        ((0.32, 0.20, 1.34), (0.23, 0.19, 0.27)),
        ((0.0, 0.34, 1.40), (0.26, 0.18, 0.26)),
    )
    subdivisions = 3 if lod == 0 else 2
    lobes = [
        create_lobe(f"SuicideCapsid_LOD{lod}_Lobe_{index:02d}", location, scale, subdivisions, upper)
        for index, (location, scale) in enumerate(lobe_specs, start=1)
    ]
    return (body, upper_obj, *lobes)


def create_limb_segment(name, start, end, radius, material, material_index):
    start = Vector(start)
    end = Vector(end)
    midpoint = (start + end) * 0.5
    direction = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=radius, depth=direction.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = material_index
        polygon.use_smooth = True
    return obj


def create_joint(name, location, radius, material):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True
    return obj


def create_leg_meshes(lower, lod):
    meshes = []
    radius = 0.035
    for side_position, points in LEG_SPECS.items():
        for index in range(3):
            meshes.append(
                create_limb_segment(
                    f"SuicideCapsid_LOD{lod}_{side_position}_Segment{index + 1}",
                    points[index],
                    points[index + 1],
                    radius * (1.0 - index * 0.14),
                    lower,
                    0,
                )
            )
        for index, point in enumerate(points):
            meshes.append(create_joint(f"SuicideCapsid_LOD{lod}_{side_position}_Joint{index + 1}", point, radius * (1.16 - index * 0.08), lower))
    return meshes


def create_base_plate(lower, lod):
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=2 if lod == 0 else 1,
        radius=1.0,
        location=(0.0, 0.0, 0.365),
    )
    plate = bpy.context.object
    plate.name = f"SuicideCapsid_LOD{lod}_OrganicBasePlate"
    plate.scale = (0.405, 0.365, 0.075)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Break the perfect ellipse while keeping all six hip overlaps deterministic.
    for vertex in plate.data.vertices:
        angle = math.atan2(vertex.co.y, vertex.co.x)
        radial_scale = 1.0 + 0.055 * math.sin(angle * 3.0) + 0.025 * math.cos(angle * 5.0)
        vertex.co.x *= radial_scale
        vertex.co.y *= radial_scale
    plate.data.materials.append(lower)
    for polygon in plate.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = True
    group = plate.vertex_groups.new(name="__BODY_PLATE")
    group.add([vertex.index for vertex in plate.data.vertices], 1.0, "REPLACE")
    return plate


def import_source_body():
    bpy.ops.import_scene.fbx(filepath=str(SOURCE / "SuicideCapsid_Meshy6.fbx"))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one Meshy source mesh, found {len(meshes)}")
    source = meshes[0]
    source.name = "SOURCE_MeshyHigh_BodyCapsid"
    bpy.ops.object.select_all(action="DESELECT")
    source.select_set(True)
    bpy.context.view_layer.objects.active = source
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    scale = TARGET_HEIGHT / max(source.dimensions.z, 1e-6)
    source.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    corners = [source.matrix_world @ Vector(corner) for corner in source.bound_box]
    minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    source.location -= Vector(((minimum.x + maximum.x) * 0.5, (minimum.y + maximum.y) * 0.5, minimum.z))
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Meshy fused all eight legs to the body into one connected component. Remove
    # the lower leg band while preserving the original capsid, torso, UVs, and normals.
    bm = bmesh.new()
    bm.from_mesh(source.data)
    remove = [vertex for vertex in bm.verts if vertex.co.z < SOURCE_BODY_CUT_Z]
    bmesh.ops.delete(bm, geom=remove, context="VERTS")
    bm.to_mesh(source.data)
    bm.free()
    source.data.update()
    source.hide_set(True)
    source.hide_render = True
    source["source_component_count"] = 1
    source["source_leg_repair"] = "All fused lower legs removed below Z=0.37; six clean chains rebuilt."
    return source


def decimated_copy(source, name, target_triangles):
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    obj.data.name = name + "_Mesh"
    bpy.context.scene.collection.objects.link(obj)
    obj.hide_set(False)
    obj.hide_render = False
    obj.data.calc_loop_triangles()
    current = len(obj.data.loop_triangles)
    modifier = obj.modifiers.new("Hybrid_Source_Decimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = min(1.0, target_triangles / max(current, 1))
    modifier.use_collapse_triangulate = True
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def join_meshes(objects, name, lower, upper):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    source_vertex_counts = {obj.name: len(obj.data.vertices) for obj in objects}
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = name
    joined.data.name = name + "_Mesh"
    joined.data.materials.clear()
    joined.data.materials.append(lower)
    joined.data.materials.append(upper)
    # Hybrid source owns the complete high-quality capsid/body and is Upper;
    # all rebuilt leg segments/joints are Lower. This avoids a visible Z seam.
    source_vertices = source_vertex_counts.get(next((key for key in source_vertex_counts if "Hybrid_Source" in key), ""), 0)
    for polygon in joined.data.polygons:
        polygon.material_index = 1 if polygon.vertices and min(polygon.vertices) < source_vertices else 0
    if not joined.data.uv_layers:
        joined.data.uv_layers.new(name="UVMap")
    return joined


def create_rig(name):
    data = bpy.data.armatures.new(name + "_Data")
    rig = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(bone_name, head, tail, parent=None, connected=False):
        item = data.edit_bones.new(bone_name)
        item.head, item.tail = head, tail
        item.parent = parent
        item.use_connect = connected
        return item

    root = bone("root", (0, 0, 0), (0, 0, 0.18))
    body = bone("body", (0, 0, 0.18), (0, 0, 0.82), root)
    upper = bone("upper", (0, 0, 0.82), (0, 0, 1.62), body, True)
    for side_position, points in LEG_SPECS.items():
        parent = body
        for index in range(3):
            parent = bone(
                f"leg_{side_position}_{index + 1:02d}",
                points[index],
                points[index + 1],
                parent,
                index > 0,
            )
    bpy.ops.object.mode_set(mode="OBJECT")
    for pose_bone in rig.pose.bones:
        if pose_bone.name.startswith("leg_"):
            pose_bone.rotation_mode = "XYZ"
    rig["leg_layout"] = "L/R Front/Mid/Rear; exactly six chains; three bones per chain"
    return rig


def assign_skin(mesh, rig):
    plate_group = mesh.vertex_groups.get("__BODY_PLATE")
    plate_vertices = set()
    if plate_group:
        plate_vertices = {
            vertex.index
            for vertex in mesh.data.vertices
            if any(group.group == plate_group.index for group in vertex.groups)
        }
    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)
    groups = {bone.name: mesh.vertex_groups.new(name=bone.name) for bone in rig.data.bones}
    leg_bones = {name: [f"leg_{name}_{i:02d}" for i in range(1, 4)] for name in LEG_SPECS}
    for vertex in mesh.data.vertices:
        position = vertex.co
        if vertex.index in plate_vertices:
            groups["body"].add([vertex.index], 1.0, "REPLACE")
            continue
        if position.z >= UPPER_START_Z:
            groups["upper"].add([vertex.index], 1.0, "REPLACE")
            continue
        nearest_leg = min(
            LEG_SPECS,
            key=lambda name: min((position - Vector(point)).length_squared for point in LEG_SPECS[name]),
        )
        points = LEG_SPECS[nearest_leg]
        segment_distances = []
        for i in range(3):
            a, b = Vector(points[i]), Vector(points[i + 1])
            t = max(0.0, min(1.0, (position - a).dot(b - a) / max((b - a).length_squared, 1e-8)))
            segment_distances.append((position - (a + (b - a) * t)).length)
        segment = min(range(3), key=segment_distances.__getitem__)
        if position.z < 0.43 and segment_distances[segment] < 0.095:
            groups[leg_bones[nearest_leg][segment]].add([vertex.index], 1.0, "REPLACE")
        else:
            groups["body"].add([vertex.index], 1.0, "REPLACE")
    modifier = mesh.modifiers.new("SuicideCapsid_Rig", "ARMATURE")
    modifier.object = rig
    mesh.parent = rig


def action_slot_and_bag(action, rig):
    slot = action.slots.new("OBJECT", rig.name)
    layer = action.layers.new("Keys")
    strip = layer.strips.new(type="KEYFRAME")
    bag = strip.channelbags.new(slot)
    return slot, bag


def curve(bag, data_path, index, values):
    fcurve = bag.fcurves.new(data_path=data_path, index=index)
    fcurve.keyframe_points.add(len(values))
    for point, (frame, value) in zip(fcurve.keyframe_points, values):
        point.co = (frame, value)
        point.interpolation = "BEZIER"
    fcurve.update()


def bone_curve(bag, bone_name, prop, index, values):
    curve(bag, f'pose.bones["{bone_name}"].{prop}', index, values)


def create_actions(rig):
    actions = {}

    idle = bpy.data.actions.new("Idle")
    idle.use_fake_user = True
    _, bag = action_slot_and_bag(idle, rig)
    for axis, values in enumerate(((1.0, 1.025, 1.0), (1.0, 1.025, 1.0), (1.0, 1.055, 1.0))):
        bone_curve(bag, "upper", "scale", axis, [(0, values[0]), (30, values[1]), (59, values[2])])
    idle["clip_range"] = "0-59"
    idle["loop"] = True
    actions[idle.name] = idle

    move = bpy.data.actions.new("Move_LegsOnly")
    move.use_fake_user = True
    _, bag = action_slot_and_bag(move, rig)
    tripod_a = {"L_Front", "R_Mid", "L_Rear"}
    frames = (0, 6, 12, 18, 24)
    wave = (0.0, 1.0, 0.0, -1.0, 0.0)
    for leg in LEG_SPECS:
        sign = 1.0 if leg in tripod_a else -1.0
        for segment, amplitude in ((1, 0.34), (2, -0.50), (3, 0.30)):
            values = [(frame, sign * phase * amplitude) for frame, phase in zip(frames, wave)]
            bone_curve(bag, f"leg_{leg}_{segment:02d}", "rotation_euler", 0, values)
    move["clip_range"] = "0-24"
    move["loop"] = True
    move["in_place"] = True
    move["foot_contacts"] = "Tripod A frames 0/12/24; Tripod B frames 6/18"
    actions[move.name] = move

    warning = bpy.data.actions.new("Warning_Explode")
    warning.use_fake_user = True
    _, bag = action_slot_and_bag(warning, rig)
    bone_curve(bag, "body", "location", 2, [(0, 0.0), (12, -0.08), (23, -0.12)])
    for axis in range(3):
        bone_curve(bag, "upper", "scale", axis, [(0, 1.0), (12, 1.10), (23, 1.24)])
    for leg in LEG_SPECS:
        bone_curve(bag, f"leg_{leg}_01", "rotation_euler", 0, [(0, 0), (12, 0.30), (23, 0.46)])
        bone_curve(bag, f"leg_{leg}_02", "rotation_euler", 0, [(0, 0), (12, -0.42), (23, -0.62)])
    warning["clip_range"] = "0-23"
    warning["normal_duration_seconds"] = 0.8
    warning["runtime_speed_multiplier"] = 4.0
    warning["runtime_duration_seconds"] = 0.2
    actions[warning.name] = warning

    rig.animation_data_create()
    rig.animation_data.action = idle
    rig.animation_data.action_slot = idle.slots[0]
    return actions


def copy_source_textures():
    mapping = {
        "base_color.png": "SuicideCapsid_BaseColor_1K.png",
        "normal.png": "SuicideCapsid_Normal_1K.png",
        "emission.png": "SuicideCapsid_Emission_1K.png",
    }
    for source_name, output_name in mapping.items():
        source = SOURCE_TEXTURES / source_name
        if not source.exists():
            raise FileNotFoundError(source)
        shutil.copy2(source, TEXTURES / output_name)
    for output_name in mapping.values():
        image = bpy.data.images.load(str(TEXTURES / output_name), check_existing=False)
        image.scale(1024, 1024)
        image.filepath_raw = str(TEXTURES / output_name)
        image.file_format = "PNG"
        image.save()


def add_texture_nodes(lower, upper):
    for material in (lower, upper):
        nodes = material.node_tree.nodes
        links = material.node_tree.links
        bsdf = nodes.get("Principled BSDF")
        base = nodes.new("ShaderNodeTexImage")
        base.name = "BaseColor_1K"
        base.image = bpy.data.images.load(str(TEXTURES / "SuicideCapsid_BaseColor_1K.png"), check_existing=True)
        links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
        normal = nodes.new("ShaderNodeTexImage")
        normal.name = "Normal_1K"
        normal.image = bpy.data.images.load(str(TEXTURES / "SuicideCapsid_Normal_1K.png"), check_existing=True)
        normal.image.colorspace_settings.name = "Non-Color"
        normal_map = nodes.new("ShaderNodeNormalMap")
        normal_map.inputs["Strength"].default_value = 0.75
        links.new(normal.outputs["Color"], normal_map.inputs["Color"])
        links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
        emission = nodes.new("ShaderNodeTexImage")
        emission.name = "Emission_1K"
        emission.image = bpy.data.images.load(str(TEXTURES / "SuicideCapsid_Emission_1K.png"), check_existing=True)
        if material == upper:
            emission_ramp = nodes.new("ShaderNodeValToRGB")
            emission_ramp.color_ramp.elements[0].color = (0.0, 0.0, 0.0, 1.0)
            emission_ramp.color_ramp.elements[1].color = (0.823, 0.012, 0.025, 1.0)
            links.new(emission.outputs["Color"], emission_ramp.inputs["Fac"])
            target = bsdf.inputs.get("Emission Color") or bsdf.inputs.get("Emission")
            if target:
                links.new(emission_ramp.outputs["Color"], target)


def export_fbx(path, mesh, rig, actions):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.hide_set(False)
    rig.hide_set(False)
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    for action in actions.values():
        rig.animation_data.action = action
        rig.animation_data.action_slot = action.slots[0]
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL",
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=False,
        bake_anim_simplify_factor=0.0,
        path_mode="COPY",
        embed_textures=False,
    )


def setup_preview(mesh, rig):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("Preview_World")
    scene.world.color = (0.035, 0.035, 0.035)
    bpy.ops.object.camera_add(location=(3.0, -4.8, 2.5))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.35
    scene.camera = camera

    def look_at(obj, target):
        obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

    look_at(camera, (0, 0, 0.9))
    for name, location, energy, size in (
        ("Key", (2.5, -3, 4), 1000, 3.0),
        ("Fill", (-2.5, -1, 2.5), 600, 2.5),
        ("Rim", (0, 3, 3), 700, 2.0),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = energy, "DISK", size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        look_at(light, (0, 0, 0.9))
    return camera


def render_stills(camera, rig, actions):
    scene = bpy.context.scene
    rig.animation_data.action = actions["Idle"]
    rig.animation_data.action_slot = actions["Idle"].slots[0]
    scene.frame_set(0)
    views = {
        "Front": ((0, -4.5, 1.0), (0, 0, 0.9)),
        "Side": ((4.5, 0, 1.0), (0, 0, 0.9)),
        "Top": ((0, 0, 5.0), (0, 0, 0.7)),
        "Turntable": ((3.4, -3.4, 2.3), (0, 0, 0.9)),
    }
    for name, (location, target) in views.items():
        camera.location = location
        camera.data.ortho_scale = 2.35
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(VALIDATION / f"SuicideCapsid_{name}.png")
        bpy.ops.render.render(write_still=True)


def render_action_video(camera, rig, actions):
    scene = bpy.context.scene
    camera.location = (3.4, -4.3, 2.2)
    camera.rotation_euler = (Vector((0, 0, 0.9)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = 2.35
    segments = (("Idle", 0, 59), ("Move_LegsOnly", 0, 24), ("Warning_Explode", 0, 23))
    checkpoints = {"Idle": {0, 30, 59}, "Move_LegsOnly": {0, 12, 24}, "Warning_Explode": {0, 12, 23}}
    frames = []
    for action_name, start, end in segments:
        rig.animation_data.action = actions[action_name]
        rig.animation_data.action_slot = actions[action_name].slots[0]
        for source_frame in range(start, end + 1):
            scene.frame_set(source_frame)
            path = VALIDATION / f"_frame_{len(frames):04d}.png"
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            frames.append(path)
            if source_frame in checkpoints[action_name]:
                checkpoint = VALIDATION / f"Action_{action_name}_Frame{source_frame:02d}.png"
                shutil.copy2(path, checkpoint)
    ffmpeg = shutil.which("ffmpeg")
    if ffmpeg:
        subprocess.run(
            [ffmpeg, "-y", "-framerate", str(FPS), "-i", str(VALIDATION / "_frame_%04d.png"), "-c:v", "libx264", "-pix_fmt", "yuv420p", str(MP4_PATH)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        for frame in frames:
            frame.unlink(missing_ok=True)


def mesh_stats(mesh):
    mesh.data.calc_loop_triangles()
    influence_counts = []
    for vertex in mesh.data.vertices:
        influence_counts.append(sum(1 for group in vertex.groups if group.weight > 1e-6))
    return {
        "vertices": len(mesh.data.vertices),
        "triangles": len(mesh.data.loop_triangles),
        "uv_layers": len(mesh.data.uv_layers),
        "material_slots": [slot.material.name for slot in mesh.material_slots],
        "max_weights": max(influence_counts, default=0),
    }


def validate_fresh_import(fbx_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), anim_offset=0.0)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(meshes) != 1 or len(rigs) != 1:
        raise RuntimeError(f"Fresh import expected 1 mesh/1 rig: {len(meshes)}/{len(rigs)}")
    mesh, rig = meshes[0], rigs[0]
    stats = mesh_stats(mesh)
    bone_names = [bone.name for bone in rig.data.bones]
    leg_roots = [name for name in bone_names if name.startswith("leg_") and name.endswith("_01")]
    raw_actions = {action.name: [round(v, 3) for v in action.frame_range] for action in bpy.data.actions}
    actions = {name.rsplit("|", 1)[-1]: frame_range for name, frame_range in raw_actions.items()}
    return {
        **stats,
        "bone_count": len(bone_names),
        "bone_names": bone_names,
        "leg_chain_count": len(leg_roots),
        "actions": actions,
        "raw_fbx_actions": raw_actions,
        "bounds_min_z": min((mesh.matrix_world @ Vector(corner)).z for corner in mesh.bound_box),
    }


def main():
    clean()
    copy_source_textures()
    lower, upper = make_materials()
    add_texture_nodes(lower, upper)
    source_high = import_source_body()
    source_lod0 = decimated_copy(source_high, "Hybrid_Source_LOD0", 4000)
    lod0 = join_meshes([source_lod0, create_base_plate(lower, 0), *create_leg_meshes(lower, 0)], "SuicideCapsid_LOD0", lower, upper)
    source_lod1 = decimated_copy(source_high, "Hybrid_Source_LOD1", 2050)
    lod1 = join_meshes([source_lod1, create_base_plate(lower, 1), *create_leg_meshes(lower, 1)], "SuicideCapsid_LOD1", lower, upper)
    # The last joint spheres extend slightly below their authored contact point.
    # Normalize both deliverables so the object origin is the actual ground plane.
    ground_offset = -min(vertex.co.z for vertex in lod0.data.vertices)
    for obj in (lod0, lod1):
        obj.location.z += ground_offset
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
        obj.select_set(False)
    rig = create_rig("SuicideCapsid_Rig")
    assign_skin(lod0, rig)
    assign_skin(lod1, rig)
    actions = create_actions(rig)
    camera = setup_preview(lod0, rig)
    lod1.hide_set(True)
    lod1.hide_render = True
    render_stills(camera, rig, actions)
    render_action_video(camera, rig, actions)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    export_fbx(LOD0_FBX, lod0, rig, actions)
    lod0.hide_set(True)
    lod0.hide_render = True
    lod1.hide_set(False)
    lod1.hide_render = False
    export_fbx(LOD1_FBX, lod1, rig, actions)

    source_record = {
        "source_fbx": str(SOURCE / "SuicideCapsid_Meshy6.fbx"),
        "source_task_id": "019ff514-d5bc-7a16-b2bf-b21dcfc842b4",
        "source_preserved": True,
        "source_connected_components": 1,
        "design": "Hybrid rebuild preserves and decimates the Meshy capsid/body with original UVs/normals, removes the fused eight-leg lower band, and adds an overlapping organic base plate plus six explicit low-poly chains.",
        "lod0_before_export": mesh_stats(lod0),
        "lod1_before_export": mesh_stats(lod1),
    }
    fresh_lod0 = validate_fresh_import(LOD0_FBX)
    fresh_lod1 = validate_fresh_import(LOD1_FBX)
    report = {**source_record, "fresh_lod0": fresh_lod0, "fresh_lod1": fresh_lod1}
    QA_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")

    assert fresh_lod0["leg_chain_count"] == 6
    assert fresh_lod0["triangles"] <= 5000
    assert 1800 <= fresh_lod1["triangles"] <= 3200
    assert fresh_lod0["max_weights"] <= 4 and fresh_lod1["max_weights"] <= 4
    assert fresh_lod0["uv_layers"] >= 1 and fresh_lod1["uv_layers"] >= 1
    assert len(fresh_lod0["material_slots"]) == 2
    assert fresh_lod0["actions"] == {
        "Idle": [0.0, 59.0],
        "Move_LegsOnly": [0.0, 24.0],
        "Warning_Explode": [0.0, 23.0],
    }
    assert abs(fresh_lod0["bounds_min_z"]) < 0.001
    print("SUICIDE_CAPSID_QA=" + json.dumps(report, separators=(",", ":")))


if __name__ == "__main__":
    main()
