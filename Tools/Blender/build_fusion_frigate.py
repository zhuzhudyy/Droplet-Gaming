"""FusionFrigate, Blender 5.2.1. Run only in an isolated background process.

Authoring metres: +Y forward, +Z up. Never writes inside Unity Assets.
Editable parts and linked module prototypes; no external models or textures.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'ArtSource/Blender/Ships/FusionFrigate'
EXPORT = ROOT / 'ArtSource/Exports/Ships/FusionFrigate'
EVIDENCE = ROOT / 'docs/verification/FusionFrigate'
BLEND = ART / 'FusionFrigate.blend'
MATERIALS = []
PI = math.pi


class Geometry:
    def __init__(self):
        self.vertices, self.faces, self.materials, self.smooth = [], [], [], []

    def add(self, vertices, faces, material=0, smooth=False, matrix=None):
        start = len(self.vertices)
        self.vertices.extend(tuple(matrix @ Vector(v)) if matrix else tuple(v) for v in vertices)
        self.faces.extend(tuple(i + start for i in f) for f in faces)
        self.materials.extend([material] * len(faces))
        self.smooth.extend([smooth] * len(faces))

    def box(self, center, size, material=0, bevel=0, segments=1, rotation=None):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for v in bm.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=segments,
                            affect='EDGES', clamp_overlap=True)
        bm.verts.ensure_lookup_table()
        bm.verts.index_update()
        matrix = Matrix.Translation(Vector(center))
        if rotation:
            matrix = matrix @ rotation
        self.add([v.co.copy() for v in bm.verts],
                 [tuple(v.index for v in f.verts) for f in bm.faces], material, matrix=matrix)
        bm.free()

    def beam(self, a, b, width, depth, material=1, bevel=0, segments=1):
        delta = Vector(b) - Vector(a)
        rotation = delta.to_track_quat('Y', 'Z').to_matrix().to_4x4()
        self.box((Vector(a) + Vector(b)) / 2, (width, delta.length, depth),
                 material, bevel, segments, rotation)

    def lathe(self, profile, segments, material=2, center=(0, 0, 0), smooth=True,
              scale=(1, 1, 1), rotate=None):
        # A closed cross-section in (axial Y, radius), including the inner wall.
        verts = []
        for y, radius in profile:
            for j in range(segments):
                theta = 2 * PI * j / segments
                point = Vector((math.cos(theta) * radius * scale[0], y * scale[1],
                                math.sin(theta) * radius * scale[2]))
                if rotate:
                    point = rotate @ point
                verts.append(point + Vector(center))
        faces = []
        for k in range(len(profile)):
            nxt = (k + 1) % len(profile)
            for j in range(segments):
                jn = (j + 1) % segments
                faces.append((k * segments + j, nxt * segments + j,
                              nxt * segments + jn, k * segments + jn))
        self.add(verts, faces, material, smooth)

    def cylinder(self, a, b, radius, segments=12, material=2):
        # Closed cylinder with a single ngon at each end (no coincident pole vertices).
        d = Vector(b) - Vector(a)
        rot = d.to_track_quat('Y', 'Z').to_matrix()
        verts = []
        for p in (Vector(a), Vector(b)):
            for j in range(segments):
                angle = 2 * PI * j / segments
                verts.append(p + rot @ Vector((radius * math.cos(angle), 0, radius * math.sin(angle))))
        faces = [tuple(reversed(range(segments))), tuple(range(segments, 2 * segments))]
        faces.extend((j, (j + 1) % segments, (j + 1) % segments + segments, j + segments)
                     for j in range(segments))
        self.add(verts, faces, material)

    def pipe(self, points, radius, segments=8, material=2):
        verts = []
        for i, pt in enumerate(points):
            tangent = Vector(points[min(i + 1, len(points) - 1)]) - Vector(points[max(0, i - 1)])
            rotation = tangent.to_track_quat('Y', 'Z').to_matrix()
            verts.extend(Vector(pt) + rotation @ Vector((radius * math.cos(j * 2 * PI / segments),
                         0, radius * math.sin(j * 2 * PI / segments))) for j in range(segments))
        faces = [tuple(reversed(range(segments))), tuple((len(points) - 1) * segments + j for j in range(segments))]
        for k in range(len(points) - 1):
            for j in range(segments):
                faces.append((k * segments + j, k * segments + (j + 1) % segments,
                              (k + 1) * segments + (j + 1) % segments, (k + 1) * segments + j))
        self.add(verts, faces, material, True)

    def loft(self, rings, material=0, chamfer=.22):
        # Closed, eight-sided hull cross-sections: y, half-width, bottom, top.
        verts = []
        for y, w, b, t in rings:
            h = (t - b) * chamfer
            verts.extend([(-w * .72, y, b), (w * .72, y, b), (w, y, b + h),
                          (w, y, t - h), (w * .72, y, t), (-w * .72, y, t),
                          (-w, y, t - h), (-w, y, b + h)])
        faces = [tuple(reversed(range(8)))]
        for k in range(len(rings) - 1):
            faces.extend((k * 8 + i, k * 8 + (i + 1) % 8,
                          (k + 1) * 8 + (i + 1) % 8, (k + 1) * 8 + i) for i in range(8))
        faces.append(tuple((len(rings) - 1) * 8 + i for i in range(8)))
        self.add(verts, faces, material)

    def object(self, name, collection, lod, group, position=(0, 0, 0), bevel=0, segments=1):
        mesh = bpy.data.meshes.new(name + '_Mesh')
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        # Every island is a closed authored solid. Recalculate winding per island.
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        for mat in MATERIALS:
            mesh.materials.append(mat)
        for p, mat, smooth in zip(mesh.polygons, self.materials, self.smooth):
            p.material_index = mat
            p.use_smooth = smooth
        mesh.update()
        # Sharp seams across the profile, smoothing only circular segments.
        edge_faces = {}
        for face in mesh.polygons:
            for key in face.edge_keys:
                edge_faces.setdefault(tuple(sorted(key)), []).append(face)
        for edge in mesh.edges:
            fs = edge_faces.get(tuple(sorted(edge.vertices)), [])
            if len(fs) == 2 and fs[0].normal.dot(fs[1].normal) < .77:
                edge.use_edge_sharp = True
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        obj.location = position
        obj['ff_lod'], obj['ff_group'], obj['ff_owned'] = lod, group, True
        obj.parent = bpy.data.objects[f'FusionFrigate_LOD{lod}']
        if bevel:
            mod = obj.modifiers.new('Key edge chamfers', 'BEVEL')
            mod.width, mod.segments = bevel, segments
            mod.limit_method = 'ANGLE'
        return obj


def linked(proto, name, collection, position):
    obj = proto.copy()
    obj.data = proto.data
    obj.name = name
    obj.location = position
    collection.objects.link(obj)
    return obj


def make_palette():
    for name, color, metal, rough in [
        ('FF_Armor', (.39, .43, .455), .5, .42),
        ('FF_Structure', (.063, .083, .103), .45, .52),
        ('FF_EngineMetal', (.235, .285, .325), .78, .34),
        ('FF_BlueGray', (.075, .25, .345), .35, .32),
    ]:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color, 1)
        mat.metallic, mat.roughness = metal, rough
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = (*color, 1)
        bsdf.inputs['Metallic'].default_value = metal
        bsdf.inputs['Roughness'].default_value = rough
        bsdf.inputs['Emission Strength'].default_value = 0
        mat['ff_owned'] = True
        MATERIALS.append(mat)


def ring_profile(y, radius, width, thickness, detailed):
    a, b, inner = y - width / 2, y + width / 2, radius - thickness
    if not detailed:
        return [(a, radius), (b, radius), (b, inner), (a, inner)]
    c = min(width, thickness) * .22
    return [(a, radius - c), (a + c, radius), (b - c, radius), (b, radius - c),
            (b, inner + c), (b - c, inner), (a + c, inner), (a, inner + c)]


def drive(lod, auxiliary=False):
    n = ((32, 16) if lod == 0 else (20, 12) if lod == 1 else (12, 8))[int(auxiliary)]
    g = Geometry()
    if lod == 2 and auxiliary:
        # Purpose-built distant silhouette: a single closed hollow casing, not decimation.
        g.lathe([(.15, 1.28), (-2.35, 1.6439), (-2.70, 1.19), (-4.62, 1.55),
                 (-5.07, 1.47), (-5.07, 1.30), (-2.62, .43)], 8, 2)
        g.cylinder((0, -2.65, 0), (0, -2.57, 0), .435, 4, 3)
        for side in (-1, 1):
            g.beam((side * 1.17, .15, 0), (side * 1.55, -4.48, 0), .16, .19, 1)
        g.vertices = [(x * .445, y * .70, z * .445) for x, y, z in g.vertices]
        return g
    bevel = .035 if lod == 0 else 0
    ring_centers = (-.65, -1.70, -2.78) if lod < 2 else (-2.78,)
    if auxiliary:
        ring_centers = (-.65, -2.35) if lod < 2 else (-2.35,)
    for y in ring_centers:
        radius = 1.47 + (-y) * .074
        g.lathe(ring_profile(y, radius, .25, .20, lod == 0), n, 2)
    # Reactor casing: externally readable shoulder, no hidden reactor interior.
    if lod < 2:
        profile = [(.15, 1.12), (-.04, 1.28), (-.90, 1.28), (-1.24, 1.10),
                   (-1.30, .96), (-.08, .96)]
    else:
        profile = [(.15, 1.28), (-1.3, 1.1), (-1.3, .96), (.15, .96)]
    g.lathe(profile, n, 1)
    # The nozzle has a continuous lip, visible tapering inner wall, and a recessed core.
    if lod == 0:
        nozzle = [(-2.45, 1.12), (-3.22, 1.32), (-4.62, 1.55), (-4.94, 1.55),
                  (-5.07, 1.47), (-5.07, 1.30), (-4.82, 1.28), (-3.8, .92),
                  (-2.84, .43), (-2.62, .43), (-2.45, .70)]
    elif lod == 1:
        nozzle = [(-2.45, 1.12), (-4.62, 1.55), (-5.07, 1.47), (-5.07, 1.30),
                  (-3.8, .92), (-2.62, .43), (-2.45, .70)]
    else:
        nozzle = [(-2.45, 1.12), (-4.62, 1.55), (-5.07, 1.47), (-5.07, 1.30),
                  (-2.62, .43)]
    g.lathe(nozzle, n, 2)
    g.cylinder((0, -2.65, 0), (0, -2.57, 0), .435, n if lod < 2 else 8, 3)
    # Cage beams terminate at the lip shoulder, never span across the aperture.
    for j in range(4):
        angle = PI / 4 + j * PI / 2
        r = 1.41
        x, z = r * math.cos(angle), r * math.sin(angle)
        g.beam((x * .88, .16, z * .88), (x * 1.12, -4.48, z * 1.12),
               .16, .19, 1, bevel, 1)
        if lod == 0:
            g.box((x, -1.68, z), (.28, .48, .25), 0, .025)
    if lod < 2:
        # Four broad thermal jackets, with open windows between them.
        for j in range(4):
            angle = j * PI / 2
            center = (1.48 * math.cos(angle), -3.92, 1.48 * math.sin(angle))
            g.box(center, (.19, 1.15, .75), 0, bevel, 1,
                  Matrix.Rotation(-angle, 4, 'Y'))
    if lod == 0:
        for j in range(4):
            angle = j * PI / 2
            def pt(r, y):
                return (r * math.cos(angle), y, r * math.sin(angle))
            g.pipe([pt(1.1, .2), pt(1.36, -.16), pt(1.39, -1.22),
                    pt(1.2, -1.50), pt(1.12, -2.50)], .075, 8, 2)
        # Key structural saddles replace small bolts and greebles.
        for j in range(8 if not auxiliary else 4):
            a = j * 2 * PI / (8 if not auxiliary else 4)
            g.box((1.52 * math.cos(a), -4.48, 1.52 * math.sin(a)),
                  (.17, .25, .25), 1, .02, 1, Matrix.Rotation(-a, 4, 'Y'))
    if auxiliary:
        g.vertices = [(x * .445, y * .70, z * .445) for x, y, z in g.vertices]
    return g


def create_hull(lod, col):
    bevel = .035 if lod == 0 else 0
    if lod == 2:
        hull = Geometry()
        hull.loft([(-6.55, 3.12, -1.24, 1.30), (-3.0, 2.98, -1.40, 1.34),
                   (1.5, 2.65, -1.38, 1.28), (3.90, 2.24, -1.12, 1.11),
                   (6.2, 1.80, -.80, .86), (9.55, .85, -.42, .42),
                   (11.07, .10, -.09, .08)], 0)
        for side in (-1, 1):
            hull.beam((side * 1.65, -6.16, -.8), (side * 1.62, -7.04, -2.0), .58, .49, 1)
            hull.beam((side * 2.4, -6.18, .1), (side * 2.5, -7.1, .15), .61, .50, 1)
        hull.object('FF_LOD2_HullSilhouette', col, lod, 'Hull')
        return
    core = Geometry()
    core.loft([(-6.28, 2.86, -1.12, 1.08), (-3.0, 2.86, -1.20, 1.15),
               (1.5, 2.55, -1.07, 1.14), (5.8, 1.70, -.79, .85),
               (9.55, .77, -.35, .39), (11.07, .08, -.07, .06)], 1)
    core.object(f'FF_LOD{lod}_PressureHull', col, lod, 'Hull', bevel=bevel)
    # Large forebody plates, including a real sloping chin and closed belly.
    nose = Geometry()
    nose.loft([(3.90, 2.16, -.88, .99), (6.2, 1.80, -.80, .86),
               (9.55, .85, -.42, .42), (11.07, .10, -.09, .08)], 0)
    nose.object(f'FF_LOD{lod}_ProwArmor', col, lod, 'Hull', bevel=bevel)
    bottom = Geometry()
    bottom.loft([(-5.9, 1.70, -1.45, -.92), (-1.0, 1.80, -1.52, -1.0),
                 (4.4, 1.43, -1.05, -.81), (7.7, .73, -.68, -.51)], 0)
    bottom.object(f'FF_LOD{lod}_VentralKeel', col, lod, 'Hull', bevel=bevel)
    # Two linked midship armour stations, retaining large panel language.
    station = Geometry()
    station.loft([(-.93, 2.98, .66, 1.34), (.93, 2.98, .66, 1.34)], 0)
    for side in (-1, 1):
        station.box((side * 2.82, 0, -.63), (.34, 1.86, .71), 0, bevel, 2)
        if lod == 0:
            station.box((side * 2.15, -.64, 1.36), (.64, .41, .14), 2, .025)
            station.box((side * 2.15, .60, 1.36), (.64, .41, .14), 2, .025)
    proto = station.object(f'FF_LOD{lod}_ArmorStation_A', col, lod, 'Hull', (0, -4.64, 0))
    linked(proto, f'FF_LOD{lod}_ArmorStation_B', col, (0, -2.50, 0))
    forward = Geometry()
    forward.loft([(-1.33, 2.87, .68, 1.32), (1.0, 2.72, .63, 1.32),
                  (3.66, 2.24, .51, 1.11)], 0)
    for side in (-1, 1):
        forward.beam((side * 2.68, -1.2, -.65), (side * 2.30, 3.55, -.52), .43, .61, 0, bevel)
    forward.object(f'FF_LOD{lod}_ForeDeckArmor', col, lod, 'Hull')
    # Side openings: recessed dark bulkhead + proud closed frame solids, no interior hangars.
    if lod < 2:
        sockets = Geometry()
        for side in (-1, 1):
            for y, x in [(-3.5, 2.94), (.65, 2.73)]:
                sockets.box((side * (x - .08), y, .025), (.14, 1.55, .60), 1)
                for z in (-.33, .38):
                    sockets.box((side * (x + .11), y, z), (.20, 1.86, .14), 2, bevel)
                for dy in (-.86, .86):
                    sockets.box((side * (x + .11), y + dy, .025), (.20, .15, .72), 0, bevel)
                sockets.box((side * (x + .015), y, -.07), (.16, .92, .19), 2, bevel)
                if lod == 0:
                    sockets.beam((side * (x + .13), y - .42, -.24),
                                 (side * (x + .13), y + .25, .18), .065, .065, 2)
        sockets.object(f'FF_LOD{lod}_RecessedEquipmentBays', col, lod, 'Details')
    # Structural aft mounting bulkhead with physically attached auxiliary outriggers.
    stern = Geometry()
    stern.loft([(-6.55, 3.12, -1.24, 1.30), (-5.75, 3.08, -1.22, 1.29)], 0)
    for side in (-1, 1):
        stern.beam((side * 1.65, -6.16, -.8), (side * 1.62, -7.04, -2.00), .58, .49, 1, bevel)
        stern.beam((side * 2.4, -6.18, .10), (side * 2.5, -7.1, .15), .61, .50, 1, bevel)
    stern.object(f'FF_LOD{lod}_DriveMount', col, lod, 'FusionDrive')


def create_superstructure(lod, col):
    b = .04 if lod == 0 else 0
    if lod == 2:
        bridge = Geometry()
        bridge.loft([(-2.3, 1.24, 1.25, 1.77), (1.24, .73, 1.22, 1.62)], 0)
        bridge.loft([(-1.91, .80, 1.72, 2.26), (.15, .48, 1.68, 2.12)], 0)
        bridge.object('FF_LOD2_LayeredBridge', col, lod, 'Superstructure')
        sensors = Geometry()
        for x, y, height in [(0, -1.25, 3.48), (-.64, -1.56, 3.04), (.64, -1.56, 3.04)]:
            sensors.box((x, y, (height + 2.2) / 2), (.078, .078, height - 2.2), 2)
        sensors.object('FF_LOD2_Sensors', col, lod, 'Superstructure')
        turret = Geometry()
        turret.loft([(-.35, .36, 0, .32), (.42, .23, 0, .23)], 0)
        turret.box((0, .68, .22), (.15, .88, .096), 2)
        proto = turret.object('FF_LOD2_Turret_01', col, lod, 'Superstructure', (-1.23, 2.15, 1.12))
        linked(proto, 'FF_LOD2_Turret_02', col, (1.23, 2.15, 1.12))
        linked(proto, 'FF_LOD2_Turret_03', col, (0, 6.25, .89))
        return
    bridge = Geometry()
    bridge.loft([(-2.3, 1.24, 1.25, 1.69), (.10, 1.22, 1.25, 1.69),
                 (1.24, .73, 1.22, 1.53)], 0)
    bridge.loft([(-1.97, .95, 1.60, 2.09), (-.14, .95, 1.60, 2.09),
                 (.77, .64, 1.54, 1.94)], 0)
    if lod < 2:
        bridge.loft([(-.15, .964, 1.77, 1.89), (.78, .65, 1.68, 1.79)], 3)
    bridge.loft([(-1.91, .80, 2.00, 2.26), (-.44, .80, 2.00, 2.26),
                 (.15, .48, 1.98, 2.12)], 0)
    bridge.object(f'FF_LOD{lod}_LayeredBridge', col, lod, 'Superstructure')
    sensor = Geometry()
    for x, y, height in [(0, -1.25, 3.48), (-.64, -1.56, 3.04), (.64, -1.56, 3.04)]:
        sensor.box((x, y, 2.48), (.19, .22, .51), 1, b)
        sensor.cylinder((x, y, 2.6), (x, y, height), .039, 8 if lod < 2 else 4, 2)
        if lod == 0:
            sensor.beam((x - .23, y, height - .28), (x + .23, y, height - .28), .035, .035, 2)
    if lod < 2:
        sensor.box((0, -1.18, 2.72), (.62, .17, .29), 2, b)
        for side in (-1, 1):
            sensor.box((side * 1.16, -1.54, 1.85), (.32, .55, .42), 1, b)
    sensor.object(f'FF_LOD{lod}_Sensors', col, lod, 'Superstructure')
    turret = Geometry()
    turret.cylinder((0, 0, -.04), (0, 0, .095), .42, 20 if lod == 0 else 12 if lod == 1 else 6, 1)
    turret.loft([(-.35, .36, .08, .32), (.19, .36, .08, .32), (.42, .23, .08, .23)], 0)
    for side in (-1, 1):
        turret.cylinder((side * .12, .27, .22), (side * .12, 1.12, .22), .048,
                        10 if lod == 0 else 6 if lod == 1 else 4, 2)
        if lod == 0:
            turret.box((side * .12, .39, .22), (.14, .26, .15), 1, .025)
    proto = turret.object(f'FF_LOD{lod}_Turret_01', col, lod, 'Superstructure', (-1.23, 2.15, 1.12))
    linked(proto, f'FF_LOD{lod}_Turret_02', col, (1.23, 2.15, 1.12))
    linked(proto, f'FF_LOD{lod}_Turret_03', col, (0, 6.25, .89))


def create_details(lod, col):
    radiator = Geometry()
    b = .025 if lod == 0 else 0
    for side in (-1, 1):
        radiator.beam((side * 2.05, -6.05, .92), (side * 3.55, -7.26, 1.14), .25, .28, 2, b)
        # Two moderate, thick-backed radiator assemblies, connected to the drive mount.
        radiator.box((side * 3.26, -7.12, 1.22), (.86, 1.66, .16), 1, b)
        if lod < 2:
            for dy in (-.55, 0, .55):
                radiator.box((side * 3.26, -7.12 + dy, 1.34), (.80, .11, .16), 2, b)
    radiator.object(f'FF_LOD{lod}_RadiatorBanks', col, lod, 'FusionDrive')
    if lod < 2:
        detail = Geometry()
        for y in (-4.9, -3.05, 1.02):
            detail.box((0, y, 1.38), (1.13, .79, .09), 1, b)
        for side in (-1, 1):
            # Narrow inset colour strips, no lights or emissions.
            detail.beam((side * 1.58, 5.05, .59), (side * 1.17, 6.57, .46), .035, .08, 3)
            detail.box((side * 2.88, -5.17, .67), (.04, .58, .07), 3)
        if lod == 0:
            for side in (-1, 1):
                for y in (-4.70, -2.55):
                    detail.box((side * 1.22, y, 1.38), (.72, 1.36, .055), 2, .01)
                detail.beam((side * .72, 7.3, .68), (side * .45, 8.45, .54), .04, .06, 1)
            for y in (-3.8, -1.6, .6):
                detail.box((0, y, -1.53), (.84, .59, .13), 1, .025)
        detail.object(f'FF_LOD{lod}_ServicePanels', col, lod, 'Details')


AUX_POSITIONS = [(-2.50, -7.0, .15), (2.50, -7.0, .15),
                 (-1.62, -7.0, -2.0), (1.62, -7.0, -2.0)]


def set_lod(scene, index):
    for layer in scene.view_layers[0].layer_collection.children:
        if layer.name == 'FusionFrigate':
            for child in layer.children:
                if child.name.startswith('FusionFrigate_LOD'):
                    child.exclude = int(child.name[-1]) != index
    for j in range(3):
        bpy.data.collections[f'FusionFrigate_LOD{j}'].hide_render = j != index
    bpy.context.view_layer.update()


def mesh_stats(scene):
    result = {}
    for lod in range(3):
        set_lod(scene, lod)
        graph = bpy.context.evaluated_depsgraph_get()
        objects = [o for o in bpy.data.collections[f'FusionFrigate_LOD{lod}'].all_objects if o.type == 'MESH']
        tris, points, details = 0, [], []
        for obj in objects:
            evaluated = obj.evaluated_get(graph)
            mesh = evaluated.to_mesh()
            mesh.calc_loop_triangles()
            count = len(mesh.loop_triangles)
            tris += count
            points.extend(evaluated.matrix_world @ v.co for v in mesh.vertices)
            details.append({'object': obj.name, 'triangles': count, 'mesh': obj.data.name})
            evaluated.to_mesh_clear()
        lo = [min(v[i] for v in points) for i in range(3)]
        hi = [max(v[i] for v in points) for i in range(3)]
        result[f'LOD{lod}'] = {'triangles': tris, 'mesh_objects': len(objects),
                             'unique_meshes': len({o.data.as_pointer() for o in objects}),
                             'min': lo, 'max': hi, 'dimensions': [hi[i] - lo[i] for i in range(3)],
                             'objects': details}
    set_lod(scene, 0)
    return result


def create_sockets(root_col, root_obj):
    collection = bpy.data.collections.new('FusionFrigate_Sockets')
    root_col.children.link(collection)
    parent = bpy.data.objects.new('Sockets', None)
    parent.parent = root_obj
    collection.objects.link(parent)
    socket_locations = [('MainExhaust', (0, -11.07, -.05))]
    socket_locations.extend((f'AuxiliaryExhaust_{i + 1:02}', (x, y - 5.07 * .7, z))
                            for i, (x, y, z) in enumerate(AUX_POSITIONS))
    for name, loc in socket_locations:
        obj = bpy.data.objects.new(name, None)
        collection.objects.link(obj)
        obj.parent = parent
        obj.location = loc
        obj.rotation_euler = (PI / 2, 0, 0)  # Socket local +Z points outward along Blender -Y.
        obj.empty_display_type = 'ARROWS'
        obj.empty_display_size = .35
        obj.hide_set(True)
        obj['ff_socket'] = True
        obj['ff_forward'] = 'local +Z = exhaust direction'
    return socket_locations


def setup_preview(scene):
    collection = bpy.data.collections.new('FusionFrigate_Preview_ONLY')
    scene.collection.children.link(collection)
    camera_data = bpy.data.cameras.new('FF_InspectionCamera')
    camera_data.type = 'ORTHO'
    camera = bpy.data.objects.new('FF_InspectionCamera', camera_data)
    collection.objects.link(camera)
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = 960, 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.studiolight_rotate_z = .25
    shading.color_type = 'MATERIAL'
    shading.show_shadows = True
    shading.show_cavity = True
    shading.cavity_type = 'BOTH'
    shading.curvature_ridge_factor = 1.1
    shading.curvature_valley_factor = .75
    shading.cavity_ridge_factor = .8
    shading.cavity_valley_factor = .6
    shading.show_object_outline = False
    shading.background_type = 'WORLD'
    scene.world = bpy.data.worlds.new('FF_NeutralWorld')
    scene.world.color = (.065, .073, .085)
    scene.view_settings.view_transform = 'Standard'
    scene.render.film_transparent = False
    point_camera(scene, (27, 30, 21), (0, 0, .2), 26)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.clip_end = 1000
                area.spaces.active.region_3d.view_distance = 28
                area.spaces.active.region_3d.view_location = (0, 0, .2)
                area.spaces.active.region_3d.view_rotation = scene.camera.rotation_euler.to_quaternion()
                area.spaces.active.shading.type = 'SOLID'
                area.spaces.active.shading.color_type = 'MATERIAL'


def point_camera(scene, location, target, scale):
    scene.camera.location = location
    scene.camera.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    scene.camera.data.ortho_scale = scale
    bpy.context.view_layer.update()


def build():
    if not bpy.app.background or bpy.data.filepath:
        raise RuntimeError('Build requires a fresh --background --factory-startup process; open authoring scenes are protected.')
    stamp = EVIDENCE / 'build_manifest.json'
    if BLEND.exists():
        if not stamp.exists() or json.loads(stamp.read_text(encoding='utf-8')).get('blend_sha256') != hashlib.sha256(BLEND.read_bytes()).hexdigest():
            raise RuntimeError('Existing FusionFrigate source differs from the last generated hash. Preserve manual edits; build aborted.')
    # Remove only the three known factory startup objects in this isolated process.
    for name, expected_type in [('Cube', 'MESH'), ('Camera', 'CAMERA'), ('Light', 'LIGHT')]:
        obj = bpy.data.objects.get(name)
        if obj and obj.type == expected_type:
            data = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if data.users == 0:
                {'MESH': bpy.data.meshes, 'CAMERA': bpy.data.cameras, 'LIGHT': bpy.data.lights}[expected_type].remove(data)
    scene = bpy.context.scene
    scene.name = 'FusionFrigate_Authoring'
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene['ff_authoring'] = 'metres; +Y forward; +Z up; original game-size interpretation'
    root_col = bpy.data.collections.new('FusionFrigate')
    scene.collection.children.link(root_col)
    root_obj = bpy.data.objects.new('FusionFrigate', None)
    root_col.objects.link(root_obj)
    root_obj['ff_owned'] = True
    root_obj['reference'] = 'ArtSource/References/FusionFrigate/FusionFrigate_reference.png'
    root_obj['ff_axis_contract'] = 'Blender +Y forward / +Z up; Unity +Z forward / +Y up'
    make_palette()
    for lod in range(3):
        col = bpy.data.collections.new(f'FusionFrigate_LOD{lod}')
        root_col.children.link(col)
        parent = bpy.data.objects.new(f'FusionFrigate_LOD{lod}', None)
        parent.parent = root_obj
        col.objects.link(parent)
        create_hull(lod, col)
        create_superstructure(lod, col)
        main = drive(lod)
        main.object(f'FF_LOD{lod}_MainFusionDrive', col, lod, 'FusionDrive', (0, -6.0, -.05))
        aux = drive(lod, True)
        proto = aux.object(f'FF_LOD{lod}_AuxiliaryDrive_01', col, lod, 'FusionDrive', AUX_POSITIONS[0])
        for i, pos in enumerate(AUX_POSITIONS[1:], 2):
            linked(proto, f'FF_LOD{lod}_AuxiliaryDrive_{i:02}', col, pos)
        create_details(lod, col)
    socket_locations = create_sockets(root_col, root_obj)
    setup_preview(scene)
    stats = mesh_stats(scene)
    set_lod(scene, 0)
    # Save only LOD0 visible. Camera is an inspection aid, never exported.
    bpy.context.view_layer.objects.active = None
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    manifest = {'blender': bpy.app.version_string, 'build_hash': bpy.app.build_hash.decode(),
                'python': sys.version, 'stats': stats, 'sockets': socket_locations,
                'materials': [m.name for m in MATERIALS], 'textures': 0,
                'source': str(BLEND.relative_to(ROOT)),
                'blend_sha256': hashlib.sha256(BLEND.read_bytes()).hexdigest()}
    stamp.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print('FUSION_BUILD_COMPLETE', json.dumps({k: {a: b for a, b in v.items() if a != 'objects'} for k, v in stats.items()}))


def render():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    scene = bpy.context.scene
    views = [
        ('ThreeQuarter', (27, 30, 21), (0, 0, .15), 26),
        ('Side', (35, 0, 0), (0, 0, .15), 25),
        ('Top', (0, 0, 38), (0, 0, 0), 25),
        ('Rear', (0, -38, 0), (0, -5, .25), 10.8),
        ('Underside', (23, 26, -19), (0, 0, -.3), 25),
        ('Bow', (0, 35, 0), (0, 0, .35), 10.8),
        ('DriveDetail', (13, -22, 9), (0, -7.65, -.10), 12.5),
    ]
    out = ART / 'Previews'
    out.mkdir(exist_ok=True)
    for lod in range(3):
        set_lod(scene, lod)
        for name, location, target, scale in views if lod == 0 else [views[0], views[3]]:
            point_camera(scene, location, target, scale)
            if name == 'Top':
                scene.camera.rotation_euler = (scene.camera.rotation_euler.to_quaternion() @
                    Matrix.Rotation(PI / 2, 4, 'Z').to_quaternion()).to_euler()
            scene.render.filepath = str(out / f'FusionFrigate_LOD{lod}_{name}.png')
            bpy.ops.render.render(write_still=True)
            print('RENDERED', scene.render.filepath, flush=True)
    # No inspection state is written back to the authoring file.


def main():
    args = argparse.ArgumentParser()
    args.add_argument('--phase', choices=['build', 'render'], default='build')
    cfg = args.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    for p in (ART, EXPORT, EVIDENCE):
        p.mkdir(parents=True, exist_ok=True)
    {'build': build, 'render': render}[cfg.phase]()


if __name__ == '__main__':
    main()
