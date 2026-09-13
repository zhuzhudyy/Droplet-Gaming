"""Explicit, deterministic expanded fleet authoring; never writes Unity Assets.

The JSON is authoritative. --reflow explicitly rebuilds poses from its parameters;
ordinary Blender --export consumes the stored poses, preserving manual JSON edits.
The saved .blend is a generated marker-only review copy, not a second authority.
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
SOURCE = ROOT / "ArtSource/Blender/FleetExpansion/FleetLayout_Expanded.blend"
EXPORT = ROOT / "ArtSource/Exports/FleetExpansion"
MANIFEST = EXPORT / "FleetLayout_Expanded_manifest.json"
EVIDENCE = ROOT / "docs/verification/FleetExpansion"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def reflow(config):
    """No count multiplier: the recorded original/target values remain fixed."""
    if config["targetCount"] != config["squadronCount"] * config["rows"] * config["columns"]:
        raise ValueError("Explicit target count must equal groups x rows x columns.")
    markers = []
    for group_index, group in enumerate(config["squadrons"]):
        for column in range(config["columns"]):
            for row in range(config["rows"]):
                anchor = group["anchor"]
                # Column zero is the stable 5-target attack lane in each squadron.
                stagger = 0 if column == 0 else (row % 2) * config["rowLateralStagger"]
                rise = 0 if column == 0 else ((row % 3) - 1) * config["rowVerticalStagger"]
                p = [anchor[0] + config["columnSlots"][column] * config["lateralSpacing"] + stagger,
                     anchor[1] + config["columnLayers"][column] * config["verticalSpacing"] + rise,
                     anchor[2] + row * config["forwardSpacing"] + column * config["columnDepthStagger"]]
                identity = "FF%03d" % (len(markers) + 1)
                markers.append(dict(name="SPAWN_Small_" + identity, id=identity, group=group["name"],
                    squadron=group_index + 1, column=column, row=row,
                    position=[round(v, 6) for v in p], forward=[0, 0, 1], up=[0, 1, 0],
                    scale=[1, 1, 1], yaw=0, pitch=0))
    config["markers"] = markers
    config["measurements"] = measure(config)
    route = representative_route(config)
    config["routeEstimate"] = route
    config["missionDurationSeconds"] = route["roundedMissionSeconds"]
    write_json(CONFIG, config)


def aabb(points, low=(0, 0, 0), high=(0, 0, 0)):
    minimum = [min(p[i] for p in points) + low[i] for i in range(3)]
    maximum = [max(p[i] for p in points) + high[i] for i in range(3)]
    return dict(min=minimum, max=maximum, size=[maximum[i] - minimum[i] for i in range(3)])


def box_distance(a, b):
    return math.sqrt(sum(max(a["min"][i] - b["max"][i], b["min"][i] - a["max"][i], 0) ** 2 for i in range(3)))


def measure(config):
    markers = config["markers"]
    if len(markers) != config["targetCount"] or len({m["id"] for m in markers}) != len(markers):
        raise ValueError("Marker count/ID validation failed.")
    if len({m["name"] for m in markers}) != len(markers):
        raise ValueError("Duplicate imported marker name.")
    for marker in markers:
        if marker["scale"] != [1, 1, 1] or marker["forward"] != [0, 0, 1] or marker["up"] != [0, 1, 0]:
            raise ValueError("Expanded fleet requires positive unit scale and +Z/+Y pose.")
        if len(marker["position"]) != 3 or not all(math.isfinite(x) for x in marker["position"]):
            raise ValueError("Invalid marker position.")
    points = [m["position"] for m in markers]
    nearest = [min(math.dist(p, q) for j, q in enumerate(points) if i != j) for i, p in enumerate(points)]
    bounds = config["shipBounds"]
    groups = []
    for group in config["squadrons"]:
        group_points = [m["position"] for m in markers if m["group"] == group["name"]]
        if len(group_points) != config["shipsPerSquadron"]:
            raise ValueError("Incorrect group count: " + group["name"])
        groups.append(dict(name=group["name"], count=len(group_points), centerBounds=aabb(group_points),
                           visualBounds=aabb(group_points, bounds["min"], bounds["max"])))
    gap_pairs = [dict(a=a["name"], b=b["name"], clearDistance=box_distance(a["visualBounds"], b["visualBounds"]))
                 for i, a in enumerate(groups) for b in groups[i+1:]]
    minimum_gap = min(p["clearDistance"] for p in gap_pairs)
    if minimum_gap < config["minimumSquadronClearanceLengths"] * config["shipLength"]:
        raise ValueError("Squadron bounds violate recorded clearance.")
    median = statistics.median(nearest)
    if median < config["baseline"]["nearestMedian"] * 2:
        raise ValueError("Actual median nearest-neighbour distance must double baseline.")
    solar = json.loads((ROOT / config["solarConfig"]).read_text(encoding="utf-8"))
    rock_models = json.loads((ROOT / "ArtSource/Blender/SpaceEnvironment/Exports/export-manifest.json").read_text(encoding="utf-8"))["models"]
    # Full AABB diagonal (rather than half) conservatively covers these pivot-centred prototypes.
    radii = {}
    for model in rock_models:
        if model["assetId"].startswith("Rock"):
            radii[model["assetId"]] = max(radii.get(model["assetId"], 0), math.sqrt(sum(x*x for x in model["dimensions"])))
    ship_radius = max(math.sqrt(sum(c*c for c in corner)) for corner in (bounds["min"], bounds["max"]))
    clearances = [dict(target=m["name"], rock=r["id"], conservativeClearance=math.dist(m["position"], r["position"]) - radii[r["assetId"]] * r["scale"] - ship_radius)
                  for m in markers for r in solar["rocks"]]
    closest = min(clearances, key=lambda x: x["conservativeClearance"])
    if closest["conservativeClearance"] <= 0:
        raise ValueError("Conservative rock separation failed: " + str(closest))
    max_radius = max(math.dist(p, solar["localOrigin"]) for p in points) + ship_radius
    if max_radius + config["turningReserve"] >= solar["warningRadius"]:
        raise ValueError("Targets or turning reserve exceed existing warning radius.")
    return dict(count=len(markers), centerBounds=aabb(points), visualBounds=aabb(points, bounds["min"], bounds["max"]),
                nearestMin=min(nearest), nearestMedian=median, nearestMax=max(nearest),
                nearestMedianToOld=median / config["baseline"]["nearestMedian"],
                nearestMedianInShipLengths=median / config["shipLength"],
                squadronVisualBounds=groups, squadronGaps=gap_pairs,
                minimumSquadronClearance=minimum_gap, minimumSquadronClearanceInLengths=minimum_gap/config["shipLength"],
                closestRockConservative=closest, rockMethod="All-LOD prototype full AABB diagonal x scale plus ship corner radius; deliberately conservative sphere separation, not Unity physics.",
                maximumFleetRadiusFromSolarOrigin=max_radius, boundaryRadius=solar["boundaryRadius"], warningRadius=solar["warningRadius"],
                shipPairVisualAabbOverlapCount=sum(box_distance(aabb([p], bounds["min"], bounds["max"]), aabb([q], bounds["min"], bounds["max"])) == 0
                    for i, p in enumerate(points) for q in points[i+1:]))


def representative_route(config):
    lookup = {(m["group"], m["column"], m["row"]): m for m in config["markers"]}
    route = []
    for name in config["routeSquadronOrder"]:
        for column in range(config["columns"]):
            rows = range(config["rows"]) if column % 2 == 0 else reversed(range(config["rows"]))
            route.extend(lookup[(name, column, row)] for row in rows)
    points = [config["playerSpawn"]] + [m["position"] for m in route]
    distance = sum(math.dist(a, b) for a, b in zip(points, points[1:]))
    controls = config["timing"]
    effective_speed = 1 / (controls["cruiseDistanceFraction"] / controls["cruiseSpeed"] +
                            (1 - controls["cruiseDistanceFraction"]) / controls["boostSpeed"])
    travel_seconds = distance / effective_speed
    turn_allowance = (config["squadronCount"] * (config["columns"] - 1) + config["squadronCount"] - 1) * controls["secondsPerLaneTurn"]
    aim_allowance = config["targetCount"] * controls["secondsPerTargetAlignment"]
    allowed = (travel_seconds + turn_allowance + aim_allowance) * controls["marginMultiplier"]
    return dict(method="Non-optimal reproducible all-120-target column-snake route; 60% cruise distance/40% boost distance, harmonic effective speed; lane turns and alignment plus margin. Estimate, not a completed human playthrough.",
                targetOrder=[m["name"] for m in route], lengthMeters=distance,
                effectiveSpeedMetersPerSecond=effective_speed, travelSeconds=travel_seconds,
                laneTurnAllowanceSeconds=turn_allowance, targetAlignmentAllowanceSeconds=aim_allowance,
                marginMultiplier=controls["marginMultiplier"], unroundedMissionSeconds=allowed,
                roundedMissionSeconds=math.ceil(allowed / 60) * 60)


def export_blender(config):
    import bpy
    import importlib.util
    if not bpy.app.background or bpy.data.filepath:
        raise RuntimeError("Use a new --background --factory-startup Blender process; never replace an open user's file.")
    if SOURCE.exists():
        if not MANIFEST.exists() or digest(SOURCE) != json.loads(MANIFEST.read_text(encoding="utf-8")).get("sourceSha256"):
            raise RuntimeError("Generated .blend has changed without matching receipt; preserve it and resolve the source change before regenerating.")
    spec = importlib.util.spec_from_file_location("calibrated_fleet_authoring", ROOT / "Tools/Blender/build_fleet_assets.py")
    pipeline = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(pipeline)
    pipeline.reset()
    # Factory-startup contains unused default data; keep the review file truly
    # marker-only. This process has never loaded the user's Blender session.
    for datablocks in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
        for datablock in list(datablocks):
            datablocks.remove(datablock)
    root_collection = pipeline.new_collection("FleetExpansion_Markers")
    fleet_root = pipeline.empty("FleetLayout_Expanded", root_collection)
    fleet_root["authority"] = str(CONFIG.relative_to(ROOT)).replace("\\", "/")
    for group in config["squadrons"]:
        collection = bpy.data.collections.new(group["name"])
        root_collection.children.link(collection)
        for marker in (m for m in config["markers"] if m["group"] == group["name"]):
            obj, _ = pipeline.spawn(collection, fleet_root, marker["name"], marker["position"], marker["yaw"], marker["pitch"])
            obj["squadron"] = marker["group"]
    bpy.context.view_layer.update()
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    EXPORT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    export_objects = [fleet_root] + [obj for obj in bpy.data.objects if obj.name.startswith("SPAWN_")]
    pipeline.export_fbx(EXPORT / "FleetLayout_Expanded.fbx", export_objects)
    manifest = dict(schemaVersion=1, blender=bpy.app.version_string, blenderBuild=bpy.app.build_hash.decode(),
        configPath=str(CONFIG.relative_to(ROOT)).replace("\\", "/"), configSha256=digest(CONFIG),
        sourcePath=str(SOURCE.relative_to(ROOT)).replace("\\", "/"), sourceSha256=digest(SOURCE),
        markerCount=len(config["markers"]), markers=config["markers"], measurements=measure(config),
        coordinateContract="Blender +Y forward/+Z up; Unity +Z forward/+Y up; positive unit scale.",
        exportRepresentation="Source .blend contains only Empties. FBX reuses calibrated temporary four-vertex mesh pose proxies; Unity FleetLayoutImporter reads Transform only and never places marker geometry.",
        exportOptions={k:sorted(v) if isinstance(v,set) else v for k,v in pipeline.EXPORT_OPTIONS.items()},
        resourceCounts=dict(sourceShipMeshes=len(bpy.data.meshes), sourceMaterials=len(bpy.data.materials), sourceTextures=len(bpy.data.images), sourceCameras=0, sourceLights=0,
                            fbxTemporaryPoseProxies=len(config["markers"]), fbxShipGeometry=0))
    write_json(MANIFEST, manifest)
    write_json(EXPORT / "FleetLayout_Expanded_expected.json", dict(markers=config["markers"]))
    write_json(EVIDENCE / "layout-authoring-measurements.json", dict(passed=True, authoritySha256=digest(CONFIG), measurements=manifest["measurements"], routeEstimate=representative_route(config)))
    print("FLEET_EXPANSION_EXPORT_PASS " + json.dumps(dict(markerCount=len(config["markers"]), configSha256=digest(CONFIG), measurements=manifest["measurements"])))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--reflow", action="store_true", help="Explicitly rebuild stored marker poses using config parameters.")
    parser.add_argument("--export", action="store_true", help="Generate marker .blend and calibrated FBX; must run inside fresh background Blender.")
    argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else sys.argv[1:]
    args = parser.parse_args(argv)
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    if args.reflow:
        reflow(config)
    measure(config)
    if args.export:
        export_blender(config)
    elif not args.reflow:
        print(json.dumps(measure(config), indent=2))


if __name__ == "__main__":
    main()
