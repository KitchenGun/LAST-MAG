import bpy
from mathutils import Vector


mesh = bpy.data.objects["WORKING_DECIMATE"].data
points = [Vector((v.co.x, v.co.y)) for v in mesh.vertices if v.co.z < 0.28]
points = points[::max(1, len(points) // 12000)]


def cluster(count):
    centers = [points[0]]
    while len(centers) < count:
        centers.append(max(points, key=lambda point: min((point - center).length_squared for center in centers)))
    for _ in range(30):
        groups = [[] for _ in centers]
        for point in points:
            index = min(range(len(centers)), key=lambda i: (point - centers[i]).length_squared)
            groups[index].append(point)
        new_centers = []
        for center, group in zip(centers, groups):
            if group:
                new_centers.append(sum(group, Vector((0.0, 0.0))) / len(group))
            else:
                new_centers.append(center)
        if max((a - b).length for a, b in zip(centers, new_centers)) < 0.00001:
            centers = new_centers
            break
        centers = new_centers
    inertia = sum(min((point - center).length_squared for center in centers) for point in points) / len(points)
    return sorted(centers, key=lambda point: (point.y, point.x)), inertia


for count in (4, 6, 8):
    centers, inertia = cluster(count)
    print(f"K={count} INERTIA={inertia:.8f} CENTERS=" + ",".join(f"{p.x:.4f}/{p.y:.4f}" for p in centers))
