"""New solar layout, reusing the delivered meshes. Run through run.py only.
Config is authoritative; the .blend is an editable projection. adopt-local is an
explicit round trip for local transform edits. Macro schematic objects never export.
"""
import bpy, json, math, random, hashlib, sys, shutil
from pathlib import Path
from mathutils import Vector, Euler
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).parent
ART=ROOT/'ArtSource/Blender/SpaceEnvironment'
DEST=ART/'SpaceEnvironment_SolarLayout.blend'
OUT=ART/'SolarLayoutExports'
EVID=ROOT/'docs/verification/SolarSystemLayout'
CFG_PATH=HERE/'layout_config.json'
CFG=json.loads(CFG_PATH.read_text(encoding='utf-8'))
C=lambda a: Vector((a[0],a[2],a[1]))
U=lambda a: [a.x,a.z,a.y]
def digest(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def mesh_digest(m):
 return hashlib.sha256(json.dumps([[list(v.co) for v in m.vertices],[list(p.vertices) for p in m.polygons]],separators=(',',':')).encode()).hexdigest()
def orbit(r,p,i=0,n=0):
 p,i,n=map(math.radians,(p,i,n));x=r*math.cos(p);z=r*math.sin(p)
 return Vector((x*math.cos(n)-z*math.cos(i)*math.sin(n),z*math.sin(i),x*math.sin(n)+z*math.cos(i)*math.cos(n)))
def bodypos(b): return orbit(b['semiMajorAu'],b['phaseDeg'],b['inclinationDeg'],b['ascendingNodeDeg'])
def anchor(): return orbit(CFG['battleRadiusAu'],CFG['battlePhaseDeg'],CFG['battleInclinationDeg'],CFG['battleAscendingNodeDeg'])
def macro_tuple(r,p,i=0,n=0):
 # Python floats are doubles; mathutils vectors are not used for subtraction at AU scale.
 p,i,n=map(math.radians,(p,i,n));x=r*math.cos(p);z=r*math.sin(p)
 return (x*math.cos(n)-z*math.cos(i)*math.sin(n),z*math.sin(i),x*math.sin(n)+z*math.cos(i)*math.cos(n))
def map_body(b,observer):
 a=macro_tuple(CFG['battleRadiusAu'],CFG['battlePhaseDeg'],CFG['battleInclinationDeg'],CFG['battleAscendingNodeDeg'])
 p=macro_tuple(b['semiMajorAu'],b['phaseDeg'],b['inclinationDeg'],b['ascendingNodeDeg'])
 rel=[p[k]-(a[k]+(observer[k]-CFG['localOrigin'][k])*CFG['metersPerUnit']/1000/CFG['auKm']) for k in range(3)]
 d=math.sqrt(sum(x*x for x in rel));ratio=b['radiusKm']/(d*CFG['auKm']);assert 0<ratio<1
 proxy=CFG['proxyNear']+CFG['proxySpan']*d/(d+1)
 return [observer[k]+rel[k]/d*proxy for k in range(3)],proxy*ratio*b['readabilityMultiplier'],math.degrees(2*math.asin(ratio)),d
def col(sc,name):
 c=bpy.data.collections.new(name);sc.collection.children.link(c);return c
def obj(c,name,data=None):
 o=bpy.data.objects.new(name,data);c.objects.link(o);return o
def camera(c,name,pos,target,fov=65,ortho=0):
 d=bpy.data.cameras.new(name);o=obj(c,name,d);o.location=C(pos);o.rotation_euler=(C(target)-o.location).to_track_quat('-Z','Y').to_euler()
 d.lens=36/(2*math.tan(math.radians(fov)/2)*16/9);d.sensor_width=36;d.clip_start=.1;d.clip_end=CFG['farClip']
 if ortho:d.type='ORTHO';d.ortho_scale=ortho
 return o
def setup(sc):
 sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=1280;sc.render.resolution_y=720;sc.render.resolution_percentage=100
 sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_shadows=False;sc.display.shading.show_cavity=True
 sc.display.shading.background_type='WORLD';sc.world=bpy.data.worlds.new(sc.name+'World');sc.world.color=(.003,.005,.008)
 sc.view_settings.view_transform='Standard';sc.render.image_settings.file_format='PNG'
def guard():
 if DEST.exists():
  receipt=OUT/'source-receipt.json'
  if not receipt.exists() or not json.loads(receipt.read_text()).get('regenerationAllowed',True) or json.loads(receipt.read_text())['source_sha256']!=digest(DEST):
   raise RuntimeError('Edited solar source detected. Use adopt-local to accept supported local edits, or work in a separate copy. Refusing overwrite.')
  backup=DEST.with_name('SpaceEnvironment_SolarLayout.before-'+digest(DEST)[:12]+'.blend')
  if not backup.exists():shutil.copy2(DEST,backup)
def build():
 guard();OUT.mkdir(exist_ok=True);EVID.mkdir(parents=True,exist_ok=True)
 oldscenes=list(bpy.data.scenes)
 local=bpy.data.scenes.new('SolarLayout_LocalBattle');bpy.context.window.scene=local
 for sc in oldscenes:bpy.data.scenes.remove(sc)
 setup(local)
 library=col(local,'SharedLibrary_NOT_EXPORTED');rocks=col(local,'SparseAsteroids');bodies=col(local,'CelestialProxies');views=col(local,'ReviewCameras');marks=col(local,'GameplayReserve_DIAGRAM_ONLY')
 with bpy.data.libraries.load(str(ART/'SpaceEnvironment.blend'),link=False) as (src,dst):
  dst.objects=[n for n in src.objects if n.startswith(('Rock','Sun_LOD','Earth_LOD','Sky_LOD')) and '_LOD' in n]
 protos={}
 for o in dst.objects:
  if not o:continue
  library.objects.link(o);o.location=(0,0,0);o.rotation_euler=(0,0,0);o.scale=(1,1,1);protos[o.name]=o
 for r in CFG['rocks']:
  o=obj(rocks,'ENV_'+r['id'],protos[r['assetId']+'_LOD0'].data);o.location=C(r['position']);o.scale=(r['scale'],)*3
  o.rotation_euler=Euler(tuple(math.radians(x) for x in r['rotationDeg']),'XYZ');o['instanceId']=r['id'];o['assetId']=r['assetId'];o['coordinateSemantic']='local-metre';o['group']=r['group'];o['materialVariant']=r['materialVariant']
  for slot in o.material_slots:slot.link='OBJECT';slot.material=bpy.data.materials['RockBasalt' if r['materialVariant']==0 else 'RockSlate']
 for b in CFG['bodies']:
  if not b['assetId']:continue
  o=obj(bodies,'PROXY_'+b['id'],protos[b['assetId']+'_LOD0'].data);o['assetId']=b['assetId'];o['coordinateSemantic']='controlled-distance-proxy';o['physicalRadiusKm']=b['radiusKm'];o['readabilityMultiplier']=b['readabilityMultiplier']
 sky=obj(bodies,'PROXY_Sky',protos['Sky_LOD0'].data);sky.scale=(CFG['skyRadius'],)*3;sky.hide_render=True;sky.hide_set(True);sky['note']='Existing shared sky; hidden only for Workbench neutral inspection.'
 for v in CFG['cameras']:camera(views,v['name'],v['position'],v['target'],v['fov'])
 for name,r in [('OldBoundary_DIAGRAM',CFG['oldBoundaryRadius']),('WarningBoundary_DIAGRAM',CFG['warningRadius']),('FlightBoundary_DIAGRAM',CFG['boundaryRadius']),('CombatClearance_DIAGRAM',CFG['reserveRadius'])]:
  o=obj(marks,name);o.empty_display_type='SPHERE';o.empty_display_size=r;o.location=C(CFG['localOrigin']);o['diagramOnly']=True
 spawn=obj(marks,'PlayerSpawn_1mUnits');spawn.location=C(CFG['spawn']);spawn.empty_display_type='ARROWS';spawn.empty_display_size=20
 # Authored combat reference is reused unchanged and excluded from environment export.
 with bpy.data.libraries.load(str(ROOT/'ArtSource/Blender/fleet_layout.blend'),link=False) as (src,dst):
  dst.collections=[n for n in src.collections if n=='FleetLayout_EditablePreviewInstances']
 for c in dst.collections:
  if c:local.collection.children.link(c);c.name='FleetReference_NOT_EXPORTED';c.hide_render=False
 for o in library.objects:o.hide_render=True;o.hide_set(True)
 local['authoritative_config']=str(CFG_PATH.relative_to(ROOT));local['config_sha256']=digest(CFG_PATH);local['scale_note']='1 unit = 1 metre. Sphere proxies preserve angle, are not travel destinations.'
 local.camera=bpy.data.objects['SpawnForward'];apply_mapping(local.camera)
 # Two clearly separated inspection scenes; macro marker geometry has no gameplay export path.
 macro=bpy.data.scenes.new('SolarLayout_MacroOverview_DIAGRAM');bpy.context.window.scene=macro;setup(macro)
 diag=col(macro,'SCHEMATIC_ONLY_NoRuntimeExport');cv=col(macro,'MacroCameras');unit=CFG['macroOverviewUnitsPerAu']
 colors={'Sun':(1,.75,.25,1),'Earth':(.2,.55,1,1),'BattleAnchor':(1,.2,.15,1)}
 def mat(name,color):
  m=bpy.data.materials.new(name);m.diffuse_color=color;return m
 lineMat=mat('DiagramOrbits',(.17,.24,.31,1));beltMat=mat('DiagramBelt',(.48,.43,.31,1))
 def curve(name,coords,material,thickness=.018):
  d=bpy.data.curves.new(name,'CURVE');d.dimensions='3D';d.bevel_depth=thickness;d.bevel_resolution=0;s=d.splines.new('POLY');s.points.add(len(coords)-1)
  for p,v in zip(s.points,coords):p.co=(*C(v),1)
  d.materials.append(material);o=obj(diag,name,d);o['diagramOnly']=True;return o
 for b in CFG['bodies']:
  if b['semiMajorAu']:
   curve(b['id']+'_orbit_AU',[[v*unit for v in macro_tuple(b['semiMajorAu'],p,b['inclinationDeg'],b['ascendingNodeDeg'])] for p in range(361)],lineMat)
  p=bodypos(b)*unit;o=obj(diag,b['id']+'_POSITION_MARKER');o.location=C(p);o.empty_display_type='SPHERE';o.empty_display_size=.5;o['macroAu']=list(bodypos(b));o['diagramOnly']=True
  # Renderable small crosses, explicitly illustrative rather than body radii.
  material=mat('Diagram_'+b['id'],colors.get(b['id'],(.6,.65,.7,1)))
  size=.25 if b['id']!='Sun' else .4
  curve(b['id']+'_inflated_cross',[[p.x-size,p.y,p.z],[p.x+size,p.y,p.z]],material,.07)
  curve(b['id']+'_inflated_cross_Z',[[p.x,p.y,p.z-size],[p.x,p.y,p.z+size]],material,.07)
  d=bpy.data.curves.new(b['id']+'_label','FONT');d.body=b['id']+('  '+str(b['semiMajorAu'])+' AU' if b['semiMajorAu'] else '  reference origin');d.size=.7;d.materials.append(material);label=obj(diag,b['id']+'_label_DIAGRAM',d);label.location=C((p.x+.5,p.y+.1,p.z+.5))
 ap=anchor()*unit
 for k in [0,2]:
  p1=list(ap);p2=list(ap);p1[k]-=.65;p2[k]+=.65;curve('BattleAnchor_cross_'+str(k),[p1,p2],mat('AnchorRed'+str(k),colors['BattleAnchor']),.10)
 d=bpy.data.curves.new('AnchorLabel','FONT');d.body=f"BATTLE  {CFG['battleRadiusAu']} AU / phase {CFG['battlePhaseDeg']} deg";d.size=.7;d.materials.append(bpy.data.materials['AnchorRed0']);o=obj(diag,'BattleAnchor_LABEL',d);o.location=C((ap.x+.7,ap.y,ap.z))
 rng=random.Random(CFG['seed']);verts=[];faces=[];distribution=[]
 for i in range(CFG['macroMarkerCount']):
  r=rng.uniform(CFG['beltInnerAu'],CFG['beltOuterAu']);p=rng.uniform(0,360);inc=rng.uniform(0,CFG['beltMaxInclinationDeg']);node=rng.uniform(0,360);pos=macro_tuple(r,p,inc,node);distribution.append(dict(radiusAu=r,phaseDeg=p,inclinationDeg=inc,nodeDeg=node))
  v=C([x*unit for x in pos]);n=len(verts);verts.extend([v+Vector((-.04,0,0)),v+Vector((.04,0,0)),v+Vector((0,.08,.025))]);faces.append((n,n+1,n+2))
 mesh=bpy.data.meshes.new('schematic_samples_NOT_asteroid_mesh');mesh.from_pydata(verts,[],faces);mesh.materials.append(beltMat);o=obj(diag,'MainBelt_DIAGRAM_ONLY',mesh);o['diagramOnly']=True;o['distribution_json']=json.dumps(distribution)
 # Dashed boundary arcs convey only the design envelope, not an actual solid ring.
 for radius in [CFG['beltInnerAu'],CFG['beltOuterAu']]:
  for a in range(0,360,20):curve('BeltEnvelope_DASH',[[x*unit for x in macro_tuple(radius,p)] for p in range(a,a+8)],beltMat,.012)
 camera(cv,'MacroInnerTop',[0,100,0],[0,0,0],ortho=200)
 camera(cv,'MacroInnerSide',[0,12,-105],[0,0,0],ortho=160)
 camera(cv,'MacroAllOrbits',[0,600,0],[0,0,0],ortho=1120)
 macro.camera=bpy.data.objects['MacroInnerTop'];macro['diagramOnly']=True;macro['auPerUnit']=1/unit;macro['assumptions']='Fixed circular design phases. Crosses enlarged for editing, never exported. Samples show orbital envelope, not physical density.'
 for sc in [local,macro]:
  for area in bpy.context.screen.areas:
   if area.type=='VIEW_3D':area.spaces.active.clip_end=30000
 bpy.context.window.scene=local
 bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(DEST))
 (OUT/'source-receipt.json').write_text(json.dumps(dict(source_sha256=digest(DEST),config_sha256=digest(CFG_PATH),blender=bpy.app.version_string,regenerationAllowed=True,sharedMeshHashes={o.name:mesh_digest(o.data) for o in library.objects}),indent=2))
 print('BUILD PASS',DEST)
