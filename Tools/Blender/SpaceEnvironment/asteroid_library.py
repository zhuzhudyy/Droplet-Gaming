"""Original, bounded asteroid mesh library for Blender 5.2.

The coordinator owns scene creation, placement, saving and export. This module
only adds 24 explicitly owned mesh objects to the supplied visible collection.
Objects have identity transforms and a common origin for all three LODs. Shape
is authored into vertices; no modifiers remain on returned objects. Blender
authoring convention is +Y forward, +Z up (no asset-local corrective rotation).

API: build_asteroid_library(collection, materials) -> {asset_id: [lod0, lod1, lod2]}
"""

import math

import bmesh
import bpy
from mathutils import Vector


OWNER = "DropletPrototype.SpaceEnvironment"
SPECS = (
    ("Asteroid_Rounded", "rounded", (1.00, .92, .89)),
    ("Asteroid_Elongated", "elongated", (1.83, .62, .69)),
    ("Asteroid_Flattened", "flattened", (1.14, .94, .34)),
    ("Asteroid_Angular", "angular", (1.02, .91, .94)),
    ("Asteroid_Notched", "notched", (1.04, .89, 1.03)),
    ("Asteroid_ContactBinary", "binary", (1.12, .76, .79)),
    ("Asteroid_ThreeLobed", "trilobed", (1.04, 1.00, .70)),
    ("Asteroid_Wedge", "wedge", (1.29, .96, .68)),
)


def _gaussian(value, center, width):
    return math.exp(-((value - center) / width) ** 2)


def _rock_wave(direction, index):
    """Broad, deterministic deformation: intentionally no micro-displacement."""
    x, y, z = direction
    phase = 1.731 * (index + 1)
    return (
        .067 * math.sin(3.7*x + 2.3*y - 1.5*z + phase)
        + .046 * math.sin(-2.2*x + 5.1*y + 3.1*z - phase*.63)
        + .023 * math.sin(8.1*x - 4.3*y + 6.5*z + phase*1.4)
    )


def _crater(direction, center, angular_radius, depth):
    angle = math.acos(max(-1.0, min(1.0, direction.dot(Vector(center).normalized()))))
    t = angle / angular_radius
    # Rounded basin plus modest broken impact rim; broad enough for all LODs.
    return -depth * math.exp(-(t / .66) ** 4) + depth*.23*_gaussian(t, .98, .20)


def _polyhedral_radius(direction):
    """Ray intersection with an irregular clipped polyhedron; all planes closed."""
    planes = (
        ((1, .12, .08), .88), ((-1, -.13, .06), .98),
        ((.11, 1, -.11), .85), ((-.04, -1, .21), .94),
        ((.13, -.17, 1), .77), ((-.17, .08, -1), .92),
        ((.74, .74, .69), 1.10), ((-.73, -.81, .72), 1.10),
    )
    intersections = []
    for normal, distance in planes:
        normal = Vector(normal).normalized()
        denominator = normal.dot(direction)
        if denominator > .00001:
            intersections.append(distance / denominator)
    return min(intersections)


def _shape(direction, kind, axes, index):
    x, y, z = direction
    wave = _rock_wave(direction, index)
    if kind == "rounded":
        radius = 1 + wave
    elif kind == "elongated":
        radius = (1 + .15*x + .10*z*x) * (1 + wave*.8)
    elif kind == "flattened":
        # Broad slab with unequal lobes and a subtly polygonal perimeter.
        radius = (1 + .10*math.cos(5*math.atan2(y, x))*(1-z*z)) * (1 + wave*.75)
    elif kind == "angular":
        radius = _polyhedral_radius(direction) * (1 + wave*.32)
    elif kind == "notched":
        # Deep open bite between two substantial shoulders. Still a closed,
        # star-shaped manifold, with no Boolean seams or internal surfaces.
        bite = math.acos(max(-1., min(1., direction.dot(Vector((.92, -.12, .38)).normalized()))))
        radius = (1 + wave) * (1 - .78*math.exp(-(bite/.49)**4))
    elif kind == "binary":
        # Two differently sized joined lobes and a readable narrow waist.
        radius = (.66 + .80*abs(x)**2.0 + .11*x) * (1 + wave*.8)
    elif kind == "trilobed":
        angle = math.atan2(y, x)
        radius = (.89 + .25*math.cos(3*angle + .35)*(1-z*z)**1.4 + .10*z) * (1 + wave*.75)
    elif kind == "wedge":
        radius = _polyhedral_radius(direction) * (1 + .28*x - .12*y) * (1 + wave*.45)
        # A large oblique fracture truncates one end into a visible plane.
        dot = direction.dot(Vector((.62, -.17, .77)).normalized())
        if dot > .0001:
            radius = min(radius, .57/dot)
    else:
        raise ValueError("Unknown asteroid shape: " + kind)

    if kind not in ("notched", "wedge"):
        radius += _crater(direction, (.12, -.88, .53), .46, .16)
    radius += _crater(direction, (-.67, .39, .58), .34, .11)
    radius += _crater(direction, (.58, .58, -.57), .26, .070)
    # A strictly positive radial field preserves the sphere topology and keeps
    # every point on its original ray, even at the deep notched silhouette.
    radius = max(.18, radius)
    return Vector((x*axes[0], y*axes[1], z*axes[2])) * radius


def _base_mesh(asset_id, kind, axes, index):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    for vertex in bm.verts:
        vertex.co = _shape(vertex.co.normalized(), kind, axes, index)

    minimum = Vector(tuple(min(v.co[i] for v in bm.verts) for i in range(3)))
    maximum = Vector(tuple(max(v.co[i] for v in bm.verts) for i in range(3)))
    center = (minimum + maximum) * .5
    for vertex in bm.verts:
        vertex.co -= center
    radius = max(v.co.length for v in bm.verts)
    for vertex in bm.verts:
        vertex.co /= radius

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    data = bpy.data.meshes.new(asset_id + "_LOD0_Mesh")
    bm.to_mesh(data)
    bm.free()
    data.update()
    return data


