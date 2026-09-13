"""Source-only authoring. Execute in a separate --background --factory-startup process.
Uses the project's calibrated Blender +Y forward/+Z up FBX contract.
Generated source is backed up on reruns; no other .blend or live session is changed.
"""
import bpy, bmesh, json, math, random, sys, shutil, hashlib
from pathlib import Path
from datetime import datetime
from mathutils import Vector, Matrix
ROOT=Path(__file__).resolve().parents[3]
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from asteroid_library import build_asteroid_library
from celestial_library import build_celestial_library
from source_guard import check_project_source
ART=ROOT/'ArtSource/Blender/SpaceEnvironment'
ART.mkdir(parents=True,exist_ok=True)
CFG=json.loads((HERE/'config.json').read_text(encoding='utf-8'))
C=lambda v: Vector((v[0],v[2],v[1]))
def collection(name):
    c=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c); return c
def material(name,col):
    m=bpy.data.materials.new(name); m.diffuse_color=(*col,1); return m
def mesh_object(name,mesh,col):
    o=bpy.data.objects.new(name,mesh); col.objects.link(o); return o
def move_collection(o,col):
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
def camera(name,pos,target,col,orthographic=0):
    d=bpy.data.cameras.new(name); o=mesh_object(name,d,col); o.location=C(pos)
    o.rotation_euler=(C(target)-o.location).to_track_quat('-Z','Y').to_euler()
    d.lens=36/(2*math.tan(math.radians(CFG['vertical_fov'])/2)*16/9)
    d.sensor_width=36; d.clip_start=CFG['near_clip']; d.clip_end=CFG['far_clip']
    if orthographic: d.type='ORTHO'; d.ortho_scale=orthographic
    return o
def point_at_screen(u,v,depth):
    cam=CFG['reference_camera']; hh=depth*math.tan(math.radians(CFG['vertical_fov'])/2)
    return [cam[0]+(u-.5)*2*hh*16/9,cam[1]+(.5-v)*2*hh,cam[2]+depth]
