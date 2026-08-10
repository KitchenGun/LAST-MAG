import bpy
from math import pi, radians, sin
from mathutils import Matrix, Vector
from pathlib import Path


ROOT = Path(r"C:\Users\kang9\Documents\ChatGPT\Gulag-project")
SOURCE_BLEND = ROOT / "BlenderSource" / "Enemies" / "HumanoidBlob" / "HumanoidBlob_UnityExport.blend"
OUTPUT_BLEND = ROOT / "BlenderSource" / "Enemies" / "HumanoidBlob" / "HumanoidBlob_HitDeath_Animation.blend"
OUTPUT_FBX = ROOT / "Assets" / "Art" / "Enemies" / "HumanoidBlob" / "SM_HumanoidBlob_Low.fbx"


def reset_pose(armature):
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis.identity()
    bpy.context.view_layer.update()


def aim_bone(armature, bone_name, target, up_axis="Z"):
    bone = armature.pose.bones[bone_name]
    direction = Vector(target) - bone.head
    if direction.length < 0.001:
        return
    rotation = direction.normalized().to_track_quat("Y", up_axis).to_matrix().to_4x4()
    bone.matrix = Matrix.Translation(bone.head) @ rotation
    bpy.context.view_layer.update()


def apply_idle_arms(armature):
    aim_bone(armature, "LeftArm", (35.0, 0.0, 105.0))
    aim_bone(armature, "LeftForeArm", (29.0, -3.0, 83.0))
    aim_bone(armature, "LeftHand", (28.0, -6.0, 73.0))
    aim_bone(armature, "RightArm", (-35.0, 0.0, 105.0))
    aim_bone(armature, "RightForeArm", (-29.0, -3.0, 83.0))
    aim_bone(armature, "RightHand", (-28.0, -6.0, 73.0))


def rotate_bone(armature, bone_name, angle_degrees, axis="X"):
    bone = armature.pose.bones[bone_name]
    pivot = bone.head.copy()
    rotation = Matrix.Rotation(radians(angle_degrees), 4, axis)
    bone.matrix = Matrix.Translation(pivot) @ rotation @ Matrix.Translation(-pivot) @ bone.matrix
    bpy.context.view_layer.update()


def translate_bone(armature, bone_name, offset):
    bone = armature.pose.bones[bone_name]
    bone.matrix = Matrix.Translation(Vector(offset)) @ bone.matrix
    bpy.context.view_layer.update()


def key_pose(armature, frame):
    for bone in armature.pose.bones:
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def set_idle_key(armature, frame):
    reset_pose(armature)
    apply_idle_arms(armature)
    key_pose(armature, frame)


def begin_action(armature, name, end_frame):
    existing = bpy.data.actions.get(name)
    if existing is not None:
        bpy.data.actions.remove(existing)
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


def pose_hit(armature, strength):
    reset_pose(armature)
    apply_idle_arms(armature)
    rotate_bone(armature, "Spine02", -12.0 * strength)
    rotate_bone(armature, "Spine01", -7.0 * strength)
    rotate_bone(armature, "neck", 5.0 * strength)
    rotate_bone(armature, "Head", 4.0 * strength)
    rotate_bone(armature, "LeftUpLeg", -7.0 * strength)
    rotate_bone(armature, "RightUpLeg", -7.0 * strength)
    rotate_bone(armature, "LeftLeg", 12.0 * strength)
    rotate_bone(armature, "RightLeg", 12.0 * strength)
    aim_bone(armature, "LeftArm", (37.0, 6.0 * strength, 112.0))
    aim_bone(armature, "LeftForeArm", (34.0, 10.0 * strength, 91.0))
    aim_bone(armature, "LeftHand", (32.0, 12.0 * strength, 79.0))
    aim_bone(armature, "RightArm", (-37.0, 6.0 * strength, 112.0))
    aim_bone(armature, "RightForeArm", (-34.0, 10.0 * strength, 91.0))
    aim_bone(armature, "RightHand", (-32.0, 12.0 * strength, 79.0))


