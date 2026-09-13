"""Explicit FBX staging and independent Blender round-trip verification.

Only writes FusionFrigate-owned paths outside Assets. Source is opened read-only.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from bpy_extras.io_utils import axis_conversion

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Blender/Ships/FusionFrigate'
OUT = ROOT / 'ArtSource/Exports/Ships/FusionFrigate'
EVIDENCE = ROOT / 'docs/verification/FusionFrigate'
SOURCE = ART / 'FusionFrigate.blend'
FBX = OUT / 'FusionFrigate.fbx'
OPTIONS = dict(use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1.0,
               apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
               axis_forward='-Z', axis_up='Y', use_space_transform=True,
               bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
               add_leaf_bones=False, bake_anim=False, path_mode='AUTO', use_custom_props=True)


def rows(matrix):
    return [[float(v) for v in row] for row in matrix]


def activate_all():
    def walk(layer):
        layer.exclude = False
        layer.hide_viewport = False
        layer.collection.hide_viewport = False
        for child in layer.children:
            walk(child)
    walk(bpy.context.view_layer.layer_collection)
    bpy.context.view_layer.update()


def triangulate(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def empty(name, collection, parent=None):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.parent = parent
    return obj


def export():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    activate_all()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    source_objects = [o for o in bpy.context.scene.objects if o.type == 'MESH' and 'ff_lod' in o]
    source_sockets = [o for o in bpy.context.scene.objects if o.get('ff_socket')]
    socket_expected = {o.name: rows(o.matrix_world) for o in source_sockets}
    # Free only the desired container names in memory. The .blend source is never saved.
    for obj in list(bpy.context.scene.objects):
        if obj.type == 'EMPTY':
            obj.name = '__source_' + obj.name
    scene = bpy.data.scenes.new('FF_Export_Staging')
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    col = bpy.data.collections.new('FF_Export_Objects')
    scene.collection.children.link(col)
    cache, copies = {}, []
    for source in source_objects:
        key = (source.data.as_pointer(), tuple((m.type, m.name) for m in source.modifiers))
        mesh = cache.get(key)
        if mesh is None:
            mesh = bpy.data.meshes.new_from_object(source.evaluated_get(depsgraph),
                        preserve_all_data_layers=True, depsgraph=depsgraph)
            triangulate(mesh)
            cache[key] = mesh
        obj = bpy.data.objects.new(source.name.replace('FF_', 'FusionFrigate_', 1), mesh)
        col.objects.link(obj)
        obj.matrix_world = source.matrix_world.copy()
        obj['ff_lod'], obj['ff_group'] = source['ff_lod'], source['ff_group']
        copies.append(obj)
    bpy.context.window.scene = scene
    root = empty('FusionFrigate', col)
    root['ff_export_root'] = True
    root['ff_axis_contract'] = '+Y forward +Z up in Blender; calibrated Unity +Z forward +Y up'
    groups = {}
    for lod in range(3):
        parent = empty(f'FusionFrigate_LOD{lod}', col, root)
        parent['ff_lod'] = lod
        parent['ff_lod_container'] = True
        groups[lod] = parent
    # Merge static structural parts. Keep auxiliaries, turrets and repeated armour linked.
    retained = ('AuxiliaryDrive_', 'Turret_', 'ArmorStation_')
    for lod in range(3):
        for group in ('Hull', 'Superstructure', 'FusionDrive', 'Details'):
            members = [o for o in scene.objects if o.type == 'MESH' and o.get('ff_lod') == lod and o.get('ff_group') == group
                       and not any(token in o.name for token in retained)]
            if not members:
                continue
            bpy.ops.object.select_all(action='DESELECT')
            for obj in members:
                obj.select_set(True)
            bpy.context.view_layer.objects.active = members[0]
            if len(members) > 1:
                bpy.ops.object.join()
            joined = bpy.context.view_layer.objects.active
            joined.name = f'FusionFrigate_LOD{lod}_{group}'
            joined['ff_lod'], joined['ff_group'] = lod, group
            joined.parent = groups[lod]
        for obj in list(scene.objects):
            if obj.type == 'MESH' and obj.get('ff_lod') == lod and obj.parent is None:
                obj.parent = groups[lod]
    sockets = empty('Sockets', col, root)
    sockets['ff_socket_container'] = True
    for name, matrix in socket_expected.items():
        socket = empty(name, col, sockets)
        socket.matrix_world = Matrix(matrix)
        socket['ff_socket'] = True
        socket['ff_forward'] = 'local +Z exhaust'
    bpy.context.view_layer.update()
    # Exporter source: fbx_utils.ObjectWrapper.fbx_object_matrix, lines 1802-1813,
    # recursively mixes the parent's Blender local and FBX local matrices.
    # With this exact two-level hierarchy and identity authored containers, the
    # default reimport gives G^-1 W for meshes and G^-1 S G^-1 for socket empties.
    # Thus disposable staging copies use G W / G S G; identity containers use G.
    # Verified numerically against source WORLD vertices and complete socket poses.
    # The source and any gameplay roots are never modified or guessed rotations.
    global_matrix = axis_conversion(to_forward='-Z', to_up='Y').to_4x4()
    originals = {obj: obj.matrix_world.copy() for obj in scene.objects}
    def depth(obj):
        return depth(obj.parent) + 1 if obj.parent else 0
    for obj in sorted(scene.objects, key=depth):
        if obj.type == 'MESH':
            assert depth(obj) == 2, 'Exporter correction requires Root / LOD / Mesh.'
            obj.matrix_world = global_matrix @ originals[obj]
        elif obj.get('ff_socket'):
            assert depth(obj) == 2, 'Exporter correction requires Root / Sockets / Socket.'
            obj.matrix_world = global_matrix @ originals[obj] @ global_matrix
        else:
            assert max(abs(originals[obj][i][j] - Matrix.Identity(4)[i][j])
                       for i in range(4) for j in range(4)) < 1e-6
            obj.matrix_world = global_matrix
        bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(FBX), **OPTIONS)
    manifest = {'source_sha256': source_hash, 'fbx_sha256': hashlib.sha256(FBX.read_bytes()).hexdigest(),
                'options': {k: sorted(v) if isinstance(v, set) else v for k, v in OPTIONS.items()},
                'export_axis_matrix_G': rows(global_matrix),
                'hierarchy_correction': 'identity containers=G; depth-2 mesh=G W; depth-2 socket=G S G',
                'sockets_expected_blender': socket_expected,
                'lods': {}, 'scene_objects': len(scene.objects)}
    for lod in range(3):
        objects = [o for o in scene.objects if o.type == 'MESH' and o.get('ff_lod') == lod]
        for obj in objects:
            obj.data.calc_loop_triangles()
        manifest['lods'][f'LOD{lod}'] = {
            'mesh_objects': len(objects), 'unique_meshes': len({o.data.as_pointer() for o in objects}),
            'triangles': sum(len(o.data.loop_triangles) for o in objects),
            'objects': [{'name': o.name, 'mesh': o.data.name,
                         'matrix_world_source': rows(originals[o]) if o in originals else rows(o.matrix_world)} for o in objects]}
    (OUT / 'FusionFrigate_export_manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    (OUT / 'FusionFrigate_sockets.json').write_text(json.dumps({
        'authoring_unit': 'metre', 'axes': 'Blender +Y forward +Z up',
        'socket_forward': 'local +Z', 'sockets': socket_expected}, indent=2), encoding='utf-8')
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == source_hash
    print('FUSION_EXPORT_COMPLETE', json.dumps(manifest['lods']))


def verify():
    # A new background process and a new scene hold only the imported deliverable.
    for name, expected_type in [('Cube', 'MESH'), ('Camera', 'CAMERA'), ('Light', 'LIGHT')]:
        obj = bpy.data.objects.get(name)
        if obj and obj.type == expected_type:
            bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.name = 'FusionFrigate_FBX_Roundtrip'
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_anim=False, use_custom_normals=True,
                             use_image_search=False)
    bpy.context.view_layer.update()
    expected = json.loads((OUT / 'FusionFrigate_export_manifest.json').read_text(encoding='utf-8'))
    source = json.loads((EVIDENCE / 'build_manifest.json').read_text(encoding='utf-8'))
    errors, report = [], {'lods': {}, 'sockets': {}, 'axis_checks': {}, 'materials': {}}
    root = next(o for o in scene.objects if o.get('ff_export_root'))
    report['root_matrix'] = rows(root.matrix_world)
    report['root_identity_max_error'] = max(abs(root.matrix_world[i][j] - Matrix.Identity(4)[i][j]) for i in range(4) for j in range(4))
    # Blender's imported FBX root may carry its native coordinate conversion.
    # Geometry and socket WORLD transforms below establish the actual contract.
    for lod in range(3):
        objects = [o for o in scene.objects if o.type == 'MESH' and o.get('ff_lod') == lod]
        points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
        lo = [min(v[i] for v in points) for i in range(3)]
        hi = [max(v[i] for v in points) for i in range(3)]
        for obj in objects:
            obj.data.calc_loop_triangles()
        actual = {'min': lo, 'max': hi, 'dimensions': [hi[i] - lo[i] for i in range(3)],
                  'triangles': sum(len(o.data.loop_triangles) for o in objects),
                  'mesh_objects': len(objects), 'unique_meshes': len({o.data.as_pointer() for o in objects})}
        ref = source['stats'][f'LOD{lod}']
        actual['bounds_max_error'] = max(abs(actual[k][i] - ref[k][i]) for k in ('min', 'max') for i in range(3))
        if actual['bounds_max_error'] > 1e-4 or actual['triangles'] != ref['triangles']:
            errors.append(f'LOD{lod}: size, position or triangle mismatch.')
        report['lods'][f'LOD{lod}'] = actual
        # Different ends are recognized by actual world-space geometry, not an AABB alone.
        nose = [p for p in points if p.y > 10.95]
        report['axis_checks'][f'LOD{lod}_narrow_bow_at_positive_Y'] = bool(nose) and max(abs(p.x) for p in nose) < .15
        report['axis_checks'][f'LOD{lod}_mast_at_positive_Z'] = max(p.z for p in points) > 3.47
    for name, values in expected['sockets_expected_blender'].items():
        obj = bpy.data.objects.get(name)
        if obj is None:
            errors.append(f'Missing socket {name}')
            continue
        delta = max(abs(obj.matrix_world[i][j] - values[i][j]) for i in range(4) for j in range(4))
        axis = obj.matrix_world.to_3x3() @ Vector((0, 0, 1))
        report['sockets'][name] = {'matrix': rows(obj.matrix_world), 'max_error': delta,
                                 'outward_direction': list(axis),
                                 'position': list(obj.matrix_world.translation)}
        if delta > 1e-4:
            errors.append(f'Socket pose mismatch: {name}')
    if not all(report['axis_checks'].values()):
        errors.append('Positive-Y bow / positive-Z mast check failed.')
    used_materials = {m for o in scene.objects if o.type == 'MESH' for m in o.data.materials if m}
    for material in used_materials:
        shader = material.node_tree.nodes.get('Principled BSDF')
        report['materials'][material.name] = {'base_color': list(shader.inputs['Base Color'].default_value),
                  'metallic': float(shader.inputs['Metallic'].default_value),
                  'roughness': float(shader.inputs['Roughness'].default_value)}
    if {m.name for m in used_materials} != {'FF_Armor', 'FF_Structure', 'FF_EngineMetal', 'FF_BlueGray'}:
        errors.append('Four named material references did not survive roundtrip.')
    report['unexpected_export_types'] = [o.name for o in scene.objects if o.type not in {'MESH', 'EMPTY'}]
    if report['unexpected_export_types']:
        errors.append('Preview objects leaked into FBX.')
    report['errors'], report['passed'] = errors, not errors
    (EVIDENCE / 'roundtrip_comparison.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    if errors:
        print('ROUNDTRIP_FAILED', json.dumps(errors))
        raise RuntimeError('FBX roundtrip failed. See roundtrip_comparison.json')
    # Create clear LOD collections for the temporary reimport inspection file.
    for lod in range(3):
        col = bpy.data.collections.new(f'FusionFrigate_LOD{lod}')
        scene.collection.children.link(col)
        for obj in list(scene.objects):
            if obj.get('ff_lod') == lod:
                for old in list(obj.users_collection):
                    old.objects.unlink(obj)
                col.objects.link(obj)
        col.hide_render = lod != 0
    for layer in bpy.context.view_layer.layer_collection.children:
        if layer.name.startswith('FusionFrigate_LOD'):
            layer.exclude = layer.name != 'FusionFrigate_LOD0'
    for obj in scene.objects:
        if obj.get('ff_socket'):
            obj.hide_set(True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(EVIDENCE / 'FusionFrigate_FBX_Roundtrip.blend'))
    print('FUSION_ROUNDTRIP_COMPLETE', json.dumps(report['lods']))


if __name__ == '__main__':
    args = argparse.ArgumentParser()
    args.add_argument('--verify', action='store_true')
    cfg = args.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for path in (OUT, EVIDENCE):
        path.mkdir(parents=True, exist_ok=True)
    verify() if cfg.verify else export()
