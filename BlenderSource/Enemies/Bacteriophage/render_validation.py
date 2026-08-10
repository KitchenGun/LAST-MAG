from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


camera = bpy.data.objects["Preview_Camera"]
scene = bpy.context.scene
scene.camera = camera
scene.render.resolution_x = 768
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"

views = {
    "Front": ((0.0, -5.0, 1.05), (0.0, 0.0, 1.0), 2.45),
    "Left": ((5.0, 0.0, 1.05), (0.0, 0.0, 1.0), 2.45),
    "Top": ((0.0, 0.0, 5.0), (0.0, 0.0, 0.9), 2.2),
}

for name, (location, target, scale) in views.items():
    camera.location = location
    camera.data.ortho_scale = scale
    look_at(camera, target)
    scene.render.filepath = str(ROOT / f"Bacteriophage_Validation_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"RENDERED={scene.render.filepath}")

armature = bpy.data.objects["Bacteriophage_Rig"]
armature.animation_data.action = bpy.data.actions["Move_LegsOnly"]
scene.frame_set(7)
camera.location = (0.0, -5.0, 1.05)
camera.data.ortho_scale = 2.45
look_at(camera, (0.0, 0.0, 1.0))
scene.render.filepath = str(ROOT / "Bacteriophage_Validation_Move_Frame07.png")
bpy.ops.render.render(write_still=True)
print(f"RENDERED={scene.render.filepath}")

working = bpy.data.objects["WORKING_DECIMATE"]
print("MODIFIERS=" + ",".join(f"{modifier.name}:{modifier.type}" for modifier in working.modifiers))
print("MATERIALS=" + ",".join(slot.material.name for slot in working.material_slots))
material_counts = [0] * len(working.material_slots)
for polygon in working.data.polygons:
    material_counts[polygon.material_index] += 1
print("MATERIAL_FACE_COUNTS=" + ",".join(str(count) for count in material_counts))
print("BONES=" + ",".join(bone.name for bone in armature.data.bones))
print("ACTIONS=" + ",".join(action.name for action in bpy.data.actions))
