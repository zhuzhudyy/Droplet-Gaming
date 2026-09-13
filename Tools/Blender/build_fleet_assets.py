"""Original DropletPrototype models and authored fleet. Run with Blender 5.2.

blender --background --factory-startup --python Tools/Blender/build_fleet_assets.py -- --stage calibration
blender --background --factory-startup --python Tools/Blender/build_fleet_assets.py -- --stage full

All writes are confined to ArtSource/Blender; Unity imports are coordinated separately.
Authoring uses metres, +Y forward, +Z up. FBX conversion is verified by calibration.
"""
import argparse
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource" / "Blender"
EXPORT = ART / "Exports"
PREVIEW = ART / "Previews"
for directory in (ART, EXPORT, PREVIEW):
    directory.mkdir(parents=True, exist_ok=True)

MATS = {}
EXPORT_OPTIONS = dict(use_selection=True, object_types={'MESH', 'EMPTY'},
                      global_scale=1.0, apply_unit_scale=True,
                      apply_scale_options='FBX_SCALE_UNITS',
                      axis_forward='-Z', axis_up='Y', use_space_transform=True,
                      bake_space_transform=True, mesh_smooth_type='FACE',
                      use_mesh_modifiers=True, add_leaf_bones=False,
                      bake_anim=False, path_mode='AUTO', use_custom_props=True)


def reset():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != 'Collection':
            bpy.data.collections.remove(collection)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.preferences.filepaths.save_version = 0