def apply_mapping(cam):
 for b in CFG['bodies']:
  if not b['assetId']:continue
  pos,r,_,_=map_body(b,U(cam.location));o=bpy.data.objects['PROXY_'+b['id']];o.location=C(pos);o.scale=(r,)*3
 sky=bpy.data.objects.get('PROXY_Sky')
 if sky:sky.location=cam.location
def inspect():
 bpy.ops.wm.open_mainfile(filepath=str(DEST));local=bpy.data.scenes['SolarLayout_LocalBattle'];macro=bpy.data.scenes['SolarLayout_MacroOverview_DIAGRAM'];results={}
 assert local['config_sha256']==digest(CFG_PATH),'Config/source out of sync; rebuild.'
 rocks=list(bpy.data.collections['SparseAsteroids'].objects);assert len(rocks)==len(CFG['rocks'])
 lookup={r['id']:r for r in CFG['rocks']};assert len(lookup)==len(rocks)
 for o in rocks:
  r=lookup[o['instanceId']]
  assert o.data==bpy.data.objects[r['assetId']+'_LOD0'].data,'Rock prototype edited/copied; models must stay shared.'
  assert (o.location-C(r['position'])).length<.002 and all(abs(x-r['scale'])<.001 for x in o.scale),'Local pose differs from authority; use explicit adopt-local first.'
  assert all(abs(math.degrees(a)-b)<.002 for a,b in zip(o.rotation_euler,r['rotationDeg'])),'Local rotation differs from authority.'
 clearances=[(o.location-C(CFG['localOrigin'])).length-max(v.co.length for v in o.data.vertices)*max(o.scale) for o in rocks]
 assert min(clearances)>CFG['reserveRadius']
 spacing=min((a.location-b.location).length for i,a in enumerate(rocks) for b in rocks[i+1:]);assert spacing>=CFG['minimumRockSpacing']-.01
 shared=[o for o in bpy.data.collections['SharedLibrary_NOT_EXPORTED'].objects if o.type=='MESH']
 receipt=json.loads((OUT/'source-receipt.json').read_text())
 for o in shared:assert mesh_digest(o.data)==receipt['sharedMeshHashes'][o.name],'Shared model changes are outside layout-only task.'
 stats=[]
 for o in shared:
  o.data.calc_loop_triangles();assert all(math.isfinite(v) for p in o.data.vertices for v in p.co)
  stats.append(dict(name=o.name,triangles=len(o.data.loop_triangles),vertices=len(o.data.vertices)))
 assert sum(v['triangles'] for v in stats)<=60000
 dist=json.loads(bpy.data.objects['MainBelt_DIAGRAM_ONLY']['distribution_json']);assert all(CFG['beltInnerAu']<=p['radiusAu']<=CFG['beltOuterAu'] and 0<=p['inclinationDeg']<=CFG['beltMaxInclinationDeg'] for p in dist)
 for sc,names in [(macro,['MacroInnerTop','MacroInnerSide','MacroAllOrbits']),(local,[v['name'] for v in CFG['cameras']])]:
  bpy.context.window.scene=sc
  for name in names:
   sc.camera=bpy.data.objects[name]
   if sc==local:apply_mapping(sc.camera)
   sc.render.filepath=str(EVID/('Blender-'+name+'.png'));bpy.ops.render.render(write_still=True)
 results=dict(passed=True,source_sha256=digest(DEST),config_sha256=digest(CFG_PATH),blender=bpy.app.version_string,rocks=len(rocks),uniqueRockMeshes=8,minimumClearance= min(clearances),minimumSpacing=spacing,sharedMeshes=stats,macroSamples=len(dist),macroExported=False,angular=[dict(id=b['id'],angleDeg=map_body(b,CFG['spawn'])[2],distanceAu=map_body(b,CFG['spawn'])[3]) for b in CFG['bodies'] if b['assetId']],generatedImages=[p.name for p in EVID.glob('Blender-*.png')],visualReview='Pending separate image observation')
 (EVID/'blender-selfcheck.json').write_text(json.dumps(results,indent=2));print('REOPEN + NUMERIC + RENDERS PASS')