def make_hit_action(armature):
    action = begin_action(armature, "Hit_FullBody", 7)
    set_idle_key(armature, 1)
    pose_hit(armature, 1.0)
    key_pose(armature, 3)
    pose_hit(armature, 0.45)
    key_pose(armature, 5)
    set_idle_key(armature, 7)
    return action


def pose_death(armature, progress):
    reset_pose(armature)
    apply_idle_arms(armature)
    fall_clearance = 18.0 * sin(pi * progress)
    translate_bone(armature, "Hips", (0.0, 34.0 * progress, -72.0 * progress + fall_clearance))
    rotate_bone(armature, "Hips", -88.0 * progress)
    rotate_bone(armature, "LeftUpLeg", -28.0 * progress)
    rotate_bone(armature, "RightUpLeg", -22.0 * progress)
    rotate_bone(armature, "LeftLeg", 52.0 * progress)
    rotate_bone(armature, "RightLeg", 44.0 * progress)
    rotate_bone(armature, "LeftFoot", -18.0 * progress)
    rotate_bone(armature, "RightFoot", -12.0 * progress)
    rotate_bone(armature, "Spine02", -8.0 * progress)
    rotate_bone(armature, "Spine01", 6.0 * progress)
    rotate_bone(armature, "neck", 10.0 * progress)
    rotate_bone(armature, "Head", 8.0 * progress)
    aim_bone(armature, "LeftArm", (46.0, 25.0 * progress, 115.0 - 72.0 * progress))
    aim_bone(armature, "LeftForeArm", (66.0, 36.0 * progress, 105.0 - 72.0 * progress))
    aim_bone(armature, "LeftHand", (78.0, 42.0 * progress, 100.0 - 72.0 * progress))
    aim_bone(armature, "RightArm", (-46.0, 22.0 * progress, 116.0 - 72.0 * progress))
    aim_bone(armature, "RightForeArm", (-64.0, 34.0 * progress, 108.0 - 72.0 * progress))
    aim_bone(armature, "RightHand", (-76.0, 40.0 * progress, 104.0 - 72.0 * progress))


def make_death_action(armature):
    action = begin_action(armature, "Death_Backward", 36)
    set_idle_key(armature, 1)
    pose_death(armature, 0.10)
    key_pose(armature, 7)
    pose_death(armature, 0.30)
    key_pose(armature, 14)
    pose_death(armature, 0.68)
    key_pose(armature, 24)
    pose_death(armature, 1.0)
    key_pose(armature, 32)
    key_pose(armature, 36)
    return action


def triangle_count(mesh):
    return sum(len(polygon.vertices) - 2 for polygon in mesh.polygons)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    armature = bpy.data.objects["RIG_HumanoidBlob"]
    working = bpy.data.objects["HumanoidBlob_WORKING"]

    make_hit_action(armature)
    make_death_action(armature)

    bpy.context.scene.render.fps = 30
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 36
    armature.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(1)
    bpy.context.scene["hit_action"] = "Hit_FullBody:1-7"
    bpy.context.scene["death_action"] = "Death_Backward:1-36"

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

    action_report = ",".join(
        f"{action.name}:{int(action.frame_range[0])}-{int(action.frame_range[1])}"
        for action in sorted(bpy.data.actions, key=lambda item: item.name)
    )
    print(f"OUTPUT_BLEND={OUTPUT_BLEND}")
    print(f"OUTPUT_FBX={OUTPUT_FBX}")
    print(f"BONES={len(armature.data.bones)}")
    print(f"TRIANGLES={triangle_count(working.data)}")
    print(f"MATERIALS={','.join(material.name for material in working.data.materials)}")
    print(f"ACTIONS={action_report}")


if __name__ == "__main__":
    main()