def material(name, color, metallic=0.0, roughness=.4, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    shader.inputs['Emission Color'].default_value = (*color, 1)
    shader.inputs['Emission Strength'].default_value = emission
    MATS[name] = mat
    return mat


def palette():
    material('Hull', (.19, .27, .34), .68, .32)
    material('Armor', (.53, .62, .67), .65, .28)
    material('Trim', (.045, .074, .105), .75, .36)
    material('Engine', (.12, .67, 1.0), .35, .23, 3.0)
    material('DropletMetal', (.82, .88, .95), 1.0, .12)
    material('Warm', (1.0, .27, .045), .25, .3, 1.5)


def new_collection(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to(obj, collection):
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def empty(name, collection, position=(0, 0, 0), parent=None):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = position
    obj.empty_display_type = 'ARROWS'
    obj.empty_display_size = 3
    obj.parent = parent
    return obj


def finish_mesh(obj, collection, parent, mat, bevel=0, smooth=False):
    move_to(obj, collection)
    obj.parent = parent
    obj.data.materials.append(MATS[mat])
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Machined edge radii', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    else:
        mod = obj.modifiers.new('Weighted panel normals', 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
    obj.select_set(False)
    return obj


def box(name, position, size, mat, collection, parent, bevel=.12):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    return finish_mesh(obj, collection, parent, mat, bevel)


def hull(name, rings, mat, collection, parent, bevel=.12):
    """Four-point rectangular cross-sections, ordered from stern to bow."""
    verts = []
    for y, halfwidth, bottom, top in rings:
        verts.extend([(-halfwidth, y, bottom), (halfwidth, y, bottom),
                      (halfwidth, y, top), (-halfwidth, y, top)])
    faces = [(0, 1, 2, 3)]
    for r in range(len(rings) - 1):
        a, b = 4 * r, 4 * (r + 1)
        for i in range(4):
            faces.append((a + i, b + i, b + (i + 1) % 4, a + (i + 1) % 4))
    end = 4 * (len(rings) - 1)
    faces.append((end + 3, end + 2, end + 1, end))
    mesh = bpy.data.meshes.new(name + 'Mesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    return finish_mesh(obj, collection, parent, mat, bevel)


def nozzle(name, position, radius, depth, collection, parent):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=depth,
                                      location=position, rotation=(math.pi / 2, 0, 0))
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, collection, parent, 'Engine', .06)


def frigate(collection=None):
    col = collection or new_collection('Frigate_Source')
    root = empty('Frigate', col)
    root['ship_type'] = 'Small'
    root['forward_axis'] = '+Y'
    hull('Keel', [(-10, 1.55, -1.55, .8), (-3, 2.1, -1.7, 1.4),
                  (6, 1.45, -.65, .85), (11, .08, -.05, .12)], 'Hull', col, root)
    hull('DorsalArmor', [(-5, 1.7, .8, 1.9), (1, 1.65, 1.1, 2.1),
                         (7, .65, .65, 1.25)], 'Armor', col, root)
    box('WaistSpar', (0, -3.3, -.25), (8.7, 2.0, .8), 'Trim', col, root)
    for side in (-1, 1):
        pod = hull('PortDrive' if side < 0 else 'StarboardDrive',
                   [(-11, 1.05, -1.4, .85), (-3, 1.05, -1.1, 1.15),
                    (2.8, .45, -.45, .65)], 'Armor', col, root)
        pod.location.x = side * 3.45
        box('DriveInset', (side * 3.45, -4, 1.12), (1.15, 4.2, .13), 'Trim', col, root, .02)
        nozzle('IonDrive', (side * 3.45, -11.05, -.1), .71, .18, col, root)
        box('NavigationStrip', (side * 1.78, .3, 1.67), (.08, 3.6, .11), 'Engine', col, root, .015)
        fin = box('Stabilizer', (side * 3.5, -6.3, 1.65), (.24, 2.5, 2.2), 'Hull', col, root)
        fin.rotation_euler.y = side * .14
    for y in (-3.1, -.6, 1.9):
        box('SpinePanel', (0, y, 2.12), (1.0, 1.4, .12), 'Trim', col, root, .03)
    return col, root


def cruiser(command=False):
    title = 'Command' if command else 'Cruiser'
    col = new_collection(title + '_Source')
    root = empty(title, col)
    root['ship_type'] = 'Command' if command else 'Large'
    hull('ArmoredCore', [(-16, 4.5, -2.7, 1.8), (-7, 6.0, -3.3, 2.6),
                         (7, 3.9, -2.2, 1.8), (17, 1.0, -.5, .65)], 'Hull', col, root, .22)
    hull('DorsalCitadel', [(-11, 3.6, 1.5, 4.0), (-2, 4.0, 2.0, 4.6),
                           (5, 2.3, 1.6, 3.0)], 'Armor', col, root, .2)
    box('CrossBrace', (0, -6, -.4), (18.5, 4.0, 2.8), 'Trim', col, root, .22)
    for side in (-1, 1):
        outrigger = hull('PortLance' if side < 0 else 'StarboardLance',
                         [(-16, 2.0, -2.0, 1.8), (-3, 2.25, -2.2, 2.8),
                          (7.5, 1.25, -.9, 1.8), (14, .2, -.15, .35)],
                         'Armor', col, root, .18)
        outrigger.location.x = side * 7.1
        box('LanceSpine', (side * 7.1, -3.8, 2.7), (1.1, 8.5, .28), 'Hull', col, root)
        box('RadiatorBank', (side * 5.15, -11.6, 2.1), (.7, 6.8, .75), 'Trim', col, root)
        for y in (-13, -10.8, -8.6):
            box('RadiatorRib', (side * 5.15, y, 2.53), (1.35, .3, .18), 'Hull', col, root, .03)
        nozzle('HeavyIonDrive', (side * 7.1, -16.15, -.1), 1.3, .25, col, root)
        box('FleetBeacon', (side * 3.35, -2.6, 4.3), (.16, 4.8, .18), 'Engine', col, root, .03)
        box('SideArmor', (side * 4.6, 2.4, .45), (1.0, 6.5, 2.4), 'Armor', col, root, .13)
    nozzle('CentralDrive', (0, -16.1, -.3), 1.7, .25, col, root)
    for y in (-8, -4.5, -1):
        box('CitadelInset', (0, y, 4.64), (4.8, 1.0, .14), 'Trim', col, root, .025)
    if command:
        hull('CommandCrown', [(-8, 2.6, 4.0, 6.4), (-3, 3.0, 4.2, 6.8),
                              (2.5, 1.0, 3.1, 5.1)], 'Hull', col, root, .15)
        box('AntennaCrest', (0, -4.3, 7.0), (.4, 7.2, 1.1), 'Armor', col, root, .08)
        for side in (-1, 1):
            blade = hull('CommandWing', [(-13, 1.5, -.5, 1.0), (-2, 1.1, .0, 1.7),
                                         (5, .1, .4, .6)], 'Hull', col, root)
            blade.location.x = side * 9.7
            box('FlagshipSignal', (side * 2.25, -3.2, 6.75), (.15, 5, .2), 'Engine', col, root, .02)
        hull('CommandProw', [(11, 1.2, -.55, .7), (21, .1, -.05, .2)], 'Armor', col, root)
    return col, root


def droplet():
    col = new_collection('Droplet_Source')
    root = empty('Droplet', col)
    verts, faces = [(0, -1.2, 0)], []
    ring_count, radial = 56, 48
    # Rounded leading hemisphere and long taper towards a singular, closed tail.
    for j in range(1, ring_count):
        t = j / ring_count
        y = -1.2 + 2.4 * t
        peak = (5/7)**1.25 * (2/7)**.5
        radius = .6 * t**1.25 * (1-t)**.5 / peak
        for i in range(radial):
            a = 2 * math.pi * i / radial
            verts.append((radius * math.cos(a), y, radius * math.sin(a)))
    tip = len(verts)
    verts.append((0, 1.2, 0))
    for i in range(radial):
        faces.append((0, 1 + (i + 1) % radial, 1 + i))
    for j in range(ring_count - 2):
        a, b = 1 + j * radial, 1 + (j + 1) * radial
        for i in range(radial):
            faces.append((a + i, a + (i + 1) % radial, b + (i + 1) % radial, b + i))
    last = 1 + (ring_count - 2) * radial
    for i in range(radial):
        faces.append((last + i, last + (i + 1) % radial, tip))
    mesh = bpy.data.meshes.new('SeamlessDropletMesh')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new('SeamlessDroplet', mesh)
    col.objects.link(obj)
    obj.parent = root
    obj.data.materials.append(MATS['DropletMetal'])
    for p in mesh.polygons:
        p.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)
    return col, root


def marker_pose(position, yaw=0, pitch=0):
    yaw, pitch = math.radians(yaw), math.radians(pitch)
    forward = Vector((math.sin(yaw)*math.cos(pitch), -math.sin(pitch), math.cos(yaw)*math.cos(pitch)))
    up = Vector((math.sin(yaw)*math.sin(pitch), math.cos(pitch), math.cos(yaw)*math.sin(pitch)))
    right = Vector((math.cos(yaw), 0, -math.sin(yaw)))
    convert = lambda v: Vector((v.x, v.z, v.y))
    rot = Matrix((convert(right), convert(forward), convert(up))).transposed().to_4x4()
    rot.translation = convert(Vector(position))
    return rot, list(forward), list(up)


def spawn(col, root, name, position, yaw=0, pitch=0):
    obj = empty(name, col, parent=root)
    obj.matrix_local, forward, up = marker_pose(position, yaw, pitch)
    obj['stable_id'] = name.rsplit('_', 1)[1]
    obj['ship_type'] = name.split('_')[1]
    return obj, dict(name=name, position=list(position), forward=forward, up=up,
                     scale=[1, 1, 1], yaw=yaw, pitch=pitch)


def export_fbx(path, objects):
    bpy.context.view_layer.update()
    # Blender 5.2 FBX bake_space_transform converts meshes correctly but not Empty
    # transforms. Preserve authored Empties and export tiny mesh pose proxies.
    # They are deliberately discarded by the Unity fleet importer after reading
    # the imported world transform. Flat export avoids parent-conversion mixing.
    proxies, original_names = [], {}
    selected = []
    for obj in objects:
        if obj.type == 'EMPTY' and obj.name.startswith('SPAWN_'):
            name = obj.name
            original_names[obj] = name
            obj.name = 'SOURCE_' + name
            mesh = bpy.data.meshes.new(name + '_PoseProxy')
            mesh.from_pydata([(0, 0, 0), (.02, 0, 0), (0, .06, 0), (0, 0, .04)],
                             [], [(0, 2, 1), (0, 1, 3), (0, 3, 2), (1, 2, 3)])
            mesh.update()
            proxy = bpy.data.objects.new(name, mesh)
            bpy.context.scene.collection.objects.link(proxy)
            proxy.matrix_world = obj.matrix_world.copy()
            proxy['stable_id'] = obj['stable_id']
            proxy['ship_type'] = obj['ship_type']
            proxy['pose_proxy_only'] = True
            proxies.append(proxy)
            selected.append(proxy)
        else:
            selected.append(obj)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in selected:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = selected[0]
    bpy.ops.export_scene.fbx(filepath=str(path), **EXPORT_OPTIONS)
    bpy.ops.object.select_all(action='DESELECT')
    for proxy in proxies:
        mesh = proxy.data
        bpy.data.objects.remove(proxy, do_unlink=True)
        bpy.data.meshes.remove(mesh)
    for obj, name in original_names.items():
        obj.name = name


def mesh_report(collection):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    triangles = 0
    bounds = []
    for obj in collection.objects:
        if obj.type != 'MESH':
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        bounds.extend([evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box])
        evaluated.to_mesh_clear()
    minimum = [min(v[i] for v in bounds) for i in range(3)]
    maximum = [max(v[i] for v in bounds) for i in range(3)]
    size = [maximum[i]-minimum[i] for i in range(3)]
    return dict(triangles=triangles, blender_dimensions=size,
                expected_unity_dimensions=[size[0], size[2], size[1]],
                mesh_count=sum(o.type == 'MESH' for o in collection.objects))


def export_merged_model(path, collection, root):
    """Export copy combines authoring modules into one shared-material renderer."""
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    vertices, faces, materials, material_ids, smooth, normals = [], [], [], [], [], []
    for obj in collection.objects:
        if obj.type != 'MESH':
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        offset = len(vertices)
        matrix = root.matrix_world.inverted() @ evaluated.matrix_world
        normal_matrix = matrix.to_3x3().inverted().transposed()
        vertices.extend([tuple(matrix @ v.co) for v in mesh.vertices])
        for poly in mesh.polygons:
            faces.append(tuple(offset+i for i in poly.vertices))
            mat = mesh.materials[poly.material_index]
            if mat not in materials:
                materials.append(mat)
            material_ids.append(materials.index(mat))
            smooth.append(poly.use_smooth)
            for loop_index in poly.loop_indices:
                normals.append(tuple((normal_matrix @ mesh.corner_normals[loop_index].vector).normalized()))
        evaluated.to_mesh_clear()
    data = bpy.data.meshes.new(root.name+'_ExportCombinedMesh')
    data.from_pydata(vertices, [], faces)
    data.update()
    for mat in materials:
        data.materials.append(mat)
    for poly,index,is_smooth in zip(data.polygons,material_ids,smooth):
        poly.material_index,poly.use_smooth=index,is_smooth
    data.normals_split_custom_set(normals)
    obj = bpy.data.objects.new('VisualMesh', data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = root
    export_fbx(path,[root,obj])
    bpy.data.objects.remove(obj,do_unlink=True)
    bpy.data.meshes.remove(data)


def save(path):
    bpy.ops.wm.save_as_mainfile(filepath=str(path))


def preview(name, center, distance, resolution=(1400, 900)):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.world.color = (.11, .13, .17)
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.09, .13, .20, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .35
    col = new_collection('Preview_Rig')
    cdata = bpy.data.cameras.new('PreviewCamera')
    camera = bpy.data.objects.new('PreviewCamera', cdata)
    col.objects.link(camera)
    target = Vector(center)
    camera.location = target + Vector((.70, 1.1, .70)).normalized() * distance
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    cdata.type = 'ORTHO'
    cdata.ortho_scale = distance * .82
    scene.camera = camera
    for title, offset, energy, size, color in [
        ('Key', (0, 1, 1), 150000, 22, (.66, .80, 1)),
        ('Rim', (-1, -.3, .6), 110000, 18, (.32, .59, 1)),
        ('WarmFill', (1, -.5, .2), 60000, 16, (1, .74, .51)),
    ]:
        data = bpy.data.lights.new(title, 'AREA')
        data.energy, data.shape, data.size, data.color = energy, 'DISK', size, color
        obj = bpy.data.objects.new(title, data)
        col.objects.link(obj)
        obj.location = target + Vector(offset) * distance * .7
        obj.rotation_euler = (target-obj.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(PREVIEW / name)
    save(ART / ('calibration.blend' if name.startswith('calibration') else 'fleet_assets.blend'))
    bpy.ops.render.render(write_still=True)
    return col


def calibration():
    reset()
    palette()
    col = new_collection('Calibration_Export')
    root = empty('Calibration', col)
    box('Cube_1m', (-8, 0, 0), (1, 1, 1), 'Armor', col, root, 0)
    box('FRONT_stem', (0, 1.4, 0), (.14, 2.8, .14), 'Engine', col, root, 0)
    box('FRONT_TIP', (0, 3, 0), (.6, .6, .25), 'Engine', col, root, 0)
    box('UP_stem', (0, 0, 1), (.12, .12, 2), 'Warm', col, root, 0)
    box('UP_TIP', (0, 0, 2.2), (.25, .25, .5), 'Warm', col, root, 0)
    box('RIGHT_TIP', (1.5, 0, 0), (.4, .2, .2), 'Trim', col, root, 0)
    ship_col, ship_root = frigate()
    ship_root.location.x = 8
    marker_root = empty('CalibrationMarkers', col, parent=root)
    expected = []
    for args in [('SPAWN_Small_001', (0, 8, 100), 0, 0),
                 ('SPAWN_Small_002', (-20, 15, 140), 90, 0),
                 ('SPAWN_Small_003', (30, -5, 180), -35, 15)]:
        _, record = spawn(col, marker_root, *args)
        expected.append(record)
    export_fbx(EXPORT / 'calibration.fbx', list(col.objects) + list(ship_col.objects))
    report = dict(blender_version=bpy.app.version_string,
                  exporter='bundled io_scene_fbx', export_options={k:sorted(v) if isinstance(v,set) else v for k,v in EXPORT_OPTIONS.items()},
                  cube_center_unity=[-8,0,0], cube_dimensions_unity=[1,1,1],
                  front_tip_center_unity=[0,0,3], up_tip_center_unity=[0,2.2,0],
                  right_tip_center_unity=[1.5,0,0], frigate=mesh_report(ship_col),
                  frigate_root_position_unity=[8,0,0], markers=expected)
    (EXPORT / 'calibration_expected.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    save(ART / 'calibration.blend')
    # Hide faraway orientation markers only for model preview, never from FBX.
    preview('calibration-preview.png', (5, 0, 0), 38)
    print('CALIBRATION_READY ' + str(EXPORT / 'calibration.fbx'))


def wreck(source, name):
    """Six closed, pre-cut presentation pieces derived from the actual ship."""
    col = new_collection(name + '_Source')
    root = empty(name, col)
    vertices, faces, material_indices = [], [], []
    material_names = ['Hull', 'Armor', 'Trim', 'Engine']
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in source.objects:
        if obj.type != 'MESH':
            continue
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        offset = len(vertices)
        vertices.extend([tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices])
        for polygon in mesh.polygons:
            faces.append(tuple(offset+i for i in polygon.vertices))
            mat_name = obj.data.materials[polygon.material_index].name
            material_indices.append(material_names.index(mat_name))
        evaluated.to_mesh_clear()
    whole = bpy.data.meshes.new(name + '_Uncut')
    whole.from_pydata(vertices, [], faces)
    whole.update()
    for polygon, index in zip(whole.polygons, material_indices):
        polygon.material_index = index
    base = bmesh.new()
    base.from_mesh(whole)
    section_edges = [-100, -5.5, 4.0, 100]
    piece_id = 0
    for side in (-1, 1):
        for segment in range(3):
            mesh = base.copy()
            cuts = [(Vector((0,0,0)), Vector((-side,0,0))),
                    (Vector((0,section_edges[segment],0)), Vector((0,-1,0))),
                    (Vector((0,section_edges[segment+1],0)), Vector((0,1,0)))]
            for point, normal in cuts:
                bmesh.ops.bisect_plane(mesh, geom=list(mesh.verts)+list(mesh.edges)+list(mesh.faces),
                                      dist=.00001, plane_co=point, plane_no=normal,
                                      clear_outer=True, clear_inner=False)
                boundary = [edge for edge in mesh.edges if edge.is_boundary]
                if boundary:
                    filled = bmesh.ops.holes_fill(mesh, edges=boundary, sides=0)
                    for face in filled.get('faces', []):
                        face.material_index = 2  # Dark authored break surfaces.
            if not mesh.verts:
                mesh.free()
                continue
            piece_id += 1
            bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
            bmesh.ops.triangulate(mesh, faces=list(mesh.faces))
            minimum = Vector(tuple(min(v.co[i] for v in mesh.verts) for i in range(3)))
            maximum = Vector(tuple(max(v.co[i] for v in mesh.verts) for i in range(3)))
            center = (minimum+maximum)*.5
            for v in mesh.verts:
                v.co -= center
            data = bpy.data.meshes.new(name + '_Piece_%02d' % piece_id)
            mesh.to_mesh(data)
            mesh.free()
            obj = bpy.data.objects.new('Piece_%02d' % piece_id, data)
            col.objects.link(obj)
            obj.parent = root
            obj.location = center
            for mat_name in material_names:
                data.materials.append(MATS[mat_name])
            obj['presentation_only'] = True
            obj['drift_origin'] = list(center)
    base.free()
    bpy.data.meshes.remove(whole)
    return col, root


def fleet_layout(models):
    col = new_collection('FleetLayout_Markers')
    root = empty('FleetLayout', col)
    preview_col = new_collection('FleetLayout_EditablePreviewInstances')
    records = []
    groups = [
        ('Approach lane', [('Small', (0,8,z), 0, 0) for z in (100,145,190,235)]),
        ('Port diagonal lances', [
            ('Large' if i == 2 else 'Small', (-45-40*i, 8+27*r, 175+35*i+75*r), -42, -3*r)
            for r in range(2) for i in range(4)]),
        ('Starboard diverging lances', [
            ('Large' if i == 2 else 'Small', (50+40*i-10*r, -3-11*i+49*r, 185+37*i+78*r), 42, 3-7*r)
            for r in range(2) for i in range(4)]),
        ('Upper screening arc', [
            ('Large' if i in (1,5) else 'Small', p, -42+12*i, -8)
            for i,p in enumerate([(-140,70,350),(-100,83,390),(-55,90,420),(-10,95,445),
                                  (40,90,465),(85,80,450),(125,65,420),(160,50,380)])]),
        ('Lower defensive crescent', [
            ('Large' if i in (2,5) else 'Small', p, -50+14*i, 5)
            for i,p in enumerate([(-160,-24,350),(-120,-34,395),(-75,-38,420),(-25,-34,445),
                                  (25,-25,455),(75,-15,445),(120,-10,415),(165,-40,390)])]),
        ('Command focus', [('Large',(-65,34,500),-14,0),('Large',(65,34,500),14,0),
                           ('Small',(0,78,520),0,5),('Command',(0,30,520),0,0)]),
    ]
    counter = 0
    for group, entries in groups:
        for ship_type, position, yaw, pitch in entries:
            counter += 1
            marker, record = spawn(col, root, 'SPAWN_%s_%03d' % (ship_type,counter), position,yaw,pitch)
            record['group'] = group
            records.append(record)
            instance = empty('PREVIEW_%03d_%s' % (counter,ship_type), preview_col)
            instance.instance_type = 'COLLECTION'
            instance.instance_collection = models[ship_type]
            bpy.context.view_layer.update()
            instance.matrix_world = marker.matrix_world.copy()
            instance['linked_marker'] = marker.name
    assert counter == 40
    return col, root, preview_col, records


def full(export=True, layout_only=False):
    reset()
    palette()
    model_entries = {}
    for builder in (droplet, frigate, cruiser, lambda:cruiser(True)):
        col, root = builder()
        model_entries[root.name] = (col,root)
    for title in ('Frigate','Cruiser','Command'):
        model_entries[title+'Wreck'] = wreck(model_entries[title][0], title+'Wreck')
    layout_col, layout_root, layout_previews, records = fleet_layout({
        'Small':model_entries['Frigate'][0], 'Large':model_entries['Cruiser'][0],
        'Command':model_entries['Command'][0]})
    manifest = dict(blender_version=bpy.app.version_string,
                    blender_build_hash=bpy.app.build_hash.decode(),
                    origin='Original procedural authoring for DropletPrototype; no external assets.',
                    axes='Blender +Y forward / +Z up; Unity +Z forward / +Y up',
                    layout_export='Tiny mesh pose proxies; authored sources remain Empties.',
                    models={name:mesh_report(entry[0]) for name,entry in model_entries.items()},
                    marker_count=len(records), ship_types={t:sum(r['name'].split('_')[1]==t for r in records) for t in ('Small','Large','Command')},
                    export_options={k:sorted(v) if isinstance(v,set) else v for k,v in EXPORT_OPTIONS.items()})
    if export:
        for name, (col, root) in model_entries.items():
            if not layout_only:
                if name.endswith('Wreck'):
                    export_fbx(EXPORT / (name+'.fbx'), list(col.objects))
                else:
                    export_merged_model(EXPORT / (name+'.fbx'), col, root)
            manifest['models'][name]['exported_mesh_count'] = 6 if name.endswith('Wreck') else 1
        export_fbx(EXPORT / 'FleetLayout.fbx', list(layout_col.objects))
        (EXPORT/'asset_manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
        (EXPORT/'FleetLayout_expected.json').write_text(json.dumps(dict(markers=records),indent=2),encoding='utf-8')
    # Library source is editable and opens on a deliberately arranged showcase.
    # Collection instances preserve local authoring coordinates of original models.
    layout_col.hide_render = True
    layout_previews.hide_render = True
    for name,(col,root) in model_entries.items():
        col.hide_render = True
    showcase = new_collection('Asset_Showcase_Instances')
    offsets = {'Frigate':(-23,0,0),'Cruiser':(0,0,0),'Command':(29,0,0),'Droplet':(-23,22,2)}
    for name,position in offsets.items():
        obj = empty('SHOWCASE_'+name,showcase,position)
        obj.instance_type = 'COLLECTION'
        obj.instance_collection = model_entries[name][0]
        # hide_render on source collection propagates to instances, so source
        # originals are instead excluded by keeping them in a separate view layer.
    for name,(col,root) in model_entries.items():
        col.hide_render = False
        for obj in col.objects:
            obj.hide_render = False
    # Move original collection origins far away via collection instance offsets
    # would modify exports. Exclude source collections in the active view layer;
    # referenced collection instances still render their members.
    for name,(col,root) in model_entries.items():
        bpy.context.view_layer.layer_collection.children[col.name].exclude = True
    rig = preview('fleet-assets-preview.png', (4,2,0), 100, (1600,1000))
    camera = bpy.context.scene.camera
    droplet_center = Vector(offsets['Droplet'])
    camera.location = droplet_center+Vector((3,5,2.4))
    camera.rotation_euler = (droplet_center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = 3.5
    bpy.context.scene.render.filepath = str(PREVIEW/'droplet-preview.png')
    bpy.ops.render.render(write_still=True)
    showcase.hide_render = True
    layout_previews.hide_render = False
    for obj in rig.objects:
        if obj.type == 'LIGHT':
            obj.data.energy *= 60
            obj.data.size *= 12
    target = Vector((0,305,20))
    camera.location = target+Vector((530,-600,470))
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale = 640
    bpy.context.scene.render.filepath = str(PREVIEW/'fleet-layout-preview.png')
    for obj in rig.objects:
        if obj.type == 'LIGHT':
            direction = (obj.location-Vector((4,2,0))).normalized()
            obj.location = target+direction*550
            obj.rotation_euler = (target-obj.location).to_track_quat('-Z','Y').to_euler()
    save(ART/'fleet_layout.blend')
    bpy.ops.render.render(write_still=True)
    print('FULL_ASSETS_READY ' + json.dumps(manifest))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', choices=('calibration', 'source', 'full', 'layout'), default='calibration')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.stage == 'calibration':
        calibration()
    else:
        full(export=args.stage!='source', layout_only=args.stage=='layout')
