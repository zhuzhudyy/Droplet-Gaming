"""Read-only independent Blender source reopen and FBX roundtrip pose audit."""
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("fleet_expansion_author", Path(__file__).with_name("author_layout.py"))
author = importlib.util.module_from_spec(spec)
spec.loader.exec_module(author)
config = json.loads(author.CONFIG.read_text(encoding="utf-8"))
if not bpy.app.background or bpy.data.filepath:
    raise RuntimeError("Use an independent --background --factory-startup process.")


def audit(context):
    actual = {o.name: o for o in bpy.data.objects if o.name.startswith("SPAWN_")}
    if len(actual) != config["targetCount"]:
        raise AssertionError("Expected 120 distinct marker objects in " + context)
    max_error = 0
    max_scale_error = 0
    for expected in config["markers"]:
        obj = actual[expected["name"]]
        p = expected["position"]
        # Compare imported float coordinates against JSON doubles; converting
        # both sides to Vector first would hide float quantization error.
        error = math.dist(tuple(obj.matrix_world.translation), (p[0], p[2], p[1]))
        max_error = max(max_error, error)
        scale_error = (obj.matrix_world.to_scale() - Vector((1, 1, 1))).length
        max_scale_error = max(max_scale_error, scale_error)
        assert error < .001, (context, obj.name, "position", error)
        assert scale_error < .00001, (context, obj.name, "scale", scale_error)
        # Blender's reimport represents the baked mesh basis with an object
        # rotation and inverse mesh data rotation. Inspect the asymmetric
        # physical proxy directions, rather than mistaking local mesh axes for
        # source Empty axes. Actual Unity Transform is checked by its importer.
        matrix = obj.matrix_world.to_3x3().normalized()
        if obj.type == "MESH":
            forward = (matrix @ (obj.data.vertices[2].co - obj.data.vertices[0].co)).normalized()
            up = (matrix @ (obj.data.vertices[3].co - obj.data.vertices[0].co)).normalized()
        else:
            forward = matrix @ Vector((0, 1, 0))
            up = matrix @ Vector((0, 0, 1))
        assert (forward - Vector((0, 1, 0))).length < .00001, (context, obj.name, "forward")
        assert (up - Vector((0, 0, 1))).length < .00001, (context, obj.name, "up")
    return dict(context=context, passed=True, markerCount=len(actual), maximumPositionErrorMeters=max_error,
                maximumUnitScaleError=max_scale_error, objectCount=len(bpy.data.objects),
                meshes=len(bpy.data.meshes), materials=len(bpy.data.materials), images=len(bpy.data.images),
                cameras=sum(o.type == "CAMERA" for o in bpy.data.objects), lights=sum(o.type == "LIGHT" for o in bpy.data.objects))


before = author.digest(author.SOURCE)
bpy.ops.wm.open_mainfile(filepath=str(author.SOURCE))
bpy.context.view_layer.update()
source = audit("independent saved source reopen")
assert source["meshes"] == 0 and source["cameras"] == 0 and source["lights"] == 0
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(author.EXPORT / "FleetLayout_Expanded.fbx"), use_anim=False)
bpy.context.view_layer.update()
roundtrip = audit("independent exported FBX reimport in Blender")
assert roundtrip["cameras"] == 0 and roundtrip["lights"] == 0
assert author.digest(author.SOURCE) == before
report = dict(passed=True, blender=bpy.app.version_string, configSha256=author.digest(author.CONFIG),
              sourceReopen=source, fbxRoundtrip=roundtrip, sourceUnchanged=True,
              note="FBX mesh objects are tiny transform proxies only; actual Unity importer/scene validation is separately required.")
author.write_json(author.EVIDENCE / "layout-source-roundtrip.json", report)
print("FLEET_LAYOUT_ROUNDTRIP " + json.dumps(report))
