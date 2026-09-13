"""PerfectDroplet: one convex quintic meridian, circular rings, welded quad caps.

Run with run.py, in an isolated Blender 5.2 background process. Does not touch
the interactive session or any Unity Assets. Original .blend is hash guarded.
The saved source is +Z forward, +Y up, metres, identity transform.
"""
import bisect
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / 'ArtSource/Blender/Droplet/PerfectDroplet'
OUT = ROOT / 'ArtSource/Exports/Droplet/PerfectDroplet'
EVIDENCE = ROOT / 'docs/verification/PerfectDroplet'
SOURCE = ART / 'PerfectDroplet.blend'
FBX = OUT / 'PerfectDroplet_Game.fbx'
LENGTH = 2.4
DIAMETER = LENGTH / 3.1
CONTROL = [(-1.55, 0), (-1.55, .5), (-.95, .9), (.8, .18), (1.55, .035), (1.55, 0)]
T_REAR, T_FRONT = .075, .90
TIP_GRID_POWER = 1.6
TAU = 2 * math.pi


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding='utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bezier(points, t):
    n = len(points) - 1
    return tuple(sum(math.comb(n, i) * (1-t)**(n-i) * t**i * p[a]
                     for i, p in enumerate(points)) for a in range(2))


def derivative(points):
    return [tuple((len(points)-1)*(points[i+1][a]-points[i][a]) for a in range(2))
            for i in range(len(points)-1)]


D1 = derivative(CONTROL)
D2 = derivative(D1)
lo, hi = 0.0, 1.0
for _ in range(64):
    mid = (lo+hi)*.5
    if bezier(D1, mid)[1] > 0:
        lo = mid
    else:
        hi = mid
T_MAX = (lo+hi)*.5
SCALE_Z = LENGTH / 3.1
SCALE_R = (DIAMETER*.5) / bezier(CONTROL, T_MAX)[1]


def profile(t):
    z, r = bezier(CONTROL, t)
    return z*SCALE_Z, r*SCALE_R


def slope(t):
    z, r = bezier(D1, t)
    return z*SCALE_Z, r*SCALE_R


def normal(t, angle):
    dz, dr = slope(t)
    return Vector((dz*math.cos(angle), dz*math.sin(angle), -dr)).normalized()


def curvature(t):
    dz, dr = slope(t)
    ddz, ddr = bezier(D2, t)
    ddz, ddr = ddz*SCALE_Z, ddr*SCALE_R
    return (dr*ddz-dz*ddr)/(dz*dz+dr*dr)**1.5


def ring_parameters(bands):
    # Arc length / radius controls quad aspect ratio as the nose narrows.
    # Normal turning retains curved-end fidelity. Include the exact widest ring.
    ts = [T_REAR + (T_FRONT-T_REAR)*i/16000 for i in range(16001)]
    aspect_density, turns = [0.0], [0.0]
    for a, b in zip(ts, ts[1:]):
        za, ra = profile(a)
        zb, rb = profile(b)
        aspect_density.append(aspect_density[-1]+math.hypot(zb-za, rb-ra)/((ra+rb)*.5))
        da, db = slope(a), slope(b)
        turns.append(turns[-1]+abs(math.atan2(db[1], db[0])-math.atan2(da[1], da[0])))
    weight = [.75*a/aspect_density[-1]+.25*b/turns[-1] for a, b in zip(aspect_density, turns)]
    result = []
    for i in range(bands+1):
        u = i/bands
        j = min(max(bisect.bisect_left(weight, u), 1), len(ts)-1)
        f = (u-weight[j-1])/(weight[j]-weight[j-1])
        result.append(ts[j-1]*(1-f)+ts[j]*f)
    closest = min(range(1, bands), key=lambda j: abs(result[j]-T_MAX))
    result[closest] = T_MAX
    return result


