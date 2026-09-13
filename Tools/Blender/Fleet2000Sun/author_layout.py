"""Author the regular 2,000-ship fleet using existing FusionFrigate LOD2 instances.

The established schema-1 JSON marker format remains authoritative. --reflow is
an explicit parameter-to-pose authoring action; --generate makes its editable
Blender review source and JSON export. --export-saved explicitly publishes edits
from that saved Blender file back to the same JSON authority, never a second
runtime data source. Existing model sources and FBX files are read-only.

Run from a fresh installed Blender --background --factory-startup process with
--python-exit-code 1 --python this_script.py -- --generate / --verify / --render.
Plain Python can run --reflow before Blender generation.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import statistics
import sys

ROOT = Path(__file__).resolve().parents[3]
CONFIG = Path(__file__).with_name("layout_config.json")
SOURCE_MODEL = ROOT / "ArtSource/Blender/Ships/FusionFrigate/FusionFrigate.blend"
MODEL_FBX = ROOT / "ArtSource/Exports/Ships/FusionFrigate/FusionFrigate.fbx"
SOURCE = ROOT / "ArtSource/Blender/Fleet2000Sun/FleetLayout_2000_Sun.blend"
EXPORT = ROOT / "ArtSource/Exports/Fleet2000Sun"
LAYOUT_EXPORT = EXPORT / "FleetLayout_2000_Sun.json"
RECEIPT = EXPORT / "FleetLayout_2000_Sun_manifest.json"
EVIDENCE = ROOT / "docs/verification/Fleet2000Sun"
OWNER = "Fleet2000Sun"


def relative(path):
    return path.relative_to(ROOT).as_posix()


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def bounds(points, low=(0, 0, 0), high=(0, 0, 0)):
    minimum = [min(p[i] for p in points) + low[i] for i in range(3)]
    maximum = [max(p[i] for p in points) + high[i] for i in range(3)]
    return dict(min=minimum, max=maximum, size=[maximum[i] - minimum[i] for i in range(3)])


def default_config():
    length = 57.564
    return dict(schemaVersion=1,
        authority="This JSON is the sole authored fleet authority, following FleetExpansion schema 1. Stored markers are consumed verbatim. Blender --export-saved explicitly publishes saved layout edits here; runtime consumes only the hash-traceable export copy.",
        sourceScene="Assets/_Project/Scenes/FleetAssault_VisualUpgrade.unity",
        destinationScene="Assets/_Project/Scenes/FleetAssault_2000_Sun.unity",
        originalCount=120, targetCount=2000, modelId="FusionFrigate", shipLength=length,
        shipBounds=dict(min=[-3.69*2.6, -2.731535*2.6, -11.07*2.6], max=[3.69*2.6, 3.48*2.6, 11.07*2.6]),
        shipDimensionProvenance="Existing FusionFrigate LOD0/1/2 calibrated source bounds, uniformly enlarged 2.6 by the existing VisualUpgrade prefab. Each layout instance remains unit scale. Blender preview bakes the same constant once into its shared LOD2 prototype.",
        units="metres", metersPerUnit=1,
        coordinateSpace="Unity +X right, +Y up, +Z forward; JSON already stores Unity-space poses. Blender authoring maps (x,y,z)_Unity to (x,z,y)_Blender with local +Y ship forward and +Z ship up. Do not swap JSON axes again in Unity.",
        rotationConvention="rotationQuaternion is [x,y,z,w] in Unity; forward/up remain the existing schema's canonical orientation vectors; yaw/pitch are degrees.",
        columns=50, rows=20, layers=2, lateralSpacing=3*length,
        forwardSpacing=4*length, verticalSpacing=2*length,
        layerOffset=[.25*length, 0, .5*length], origin=[0,8,300], columnZeroSlot=-24,
        playerSpawn=[0,8,0], playerForward=[0,0,1],
        attackCorridor=dict(layer=0,column=24,centerX=0,centerY=8,firstCenterZ=300,
            lastCenterZ=300+19*4*length,ships=20,description="Twenty aligned, independently destroyable targets. Adjacent columns leave over 153 m of clear lateral space; the second layer is 115.128 m above. No perspective-dependent spacing or per-instance resizing."),
        previewSourceModel=relative(SOURCE_MODEL), previewLod=2, previewModelScale=2.6,
        sharedModelFbx=relative(MODEL_FBX), modelExportsThisTask=0,
        reference="ArtSource/thesun.png (composition only; no watermark, playback control, pixels or textures copied)",
        formation="Regular equal centre spacing; only a constant small layer offset. Perspective convergence is produced by real camera projection.",
        suggestedBoundaryRadius=7800, suggestedWarningRadius=7000, turningReserve=700,
        timing=dict(cruiseSpeed=52,boostSpeed=156,cruiseDistanceFraction=.6,
            secondsPerLaneTurn=3,secondsPerTargetAlignment=.5,marginMultiplier=1.15),
        markers=[])


def reflow(config):
    markers = []
    for layer in range(config["layers"]):
        for column in range(config["columns"]):
            for row in range(config["rows"]):
                identity = f"FS{layer+1:01d}C{column+1:02d}R{row+1:02d}"
                pos = [config["origin"][0] + (column+config["columnZeroSlot"])*config["lateralSpacing"] + layer*config["layerOffset"][0],
                    config["origin"][1] + layer*config["verticalSpacing"],
                    config["origin"][2] + row*config["forwardSpacing"] + layer*config["layerOffset"][2]]
                markers.append(dict(name="SPAWN_Small_"+identity,id=identity,modelId=config["modelId"],
                    group=f"Layer_{layer+1:02d}",squadron=layer+1,layer=layer,column=column,row=row,
                    position=[round(v,6) for v in pos], forward=[0,0,1],up=[0,1,0],
                    rotationQuaternion=[0,0,0,1],scale=[1,1,1],yaw=0,pitch=0))
    config["markers"] = markers
    config["measurements"] = measure(config)
    config["routeEstimate"] = route_estimate(config)
    config["missionDurationSeconds"] = config["routeEstimate"]["roundedMissionSeconds"]
    write_json(CONFIG,config)
    write_json(LAYOUT_EXPORT, config)


def measure(config):
    markers = config["markers"]
    assert len(markers) == 2000 == config["targetCount"]
    assert len({m["id"] for m in markers}) == len({m["name"] for m in markers}) == 2000
    assert len({(m["layer"],m["column"],m["row"]) for m in markers}) == 2000
    for m in markers:
        assert m["scale"] == [1,1,1] and m["modelId"] == "FusionFrigate"
        assert m["forward"] == [0,0,1] and m["up"] == [0,1,0]
        assert all(math.isfinite(v) for v in m["position"])
    points=[m["position"] for m in markers]
    nearest=[float("inf")]*len(points)
    overlap_count=0
    dimensions=[config["shipBounds"]["max"][i]-config["shipBounds"]["min"][i] for i in range(3)]
    min_surface_distance=float("inf")
    for i,p in enumerate(points):
        for j in range(i+1,len(points)):
            q=points[j]
            delta=[abs(p[k]-q[k]) for k in range(3)]
            d2=sum(v*v for v in delta)
            nearest[i]=min(nearest[i],d2)
            nearest[j]=min(nearest[j],d2)
            gaps=[max(delta[k]-dimensions[k],0) for k in range(3)]
            gap2=sum(v*v for v in gaps)
            if gap2==0:
                overlap_count+=1
            min_surface_distance=min(min_surface_distance,gap2)
    nearest=[math.sqrt(v) for v in nearest]
    assert overlap_count==0,"Fleet contains intersecting full-ship AABBs."
    return dict(count=2000,uniqueIds=2000,columns=50,rows=20,layers=2,
        centerBounds=bounds(points), visualBounds=bounds(points,config["shipBounds"]["min"],config["shipBounds"]["max"]),
        nearestMin=min(nearest),nearestMedian=statistics.median(nearest),nearestMax=max(nearest),
        shipPairVisualAabbOverlapCount=overlap_count,allShipPairsChecked=1999000,
        minimumVisualAabbClearance=math.sqrt(min_surface_distance),
        maximumFleetRadiusFromSolarOrigin=max(math.dist(p,[0,20,260]) for p in points)+math.sqrt(sum((v*.5)**2 for v in dimensions)),
        equalSpacing=dict(lateral=config["lateralSpacing"],longitudinal=config["forwardSpacing"],vertical=config["verticalSpacing"]),
        orientation="Every ship +Z forward/+Y up in JSON and Unity. Every Blender instance +Y forward/+Z up.",
        method="Exhaustive pairwise nearest centres and conservative full-ship visual AABB separation. No claim about Unity rock placement or gameplay collision tests.")


def route_estimate(config):
    lookup={(m["layer"],m["column"],m["row"]):m for m in config["markers"]}
    route=[]
    for layer in range(config["layers"]):
        columns=range(config["columns"]) if layer==0 else reversed(range(config["columns"]))
        for column in columns:
            rows=range(config["rows"]) if (len(route)//config["rows"])%2==0 else reversed(range(config["rows"]))
            route.extend(lookup[(layer,column,row)] for row in rows)
    points=[config["playerSpawn"]]+[m["position"] for m in route]
    length=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
    t=config["timing"]
    speed=1/(t["cruiseDistanceFraction"]/t["cruiseSpeed"]+(1-t["cruiseDistanceFraction"])/t["boostSpeed"])
    turns=(config["columns"]*config["layers"]-1)*t["secondsPerLaneTurn"]
    align=2000*t["secondsPerTargetAlignment"]
    total=(length/speed+turns+align)*t["marginMultiplier"]
    return dict(method="Conservative full-clear two-layer 100-lane snake estimate, not a human playthrough. Regular real spacing plus unchanged 52/156 m/s movement produces a long all-2000-target route. The estimate explicitly prevents reusing the old 540-second limit.",
        lengthMeters=length,effectiveSpeedMetersPerSecond=speed,travelSeconds=length/speed,
        laneTurnAllowanceSeconds=turns,targetAlignmentAllowanceSeconds=align,
        marginMultiplier=t["marginMultiplier"],unroundedMissionSeconds=total,
        roundedMissionSeconds=math.ceil(total/60)*60)


def guard_generated_source():
    if SOURCE.exists():
        receipt=json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {}
        if receipt.get("blendSha256")!=sha(SOURCE):
            raise RuntimeError("The saved fleet .blend has changed since generation. Preserve edits and run --export-saved, or choose a new output path; generation will not overwrite it.")


def generate(config):
    import bpy
    from mathutils import Matrix,Vector
    if not bpy.app.background or bpy.data.filepath:
        raise RuntimeError("Use a new --background --factory-startup Blender process.")
    guard_generated_source()
    hashes={relative(SOURCE_MODEL):sha(SOURCE_MODEL),relative(MODEL_FBX):sha(MODEL_FBX)}
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_MODEL))
    # Existing file is only read. Extract evaluated LOD2 into one new preview
    # collection; bake the existing game's constant 2.6 model scale once.
    for col in bpy.data.collections:
        col.hide_viewport=False
        col.hide_render=False
    def reveal(layer):
        layer.exclude=False
        layer.hide_viewport=False
        for child in layer.children: reveal(child)
    reveal(bpy.context.view_layer.layer_collection)
    for o in bpy.context.scene.objects:
        o.hide_set(False)
        o.hide_viewport=False
    bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    prototypes=[]
    for source in list(bpy.context.scene.objects):
        if source.type!="MESH" or source.get("ff_lod")!=2: continue
        mesh=bpy.data.meshes.new_from_object(source.evaluated_get(depsgraph),preserve_all_data_layers=True,depsgraph=depsgraph)
        mesh.transform(Matrix.Scale(config["previewModelScale"],4) @ source.matrix_world)
        mesh.name="Fleet2000_LOD2_"+source.name
        mesh.calc_loop_triangles()
        prototypes.append((source.name,mesh))
    assert prototypes,"Existing FusionFrigate LOD2 was not found."
    scene=bpy.data.scenes.new("Fleet2000Sun_Authoring")
    scene.unit_settings.system="METRIC"
    scene.unit_settings.scale_length=1
    bpy.context.window.scene=scene
    for old in list(bpy.data.scenes):
        if old!=scene: bpy.data.scenes.remove(old)
    for old in list(bpy.data.objects): bpy.data.objects.remove(old,do_unlink=True)
    for old in list(bpy.data.collections): bpy.data.collections.remove(old)
    proto=bpy.data.collections.new("FusionFrigate_LOD2_SHARED_57_564m")
    for name,mesh in prototypes:
        obj=bpy.data.objects.new(name,mesh)
        proto.objects.link(obj)
        obj["source_mesh"]=name
        obj["original_model_source"]=relative(SOURCE_MODEL)
    for mesh in list(bpy.data.meshes):
        if mesh.users==0: bpy.data.meshes.remove(mesh)
    for blocks in (bpy.data.materials,bpy.data.images,bpy.data.cameras,bpy.data.lights,bpy.data.worlds):
        for block in list(blocks):
            if block.users==0: blocks.remove(block)
    root=bpy.data.collections.new("Fleet2000Sun_AuthoredInstances")
    scene.collection.children.link(root)
    layers=[]
    for i in range(2):
        col=bpy.data.collections.new(f"Layer_{i+1:02d}_1000Ships")
        root.children.link(col)
        layers.append(col)
    for m in config["markers"]:
        obj=bpy.data.objects.new(m["name"],None)
        layers[m["layer"]].objects.link(obj)
        obj.instance_type="COLLECTION"
        obj.instance_collection=proto
        obj.location=(m["position"][0],m["position"][2],m["position"][1])
        obj.empty_display_size=12
        obj["fleet_owner"]=OWNER
        obj["stable_id"]=m["id"]
        obj["model_id"]=m["modelId"]
        obj["layer"]=m["layer"]
        obj["column"]=m["column"]
        obj["row"]=m["row"]
    scene["authority"]=relative(CONFIG)
    scene["authority_sha256"]=sha(CONFIG)
    scene["export_instructions"]="Edit ship instance transforms, save a new .blend or this file, then run author_layout.py --export-saved explicitly. Never import .blend into Unity."
    scene["coordinate_contract"]=config["coordinateSpace"]
    scene["source_model_sha256"]=hashes[relative(SOURCE_MODEL)]
    scene["prototype_scale_baked"]=config["previewModelScale"]
    setup_preview(scene)
    bpy.context.view_layer.update()
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    write_json(LAYOUT_EXPORT,config)
    points=[v.co for _,mesh in prototypes for v in mesh.vertices]
    preview_bounds=bounds(points)
    assert abs(preview_bounds["size"][1]-config["shipLength"])<.002
    for path,digest in hashes.items(): assert sha(ROOT/path)==digest,"Source model changed."
    receipt=dict(schemaVersion=1,blender=bpy.app.version_string,blenderBuild=bpy.app.build_hash.decode(),
        python=sys.version,authorityPath=relative(CONFIG),authoritySha256=sha(CONFIG),
        blendPath=relative(SOURCE),blendSha256=sha(SOURCE),exportPath=relative(LAYOUT_EXPORT),exportSha256=sha(LAYOUT_EXPORT),
        sourcePreservation=hashes,markerCount=2000,modelId="FusionFrigate",newFbxCount=0,
        sharedCollectionCount=1,sharedPrototypeMeshCount=len(prototypes),
        sharedPrototypeTriangles=sum(len(mesh.loop_triangles) for _,mesh in prototypes),
        previewBoundsBlender=preview_bounds,measurements=config["measurements"],routeEstimate=config["routeEstimate"])
    write_json(RECEIPT,receipt)
    write_json(EVIDENCE/"blender-generation.json",dict(passed=True,**receipt))
    print("FLEET2000_BLENDER_GENERATION_PASS "+json.dumps({k:receipt[k] for k in ("markerCount","sharedPrototypeMeshCount","sharedPrototypeTriangles","blendSha256")}))


def setup_preview(scene):
    import bpy
    from mathutils import Vector
    review=bpy.data.collections.new("ReviewCamera_ONLY_NOT_EXPORTED")
    scene.collection.children.link(review)
    camera=bpy.data.objects.new("Fleet2000_PerspectiveReview",bpy.data.cameras.new("Fleet2000_PerspectiveReview"))
    review.objects.link(camera)
    scene.camera=camera
    camera.data.type="PERSP"
    camera.data.lens=45
    camera.data.clip_end=50000
    camera.data.clip_start=.1
    camera.location=(-6500,-9000,6200)
    camera.rotation_euler=(Vector((100,2500,60))-camera.location).to_track_quat("-Z","Y").to_euler()
    scene.render.engine="BLENDER_WORKBENCH"
    scene.display.shading.light="STUDIO"
    scene.display.shading.studiolight_rotate_z=.6
    scene.display.shading.color_type="MATERIAL"
    scene.display.shading.show_shadows=False
    scene.display.shading.show_cavity=False
    scene.display.shading.show_specular_highlight=True
    scene.display.shading.background_type="WORLD"
    scene.world=bpy.data.worlds.new("Fleet2000_ReviewWorld")
    scene.world.color=(.008,.012,.022)
    scene.render.resolution_x=1920
    scene.render.resolution_y=1080
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                area.spaces.active.clip_end=50000
                area.spaces.active.region_3d.view_perspective="CAMERA"
                area.spaces.active.shading.type="SOLID"
                area.spaces.active.overlay.show_extras=False


def verify(config):
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.context.scene
    objects=[o for o in scene.objects if o.get("fleet_owner")==OWNER]
    assert len(objects)==2000 and len({o["stable_id"] for o in objects})==2000
    lookup={m["id"]:m for m in config["markers"]}
    maximum_error=0
    for obj in objects:
        marker=lookup[obj["stable_id"]]
        actual=[obj.matrix_world.translation.x,obj.matrix_world.translation.z,obj.matrix_world.translation.y]
        maximum_error=max(maximum_error,math.dist(actual,marker["position"]))
        assert (obj.matrix_world.to_scale()-Vector((1,1,1))).length<1e-6
        assert (obj.matrix_world.to_quaternion() @ Vector((0,1,0))-Vector((0,1,0))).length<1e-6
        assert (obj.matrix_world.to_quaternion() @ Vector((0,0,1))-Vector((0,0,1))).length<1e-6
        assert obj.instance_type=="COLLECTION" and obj["model_id"]=="FusionFrigate"
    assert maximum_error<.001
    collections={o.instance_collection.as_pointer() for o in objects}
    assert len(collections)==1
    proto=objects[0].instance_collection
    meshes=[o for o in proto.objects if o.type=="MESH"]
    points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    actual_bounds=bounds(points)
    assert abs(actual_bounds["size"][1]-57.564)<.002
    config_measurements=measure(config)
    receipt=json.loads(RECEIPT.read_text(encoding="utf-8"))
    assert sha(CONFIG)==sha(LAYOUT_EXPORT)==receipt["authoritySha256"]
    assert sha(SOURCE)==receipt["blendSha256"]
    for path,digest in receipt["sourcePreservation"].items(): assert sha(ROOT/path)==digest
    report=dict(passed=True,blender=bpy.app.version_string,blenderBuild=bpy.app.build_hash.decode(),
        actualReopenedFile=relative(SOURCE),fileSha256=sha(SOURCE),independentShipInstanceCount=len(objects),uniqueStableIds=len(lookup),
        sharedCollectionCount=len(collections),sharedPrototypeMeshes=len(meshes),perInstanceScale="all (1,1,1)",
        actualPrototypeBoundsBlender=actual_bounds,maxJsonToBlenderPositionErrorMetres=maximum_error,
        coordinateCalibration=[dict(id=objects[i]["stable_id"],blenderPosition=list(objects[i].location),unityJsonPosition=lookup[objects[i]["stable_id"]]["position"]) for i in (0,480,999,1000,1999)],
        sourceModelAndFbxUnchanged=True,newFbxCount=len(list(EXPORT.glob("*.fbx"))),measurements=config_measurements)
    assert report["newFbxCount"]==0
    write_json(EVIDENCE/"blender-reopen-verification.json",report)
    print("FLEET2000_BLENDER_REOPEN_PASS "+json.dumps({k:report[k] for k in ("independentShipInstanceCount","uniqueStableIds","sharedCollectionCount","maxJsonToBlenderPositionErrorMetres")}))


def render():
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    scene=bpy.context.scene
    views=[("panorama",(-6500,-9000,6200),(100,2500,60),45),
        ("mid",(-1100,-2200,680),(250,2400,60),40),
        ("near",(-145,50,60),(60,1500,65),45)]
    outputs=[]
    for name,location,target,lens in views:
        scene.camera.location=location
        scene.camera.rotation_euler=(Vector(target)-Vector(location)).to_track_quat("-Z","Y").to_euler()
        scene.camera.data.lens=lens
        path=EVIDENCE/f"blender-{name}.png"
        path.parent.mkdir(parents=True,exist_ok=True)
        scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
        outputs.append(dict(path=relative(path),locationBlender=location,targetBlender=target,lensMm=lens,projection="Perspective",sha256=sha(path)))
    write_json(EVIDENCE/"blender-render-verification.json",dict(blender=bpy.app.version_string,renderer=scene.render.engine,resolution=[1920,1080],purpose="Actual Blender solid previews of layout only; not a Unity gameplay or solar lighting screenshot.",views=outputs))


def export_saved(config):
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    authored=[o for o in bpy.context.scene.objects if o.get("fleet_owner")==OWNER]
    objects={o["stable_id"]:o for o in authored}
    assert len(authored)==2000 and len(objects)==2000,"Exactly 2000 unique ship instances required."
    for marker in config["markers"]:
        obj=objects[marker["id"]]
        assert (obj.matrix_world.to_scale()-Vector((1,1,1))).length<1e-6,"Unit-scale instances required."
        assert (obj.matrix_world.to_quaternion() @ Vector((0,1,0))-Vector((0,1,0))).length<1e-6,"This regular fleet requires uniform forward."
        assert (obj.matrix_world.to_quaternion() @ Vector((0,0,1))-Vector((0,0,1))).length<1e-6,"This regular fleet requires uniform up."
        p=obj.matrix_world.translation
        marker["position"]=[round(p.x,6),round(p.z,6),round(p.y,6)]
    config["measurements"]=measure(config)
    config["routeEstimate"]=route_estimate(config)
    config["missionDurationSeconds"]=config["routeEstimate"]["roundedMissionSeconds"]
    write_json(CONFIG,config)
    write_json(LAYOUT_EXPORT,config)
    bpy.context.scene["authority_sha256"]=sha(CONFIG)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    receipt=json.loads(RECEIPT.read_text(encoding="utf-8"))
    receipt.update(authoritySha256=sha(CONFIG),exportSha256=sha(LAYOUT_EXPORT),blendSha256=sha(SOURCE),measurements=config["measurements"])
    write_json(RECEIPT,receipt)
    print("FLEET2000_SAVED_LAYOUT_PUBLISHED "+sha(CONFIG))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for option in ("reflow","generate","verify","render","export-saved"):
        parser.add_argument("--"+option,action="store_true")
    argv=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:]
    args=parser.parse_args(argv)
    if args.generate or args.verify or args.render or args.export_saved:
        import bpy
        if not bpy.app.background or bpy.data.filepath:
            raise RuntimeError("Use a new --background --factory-startup process. Existing open/unsaved Blender work must be preserved.")
    config=json.loads(CONFIG.read_text(encoding="utf-8")) if CONFIG.exists() else default_config()
    if args.reflow: reflow(config)
    if args.generate: generate(config)
    if args.verify: verify(config)
    if args.render: render()
    if args.export_saved: export_saved(config)
    if not any(vars(args).values()): print(json.dumps(measure(config),indent=2))


if __name__=="__main__":
    main()
