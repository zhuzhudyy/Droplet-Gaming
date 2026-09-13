"""Render the actual six pre-cut frigate pieces; never edits a source .blend."""
from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[2]
art = root/'ArtSource'/'Blender'
bpy.ops.wm.open_mainfile(filepath=str(art/'fleet_assets.blend'))
scene = bpy.context.scene
for collection in bpy.data.collections:
    if collection.name != 'Preview_Rig':
        collection.hide_render = True
preview = bpy.data.collections.new('Breakup_Preview_Only')
scene.collection.children.link(preview)
for source in bpy.data.collections['FrigateWreck_Source'].objects:
    if source.type != 'MESH':
        continue
    piece = source.copy()
    piece.data = source.data.copy()
    piece.parent = None
    preview.objects.link(piece)
    piece.location = source.location.copy()
    direction = Vector((1 if piece.location.x > 0 else -1,
                        -1 if piece.location.y < -5 else (1 if piece.location.y > 4 else 0),
                        .3 if piece.location.x > 0 else -.3)).normalized()
    piece.location += direction*2.5
    piece.hide_render = False
target = Vector((0,0,0))
camera = scene.camera
camera.location = Vector((23,32,25))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale = 36
scene.render.resolution_x, scene.render.resolution_y = 1400,900
scene.render.filepath = str(art/'Previews'/'frigate-breakup-preview.png')
bpy.ops.render.render(write_still=True)
print('BREAKUP_PREVIEW_COMPLETE')