def disk(a, b):
    # Concentric square-to-disk map: regular square quad patch, circular boundary.
    if abs(a)+abs(b) < 1e-12:
        return 0.0, 0.0
    if abs(a) > abs(b):
        radius, theta = a, math.pi*.25*b/a
    else:
        radius, theta = b, math.pi*.5-math.pi*.25*a/b
    return radius*math.cos(theta), radius*math.sin(theta)


def parameter_at_radius(radius, front):
    lo, hi = (T_FRONT, 1.0) if front else (0.0, T_REAR)
    for _ in range(60):
        mid = (lo+hi)*.5
        smaller = profile(mid)[1] < radius
        if smaller != front:
            lo = mid
        else:
            hi = mid
    return (lo+hi)*.5


def create_mesh(name, radial, bands, collection, material):
    verts, faces, normals, parameters = [], [], [], []
    ts = ring_parameters(bands)
    for t in ts:
        z, r = profile(t)
        for j in range(radial):
            angle = TAU*j/radial
            verts.append((r*math.cos(angle), r*math.sin(angle), z))
            normals.append(normal(t, angle))
            parameters.append((t, angle, 'body'))
    for i in range(bands):
        for j in range(radial):
            nxt = (j+1) % radial
            faces.append((i*radial+j, i*radial+nxt, (i+1)*radial+nxt, (i+1)*radial+j))
    body_faces = len(faces)
    for front in (False, True):
        grid = {}
        n = radial//4
        radius = profile(T_FRONT if front else T_REAR)[1]
        ring_offset = bands*radial if front else 0
        for y in range(n+1):
            for x in range(n+1):
                dx, dy = disk(2*x/n-1, 2*y/n-1)
                angle = math.atan2(dy, dx) % TAU
                if x in (0, n) or y in (0, n):
                    index = ring_offset+round(angle/TAU*radial) % radial
                    assert abs(verts[index][0]-dx*radius) < 1e-8
                    assert abs(verts[index][1]-dy*radius) < 1e-8
                else:
                    cap_radius = math.hypot(dx, dy)**(TIP_GRID_POWER if front else 1)*radius
                    t = parameter_at_radius(cap_radius, front)
                    z, r = profile(t)
                    index = len(verts)
                    verts.append((r*math.cos(angle), r*math.sin(angle), z))
                    normals.append(normal(t, angle))
                    parameters.append((t, angle, 'front' if front else 'rear'))
                grid[x, y] = index
        for y in range(n):
            for x in range(n):
                face = (grid[x,y], grid[x+1,y], grid[x+1,y+1], grid[x,y+1])
                faces.append(face if front else tuple(reversed(face)))
    mesh = bpy.data.meshes.new(name+'_QuadSurface')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    # UV seams are loop attributes only. They never split geometric vertices or
    # the exact meridian normals. No texture or tangent normal map is required.
    uv = mesh.uv_layers.new(name='UVMap')
    cap_count = (radial//4)**2
    for polygon in mesh.polygons:
        angles = [parameters[i][1]/TAU for i in polygon.vertices]
        crosses = max(angles)-min(angles) > .5
        for k, loop_index in enumerate(polygon.loop_indices):
            v = polygon.vertices[k]
            t, angle, _ = parameters[v]
            if polygon.index < body_faces:
                u = angles[k] + (1 if crosses and angles[k] < .5 else 0)
                coord = (.04+.92*u, .04+.66*(t-T_REAR)/(T_FRONT-T_REAR))
            else:
                front = polygon.index >= body_faces+cap_count
                radius = profile(T_FRONT if front else T_REAR)[1]
                coord = ((.65 if front else .30)+.115*verts[v][0]/radius,
                         .85+.115*verts[v][1]/radius)
            uv.data[loop_index].uv = coord
    mesh.normals_split_custom_set_from_vertices(normals)
    param = mesh.attributes.new('meridian_t', 'FLOAT', 'POINT')
    for item, value in zip(param.data, parameters):
        item.value = value[0]
    obj['forward_axis'] = '+Z'
    obj['up_axis'] = '+Y'
    obj['units'] = 'metres'
    obj['geometry'] = 'Single convex quintic surface of revolution; welded all-quad grid caps'
    obj['radial_segments'] = radial
    obj['meridian_bands'] = bands
    obj['analytic_normals'] = True
    return obj


def clear_factory():
    assert bpy.app.background, 'Only run in an isolated background process.'
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def configure_view(scene):
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    scene.display.render_aa = '32'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.studio_light = 'paint.sl'
    shading.color_type = 'MATERIAL'
    shading.show_shadows = False
    shading.show_cavity = False
    shading.show_specular_highlight = True
    shading.show_object_outline = False
    shading.background_type = 'WORLD'
    scene.world.color = (.055, .063, .073)
    scene.view_settings.view_transform = 'Standard'
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    direction = Vector((3.8, -6, 3.1))
    rotation = (-direction).to_track_quat('-Z', 'Y')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_rotation = rotation
                space.region_3d.view_distance = 4.7
                space.region_3d.view_location = (0, 0, 0)
                space.overlay.show_floor = False
                space.overlay.show_axis_x = False
                space.overlay.show_axis_y = False
                space.overlay.show_extras = False
                space.clip_start = .0001
                space.shading.type = 'SOLID'
                space.shading.light = 'STUDIO'
                space.shading.studio_light = 'paint.sl'
                space.shading.color_type = 'MATERIAL'
                space.shading.show_cavity = False


def build():
    receipt = ART / 'generated-source-receipt.json'
    if SOURCE.exists():
        assert receipt.exists() and json.loads(receipt.read_text(encoding='utf-8'))['sha256'] == sha(SOURCE), 'Source was edited; refusing to overwrite it.'
    clear_factory()
    scene = bpy.context.scene
    scene.name = 'PerfectDroplet_GeometryOnly'
    scene.world = bpy.data.worlds.new('NeutralViewportBackground')
    material = bpy.data.materials.new('Droplet_NeutralInspection')
    material.diffuse_color = (.46, .48, .50, 1)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (.46, .48, .50, 1)
    bsdf.inputs['Metallic'].default_value = 0
    bsdf.inputs['Roughness'].default_value = .4
    game_col = bpy.data.collections.new('01_Game_5632tri')
    ref_col = bpy.data.collections.new('02_Reference_22528tri_HIDDEN')
    scene.collection.children.link(game_col)
    scene.collection.children.link(ref_col)
    game = create_mesh('PerfectDroplet_Game', 64, 36, game_col, material)
    reference = create_mesh('PerfectDroplet_Reference', 128, 72, ref_col, material)
    reference.hide_render = True
    ref_col.hide_render = True
    bpy.context.view_layer.layer_collection.children[ref_col.name].exclude = True
    bpy.context.view_layer.objects.active = game
    game.select_set(True)
    configure_view(scene)
    scene['delivery_scope'] = 'Geometry only. No scene lights, cameras, effects or gameplay.'
    text = bpy.data.texts.new('READ_ME')
    text.write('PERFECT DROPLET | geometry only\n\nGame: 5632 triangles, all-quad source.\nReference: 22528 triangles, excluded collection. Show ONE version at a time.\n+Z tip / +Y up. Metres. Identity transforms. Bounds-centred origin.\nOne convex quintic meridian and welded quad grid end patches.\nThe source has no lights/cameras/animation. Neutral material only.\nExact smooth meridian normals are stored; import normals from FBX.\nAfter hand editing geometry, clear custom split normals and re-evaluate shading.\nFBX and QA details: docs/DROPLET_GEOMETRY_REPORT.md\n')
    bpy.context.view_layer.update()
    ART.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    write_json(receipt, {'sha256': sha(SOURCE), 'generator': str(Path(__file__).relative_to(ROOT))})
    write_json(EVIDENCE/'build.json', {'blender': bpy.app.version_string,
        'build_hash': bpy.app.build_hash.decode(), 'python': sys.version,
        'source_sha256': sha(SOURCE), 'length': LENGTH, 'diameter': DIAMETER,
        'ratio': 3.1, 'control_points_z_r_before_scaling': CONTROL,
        'scale_z': SCALE_Z, 'scale_r': SCALE_R, 'maximum_radius_t': T_MAX,
        'rear_cap_boundary_t': T_REAR, 'tip_cap_boundary_t': T_FRONT,
        'tip_grid_radius_power': TIP_GRID_POWER,
        'maximum_radius_z': profile(T_MAX)[0],
        'tip_radius_of_curvature_m': 1/curvature(1),
        'rear_radius_of_curvature_m': 1/curvature(0),
        'game_triangles': len(game.data.polygons)*2,
        'reference_triangles': len(reference.data.polygons)*2,
        'lights': 0, 'cameras': 0, 'lods': False})
    print('BUILD_OK', len(game.data.polygons)*2, len(reference.data.polygons)*2)


def mesh_stats(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    coords = [v.co for v in mesh.vertices]
    bounds = [[min(v[a] for v in coords), max(v[a] for v in coords)] for a in range(3)]
    areas = [(coords[t.vertices[1]]-coords[t.vertices[0]]).cross(
             coords[t.vertices[2]]-coords[t.vertices[0]]).length*.5 for t in mesh.loop_triangles]
    unseen, components = set(bm.verts), 0
    while unseen:
        components += 1
        stack = [unseen.pop()]
        while stack:
            for e in stack.pop().link_edges:
                for v in e.verts:
                    if v in unseen:
                        unseen.remove(v)
                        stack.append(v)
    normal_spread = {}
    for i, loop in enumerate(mesh.loops):
        normal_spread.setdefault(loop.vertex_index, []).append(mesh.corner_normals[i].vector.copy())
    seams = max((a-b).length for values in normal_spread.values() for a in values for b in values)
    outward = min(p.normal.dot(normal(
        sum(mesh.attributes['meridian_t'].data[v].value for v in p.vertices)/len(p.vertices),
        math.atan2(p.center.y, p.center.x))) for p in mesh.polygons)
    valences = Counter(len(v.link_edges) for v in bm.verts)
    edge_ratios = sorted(max(e.calc_length() for e in f.edges)/min(e.calc_length() for e in f.edges) for f in bm.faces)
    result = {'vertices': len(mesh.vertices), 'edges': len(mesh.edges), 'faces': len(mesh.polygons),
        'triangles': len(mesh.loop_triangles), 'face_sizes': dict(Counter(len(p.vertices) for p in mesh.polygons)),
        'bounds': bounds, 'dimensions': [b-a for a,b in bounds],
        'connected_components': components, 'boundary_edges': sum(e.is_boundary for e in bm.edges),
        'nonmanifold_edges': sum(not e.is_manifold for e in bm.edges),
        'inconsistent_winding': sum(e.is_manifold and not e.is_contiguous for e in bm.edges),
        'loose_vertices': sum(not v.link_faces for v in bm.verts),
        'degenerate_triangles': sum(a < 1e-14 for a in areas),
        'minimum_triangle_area_m2': min(areas), 'signed_volume_m3': bm.calc_volume(signed=True),
        'vertex_valences': dict(valences), 'max_corner_normal_separation': seams,
        'quad_edge_length_ratio_median_p95_max': [edge_ratios[len(edge_ratios)//2], edge_ratios[int(len(edge_ratios)*.95)], edge_ratios[-1]],
        'minimum_outward_face_normal_dot': outward,
        'has_custom_normals': mesh.has_custom_normals,
        'euler_characteristic': len(mesh.vertices)-len(mesh.edges)+len(mesh.polygons),
        'scale': list(obj.scale), 'rotation': list(obj.rotation_euler), 'location': list(obj.location)}
    bm.free()
    assert result['connected_components'] == 1
    assert all(result[k] == 0 for k in ('boundary_edges', 'nonmanifold_edges', 'inconsistent_winding', 'loose_vertices', 'degenerate_triangles'))
    assert result['signed_volume_m3'] > 0 and outward > 0 and seams < 1e-6
    assert result['euler_characteristic'] == 2 and set(valences) == {3, 4}
    return result


def inspect():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    report = {'source_sha256': sha(SOURCE), 'objects': {}, 'errors': []}
    for obj in bpy.data.objects:
        if obj.type == 'MESH':
            report['objects'][obj.name] = mesh_stats(obj)
    samples = [curvature(i/20000) for i in range(20001)]
    report['meridian_curvature_samples'] = len(samples)
    report['meridian_curvature_min_max'] = [min(samples), max(samples)]
    report['strict_convexity_sampled'] = min(samples) > 0
    # Bernstein convex-hull bounds certify that r'z''-z'r'' stays positive
    # between the numeric samples. Positive speed follows monotone control Z
    # and the nonzero radial endpoint derivatives.
    coefficients = []
    for k in range(8):
        coefficients.append(sum(
            math.comb(4,i)*math.comb(3,k-i)/math.comb(7,k)
            *(D1[i][1]*D2[k-i][0]-D1[i][0]*D2[k-i][1])*SCALE_R*SCALE_Z
            for i in range(5) if 0 <= k-i < 4))
    lower_bounds = []
    def certify(values, depth=0):
        if min(values) > 0:
            lower_bounds.append(min(values))
            return
        assert depth < 20, 'Convexity was not certified.'
        rows = [values]
        while len(rows[-1]) > 1:
            rows.append([(a+b)*.5 for a,b in zip(rows[-1], rows[-1][1:])])
        certify([r[0] for r in rows], depth+1)
        certify([r[-1] for r in rows][::-1], depth+1)
    certify(coefficients)
    report['continuous_meridian_convexity_certificate'] = {
        'method': 'Bernstein polynomial subdivision positive convex-hull bounds',
        'intervals': len(lower_bounds), 'positive_numerator_lower_bound': min(lower_bounds)}
    report['no_nonmesh_scene_objects'] = all(o.type == 'MESH' for o in bpy.context.scene.objects)
    assert min(samples) > 0
    assert len(report['objects']) == 2
    write_json(EVIDENCE/'inspection.json', report)
    print('INSPECTION_OK', json.dumps(report))


def render():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene = bpy.context.scene
    configure_view(scene)
    data = bpy.data.cameras.new('Temporary_QA_Camera')
    camera = bpy.data.objects.new('Temporary_QA_Camera', data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    data.clip_start = .0001
    previews = ART/'Previews'
    previews.mkdir(exist_ok=True)
    # Side and top are defined relative to the MODEL (+Y up), not Blender Z-up.
    views = [('01_Perspective', (3.8,-6,3.1), 'PERSP', 3.1),
             ('02_Side', (5,0,0), 'ORTHO', 3.0),
             ('03_Front', (0,0,5), 'ORTHO', 1.30),
             ('04_Top', (0,5,0), 'ORTHO', 3.0)]
    outputs = []
    for name, location, kind, scale in views:
        camera.location = location
        camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
        if name in ('02_Side', '04_Top'):
            camera.rotation_euler = ((-camera.location).to_track_quat('-Z', 'X')).to_euler()
        data.type, data.ortho_scale, data.lens = kind, scale, 64
        scene.render.filepath = str(previews/(name+'.png'))
        bpy.ops.render.render(write_still=True)
        outputs.append(scene.render.filepath)
    # Stock monochrome matcap inspects normal continuity without creating lights,
    # production materials, reflection probes or a saved inspection scene.
    mats = [s.name for s in bpy.context.preferences.studio_lights if s.type == 'MATCAP']
    print('MATCAPS', mats)
    scene.display.shading.light = 'MATCAP'
    preferred = 'check_reflection_horizontal.exr'
    assert preferred in mats
    scene.display.shading.studio_light = preferred
    for i, location in enumerate(((4,-6,2), (-4,-6,1), (5,-4,-2))):
        camera.location = location
        camera.rotation_euler = (-camera.location).to_track_quat('-Z','Y').to_euler()
        data.type, data.lens = 'PERSP', 64
        scene.render.filepath = str(previews/f'05_NormalSweep_{i+1}.png')
        bpy.ops.render.render(write_still=True)
        outputs.append(scene.render.filepath)
    # Opposite meridian direction, and a highly magnified tip: real mesh renders.
    for name, target, direction, scale in (
        ('06_RearReflection', (0,0,-.98), (2,-3,-3), 1.18),
        ('07_TipReflection', (0,0,1.190), (3,-6,1), .06)):
        target = Vector(target)
        camera.location = target + Vector(direction).normalized()*3
        camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
        data.type, data.ortho_scale = 'ORTHO', scale
        scene.render.filepath = str(previews/(name+'.png'))
        bpy.ops.render.render(write_still=True)
        outputs.append(scene.render.filepath)
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.studio_light = 'paint.sl'
    game = bpy.data.objects['PerfectDroplet_Game']
    overlay = bpy.data.objects.new('Temporary_QuadEdges', game.data.copy())
    scene.collection.objects.link(overlay)
    ink = bpy.data.materials.new('Temporary_QuadEdgeInk')
    ink.diffuse_color = (.006,.009,.012,1)
    overlay.data.materials.clear()
    overlay.data.materials.append(ink)
    wire = overlay.modifiers.new('Temporary_QuadEdgeOverlay', 'WIREFRAME')
    wire.thickness, wire.offset = .0007, 1
    for name, target, direction, scale, thickness in (
        ('08_QuadTopology', (0,0,0), (3,-5,1), 3.65, .0007),
        ('09_RearQuadCap', (0,0,-1.12), (1,-1,-5), .40, .0003),
        ('10_TipQuadCap', (0,0,1.198), (3,-5,1), .018, .000015)):
        wire.thickness = thickness
        target = Vector(target)
        camera.location = target + Vector(direction).normalized()*3
        camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
        data.type, data.ortho_scale = 'ORTHO', scale
        scene.render.filepath = str(previews/(name+'.png'))
        bpy.ops.render.render(write_still=True)
        outputs.append(scene.render.filepath)
    overlay.hide_render = True
    scene.display.shading.light = 'MATCAP'
    scene.display.shading.studio_light = 'fullmetal.exr'
    for i, location in enumerate(((4,-6,2),(-4,-6,1),(5,-4,-2))):
        camera.location = location
        camera.rotation_euler = (-camera.location).to_track_quat('-Z','Y').to_euler()
        data.type, data.lens = 'PERSP', 64
        scene.render.filepath = str(previews/f'11_HighlightSweep_{i+1}.png')
        bpy.ops.render.render(write_still=True)
        outputs.append(scene.render.filepath)
    write_json(EVIDENCE/'renders.json', {'files': outputs, 'engine': 'BLENDER_WORKBENCH',
        'material': 'Neutral only', 'matcap_for_diagnostic_only': preferred,
        'source_not_resaved': True, 'light_objects_added': 0,
        'topology_overlay_temporary_only': True})
    print('RENDER_OK')


OPTIONS = dict(use_selection=True, object_types={'MESH'}, global_scale=1,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=True,
    mesh_smooth_type='OFF', use_mesh_modifiers=True, add_leaf_bones=False,
    bake_anim=False, path_mode='AUTO', use_custom_props=True)


def export():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    obj = bpy.data.objects['PerfectDroplet_Game']
    mesh = obj.data.copy()
    copy = bpy.data.objects.new('PerfectDroplet_Game_Export', mesh)
    scene = bpy.data.scenes.new('TemporaryExport')
    scene.collection.objects.link(copy)
    bpy.context.window.scene = scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    bpy.context.view_layer.objects.active = copy
    copy.select_set(True)
    # Source explicitly uses user-requested +Z/+Y. Transform ONLY export vertices
    # into the existing project's calibrated Blender +Y-forward/+Z-up convention.
    # (x,y,z) -> (-x,z,y) is a proper 180-degree rotation, determinant +1.
    C = Matrix(((-1,0,0,0), (0,0,1,0), (0,1,0,0), (0,0,0,1)))
    mesh.transform(C)
    # Modifier acts on export copy; source quad topology remains intact.
    triangulate = copy.modifiers.new('ExportTriangulation', 'TRIANGULATE')
    triangulate.quad_method = 'FIXED'
    if hasattr(triangulate, 'keep_custom_normals'):
        triangulate.keep_custom_normals = True
    bpy.ops.export_scene.fbx(filepath=str(FBX), **OPTIONS)
    write_json(OUT/'export_manifest.json', {'source_sha256': sha(SOURCE), 'fbx_sha256': sha(FBX),
        'source_axes': '+Z forward, +Y up', 'source_to_calibrated_blender_matrix': [list(r) for r in C],
        'staging_axes': '+Y forward, +Z up', 'unity_expected_axes': '+Z forward, +Y up',
        'options': {k: sorted(v) if isinstance(v,set) else v for k,v in OPTIONS.items()},
        'triangles': 5632, 'lods': False,
        'unity_import_instructions': 'globalScale=1; useFileScale=true; bakeAxisConversion=true; import normals; no animation/colliders; Read/Write optional off',
        'unity_import_executed_this_task': False})
    print('EXPORT_OK')


def roundtrip():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    source = bpy.data.objects['PerfectDroplet_Game']
    expected = [Vector((-v.co.x,v.co.z,v.co.y)) for v in source.data.vertices]
    expected_normals = {}
    for i, loop in enumerate(source.data.loops):
        n = source.data.corner_normals[i].vector
        expected_normals[loop.vertex_index] = Vector((-n.x,n.z,n.y))
    clear_factory()
    bpy.ops.import_scene.fbx(filepath=str(FBX), use_anim=False, use_custom_normals=True, use_image_search=False)
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(meshes) == 1
    obj = meshes[0]
    mesh = obj.data
    mesh.calc_loop_triangles()
    tree = KDTree(len(expected))
    for i,p in enumerate(expected):
        tree.insert(p,i)
    tree.balance()
    errors, normal_errors = [], []
    mapping = {}
    for v in mesh.vertices:
        p = obj.matrix_world @ v.co
        _, index, distance = tree.find(p)
        mapping[v.index] = index
        errors.append(distance)
    nmat = obj.matrix_world.to_3x3().inverted().transposed()
    for i,loop in enumerate(mesh.loops):
        n = (nmat @ mesh.corner_normals[i].vector).normalized()
        ref = expected_normals[mapping[loop.vertex_index]]
        normal_errors.append(math.degrees(math.acos(max(-1,min(1,n.dot(ref))))))
    points = [obj.matrix_world @ v.co for v in mesh.vertices]
    dimensions = [max(p[a] for p in points)-min(p[a] for p in points) for a in range(3)]
    report = {'fbx_sha256':sha(FBX), 'vertices':len(mesh.vertices), 'triangles':len(mesh.loop_triangles),
        'mesh_objects':len(meshes), 'max_vertex_error_m':max(errors),
        'max_normal_error_degrees':max(normal_errors), 'dimensions_calibrated_blender_xyz': dimensions,
        'narrow_tip_positive_Y': max(math.hypot(p.x,p.z) for p in points if p.y > LENGTH*.49) < DIAMETER*.02,
        'materials': [m.name for m in mesh.materials], 'source_unchanged':sha(SOURCE)==json.loads((OUT/'export_manifest.json').read_text(encoding='utf-8'))['source_sha256']}
    write_json(EVIDENCE/'roundtrip.json', report)
    assert len(mesh.loop_triangles) == 5632 and max(errors) < 1e-5
    assert max(normal_errors) < .1 and report['narrow_tip_positive_Y'] and report['source_unchanged']
    print('ROUNDTRIP_OK', json.dumps(report))


if __name__ == '__main__':
    phase = sys.argv[sys.argv.index('--')+1]
    {'build':build,'inspect':inspect,'render':render,'export':export,'roundtrip':roundtrip}[phase]()
