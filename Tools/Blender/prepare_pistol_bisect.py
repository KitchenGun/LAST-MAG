import bpy
import sys


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def bisect(obj, clear_inner, clear_outer):
    select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.bisect(
        plane_co=(0.0, 0.0, 0.105),
        plane_no=(0.0, 0.0, 1.0),
        use_fill=True,
        clear_inner=clear_inner,
        clear_outer=clear_outer,
    )
    bpy.ops.object.mode_set(mode="OBJECT")


def decimate(obj):
    select_only(obj)
    modifier = obj.modifiers.new("ViewmodelDecimate", "DECIMATE")
    modifier.ratio = 0.77
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def main(source_path, output_path):
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=source_path)
    source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    body = source.copy()
    body.data = source.data.copy()
    bpy.context.collection.objects.link(body)
    body.name = "PistolBody"
    source.name = "PistolSlideMesh"

    bisect(body, clear_inner=False, clear_outer=True)
    bisect(source, clear_inner=True, clear_outer=False)
    decimate(body)
    decimate(source)

    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(-0.09, 0.11, 0.0))
    pivot = bpy.context.object
    pivot.name = "PistolSlide"
    source.parent = pivot

    bpy.ops.object.select_all(action="DESELECT")
    for obj in [body, source, pivot]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.export_scene.fbx(filepath=output_path, use_selection=True, add_leaf_bones=False, path_mode="AUTO")
    triangles = sum(len(polygon.vertices) - 2 for obj in [body, source] for polygon in obj.data.polygons)
    print(f"TRIANGLES {triangles}")
    print("PARTS", [(obj.name, len(obj.data.polygons)) for obj in [body, source]])


if __name__ == "__main__":
    arguments = sys.argv[sys.argv.index("--") + 1 :]
    main(arguments[0], arguments[1])