def _finalize_mesh(data, flat_faces):
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    for polygon in data.polygons:
        polygon.use_smooth = not flat_faces
    data.update()
    data.calc_loop_triangles()


def _simplified_object(base, collection, lod, target_triangles, flat_faces):
    obj = bpy.data.objects.new(base.name.rsplit("_LOD", 1)[0] + "_LOD" + str(lod), base.data.copy())
    collection.objects.link(obj)
    modifier = obj.modifiers.new("Offline silhouette-preserving simplification", 'DECIMATE')
    modifier.decimate_type = 'COLLAPSE'
    modifier.ratio = target_triangles / len(base.data.loop_triangles)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(graph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=graph)
    previous = obj.data
    obj.modifiers.clear()
    obj.data = mesh
    mesh.name = obj.name + "_Mesh"
    if previous.users == 0:
        bpy.data.meshes.remove(previous)
    _finalize_mesh(mesh, flat_faces)
    return obj


def _material_values(materials):
    if isinstance(materials, dict):
        rock_materials = [value for key, value in materials.items()
                          if "rock" in str(key).lower() or "asteroid" in str(key).lower()]
        materials = rock_materials or list(materials.values())
    elif isinstance(materials, bpy.types.Material):
        materials = [materials]
    else:
        materials = list(materials)
    if not materials or any(not isinstance(material, bpy.types.Material) for material in materials):
        raise ValueError("Supply one or two existing Blender rock materials")
    if len(materials) > 2:
        raise ValueError("The asteroid library budget permits no more than two materials")
    return materials


def inspect_asteroid_library(library):
    """Return actual mesh counts and hard-fail topology/budget regressions."""
    records = {}
    limits = ((800, 2000), (200, 500), (40, 120))
    assert len(library) == 8, "Eight asteroid prototypes are required"
    all_meshes = []
    for asset_id, objects in library.items():
        assert len(objects) == 3, asset_id + ": three LODs are required"
        lod_records = []
        for lod, obj in enumerate(objects):
            obj.data.calc_loop_triangles()
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bm.normal_update()
            volume = bm.calc_volume(signed=True)
            non_manifold_edges = sum(not edge.is_manifold for edge in bm.edges)
            degenerate_faces = sum(face.calc_area() < 1e-10 for face in bm.faces)
            bm.free()
            triangles = len(obj.data.loop_triangles)
            assert limits[lod][0] <= triangles <= limits[lod][1], (asset_id, lod, triangles)
            assert non_manifold_edges == 0 and degenerate_faces == 0 and volume > 0, (asset_id, lod, volume)
            assert not obj.modifiers, asset_id + ": retained modifier"
            assert obj.location.length < 1e-7 and all(abs(value) < 1e-7 for value in obj.rotation_euler)
            assert all(abs(value - 1) < 1e-7 for value in obj.scale)
            assert len(obj.data.materials) == 1, asset_id + ": must share one assigned material"
            assert obj.data.materials[0] == objects[0].data.materials[0]
            lod_records.append(dict(lod=lod, vertices=len(obj.data.vertices), triangles=triangles,
                                    bounds=[list(v) for v in obj.bound_box],
                                    signed_volume=volume, non_manifold_edges=non_manifold_edges,
                                    degenerate_faces=degenerate_faces,
                                    shared_material=obj.data.materials[0].name))
            all_meshes.append(obj.data)
        records[asset_id] = lod_records
    assert len(set(all_meshes)) == 24, "The library should contain 24 unique LOD meshes"
    return dict(prototypes=8, unique_meshes=24,
                triangles=sum(item['triangles'] for lods in records.values() for item in lods),
                models=records)


def build_asteroid_library(collection, materials):
    """Create 8 centered, unit-radius-ish prototypes with true 1280/320/80 LODs.

    The collection must participate in the current view layer while this function
    runs, because Blender evaluates temporary Decimate modifiers offline. The
    caller may hide or exclude the library afterwards. This function refuses
    name collisions instead of deleting or replacing potentially authored work.
    """
    mats = _material_values(materials)
    reserved_names = [asset_id + "_LOD" + str(lod) for asset_id, _, _ in SPECS for lod in range(3)]
    collisions = [name for name in reserved_names if bpy.data.objects.get(name)]
    if collisions:
        raise ValueError("Asteroid library already exists; caller must manage its owned subtree: " + ", ".join(collisions))
    library = {}
    for index, (asset_id, kind, axes) in enumerate(SPECS):
        data = _base_mesh(asset_id, kind, axes, index)
        base = bpy.data.objects.new(asset_id + "_LOD0", data)
        collection.objects.link(base)
        data.materials.append(mats[index % len(mats)])
        flat_faces = kind in ("angular", "wedge")
        _finalize_mesh(data, flat_faces)
        lods = [base]
        for lod, triangles in ((1, 320), (2, 80)):
            lods.append(_simplified_object(base, collection, lod, triangles, flat_faces))
        for lod, obj in enumerate(lods):
            obj["space_environment_owner"] = OWNER
            obj["asset_id"] = asset_id
            obj["lod_index"] = lod
            obj["shape_description"] = kind
            obj["decorative_only"] = True
            obj.data["space_environment_owner"] = OWNER
            obj.data["source_shape"] = asset_id
            obj.data["offline_lod"] = lod
        library[asset_id] = lods
    bpy.context.view_layer.update()
    inspect_asteroid_library(library)
    return library
