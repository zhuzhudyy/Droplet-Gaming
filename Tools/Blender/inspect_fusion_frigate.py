"""Read-only Blender inspection of the FusionFrigate source or staged FBX.

Run in a separate Blender process, for example::

    blender --background --factory-startup --python-exit-code 1 \
      --python Tools/Blender/inspect_fusion_frigate.py -- --source
    blender --background --factory-startup --python-exit-code 1 \
      --python Tools/Blender/inspect_fusion_frigate.py -- --fbx

The input files are never saved. Only the requested JSON evidence file is written.
Topology figures are diagnostics, not a claim that geometric intersections or
all possible normal errors have been ruled out by automation. FBX axis conversion
is deliberately recorded, not inferred or repaired.
"""

import argparse
import hashlib
import json
import math
import re
import sys
import traceback
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend"
FBX = ROOT / "ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx"
EVIDENCE = ROOT / "docs/verification/FusionFrigate"
MATERIAL_NAMES = {"FF_Armor", "FF_Structure", "FF_EngineMetal", "FF_BlueGray"}
GROUPS = {"Hull", "Superstructure", "FusionDrive", "Details"}
TOLERANCE = 1e-4


def args_from_blender():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--source", nargs="?", const=str(SOURCE), help="Read saved .blend (default).")
    mode.add_argument("--fbx", nargs="?", const=str(FBX), help="Import FBX into a new temporary scene.")
    parser.add_argument("--output", type=Path, help="Output JSON evidence path.")
    return parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])


def rounded(values):
    return [round(float(value), 8) for value in values]


def matrix_rows(matrix):
    return [rounded(row) for row in matrix]


def get_lod(obj):
    current = obj
    while current is not None:
        value = current.get("ff_lod")
        if value is not None:
            try:
                if int(value) in (0, 1, 2):
                    return int(value)
            except (ValueError, TypeError):
                pass
        match = re.search(r"(?:^|[_ .-])LOD([012])(?:$|[_ .-])", current.name, re.IGNORECASE)
        if match:
            return int(match.group(1))
        current = current.parent
    for collection in obj.users_collection:
        match = re.search(r"LOD([012])(?:$|[_ .-])", collection.name, re.IGNORECASE)
        if match:
            return int(match.group(1))
    return None


def walk_layers(layer):
    yield layer
    for child in layer.children:
        yield from walk_layers(child)


def select_lod(scene, objects, lod):
    # These changes are in memory only. Reveal collection chains so that the
    # evaluated dependency graph actually contains the requested LOD.
    for layer in walk_layers(bpy.context.view_layer.layer_collection):
        layer.exclude = False
        layer.hide_viewport = False
        layer.collection.hide_viewport = False
    for obj in objects:
        owner_lod = get_lod(obj)
        if owner_lod is not None:
            obj.hide_set(owner_lod != lod)
            obj.hide_viewport = owner_lod != lod
            obj.hide_render = owner_lod != lod
    bpy.context.view_layer.update()


def mesh_topology(mesh, world):
    mesh.calc_loop_triangles()
    invalid_normals = 0
    zero_area = 0
    for polygon in mesh.polygons:
        normal = polygon.normal
        if not all(math.isfinite(component) for component in normal) or abs(normal.length - 1.0) > 1e-3:
            invalid_normals += 1
    for tri in mesh.loop_triangles:
        p0, p1, p2 = (world @ mesh.vertices[index].co for index in tri.vertices)
        area = (p1 - p0).cross(p2 - p0).length * 0.5
        if not math.isfinite(area) or area <= 1e-12:
            zero_area += 1
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        boundary = sum(edge.is_boundary for edge in bm.edges)
        loose = sum(edge.is_wire for edge in bm.edges)
        non_manifold = sum(not edge.is_manifold for edge in bm.edges)
        inconsistent_winding = sum(edge.is_manifold and not edge.is_contiguous for edge in bm.edges)
        unseen = set(bm.faces)
        closed_components = 0
        negative_volume_components = 0
        while unseen:
            seed = unseen.pop()
            component = {seed}
            pending = [seed]
            while pending:
                face = pending.pop()
                for edge in face.edges:
                    for neighbor in edge.link_faces:
                        if neighbor in unseen:
                            unseen.remove(neighbor)
                            component.add(neighbor)
                            pending.append(neighbor)
            if all(edge.is_manifold for face in component for edge in face.edges):
                closed_components += 1
                volume6 = 0.0
                for face in component:
                    points = [vertex.co for vertex in face.verts]
                    for index in range(1, len(points) - 1):
                        volume6 += points[0].dot(points[index].cross(points[index + 1]))
                if volume6 < -1e-9:
                    negative_volume_components += 1
        return {
            "boundary_edges": boundary,
            "loose_edges": loose,
            "non_manifold_edges_including_boundary": non_manifold,
            "inconsistent_winding_edges": inconsistent_winding,
            "invalid_polygon_normals": invalid_normals,
            "zero_area_world_triangles": zero_area,
            "closed_connected_components": closed_components,
            "negative_signed_volume_closed_components": negative_volume_components,
        }
    finally:
        bm.free()


