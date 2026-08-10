import bpy
from mathutils import Matrix, Vector
from pathlib import Path


ROOT = Path(r"C:\Users\kang9\Documents\ChatGPT\Gulag-project\BlenderSource\Enemies\HumanoidBlob")
RIGGED_FBX = ROOT / "SM_HumanoidBlob_Rigged.fbx"
WALK_FBX = ROOT / "AN_HumanoidBlob_Walk_Source.fbx"
RUN_FBX = ROOT / "AN_HumanoidBlob_Run_Source.fbx"
OUTPUT_BLEND = ROOT / "HumanoidBlob_HighQuality_Setup.blend"


def remove_objects(objects):
    for obj in list(objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def import_fbx(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    return list(set(bpy.context.scene.objects) - before)


def import_animation(path, final_name):
    before_actions = set(bpy.data.actions)
    imported = import_fbx(path)
    new_actions = list(set(bpy.data.actions) - before_actions)
    if not new_actions:
        raise RuntimeError(f"No action imported from {path}")
    action = max(new_actions, key=lambda item: item.frame_range[1] - item.frame_range[0])
    action.name = final_name
    action.use_fake_user = True
    action["loop"] = True
    action["in_place"] = True
    remove_objects(imported)
    return action


def reset_pose(armature):
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis.identity()
    bpy.context.view_layer.update()


def aim_bone(armature, bone_name, target, up_axis="Z"):
    bone = armature.pose.bones[bone_name]
    head = bone.head.copy()
    direction = Vector(target) - head
    if direction.length < 0.001:
        return
    rotation = direction.normalized().to_track_quat("Y", up_axis).to_matrix().to_4x4()
    bone.matrix = Matrix.Translation(head) @ rotation
    bpy.context.view_layer.update()


def apply_idle_arms(armature):
    aim_bone(armature, "LeftArm", (35.0, 0.0, 105.0))
    aim_bone(armature, "LeftForeArm", (29.0, -3.0, 83.0))
    aim_bone(armature, "LeftHand", (28.0, -6.0, 73.0))
    aim_bone(armature, "RightArm", (-35.0, 0.0, 105.0))
    aim_bone(armature, "RightForeArm", (-29.0, -3.0, 83.0))
    aim_bone(armature, "RightHand", (-28.0, -6.0, 73.0))


def key_pose(armature, frame):
    for bone in armature.pose.bones:
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def begin_action(armature, name, end_frame):
    reset_pose(armature)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    action["loop"] = False
    action["fps"] = 30
    action["frame_start"] = 1
    action["frame_end"] = end_frame
    armature.animation_data_create()
    armature.animation_data.action = action
    return action


def set_idle_key(armature, frame):
    reset_pose(armature)
    apply_idle_arms(armature)
    key_pose(armature, frame)


def make_idle(armature):
    action = begin_action(armature, "Idle", 30)
    action["loop"] = True
    set_idle_key(armature, 1)
    set_idle_key(armature, 30)
    return action


def make_ranged_attack(armature):
    action = begin_action(armature, "Attack_Ranged_Throw", 33)
    action["event_name"] = "ProjectileRelease"
    action["event_frame"] = 29
    set_idle_key(armature, 1)

    for frame in (8, 23):
        reset_pose(armature)
        apply_idle_arms(armature)
        aim_bone(armature, "RightArm", (-40.0, -12.0, 137.0))
        aim_bone(armature, "RightForeArm", (-18.0, -24.0, 148.0))
        aim_bone(armature, "RightHand", (-11.0, -29.0, 151.0))
        key_pose(armature, frame)

    reset_pose(armature)
    apply_idle_arms(armature)
    aim_bone(armature, "RightArm", (-28.0, -25.0, 132.0))
    aim_bone(armature, "RightForeArm", (-20.0, -51.0, 126.0))
    aim_bone(armature, "RightHand", (-18.0, -63.0, 123.0))
    key_pose(armature, 29)
    set_idle_key(armature, 33)
    return action


def make_melee_attack(armature):
    action = begin_action(armature, "Attack_Melee_OverheadSmash", 21)
    action["event_name"] = "MeleeHit"
    action["event_frame"] = 18
    set_idle_key(armature, 1)

    reset_pose(armature)
    aim_bone(armature, "LeftArm", (27.0, -5.0, 153.0))
    aim_bone(armature, "LeftForeArm", (12.0, -9.0, 169.0))
    aim_bone(armature, "LeftHand", (6.0, -12.0, 177.0))
    aim_bone(armature, "RightArm", (-27.0, -5.0, 153.0))
    aim_bone(armature, "RightForeArm", (-12.0, -9.0, 169.0))
    aim_bone(armature, "RightHand", (-6.0, -12.0, 177.0))
    key_pose(armature, 11)

    reset_pose(armature)
    aim_bone(armature, "LeftArm", (25.0, -18.0, 112.0))
    aim_bone(armature, "LeftForeArm", (13.0, -37.0, 92.0))
    aim_bone(armature, "LeftHand", (8.0, -47.0, 84.0))
    aim_bone(armature, "RightArm", (-25.0, -18.0, 112.0))
    aim_bone(armature, "RightForeArm", (-13.0, -37.0, 92.0))
    aim_bone(armature, "RightHand", (-8.0, -47.0, 84.0))
    key_pose(armature, 18)
    set_idle_key(armature, 21)
    return action


def add_action_markers(action, marker_name, frame):
    markers = getattr(action, "pose_markers", None)
    if markers is not None:
        markers.new(marker_name).frame = frame


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    base_objects = import_fbx(RIGGED_FBX)

    armature = next(obj for obj in base_objects if obj.type == "ARMATURE")
    mesh = max((obj for obj in base_objects if obj.type == "MESH"), key=lambda obj: len(obj.data.vertices))
    extras = [obj for obj in base_objects if obj not in {armature, mesh}]
    remove_objects(extras)

    armature.name = "RIG_HumanoidBlob"
    armature.data.name = "RIG_HumanoidBlob"
    armature.show_in_front = True
    armature["character_height_m"] = 1.70
    armature["proportion_standard"] = "Adult human, approximately 7.5 heads tall"

    for action in list(bpy.data.actions):
        if action.frame_range[0] == action.frame_range[1] == 1:
            bpy.data.actions.remove(action)

    source_collection = bpy.data.collections.new("SOURCE_HIGH_DO_NOT_EDIT")
    working_collection = bpy.data.collections.new("WORKING_POLY_CONTROL")
    bpy.context.scene.collection.children.link(source_collection)
    bpy.context.scene.collection.children.link(working_collection)

    for collection in list(mesh.users_collection):
        collection.objects.unlink(mesh)
    source_collection.objects.link(mesh)
    mesh.name = "HumanoidBlob_SOURCE_HIGH"
    mesh.data.name = "HumanoidBlob_SOURCE_HIGH_Mesh"
    mesh.hide_viewport = True
    mesh.hide_render = True
    mesh["source_triangles"] = sum(len(poly.vertices) - 2 for poly in mesh.data.polygons)

    working = mesh.copy()
    working.data = mesh.data.copy()
    working.name = "HumanoidBlob_WORKING"
    working.data.name = "HumanoidBlob_WORKING_Mesh"
    working.hide_viewport = False
    working.hide_render = False
    working_collection.objects.link(working)

    clay = bpy.data.materials.new("M_HumanoidBlob_Clay")
    clay.diffuse_color = (0.24, 0.26, 0.28, 1.0)
    clay.metallic = 0.0
    clay.roughness = 0.72
    working.data.materials.clear()
    working.data.materials.append(clay)
    mesh.data.materials.clear()
    mesh.data.materials.append(clay)

    decimate = working.modifiers.new("USER_POLY_CONTROL", "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = 1.0
    decimate.use_collapse_triangulate = True

    walk = import_animation(WALK_FBX, "Walk_Heavy_Ranged")
    walk["role"] = "Ranged enemy heavy walk"
    walk["recommended_playback_speed"] = 0.75
    run = import_animation(RUN_FBX, "Run_Fast_Melee")
    run["role"] = "Melee enemy fast run"
    run["recommended_playback_speed"] = 1.20

    idle = make_idle(armature)
    ranged = make_ranged_attack(armature)
    melee = make_melee_attack(armature)
    add_action_markers(ranged, "ProjectileRelease", 29)
    add_action_markers(melee, "MeleeHit", 18)

    bpy.context.scene.timeline_markers.new("Attack_Ranged_Throw__ProjectileRelease", frame=29)
    bpy.context.scene.timeline_markers.new("Attack_Melee_OverheadSmash__MeleeHit", frame=18)
    bpy.context.scene.render.fps = 30
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 30
    armature.animation_data.action = idle
    bpy.context.scene.frame_set(1)

    guide = bpy.data.texts.new("README_HumanoidBlob_Setup")
    guide.write(
        "HumanoidBlob setup\n"
        "- Adult human proportions: 1.70 m, approximately 7.5 heads tall.\n"
        "- SOURCE_HIGH_DO_NOT_EDIT preserves the original high-poly mesh.\n"
        "- Select HumanoidBlob_WORKING and adjust Modifier > USER_POLY_CONTROL > Ratio.\n"
        "- Keep the modifier unapplied until the target triangle count is approved.\n"
        "- Walk_Heavy_Ranged: loop, recommended playback 0.75x.\n"
        "- Run_Fast_Melee: loop, recommended playback 1.20x.\n"
        "- Attack_Ranged_Throw: hand stays in front of the face for 0.5 s; ProjectileRelease at frame 29.\n"
        "- Attack_Melee_OverheadSmash: MeleeHit at frame 18.\n"
        "- Attack actions return to Idle immediately and do not include gameplay cooldown.\n"
    )

    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    working.select_set(True)
    bpy.context.view_layer.objects.active = working

    bpy.context.scene["meshy_generation_task"] = "019fe731-0b09-7cd1-ab14-c2670a434708"
    bpy.context.scene["meshy_rig_task"] = "019fe736-4ea3-7947-9d0e-43c5deef2b7b"
    bpy.context.scene["source_triangles"] = mesh["source_triangles"]
    bpy.context.scene["texture_policy"] = "No textures"

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))
    print(f"OUTPUT_BLEND={OUTPUT_BLEND}")
    print(f"SOURCE_TRIANGLES={mesh['source_triangles']}")
    print(f"WORKING_MODIFIER={decimate.name}:{decimate.ratio}")
    print("ACTIONS=" + ",".join(sorted(action.name for action in bpy.data.actions)))


if __name__ == "__main__":
    main()
