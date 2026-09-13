"""Check actual nozzle openings and linked resources; render the reimported FBX."""
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).parent))
import build_fusion_frigate as author

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/verification/FusionFrigate'


def show_lod(lod):
    def walk(layer):
        layer.exclude = False
        layer.collection.hide_render = False
        layer.hide_viewport = False
        for child in layer.children:
            walk(child)
    walk(bpy.context.view_layer.layer_collection)
    for obj in bpy.context.scene.objects:
        if 'ff_lod' in obj:
            obj.hide_set(obj['ff_lod'] != lod)
            obj.hide_render = obj['ff_lod'] != lod
    bpy.context.view_layer.update()


def openings():
    report = {}
    for lod in range(3):
        show_lod(lod)
        graph = bpy.context.evaluated_depsgraph_get()
        objects = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.get('ff_lod') == lod]
        vertices, faces, materials = [], [], []
        for obj in objects:
            evaluated = obj.evaluated_get(graph)
            mesh = evaluated.to_mesh()
            mesh.calc_loop_triangles()
            base = len(vertices)
            vertices.extend(evaluated.matrix_world @ v.co for v in mesh.vertices)
            for triangle in mesh.loop_triangles:
                faces.append(tuple(base + i for i in triangle.vertices))
                materials.append(mesh.materials[mesh.polygons[triangle.polygon_index].material_index].name)
            evaluated.to_mesh_clear()
        tree = BVHTree.FromPolygons(vertices, faces, all_triangles=True)
        sockets = [o for o in bpy.context.scene.objects if o.get('ff_socket')]
        assert len(sockets) == 5
        results = []
        for socket in sockets:
            origin = socket.matrix_world.translation
            mouth_radius = 1.30 if socket.name == 'MainExhaust' else 1.30 * .445
            rays = [(0, 0)] + [(mouth_radius * .55 * math.cos(j * math.pi / 4),
                               mouth_radius * .55 * math.sin(j * math.pi / 4)) for j in range(8)]
            hits = []
            for dx, dz in rays:
                start = origin + Vector((dx, -.02, dz))
                point, normal, index, distance = tree.ray_cast(start, Vector((0, 1, 0)), 6)
                assert point is not None, f'LOD{lod} {socket.name}: missing interior wall/core'
                assert distance > .18, f'LOD{lod} {socket.name}: aperture blocked near lip'
                assert tree.ray_cast(start, Vector((0, -1, 0)), 6)[0] is None, 'Exhaust exit obstructed'
                hits.append({'offset': [dx, dz], 'depth_from_lip': distance - .02,
                             'first_material': materials[index]})
            assert hits[0]['first_material'] == 'FF_BlueGray', 'Center ray should reach recessed core'
            results.append({'socket': socket.name, 'tests': hits, 'exit_clear': True})
        sharing = {}
        for token, count in [('AuxiliaryDrive_', 4), ('Turret_', 3)]:
            parts = [o for o in objects if token in o.name]
            unique = len({o.data.as_pointer() for o in parts})
            assert len(parts) == count and unique == 1
            sharing[token] = {'instances': len(parts), 'unique_meshes': unique}
        report[f'LOD{lod}'] = {'openings': results, 'mesh_sharing': sharing}
    return report


def main():
    output = {}
    for name, path in [('source', author.BLEND),
                       ('fbx_reimport', EVIDENCE / 'FusionFrigate_FBX_Roundtrip.blend')]:
        bpy.ops.wm.open_mainfile(filepath=str(path))
        output[name] = openings()
        if name == 'fbx_reimport':
            show_lod(0)
            scene = bpy.context.scene
            author.setup_preview(scene)
            for view, location, target, scale in [
                ('ThreeQuarter', (27, 30, 21), (0, 0, .15), 26),
                ('Rear', (0, -38, 0), (0, -5, .25), 10.8),
                ('DriveDetail', (13, -22, 9), (0, -7.65, -.10), 12.5),
            ]:
                author.point_camera(scene, location, target, scale)
                scene.render.filepath = str(EVIDENCE / f'FBX_LOD0_{view}.png')
                bpy.ops.render.render(write_still=True)
    output['passed'] = True
    output['limits'] = 'Nine aperture rays per nozzle per LOD, plus opposing exit rays; not an exhaustive intersection proof.'
    (EVIDENCE / 'nozzle_and_sharing_checks.json').write_text(json.dumps(output, indent=2), encoding='utf-8')
    print('NOZZLE_AND_SHARING_CHECKS_PASSED')


if __name__ == '__main__':
    main()