def geometry_hash(mesh):
    """Count identical evaluated local geometry once, separately from datablocks."""
    digest = hashlib.sha256()
    for vertex in mesh.vertices:
        digest.update((",".join(f"{c:.8g}" for c in vertex.co) + ";").encode("ascii"))
    for polygon in mesh.polygons:
        digest.update((str(tuple(polygon.vertices)) + ":" + str(polygon.material_index) + ";").encode("ascii"))
    return digest.hexdigest()


def bounds_from_points(points):
    if not points:
        return None
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    return {"min": rounded(minimum), "max": rounded(maximum),
            "dimensions": rounded([maximum[i] - minimum[i] for i in range(3)]),
            "center": rounded([(maximum[i] + minimum[i]) * 0.5 for i in range(3)])}


def inspect_lod(scene, objects, lod):
    select_lod(scene, objects, lod)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    parts = [obj for obj in objects if obj.type == "MESH" and get_lod(obj) == lod]
    records, all_points, geometry_hashes = [], [], set()
    source_data = {obj.data.as_pointer() for obj in parts}
    totals = Counter()
    for instance in depsgraph.object_instances:
        evaluated = instance.object
        if evaluated.type != "MESH":
            continue
        original = evaluated.original
        instance_lod = get_lod(original)
        if instance_lod is None and instance.parent:
            instance_lod = get_lod(instance.parent.original)
        if instance_lod != lod:
            continue
        mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
        if mesh is None:
            continue
        try:
            mesh.calc_loop_triangles()
            points = [instance.matrix_world @ vertex.co for vertex in mesh.vertices]
            all_points.extend(points)
            topology = mesh_topology(mesh, instance.matrix_world)
            geometry_hashes.add(geometry_hash(mesh))
            totals.update(topology)
            records.append({
                "name": original.name,
                "mesh_datablock": original.data.name if original.type == "MESH" else None,
                "is_generated_instance": bool(instance.is_instance),
                "persistent_id": list(instance.persistent_id) if instance.is_instance else None,
                "group": original.get("ff_group"),
                "triangles_after_modifiers": len(mesh.loop_triangles),
                "vertices_after_modifiers": len(mesh.vertices),
                "polygons_after_modifiers": len(mesh.polygons),
                "local_location": rounded(original.location),
                "local_scale": rounded(original.scale),
                "world_scale": rounded(instance.matrix_world.to_scale()),
                "world_matrix": matrix_rows(instance.matrix_world),
                "world_bounds": bounds_from_points(points),
                "world_determinant": round(instance.matrix_world.to_3x3().determinant(), 8),
                "material_slots": [material.name if material else None for material in mesh.materials],
                "modifiers": [{"name": mod.name, "type": mod.type,
                               "viewport": mod.show_viewport, "render": mod.show_render}
                              for mod in original.modifiers],
                "topology": topology,
            })
        finally:
            evaluated.to_mesh_clear()
    tris = sum(record["triangles_after_modifiers"] for record in records)
    return {
        "lod": lod, "source_mesh_objects": len(parts), "evaluated_draw_instances": len(records),
        "unique_source_mesh_datablocks": len(source_data),
        "unique_evaluated_local_geometries": len(geometry_hashes),
        "triangles_after_modifiers_including_repeats": tris,
        "vertices_after_modifiers_including_repeats": sum(record["vertices_after_modifiers"] for record in records),
        "world_bounds": bounds_from_points(all_points), "topology_totals_including_repeats": dict(totals),
        "objects": records,
    }


