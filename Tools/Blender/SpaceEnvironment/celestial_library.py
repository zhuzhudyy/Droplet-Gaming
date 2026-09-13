"""Small, reusable celestial mesh library for the SpaceEnvironment authoring stage.

Call ``build_celestial_library(collection)`` from the root Blender composition
script. This module creates only its four model objects, two materials, and one
image reference. It never saves, exports, changes cameras, or resets the scene.

Authoring axes: +Z north/up, prime meridian +X, longitude increases toward +Y.
All model origins are at the centre, radii are one metre, and transforms identity.
The scene compositor owns placement and the decision about which LOD is visible.
"""

from math import cos, pi, sin
from pathlib import Path

import bpy


OWNER = "DropletPrototype.SpaceEnvironment.CelestialLibrary.v1"
ROOT = Path(__file__).resolve().parents[3]
EARTH_TEXTURE = ROOT / "ArtSource/Blender/SpaceEnvironment/Textures/Earth_BaseColor_2048x1024.jpg"
EARTH_SOURCE_URL = "https://eoimages.gsfc.nasa.gov/images/imagerecords/57000/57735/land_ocean_ice_cloud_2048.jpg"

# Numbers of longitude segments and north-to-south latitude intervals.
# A closed UV sphere has exactly 2 * longitude * (latitude - 1) triangles.
CELESTIAL_SPECS = {
    "Sun": ((32, 24),),             # 1,472 triangles
    "Earth": ((64, 48), (40, 24), (24, 12)),  # 6,016 / 1,840 / 528
}


def _material(name, color, image=None):
    existing = bpy.data.materials.get(name)
    if existing is not None:
        if existing.get("space_environment_owner") != OWNER:
            raise RuntimeError(f"Refusing to overwrite unowned material {name}")
        return existing
    material = bpy.data.materials.new(name)
    material["space_environment_owner"] = OWNER
    material["preview_only_base_appearance"] = True
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = 0.0
    shader.inputs["Roughness"].default_value = 1.0
    shader.inputs["Specular IOR Level"].default_value = 0.0
    shader.inputs["Emission Strength"].default_value = 0.0
    if image is not None:
        texture = material.node_tree.nodes.new("ShaderNodeTexImage")
        texture.name = "SharedEarthBaseColor"
        texture.label = "NASA Blue Marble base colour, clouds included"
        texture.image = image
        texture.interpolation = "Linear"
        texture.extension = "REPEAT"
        material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])
        material.node_tree.nodes.active = texture
    return material


def _sphere_geometry(longitude, latitude):
    """Return closed, outward triangles and per-corner seam-correct UVs.

    The two geometric poles are unique vertices. UVs are loop attributes, so
    seam coordinates can be both zero and one without duplicating geometry.
    Each pole triangle samples the centre of its own longitude wedge.
    """
    vertices = [(0.0, 0.0, 1.0)]
    for ring in range(1, latitude):
        theta = pi * ring / latitude
        for column in range(longitude):
            longitude_angle = -pi + 2.0 * pi * column / longitude
            vertices.append((sin(theta) * cos(longitude_angle),
                             sin(theta) * sin(longitude_angle), cos(theta)))
    south = len(vertices)
    vertices.append((0.0, 0.0, -1.0))
    triangles, triangle_uvs = [], []

    def index(ring, column):
        return 1 + (ring - 1) * longitude + column % longitude

    def triangle(indices, uvs):
        triangles.append(indices)
        triangle_uvs.append(uvs)

    for column in range(longitude):
        left, right = column / longitude, (column + 1) / longitude
        centre = (left + right) * 0.5
        top_ring_v = 1.0 - 1.0 / latitude
        triangle((0, index(1, column), index(1, column + 1)),
                 ((centre, 1.0), (left, top_ring_v), (right, top_ring_v)))
        for ring in range(1, latitude - 1):
            top_v, bottom_v = 1.0 - ring / latitude, 1.0 - (ring + 1) / latitude
            a, b = index(ring, column), index(ring + 1, column)
            c, d = index(ring + 1, column + 1), index(ring, column + 1)
            triangle((a, b, c), ((left, top_v), (left, bottom_v), (right, bottom_v)))
            triangle((a, c, d), ((left, top_v), (right, bottom_v), (right, top_v)))
        bottom_ring_v = 1.0 / latitude
        triangle((index(latitude - 1, column), south, index(latitude - 1, column + 1)),
                 ((left, bottom_ring_v), (centre, 0.0), (right, bottom_ring_v)))
    return vertices, triangles, triangle_uvs


def _uv_sphere(asset_id, lod_index, longitude, latitude, material, collection):
    name = f"{asset_id}_LOD{lod_index}"
    if bpy.data.objects.get(name) is not None or bpy.data.meshes.get(name) is not None:
        raise RuntimeError(f"Celestial library name already exists: {name}; root must manage its owned subtree")
    vertices, triangles, triangle_uvs = _sphere_geometry(longitude, latitude)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], triangles)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for polygon, coordinates in zip(mesh.polygons, triangle_uvs):
        polygon.use_smooth = True
        for loop_index, uv in zip(polygon.loop_indices, coordinates):
            uv_layer.data[loop_index].uv = uv
    mesh.materials.append(material)
    mesh["space_environment_owner"] = OWNER
    mesh["authored_radius_metres"] = 1.0
    mesh["authored_triangle_count"] = len(triangles)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj["space_environment_owner"] = OWNER
    obj["assetId"] = asset_id
    obj["lod"] = lod_index
    obj["materialId"] = material.name
    obj["north_axis"] = "+Z"
    obj["prime_meridian_axis"] = "+X"
    obj["closed_surface"] = True
    obj["reference_only_no_gameplay"] = True
    return obj


def build_celestial_library(collection):
    """Create Sun and Earth LOD objects, returning ``{assetId: [LOD0, ...]}``.

    No automatic placeholder fallback: missing or incorrectly sized texture is
    a genuine asset error for the root generator to fix before saving/export.
    Nothing is exported by this function. The compositor should use linked mesh
    copies for placed bodies and hide the source collection from its render.
    """
    if not EARTH_TEXTURE.is_file():
        raise FileNotFoundError(f"Missing Earth source texture: {EARTH_TEXTURE}")
    image = bpy.data.images.load(str(EARTH_TEXTURE), check_existing=True)
    if tuple(image.size) != (2048, 1024):
        raise RuntimeError(f"Earth base colour must be 2048 x 1024, got {tuple(image.size)}")
    image.name = "Earth_BaseColor_2048x1024"
    image.colorspace_settings.name = "sRGB"
    image["space_environment_owner"] = OWNER
    image["source_url"] = EARTH_SOURCE_URL
    image["credit"] = "NASA/GSFC; Reto Stockli and Robert Simmon, Blue Marble 2002"
    image["source_kind"] = "Satellite-based global composite; not a current weather map"
    material_by_asset = {
        "Sun": _material("SE_SunBase", (1.0, 0.90, 0.55)),
        "Earth": _material("SE_EarthBase", (0.045, 0.18, 0.36), image),
    }
    return {
        asset_id: [_uv_sphere(asset_id, lod, longitude, latitude,
                              material_by_asset[asset_id], collection)
                   for lod, (longitude, latitude) in enumerate(specifications)]
        for asset_id, specifications in CELESTIAL_SPECS.items()
    }