def main():
    # Never operate on a live/loaded file or an edited target artifact.
    if not bpy.app.background or '--factory-startup' not in sys.argv or bpy.data.filepath:
        raise RuntimeError('Use run.py build in a separate factory background process; live scene execution is forbidden.')
    check_project_source(ROOT)
    # This artifact is regenerated in its entirety, not an incremental collection editor.
    for o in list(bpy.data.objects):
        if o.name in {'Cube','Camera','Light'}: bpy.data.objects.remove(o,do_unlink=True)
    sc=bpy.context.scene; sc.name='SpaceEnvironment'; sc['owner']='DropletPrototype.SpaceEnvironment.v1'
    sc['config_json']=json.dumps(CFG); sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1
    cols={n:collection(n) for n in ['CelestialBodies','AsteroidLibrary','AsteroidBelt_Near','AsteroidBelt_Mid','AsteroidBelt_Far','GameplayReserve','ReferenceCameras','PreviewOnly','LayoutMarkers','SkyBackground','CelestialLibrary']}
    mats=[material('RockBasalt',(.29,.265,.23)),material('RockSlate',(.34,.37,.39))]
    ast=build_asteroid_library(cols['AsteroidLibrary'],mats)
    cel=build_celestial_library(cols['CelestialLibrary'])
    # Normalize the object naming contract; originals remain hidden library assets.
    library={}
    for i,(key,objs) in enumerate(ast.items()):
        aid=f'Rock{i+1:02d}'; library[aid]=objs
        for j,o in enumerate(objs): o.name=f'{aid}_LOD{j}'; o['assetId']=aid; o['lod']=j; o['shape']=key
    for aid,objs in cel.items():
        library[aid]=objs
        for j,o in enumerate(objs): o.name=f'{aid}_LOD{j}'; o['assetId']=aid; o['lod']=j
    rng=random.Random(CFG['seed']); records=[]
    center=C(CFG['gameplay_center'])
    def radius(o): return max(v.co.length for v in o.data.vertices)
    def instance(aid,uid,group,pos,scale,rot=(0,0,0),lod=0,mat=0):
        proto=library[aid][lod]
        ob=mesh_object(f'ENV_{aid}_{uid}',proto.data,cols[group]); ob.location=C(pos)
        ob.rotation_euler=rot; ob.scale=(scale,scale,scale)
        ob['assetId']=aid; ob['instanceId']=uid; ob['previewLOD']=lod; ob['materialVariant']=mat
        if aid.startswith('Rock'):
            for slot in ob.material_slots: slot.link='OBJECT'; slot.material=mats[mat]
        clearance=(ob.location-center).length-radius(proto)*scale-CFG['reserve_radius']
        assert clearance>0, (ob.name,clearance)
        marker=mesh_object(ob.name,None,cols['LayoutMarkers']); marker.name='POSE_'+ob.name
        marker.location=ob.location; marker.rotation_euler=ob.rotation_euler; marker.scale=ob.scale
        marker['assetId']=aid;marker['instanceId']=uid;marker['group']=group;marker['materialVariant']=mat
        marker.empty_display_size=5
        records.append({'id':uid,'assetId':aid,'name':ob.name,'group':group,'clearance':clearance})
        return ob
    for aid in ['Sun','Earth']:
        u,v,diam=CFG[aid.lower()+'_screen']; depth=CFG[aid.lower()+'_depth']
        ob=instance(aid,aid,'CelestialBodies',point_at_screen(u,v,depth),diam*depth*math.tan(math.radians(CFG['vertical_fov'])/2),rot=(0,0,math.radians(20)) if aid=='Earth' else (0,0,0))
    # Near hero silhouettes sit at frame edges, beyond the full movement sphere.
    near=[(-.045,.78,1550,285),(.045,1.055,1500,365),(.94,1.105,1700,390),(1.09,.82,1650,145),(.21,.71,2000,105),(.30,.85,2300,70),(.69,.80,2300,76),(.81,.72,2100,74),(.08,.56,1800,68),(.93,.58,2400,65),(.02,.43,2250,50),(.99,.99,2000,60)]
    for i,(u,v,d,s) in enumerate(near[:CFG['near_count']]):
        aid=['Rock01','Rock05','Rock04'][i] if i<3 else f'Rock{i%8+1:02d}'
        instance(aid,f'N{i+1:03d}','AsteroidBelt_Near',point_at_screen(u,v,d),s,tuple(rng.uniform(-math.pi,math.pi) for _ in range(3)),0,i%2)
    for i in range(CFG['mid_count']):
        # Most density on the visible front arc, with a substantial side/rear continuation.
        angle=rng.uniform(-1.3,1.3) if i<120 else rng.uniform(1.3,math.tau-1.3)
        r=rng.uniform(*CFG['mid_radius_range']); x=math.sin(angle)*r; z=260+math.cos(angle)*r
        y=CFG['belt_slope']*x+CFG['belt_height']+rng.uniform(-CFG['belt_thickness'],CFG['belt_thickness'])
        instance(f'Rock{i%8+1:02d}',f'M{i+1:03d}','AsteroidBelt_Mid',[x,y,z],rng.uniform(*CFG['mid_size_range']),tuple(rng.uniform(-math.pi,math.pi) for _ in range(3)),1,i%2)
    # Distant rocks are full closed icosahedra in eight bounded spatial sectors.
    bm=bmesh.new(); bmesh.ops.create_icosphere(bm,subdivisions=1,radius=1); unit=bpy.data.meshes.new('FarIcosahedronTemplate'); bm.to_mesh(unit); bm.free()
    far_stats=[]
    for sector in range(CFG['far_sectors']):
        vv=[]; ff=[]; positions=[]; min_clear=1e9
        for i in range(CFG['far_rocks_per_sector']):
            a=(sector+rng.random())/CFG['far_sectors']*math.tau
            r=rng.uniform(*CFG['far_radius_range']); x=math.sin(a)*r; z=260+math.cos(a)*r
            y=CFG['belt_slope']*x+CFG['belt_height']+rng.uniform(-130,130)
            p=C((x,y,z)); s=rng.uniform(*CFG['far_size_range']); scale=Vector((s*rng.uniform(.65,1.4),s,s*rng.uniform(.7,1.3)))
            positions.append(p); off=len(vv)
            vv.extend([tuple(p+Vector((v.co.x*scale.x,v.co.y*scale.y,v.co.z*scale.z))) for v in unit.vertices])
            ff.extend([tuple(off+j for j in f.vertices) for f in unit.polygons])
            min_clear=min(min_clear,(p-center).length-max(scale)-CFG['reserve_radius'])
        origin=sum(positions,Vector())/len(positions); vv=[tuple(Vector(v)-origin) for v in vv]
        me=bpy.data.meshes.new(f'FarSector{sector+1:02d}_Mesh'); me.from_pydata(vv,[],ff); me.materials.append(mats[0]);me.update()
        aid=f'Far{sector+1:02d}'; proto=mesh_object(aid+'_LOD0',me,cols['AsteroidLibrary']);proto['assetId']=aid;proto['lod']=0;library[aid]=[proto]
        ob=mesh_object('ENV_'+aid+'_'+aid,me,cols['AsteroidBelt_Far']);ob.location=origin;ob['assetId']=aid;ob['instanceId']=aid
        mk=mesh_object('POSE_'+ob.name,None,cols['LayoutMarkers']);mk.location=origin;mk['assetId']=aid;mk['instanceId']=aid;mk['group']='AsteroidBelt_Far';mk['materialVariant']=0
        records.append({'id':aid,'assetId':aid,'name':ob.name,'group':'AsteroidBelt_Far','clearance':min_clear});far_stats.append({'assetId':aid,'rocks':len(positions),'clearance':min_clear})
    bpy.data.meshes.remove(unit)
    # A true enclosing sphere uses an original equirectangular star-only texture.
    import numpy as np
    w,h=2048,1024; pixels=np.zeros((h,w,4),dtype=np.float32); pixels[:,:,0:3]=(.001,.002,.004);pixels[:,:,3]=1
    for i in range(3200):
        x=rng.randrange(w); lat=math.asin(rng.uniform(-1,1)); y=min(h-1,max(0,int((lat/math.pi+.5)*h)))
        value=rng.uniform(.08,.55);pixels[y,x,:3]=value
    im=bpy.data.images.new('SpaceStars_BaseColor',width=w,height=h);im.pixels.foreach_set(pixels.ravel());(ART/'Textures').mkdir(exist_ok=True)
    im.filepath_raw=str(ART/'Textures/SpaceStars_BaseColor.png');im.file_format='PNG';im.save();im.pack()
    sky_mat=material('SpaceStars',(.02,.025,.035));sky_mat.use_nodes=True
    nt=sky_mat.node_tree; tex=nt.nodes.new('ShaderNodeTexImage');tex.image=im;nt.nodes.active=tex
    nt.links.new(tex.outputs['Color'],nt.nodes.get('Principled BSDF').inputs['Base Color'])
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1); sky=bpy.context.object;sky.name='Sky_LOD0';move_collection(sky,cols['CelestialLibrary'])
    bm=bmesh.new();bm.from_mesh(sky.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(sky.data);bm.free();sky.data.materials.append(sky_mat);sky['assetId']='Sky';sky['lod']=0;library['Sky']=[sky]
    sob=mesh_object('ENV_Sky_Sky',sky.data,cols['SkyBackground']);sob.location=center;sob.scale=(CFG['sky_radius'],)*3;sob['assetId']='Sky';sob['instanceId']='Sky'
    sob.hide_render=True;sob.hide_set(True);sob['preview_note']='Hidden only for neutral Workbench preview; exported shared sky is Unlit in Unity.'
    mk=mesh_object('POSE_ENV_Sky_Sky',None,cols['LayoutMarkers']);mk.location=sob.location;mk.scale=sob.scale;mk['assetId']='Sky';mk['instanceId']='Sky';mk['group']='SkyBackground';mk['materialVariant']=0
    # Nonrendered reserve sphere and textual metadata, never exported as collision geometry.
    reserve=mesh_object('GameplayReserve_R730_Safety760',None,cols['GameplayReserve']);reserve.location=center;reserve.empty_display_type='SPHERE';reserve.empty_display_size=CFG['reserve_radius'];reserve.hide_render=True
    reserve['boundaryRadius']=730;reserve['cameraDistance']=8;reserve['cameraHeight']=2.2;reserve['hitRadius']=.7;reserve['safetyMargin']=30
    cams=cols['ReferenceCameras'];ref=camera('ReferenceCamera',CFG['reference_camera'],[0,20,260],cams)
    camera('SideCamera',[5800,1600,260],[0,20,260],cams,8500)
    camera('TopCamera',[0,6800,261],[0,20,260],cams,15000)
    camera('GameplayTurnCamera',[450,120,260],[2000,-350,900],cams)
    camera('BeltCloseCamera',[-750,-180,780],[-1800,450,1800],cams)
    # Library showcase is independent of the full scene and contains linked objects only.
    showcase=bpy.data.scenes.new('AsteroidLibraryReview');showcase.world=sc.world
    shcol=bpy.data.collections.new('PreviewOnly_LibraryGrid');showcase.collection.children.link(shcol)
    for i in range(8):
        for j in range(3):
            p=library[f'Rock{i+1:02d}'][j];o=mesh_object(f'{p.name}_Showcase',p.data,shcol);o.location=((i%4)*3.6,(i//4)*5+j*1.5,0)
    shcam=camera('LibraryCamera',[5.4,19,-10],[5.4,0,4],shcol,18);showcase.camera=shcam
    for scene in [sc,showcase]:
        scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
        scene.display.shading.light='STUDIO';scene.display.shading.studiolight_rotate_z=.35;scene.display.shading.color_type='TEXTURE'
        scene.display.shading.show_shadows=False;scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH';scene.display.shading.show_specular_highlight=False
        scene.display.shading.background_type='WORLD';scene.world=bpy.data.worlds.new(scene.name+'_NeutralWorld');scene.world.color=(.003,.005,.008)
        scene.view_settings.view_transform='Standard'
    sc.camera=ref;cols['LayoutMarkers'].hide_render=True;cols['GameplayReserve'].hide_render=True
    # Exclude prototype-only collections, not linked mesh data.
    for n in ['AsteroidLibrary','CelestialLibrary','LayoutMarkers']:
        sc.view_layers[0].layer_collection.children[n].exclude=True
    for scr in bpy.data.screens:
        for area in scr.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.clip_end=10000
    for img in bpy.data.images:
        if img.source=='FILE': img.pack()
    bpy.context.view_layer.update()
    (ART/'source_layout_summary.json').write_text(json.dumps({'config':CFG,'instances':records,'far':far_stats},indent=2),encoding='utf-8')
    dest=ART/'SpaceEnvironment.blend'
    if dest.exists(): shutil.copy2(dest,ART/('SpaceEnvironment.before-'+datetime.now().strftime('%Y%m%d-%H%M%S')+'.blend'))
    bpy.ops.wm.save_as_mainfile(filepath=str(dest))
    (ART/'generated-source-receipt.json').write_text(json.dumps({'source_sha256': hashlib.sha256(dest.read_bytes()).hexdigest(),
        'purpose': 'Generator output identity only; not a visual/export validation approval.'}, indent=2), encoding='utf-8')
    print('SOURCE_GENERATED',str(dest),'instances',len(records)+1)
if __name__=='__main__':main()
