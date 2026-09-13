"""Audit actual generated source meshes without modifying or exporting them."""
import json
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

root = Path(__file__).resolve().parents[2]
art = root/'ArtSource'/'Blender'
bpy.ops.wm.open_mainfile(filepath=str(art/'fleet_assets.blend'))
for layer in bpy.context.view_layer.layer_collection.children:
    layer.exclude = False
bpy.context.view_layer.update()
depsgraph = bpy.context.evaluated_depsgraph_get()
report = {}
for title in ('Droplet','Frigate','Cruiser','Command','FrigateWreck','CruiserWreck','CommandWreck'):
    collection = bpy.data.collections[title+'_Source']
    entries, all_bounds = [], []
    for obj in collection.objects:
        if obj.type != 'MESH':
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        bounds = [evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box]
        all_bounds.extend(bounds)
        lo = [min(v[i] for v in bounds) for i in range(3)]
        hi = [max(v[i] for v in bounds) for i in range(3)]
        size = [hi[i]-lo[i] for i in range(3)]
        center = [(hi[i]+lo[i])*.5 for i in range(3)]
        bm = bmesh.new()
        bm.from_mesh(mesh)
        entries.append(dict(name=obj.name,
                            unity_center=[center[0],center[2],center[1]],
                            unity_size=[size[0],size[2],size[1]],
                            unity_piece_pivot=[obj.location.x,obj.location.z,obj.location.y],
                            boundary_edges=sum(edge.is_boundary for edge in bm.edges),
                            nonmanifold_edges=sum(not edge.is_manifold for edge in bm.edges),
                            signed_volume=bm.calc_volume(signed=True),
                            material_slots=[mat.name for mat in obj.data.materials]))
        bm.free()
        evaluated.to_mesh_clear()
    lo = [min(v[i] for v in all_bounds) for i in range(3)]
    hi = [max(v[i] for v in all_bounds) for i in range(3)]
    size = [hi[i]-lo[i] for i in range(3)]
    center = [(hi[i]+lo[i])*.5 for i in range(3)]
    report[title] = dict(unity_center=[center[0],center[2],center[1]],
                         unity_size=[size[0],size[2],size[1]],meshes=entries)
(art/'model_geometry_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SOURCE_GEOMETRY_AUDIT_COMPLETE')