def export():
 numeric=json.loads((EVID/'blender-selfcheck.json').read_text(encoding='utf-8-sig'));visual=json.loads((EVID/'blender-visual-review.json').read_text(encoding='utf-8-sig'))
 assert numeric['passed'] and visual['passed'] and numeric['source_sha256']==visual['source_sha256']==digest(DEST)
 assert numeric['config_sha256']==digest(CFG_PATH)
 bpy.ops.wm.open_mainfile(filepath=str(DEST));sc=bpy.data.scenes['SolarLayout_LocalBattle'];bpy.context.window.scene=sc
 proxies=[];records=[]
 for o in bpy.data.collections['SparseAsteroids'].objects:
  me=bpy.data.meshes.new('PoseOnly');me.from_pydata([(0,0,0),(.02,0,0),(0,.06,0),(0,0,.04)],[],[(0,2,1),(0,1,3),(0,3,2),(1,2,3)])
  p=obj(sc.collection,'POSE_'+o['instanceId'],me);p.matrix_world=o.matrix_world.copy();proxies.append(p);mat=o.matrix_world;q=mat.to_quaternion()
  records.append(dict(name=p.name,id=o['instanceId'],assetId=o['assetId'],group=o['group'],materialVariant=o['materialVariant'],position=U(mat.translation),forward=U(q@Vector((0,1,0))),up=U(q@Vector((0,0,1))),scale=U(mat.to_scale()),coordinateSemantic='local-metre'))
 bpy.ops.object.select_all(action='DESELECT')
 for o in proxies:o.select_set(True)
 bpy.context.view_layer.objects.active=proxies[0]
 bpy.ops.export_scene.fbx(filepath=str(OUT/'SolarLayoutMarkers.fbx'),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False,use_custom_props=True)
 manifest=dict(source_sha256=digest(DEST),config_sha256=digest(CFG_PATH),sharedModelsRoot='Assets/_Project/Art/Environment/SpaceEnvironment/Models/',markers=records,cameras=CFG['cameras'],exportSemantics='FBX poses from authoritative configuration projected through editable Blender. No per-instance meshes, no macro markers exported.')
 (OUT/'manifest.json').write_text(json.dumps(manifest,indent=2));shutil.copy2(CFG_PATH,OUT/'SolarLayoutConfig.json')
 print('EXPORT PASS:',len(records),'pose markers; 0 new shared model/texture exports')
