"""Export unique prototypes once and a separate calibrated FBX pose-marker layout.
Requires a reopened numeric audit AND root's actual image review before export.
JSON stores expected validation evidence, not a second layout consumed by Unity.
"""
import bpy,json,sys,hashlib,shutil
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[3]; ART=ROOT/'ArtSource/Blender/SpaceEnvironment'
OUT=ART/'Exports'; OUT.mkdir(exist_ok=True)
AUDIT=ROOT/'docs/verification/SpaceEnvironment'
source=ART/'SpaceEnvironment.blend'
audit=json.loads((ART/'blender_selfcheck.json').read_text(encoding='utf-8'))
visual=json.loads((AUDIT/'blender-visual-review.json').read_text(encoding='utf-8-sig'))
assert audit['passed_automated_selfcheck'] and visual['passed'], 'Blender numeric + visual gates required'
assert visual['source_sha256']==hashlib.sha256(source.read_bytes()).hexdigest(),'Source changed after visual review'
bpy.ops.wm.open_mainfile(filepath=str(source)); sc=bpy.data.scenes['SpaceEnvironment'];bpy.context.window.scene=sc
for layer in sc.view_layers[0].layer_collection.children: layer.exclude=False
OPTIONS=dict(use_selection=True,object_types={'MESH'},global_scale=1.0,apply_unit_scale=True,
 apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,
 bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False,
 bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=True)
def export(path,objects):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(path),**OPTIONS)
    bpy.ops.object.select_all(action='DESELECT')
C=lambda v:[v.x,v.z,v.y]
manifest={'blender':bpy.app.version_string,'source_sha256':visual['source_sha256'],'coordinate_contract':'Blender +Y forward +Z up; Unity +Z forward +Y up; FBX conversion once; flat world marker poses',
 'export_options':{k:sorted(v) if isinstance(v,set) else v for k,v in OPTIONS.items()},'models':[],'markers':[],'cameras':[]}
for colname in ['AsteroidLibrary','CelestialLibrary']:
    for o in bpy.data.collections[colname].objects:
        if o.type!='MESH' or 'assetId' not in o:continue
        assert o.location.length<.001 and all(abs(s-1)<.001 for s in o.scale)
        name=f"{o['assetId']}_LOD{o['lod']}";export(OUT/(name+'.fbx'),[o]);o.data.calc_loop_triangles()
        manifest['models'].append({'assetId':o['assetId'],'lod':o['lod'],'file':name+'.fbx','triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices),'dimensions':C(o.dimensions),'shape':o.get('shape','')})
proxies=[]
for o in bpy.data.collections['LayoutMarkers'].objects:
    desired=o.name.removeprefix('POSE_')
    original=bpy.data.objects.get(desired)
    if original: original.name='SOURCE_'+desired
    me=bpy.data.meshes.new('PoseProxyOnly');me.from_pydata([(0,0,0),(.02,0,0),(0,.06,0),(0,0,.04)],[],[(0,2,1),(0,1,3),(0,3,2),(1,2,3)])
    p=bpy.data.objects.new(desired,me);sc.collection.objects.link(p);p.matrix_world=o.matrix_world.copy();proxies.append(p)
    mat=o.matrix_world;rot=mat.to_quaternion();s=mat.to_scale()
    manifest['markers'].append({'name':p.name,'id':o['instanceId'],'assetId':o['assetId'],'group':o['group'],'materialVariant':o.get('materialVariant',0),
      'position':C(mat.translation),'forward':C(rot@Vector((0,1,0))),'up':C(rot@Vector((0,0,1))),'scale':C(s)})
export(OUT/'SpaceEnvironmentLayout.fbx',proxies)
for p in proxies:bpy.data.objects.remove(p,do_unlink=True)
for o in bpy.data.collections['ReferenceCameras'].objects:
    rot=o.matrix_world.to_quaternion()
    manifest['cameras'].append({'name':o.name,'position':C(o.location),'forward':C(rot@Vector((0,0,-1))),'up':C(rot@Vector((0,1,0))),
      'orthographic':o.data.type=='ORTHO','orthoSize':o.data.ortho_scale/2/(16/9),'fov':json.loads(sc['config_json'])['vertical_fov'],'near':o.data.clip_start,'far':o.data.clip_end})
# The already validated asymmetric unit/pose calibration is copied byte-for-byte.
shutil.copy2(ROOT/'ArtSource/Blender/Exports/calibration.fbx',OUT/'SpaceCalibration.fbx')
(OUT/'export-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('EXPORT_COMPLETE',len(manifest['models']),'unique model files',len(manifest['markers']),'pose markers')
