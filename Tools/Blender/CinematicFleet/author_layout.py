"""Blender-owned 20 x 25 x 4 fleet; no Unity/interactive Blender writes.

blender --background --factory-startup --python-exit-code 1 --python this.py -- --generate
Use --verify for saved-source round trip; --render for front/top/side/perspective.
The existing model/FBX/old layout are read-only. All 2,000 IDs are retained.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
CONFIG = HERE / "layout_config.json"
OLD = ROOT / "Tools/Blender/Fleet2000Sun/layout_config.json"
MODEL = ROOT / "ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend"
FBX = ROOT / "ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx"
SOURCE = ROOT / "ArtSource/Blender/CinematicFleet/FleetLayout_Cubic.blend"
EXPORT = ROOT / "ArtSource/Exports/CinematicFleet"
LAYOUT = EXPORT / "FleetLayout_Cubic.json"
RECEIPT = EXPORT / "generation.json"
OWNER = "CinematicFleet"


def rel(path): return path.relative_to(ROOT).as_posix()
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
def sub(a, b): return [a[i] - b[i] for i in range(3)]
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def unit(v):
    n = math.sqrt(dot(v, v))
    assert n > 1e-10
    return [x / n for x in v]
def cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def swap(v): return (v[0], v[2], v[1])


def authored_config():
    old = json.loads(OLD.read_text(encoding="utf-8"))
    ids = sorted(old["markers"], key=lambda x: x["name"])
    assert len(ids) == len({x["name"] for x in ids}) == 2000
    spawn = [0, 8, 0]
    config = dict(schemaVersion=1, authority="Blender-owned CinematicFleet JSON; Unity consumes exported poses verbatim.",
        sourceScene="Assets/_Project/Scenes/FleetAssault_Enhanced.unity",
        destinationScene="Assets/_Project/Scenes/FleetAssault_CinematicAudio_Cubic.unity",
        targetCount=2000, columns=20, rows=25, layers=4,
        requestResolution="20 x 25 x 2 is 1,000; retain the required 2,000 unique ships with 20 x 25 x 4. The 20 x 25 front face is the XY plane.",
        units="Unity units; physical metre scale unchanged", metersPerUnityUnit=100,
        spacing=[750, 600, 4800], columnZeroSlot=-9, rowZeroSlot=-12,
        origin=[0, 8, 1000], playerSpawn=spawn, playerForward=[0, 0, 1],
        fixedInitialFacingTarget=spawn, formationCenter=[375, 8, 8200],
        sourceIds=rel(OLD), sourceIdsSha256=sha(OLD),
        coordinateSpace="Unity +X right +Y up +Z bow; Blender preview maps Unity (x,y,z) to (x,z,y), with model +Y bow/+Z up. No additional Unity axis conversion.",
        shipLength=57.564, shipBounds=old["shipBounds"], previewModelScale=2.6,
        previewSourceModel=rel(MODEL), sharedModelFbx=rel(FBX),
        orientationEvidence="Existing build_fusion_frigate.py ProwArmor +Y; MainExhaust at Y=-11.07. export_fusion_frigate.py validates positive-Y narrow bow; calibrated Unity +Z bow.",
        markers=[])
    for index, identity in enumerate(ids):
        layer, cell = divmod(index, 500)
        column, row = divmod(cell, 25)
        position = [(column-9)*750, 8+(row-12)*600, 1000+layer*4800]
        forward = unit(sub(spawn, position))
        right = unit(cross([0, 1, 0], forward))
        up = unit(cross(forward, right))
        config["markers"].append(dict(name=identity["name"], id=identity["id"], modelId="FusionFrigate",
            group=f"Cubic_Layer_{layer+1:02d}", layer=layer, column=column, row=row,
            position=position, forward=forward, up=up, scale=[1, 1, 1]))
    config["measurements"] = measure(config)
    return config


def marker_bounds(marker, config):
    f, u, p = marker["forward"], marker["up"], marker["position"]
    r = cross(u, f)
    corners = []
    low, high = config["shipBounds"]["min"], config["shipBounds"]["max"]
    for x in (low[0], high[0]):
        for y in (low[1], high[1]):
            for z in (low[2], high[2]):
                corners.append([p[i] + r[i]*x + u[i]*y + f[i]*z for i in range(3)])
    return ([min(v[i] for v in corners) for i in range(3)], [max(v[i] for v in corners) for i in range(3)])


def measure(config):
    markers = config["markers"]
    assert len(markers) == len({m["name"] for m in markers}) == 2000
    assert len({(m["layer"], m["column"], m["row"]) for m in markers}) == 2000
    previous = json.loads(OLD.read_text(encoding="utf-8"))
    assert {m["name"] for m in markers} == {m["name"] for m in previous["markers"]}
    spawn = config["playerSpawn"]
    bounds = []
    for m in markers:
        assert m["scale"] == [1, 1, 1] and m["modelId"] == "FusionFrigate"
        assert dot(m["forward"], unit(sub(spawn, m["position"]))) > .999999999
        assert abs(dot(m["forward"], m["up"])) < 1e-8
        bounds.append(marker_bounds(m, config))
    min_gap2 = float("inf")
    overlaps = 0
    for i, (a_min, a_max) in enumerate(bounds):
        for b_min, b_max in bounds[i+1:]:
            gaps = [max(0, a_min[k]-b_max[k], b_min[k]-a_max[k]) for k in range(3)]
            gap2 = dot(gaps, gaps)
            overlaps += gap2 == 0
            min_gap2 = min(min_gap2, gap2)
    assert overlaps == 0
    mins = [min(m["position"][i] for m in markers) for i in range(3)]
    maxs = [max(m["position"][i] for m in markers) for i in range(3)]
    spans = sub(maxs, mins)
    spawn_clearance = min(math.sqrt(sum(max(0, lo[k]-spawn[k], spawn[k]-hi[k])**2 for k in range(3))) for lo, hi in bounds)
    return dict(count=2000, uniqueIds=2000, retainedOldIds=2000, columns=20, rows=25, layers=4,
        centerMin=mins, centerMax=maxs, centerSpan=spans, spanAspectRatio=max(spans)/min(spans),
        allShipPairsChecked=1999000, rotatedVisualAabbOverlaps=overlaps,
        minimumRotatedVisualAabbClearance=math.sqrt(min_gap2), playerVisualAabbClearance=spawn_clearance,
        nearestShipCenterDistance=min(math.dist(m["position"], spawn) for m in markers),
        minimumForwardDot=min(dot(m["forward"], unit(sub(spawn, m["position"]))) for m in markers),
        method="Exhaustive all-pairs world AABBs of all eight rotated calibrated full-hull bounds corners. Conservative against interpenetration, not a substitute for Unity collider/runtime verification.")


def guard():
    import bpy
    if not bpy.app.background or bpy.data.filepath:
        raise RuntimeError("Use a fresh background factory-startup process; interactive work is protected.")
    if SOURCE.exists():
        previous = json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {}
        if previous.get("blendSha256") != sha(SOURCE):
            raise RuntimeError("Saved .blend changed since generation. Refusing to overwrite author edits.")


def generate():
    import bpy
    from mathutils import Matrix, Vector
    guard()
    config = authored_config()
    hashes = {rel(p): sha(p) for p in (MODEL, FBX, OLD)}
    bpy.ops.wm.open_mainfile(filepath=str(MODEL))
    for collection in bpy.data.collections:
        collection.hide_viewport = collection.hide_render = False
    def reveal(layer):
        layer.exclude = layer.hide_viewport = False
        for child in layer.children: reveal(child)
    reveal(bpy.context.view_layer.layer_collection)
    for obj in bpy.context.scene.objects:
        obj.hide_set(False)
        obj.hide_viewport = False
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    meshes = []
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH" or obj.get("ff_lod") != 2: continue
        mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(graph), preserve_all_data_layers=True, depsgraph=graph)
        mesh.transform(Matrix.Scale(2.6, 4) @ obj.matrix_world)
        mesh.calc_loop_triangles()
        meshes.append((obj.name, mesh))
    assert meshes
    scene = bpy.data.scenes.new("CinematicFleet_Cubic_Authoring")
    bpy.context.window.scene = scene
    for old in list(bpy.data.scenes):
        if old != scene: bpy.data.scenes.remove(old)
    for obj in list(bpy.data.objects): bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections): bpy.data.collections.remove(collection)
    prototype = bpy.data.collections.new("FusionFrigate_SHARED_LOD2_57_564_UU")
    for name, mesh in meshes:
        prototype.objects.link(bpy.data.objects.new(name, mesh))
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0: bpy.data.meshes.remove(mesh)
    for blocks in (bpy.data.materials, bpy.data.images, bpy.data.cameras, bpy.data.lights, bpy.data.worlds):
        for block in list(blocks):
            if block.users == 0: blocks.remove(block)
    owner = bpy.data.collections.new("CinematicFleet_2000_AuthoredInstances")
    scene.collection.children.link(owner)
    layers = []
    for i in range(4):
        layer = bpy.data.collections.new(f"Depth_{i+1:02d}_500_Ships")
        owner.children.link(layer)
        layers.append(layer)
    for marker in config["markers"]:
        obj = bpy.data.objects.new(marker["name"], None)
        layers[marker["layer"]].objects.link(obj)
        obj.instance_type, obj.instance_collection = "COLLECTION", prototype
        obj.location = swap(marker["position"])
        f, u = marker["forward"], marker["up"]
        r = cross(u, f)
        rotation = Matrix((swap(r), swap(f), swap(u))).transposed()
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = rotation.to_quaternion()
        obj.empty_display_size = 20
        for key in ("id", "modelId", "layer", "column", "row"):
            obj[key] = marker[key]
        obj["fleet_owner"] = OWNER
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene["authority"] = rel(CONFIG)
    scene["coordinate_contract"] = config["coordinateSpace"]
    scene["fixed_initial_facing_target_unity"] = config["playerSpawn"]
    setup_review(scene, config)
    bpy.context.view_layer.update()
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE), compress=True)
    write(CONFIG, config)
    write(LAYOUT, config)
    for path, digest in hashes.items(): assert sha(ROOT/path) == digest
    receipt = dict(blender=bpy.app.version_string, build=bpy.app.build_hash.decode(), python=sys.version,
        blendPath=rel(SOURCE), blendSha256=sha(SOURCE), layoutPath=rel(LAYOUT), layoutSha256=sha(LAYOUT),
        authorityPath=rel(CONFIG), authoritySha256=sha(CONFIG), preservedSources=hashes,
        instances=2000, sharedCollections=1, sharedPrototypeMeshCount=len(meshes),
        sharedPrototypeTriangles=sum(len(m.loop_triangles) for _, m in meshes), measurements=config["measurements"])
    write(RECEIPT, receipt)
    print("CINEMATIC_FLEET_GENERATED " + json.dumps(receipt))


def setup_review(scene, config):
    import bpy
    from mathutils import Vector
    review = bpy.data.collections.new("Review_ONLY_NOT_EXPORTED")
    scene.collection.children.link(review)
    center = Vector(swap(config["formationCenter"]))
    camera = bpy.data.objects.new("ReviewPerspective", bpy.data.cameras.new("ReviewPerspective"))
    review.objects.link(camera)
    camera.location = center + Vector((-23000, -34000, 24000))
    camera.rotation_euler = (center-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "PERSP"
    camera.data.lens = 45
    camera.data.clip_end = 150000
    scene.camera = camera
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (.48, .69, .86)
    scene.display.shading.show_shadows = False
    scene.display.shading.show_cavity = True
    scene.display.shading.background_type = "WORLD"
    scene.world = bpy.data.worlds.new("Cubic_Review_World")
    scene.world.color = (.013, .02, .035)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                area.spaces.active.clip_end = 150000
                area.spaces.active.region_3d.view_perspective = "CAMERA"
                area.spaces.active.overlay.show_extras = False


def verify():
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
    objects = [o for o in bpy.context.scene.objects if o.get("fleet_owner") == OWNER]
    lookup = {m["id"]: m for m in config["markers"]}
    assert len(objects) == len({o["id"] for o in objects}) == 2000
    max_position_error, min_direction_dot = 0, 1
    for obj in objects:
        marker = lookup[obj["id"]]
        max_position_error = max(max_position_error, (obj.location-Vector(swap(marker["position"]))).length)
        f = obj.rotation_quaternion @ Vector((0, 1, 0))
        min_direction_dot = min(min_direction_dot, f.dot(Vector(swap(marker["forward"]))))
        assert (obj.scale - Vector((1, 1, 1))).length < 1e-6
    assert max_position_error < .001 and min_direction_dot > .999999
    assert len({o.instance_collection.as_pointer() for o in objects}) == 1
    assert sha(CONFIG) == sha(LAYOUT) == receipt["layoutSha256"]
    assert sha(SOURCE) == receipt["blendSha256"]
    for path, digest in receipt["preservedSources"].items(): assert sha(ROOT/path) == digest
    report = dict(passed=True, blender=bpy.app.version_string, savedInstances=len(objects),
        sharedCollections=1, maxPositionError=max_position_error, minActualBlenderBowDot=min_direction_dot,
        sourceHashesUnchanged=True, measurements=measure(config))
    write(EXPORT/"verification.json", report)
    print("CINEMATIC_FLEET_VERIFY_PASS " + json.dumps(report))


def render():
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene = bpy.context.scene
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    center = Vector(swap(config["formationCenter"]))
    for name, offset, scale in (("perspective", (-23000, -34000, 24000), 28000),
                                ("front", (0, -30000, 0), 17500),
                                ("side", (30000, 0, 0), 17500),
                                ("top", (0, 0, 30000), 17500)):
        scene.camera.location = center + Vector(offset)
        scene.camera.rotation_euler = (center-scene.camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera.data.type = "PERSP" if name == "perspective" else "ORTHO"
        scene.camera.data.ortho_scale = scale
        scene.render.filepath = str(EXPORT/f"review_{name}.png")
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--generate", action="store_true")
    parser.add_argument("--verify", action="store_true")
    parser.add_argument("--render", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if args.generate: generate()
    if args.verify: verify()
    if args.render: render()
