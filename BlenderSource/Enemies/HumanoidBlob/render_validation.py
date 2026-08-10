import bpy
from mathutils import Vector
from pathlib import Path


ROOT = Path(r"C:\Users\kang9\Documents\ChatGPT\Gulag-project\BlenderSource\Enemies\HumanoidBlob")
OUT = ROOT / "Validation"
OUT.mkdir(exist_ok=True)


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.display.shading.light = "STUDIO"
scene.display.shading.studio_light = "rim.sl"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.background_type = "VIEWPORT"
scene.display.shading.background_color = (0.055, 0.06, 0.07)
scene.render.image_settings.file_format = "PNG"

camera_data = bpy.data.cameras.new("ValidationCamera")
camera = bpy.data.objects.new("ValidationCamera", camera_data)
scene.collection.objects.link(camera)
camera.location = (2.55, -4.5, 1.55)
camera_data.lens = 58
look_at(camera, (0.0, 0.0, 0.9))
scene.camera = camera

armature = bpy.data.objects["RIG_HumanoidBlob"]
shots = [
    ("Idle", 1, "01_Idle"),
    ("Attack_Ranged_Throw", 23, "02_Ranged_Hold"),
    ("Attack_Ranged_Throw", 29, "03_Ranged_Release"),
    ("Attack_Melee_OverheadSmash", 11, "04_Melee_Overhead"),
    ("Attack_Melee_OverheadSmash", 18, "05_Melee_Hit"),
    ("Run_Fast_Melee", 6, "06_Run"),
    ("Hit_FullBody", 3, "07_Hit_FullBody"),
    ("Death_Backward", 24, "08_Death_Fall"),
    ("Death_Backward", 36, "09_Death_End"),
]

for action_name, frame, output_name in shots:
    armature.animation_data.action = bpy.data.actions[action_name]
    scene.frame_set(frame)
    scene.render.filepath = str(OUT / f"{output_name}.png")
    bpy.ops.render.render(write_still=True)
    print(scene.render.filepath)