def material_inventory(objects):
    materials = {slot.material for obj in objects if obj.type == "MESH" and get_lod(obj) is not None
                 for slot in obj.material_slots if slot.material}
    images = set()
    material_records = []
    for material in sorted(materials, key=lambda item: item.name):
        principled = None
        if material.use_nodes and material.node_tree:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    images.add(node.image)
                if node.type == "BSDF_PRINCIPLED":
                    principled = node
        material_records.append({
            "name": material.name, "users": material.users, "diffuse_color": rounded(material.diffuse_color),
            "metallic": float(principled.inputs["Metallic"].default_value) if principled else float(material.metallic),
            "roughness": float(principled.inputs["Roughness"].default_value) if principled else float(material.roughness),
            "base_color": rounded(principled.inputs["Base Color"].default_value) if principled else None,
            "node_types": sorted(node.type for node in material.node_tree.nodes) if material.use_nodes else [],
        })
    image_records = []
    for img in sorted(images, key=lambda item: item.name):
        path = bpy.path.abspath(img.filepath, library=img.library) if img.filepath else None
        packed = bool(img.packed_file or img.packed_files)
        image_records.append({
            "name": img.name, "size_pixels": list(img.size), "channels": img.channels,
            "source": img.source, "packed": packed, "path": path,
            "external_exists": Path(path).is_file() if path and not packed else None,
        })
    return {"count": len(materials), "materials": material_records,
            "texture_count": len(images), "textures": image_records}