def adopt_local():
 # Explicit supported round trip. Preserve all other source content, then protect
 # this manually authored version against whole-file regeneration.
 bpy.ops.wm.open_mainfile(filepath=str(DEST));lookup={r['id']:r for r in CFG['rocks']}
 receipt=json.loads((OUT/'source-receipt.json').read_text())
 for o in bpy.data.collections['SharedLibrary_NOT_EXPORTED'].objects:assert mesh_digest(o.data)==receipt['sharedMeshHashes'][o.name]
 assert len(bpy.data.collections['SparseAsteroids'].objects)==len(lookup)
 for o in bpy.data.collections['SparseAsteroids'].objects:
  r=lookup[o['instanceId']];assert o['assetId']==r['assetId'];assert max(o.scale)-min(o.scale)<.0001
  r['position']=U(o.location);r['scale']=o.scale.x;r['rotationDeg']=[math.degrees(a) for a in o.rotation_euler]
 backup=CFG_PATH.with_suffix('.before-adopt-'+digest(CFG_PATH)[:12]+'.json')
 if not backup.exists():shutil.copy2(CFG_PATH,backup)
 blendBackup=DEST.with_name('SpaceEnvironment_SolarLayout.before-adopt-'+digest(DEST)[:12]+'.blend')
 if not blendBackup.exists():shutil.copy2(DEST,blendBackup)
 CFG_PATH.write_text(json.dumps(CFG,indent=2),encoding='utf-8')
 bpy.data.scenes['SolarLayout_LocalBattle']['config_sha256']=digest(CFG_PATH)
 bpy.ops.wm.save_as_mainfile(filepath=str(DEST));receipt.update(source_sha256=digest(DEST),config_sha256=digest(CFG_PATH),regenerationAllowed=False)
 (OUT/'source-receipt.json').write_text(json.dumps(receipt,indent=2));print('Local poses adopted. Run inspect and export after visual review. Full regeneration is now disabled to preserve manually authored content.')
if __name__=='__main__':
 assert bpy.app.background and '--factory-startup' in sys.argv,'Independent background process required'
 stage=sys.argv[sys.argv.index('--')+1];globals()[stage.replace('-','_')]()
