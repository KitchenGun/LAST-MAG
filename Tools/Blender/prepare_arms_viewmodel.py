import bpy
import sys


def main(source_path, output_path):
    bpy.ops.import_scene.fbx(filepath=source_path)
    for obj in [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name == "Cube"]:
        bpy.data.objects.remove(obj, do_unlink=True)
    source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name != "Cube")
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = source
    source.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    parts = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name != "Cube"]
    parts.sort(key=lambda obj: len(obj.data.polygons), reverse=True)
    arms = parts[:2]
    for part in parts[2:]:
        bpy.data.objects.remove(part, do_unlink=True)

    bpy.ops.object.empty_add(type="PLAIN_AXES")
    root = bpy.context.object
    root.name = "ArmsRoot"
    for index, arm in enumerate(arms):
        arm.name = f"Arm_{index + 1}"
        arm.parent = root

    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for arm in arms:
        arm.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=output_path, use_selection=True, add_leaf_bones=False, path_mode="AUTO")
    triangles = sum(len(polygon.vertices) - 2 for arm in arms for polygon in arm.data.polygons)
    print(f"TRIANGLES {triangles}")
    print(f"ARMS {[(arm.name, len(arm.data.polygons)) for arm in arms]}")


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1 :]
    main(arguments[0], arguments[1])