def inspect(args):
    mode = "fbx" if args.fbx is not None else "source"
    input_path = Path(args.fbx or args.source or SOURCE).resolve()
    if not input_path.is_file():
        raise FileNotFoundError(f"{mode} input does not exist: {input_path}")
    if mode == "source":
        if input_path.suffix.lower() != ".blend":
            raise ValueError(f"Expected a .blend source, received: {input_path}")
        bpy.ops.wm.open_mainfile(filepath=str(input_path), load_ui=False)
        scene = bpy.data.scenes.get("FusionFrigate_Authoring")
        if scene is None:
            raise ValueError("Saved source has no FusionFrigate_Authoring scene.")
        bpy.context.window.scene = scene
    else:
        if input_path.suffix.lower() != ".fbx":
            raise ValueError(f"Expected an .fbx input, received: {input_path}")
        scene = bpy.data.scenes.new("FusionFrigate_FBX_Inspection")
        bpy.context.window.scene = scene
        bpy.ops.import_scene.fbx(filepath=str(input_path), use_custom_props=True)
    objects = list(scene.objects)
    lod_objects = [obj for obj in objects if obj.type == "MESH" and get_lod(obj) is not None]
    before_visibility = [{"name": obj.name, "lod": get_lod(obj), "visible": obj.visible_get(),
                          "hide_render": obj.hide_render, "hide_viewport": obj.hide_viewport,
                          "hide_set": obj.hide_get()} for obj in lod_objects]
    initial_collections = [{"name": layer.collection.name, "exclude": layer.exclude,
                           "hide_viewport": layer.hide_viewport,
                           "collection_hide_viewport": layer.collection.hide_viewport,
                           "collection_hide_render": layer.collection.hide_render}
                          for layer in walk_layers(bpy.context.view_layer.layer_collection)]
    root = scene.objects.get("FusionFrigate")
    socket_objects = [obj for obj in objects if "Exhaust" in obj.name]
    sockets = [{"name": obj.name, "type": obj.type,
                "world_position": rounded(obj.matrix_world.translation),
                "world_matrix": matrix_rows(obj.matrix_world), "local_scale": rounded(obj.scale)}
               for obj in socket_objects]
    inventory = material_inventory(objects)
    report = {
        "mode": mode, "input": str(input_path), "blender_version": bpy.app.version_string,
        "inspector_version": "1.0", "timestamp_utc": datetime.now(timezone.utc).isoformat(),
        "input_saved_by_inspector": False, "scene": scene.name,
        "units": {"system": scene.unit_settings.system, "scale_length": scene.unit_settings.scale_length,
                  "length_unit": scene.unit_settings.length_unit},
        "axis_note": "World axes as loaded in Blender; no guessed coordinate correction was applied.",
        "bounds_tolerance": TOLERANCE,
        "root": {"name": root.name, "type": root.type, "world_matrix": matrix_rows(root.matrix_world),
                 "world_position": rounded(root.matrix_world.translation)} if root else None,
        "initial_visibility": before_visibility, "initial_collections": initial_collections,
        "sockets": sockets, "resources": inventory,
        "all_lods_unique_mesh_datablocks": len({obj.data.as_pointer() for obj in lod_objects}),
        "lods": [inspect_lod(scene, objects, lod) for lod in (0, 1, 2)],
        "notes": [
            "Triangles include evaluated modifiers and every repeated mesh placement in each LOD.",
            "Unique source mesh datablocks and unique evaluated geometry hashes are separate measurements.",
            "Topology diagnostics do not prove absence of self-intersection, z-fighting, or artistic shading defects.",
            "Signed volume only flags closed connected components; inward-facing open surfaces need visual inspection.",
            "No GPU/CPU resident memory measurement was performed; file size and geometry are not memory usage.",
        ],
    }
    errors = []
    for lod in report["lods"]:
        index, tris = lod["lod"], lod["triangles_after_modifiers_including_repeats"]
        if not lod["source_mesh_objects"] or not lod["evaluated_draw_instances"]:
            errors.append(f"LOD{index} is missing or has no evaluated meshes.")
        if not (12000 <= tris <= 18000 if index == 0 else 0 < tris <= {1: 6000, 2: 1500}[index]):
            errors.append(f"LOD{index} triangle budget failed: {tris}.")
    first_bounds = report["lods"][0]["world_bounds"]
    if first_bounds:
        for lod in report["lods"][1:]:
            bounds = lod["world_bounds"]
            if bounds and any(abs(first_bounds[key][axis] - bounds[key][axis]) > TOLERANCE
                              for key in ("min", "max") for axis in range(3)):
                errors.append(f"LOD{lod['lod']} world bounds differ from LOD0 beyond {TOLERANCE}.")
    if inventory["count"] > 4:
        errors.append(f"Material limit exceeded: {inventory['count']}.")
    if inventory["texture_count"] > 1:
        errors.append(f"Texture limit exceeded: {inventory['texture_count']}.")
    for texture in inventory["textures"]:
        if max(texture["size_pixels"], default=0) > 1024:
            errors.append(f"Texture exceeds 1024 resolution: {texture['name']}.")
        if texture["external_exists"] is False:
            errors.append(f"Missing external texture: {texture['path']}.")
    if mode == "source":
        if bpy.data.collections.get("FusionFrigate") is None:
            errors.append("Missing top-level FusionFrigate collection.")
        if root is None:
            errors.append("Missing FusionFrigate root object; origin cannot be checked.")
        elif root.matrix_world.translation.length > TOLERANCE:
            errors.append("FusionFrigate root world origin is not (0, 0, 0).")
        for lod in (0, 1, 2):
            if bpy.data.collections.get(f"FusionFrigate_LOD{lod}") is None:
                errors.append(f"Missing FusionFrigate_LOD{lod} collection.")
        for state in before_visibility:
            if state["visible"] != (state["lod"] == 0):
                errors.append(f"Default visibility must show only LOD0: {state['name']}.")
        for obj in lod_objects:
            if obj.get("ff_lod") not in (0, 1, 2):
                errors.append(f"Missing/invalid ff_lod property: {obj.name}.")
            if obj.get("ff_group") not in GROUPS:
                errors.append(f"Missing/invalid ff_group property: {obj.name}.")
        for material in inventory["materials"]:
            if material["name"] not in MATERIAL_NAMES:
                errors.append(f"Unexpected source material: {material['name']}.")
    report["checks"] = {"passed": not errors, "errors": errors,
                        "topology_is_advisory": True,
                        "fbx_axes_and_socket_poses_require_comparison": mode == "fbx"}
    return report


def main():
    args = args_from_blender()
    mode = "fbx" if args.fbx is not None else "source"
    output = (args.output or EVIDENCE / f"inspect-{mode}.json").resolve()
    try:
        report = inspect(args)
    except Exception as error:
        report = {"mode": mode, "blender_version": bpy.app.version_string,
                  "checks": {"passed": False, "errors": [f"{type(error).__name__}: {error}"]},
                  "traceback": traceback.format_exc(), "input_saved_by_inspector": False}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("FUSION_FRIGATE_INSPECTION " + json.dumps({"output": str(output), **report["checks"]}, ensure_ascii=False))
    if not report["checks"]["passed"]:
        raise RuntimeError("FusionFrigate inspection failed; read JSON evidence: " + str(output))


if __name__ == "__main__":
    main()
