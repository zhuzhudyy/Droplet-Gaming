"""Independent, editable rotational meridian. Source +Z ROUND head / +Y up.
Only the export copy is mapped to calibrated Blender +Y forward / +Z up.
No import of, repair to, or dependence on the rejected PerfectDroplet mesh.
"""
import bpy,bmesh,math,json,sys,bisect,hashlib
from pathlib import Path
from collections import Counter
from mathutils import Vector,Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'ArtSource/Blender/Droplet/Droplet_Rebuilt'
OUT=ROOT/'ArtSource/Exports/Droplet/Droplet_Rebuilt'
E=ROOT/'docs/verification/Droplet_Rebuilt'
SOURCE=ART/'Droplet_Rebuilt.blend';FBX=OUT/'Droplet_Rebuilt.fbx'
LENGTH=2.4;DIAMETER=.8;RADIAL=64;RINGS=55
# Tail to head: tangent at each pole is radial. Broadest section is near head.
# Plain JSON control points are also stored inside the .blend text datablock.
CONTROL=[(-1.2,0),(-1.2,.02),(-.50,.105),(.77,.83),(1.2,.49),(1.2,0)]
C=Matrix(((-1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
def bez(p,t):
    n=len(p)-1
    return tuple(sum(math.comb(n,i)*(1-t)**(n-i)*t**i*v[a] for i,v in enumerate(p)) for a in (0,1))
def deriv(p):return [tuple((len(p)-1)*(b[a]-a0[a]) for a in (0,1)) for a0,b in zip(p,p[1:])]
D=deriv(CONTROL);DD=deriv(D)
lo,hi=0.,1.
for _ in range(64):
    mid=(lo+hi)/2
    if bez(D,mid)[1]>0:lo=mid
    else:hi=mid
TMAX=(lo+hi)/2;RS=.4/bez(CONTROL,TMAX)[1]
def profile(t):
    z,r=bez(CONTROL,t);return z,r*RS
def normal(t,a):
    dz,dr=bez(D,t);return Vector((dz*math.cos(a),dz*math.sin(a),-dr*RS)).normalized()
def write(name,data):E.mkdir(parents=True,exist_ok=True);(E/name).write_text(json.dumps(data,indent=2),encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def camera_rotation(position):
    forward=(-Vector(position)).normalized();right=forward.cross(Vector((0,1,0))).normalized();up=right.cross(forward)
    return Matrix((right,up,-forward)).transposed().to_quaternion()
def reset():
    # Only used by fresh isolated --factory-startup processes, never a user's scene.
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def ring_ts():
    ts=[i/24000 for i in range(24001)];dens=[0.]
    for a,b in zip(ts,ts[1:]):
        za,ra=profile(a);zb,rb=profile(b)
        na=normal(a,0);nb=normal(b,0)
        # Curvature + arc length + a bounded pole density term; no subdivisions.
        ds=math.hypot(zb-za,rb-ra)
        dens.append(dens[-1]+ds+0.33*math.acos(max(-1,min(1,na.dot(nb))))+0.015*ds/max(.006,(ra+rb)/2))
    result=[]
    for i in range(1,RINGS+1):
        v=dens[-1]*i/(RINGS+1);j=bisect.bisect_left(dens,v)
        result.append(ts[j-1]+(ts[j]-ts[j-1])*(v-dens[j-1])/(dens[j]-dens[j-1]))
    j=min(range(len(result)),key=lambda j:abs(result[j]-TMAX));result[j]=TMAX
    return sorted(result)
def build():
    assert not SOURCE.exists(),'Source already exists; preserve it.'
    ART.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True);reset()
    s=bpy.context.scene;s.name='Droplet_Rebuilt_Inspection';s.unit_settings.system='METRIC';s.unit_settings.scale_length=1
    verts=[(0,0,-1.2)];ns=[Vector((0,0,-1))];faces=[]
    ts=ring_ts()
    for t in ts:
        z,r=profile(t)
        for j in range(RADIAL):
            a=2*math.pi*j/RADIAL;verts.append((r*math.cos(a),r*math.sin(a),z));ns.append(normal(t,a))
    head=len(verts);verts.append((0,0,1.2));ns.append(Vector((0,0,1)))
    for j in range(RADIAL):faces.append((0,1+(j+1)%RADIAL,1+j))
    for i in range(RINGS-1):
        a=1+i*RADIAL;b=a+RADIAL
        for j in range(RADIAL):faces.append((a+j,a+(j+1)%RADIAL,b+(j+1)%RADIAL,b+j))
    a=1+(RINGS-1)*RADIAL
    for j in range(RADIAL):faces.append((a+j,a+(j+1)%RADIAL,head))
    mesh=bpy.data.meshes.new('Droplet_Rebuilt_Surface');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('Droplet_Rebuilt',mesh);s.collection.objects.link(obj)
    for f in mesh.polygons:f.use_smooth=True
    mesh.normals_split_custom_set_from_vertices(ns)
    obj['length_m']=LENGTH;obj['diameter_m']=DIAMETER;obj['head_axis']='+Z';obj['tail_axis']='-Z';obj['source']='New quintic meridian, independent of rejected mesh'
    for name,z in [('HeadMarker',1.2),('TailMarker',-1.2),('ForwardMarker',1.7)]:
        marker=bpy.data.objects.new(name,None);s.collection.objects.link(marker);marker.location.z=z;marker.empty_display_type='PLAIN_AXES';marker.empty_display_size=.12;marker.show_name=True
    curve=bpy.data.curves.new('Editable_Longitudinal_Profile','CURVE');curve.dimensions='3D'
    spline=curve.splines.new('POLY');spline.points.add(200)
    for i,p in enumerate(spline.points):z,r=profile(i/200);p.co=(r,0,z,1)
    guide=bpy.data.objects.new('ProfileGuide_InspectionOnly',curve);s.collection.objects.link(guide);guide.hide_render=True;guide.hide_set(True)
    txt=bpy.data.texts.new('PROFILE_PARAMETERS.json');txt.write(json.dumps({'control_tail_to_head':CONTROL,'radial_scale':RS,'length':LENGTH,'diameter':DIAMETER,'ring_parameters':ts,'regenerate_with':'Tools/Blender/Droplet_Rebuilt/pipeline.py'},indent=2))
    txt=bpy.data.texts.new('REBUILD_NOTES.txt');txt.write('Round head +Z; pointed tail -Z; +Y up. One closed mesh, 7040 triangles. Markers and profile curve are inspection helpers, not surface decoration. Original stable length 2.4 m retained; new diameter .8 m for 3:1. Collision radius .7 m is a Unity gameplay setting and is unchanged. Material tests belong only to this inspection file.\n')
    grey=bpy.data.materials.new('Rebuilt_Inspection_Gray');grey.diffuse_color=(.42,.42,.42,1);grey.use_nodes=True
    bs=grey.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.42,.42,.42,1);bs.inputs['Roughness'].default_value=.48
    obj.data.materials.append(grey)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_rotation=Vector((4,2,2.2)).to_track_quat('Z','Y');area.spaces.active.region_3d.view_distance=4;area.spaces.active.region_3d.view_location=(0,0,0)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    inspect_mesh(obj,'geometry.json')
    write('source.json',{'blender':bpy.app.version_string,'build_hash':bpy.app.build_hash.decode(),'python':sys.version,'source':str(SOURCE),'sha256':sha(SOURCE),'reference':str(ROOT/'ArtSource/droplet'),'reference_sha256':sha(ROOT/'ArtSource/droplet'),'max_radius_at_z':profile(TMAX)[0],'control':CONTROL,'units':'metres','head':'+Z','tail':'-Z'})
def inspect_mesh(obj,name):
    mesh=obj.data;mesh.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(mesh)
    edges=Counter(tuple(sorted(e.vertices)) for e in mesh.edges)
    facekeys=Counter(tuple(sorted(p.vertices)) for p in mesh.polygons)
    points=[obj.matrix_world@v.co for v in mesh.vertices]
    tree=BVHTree.FromPolygons(points,[list(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
    overlaps=tree.overlap(tree);pairs=[]
    for a,b in overlaps:
        if a<b and not set(mesh.loop_triangles[a].vertices)&set(mesh.loop_triangles[b].vertices):pairs.append((a,b))
    seen=set();components=0
    for v in bm.verts:
        if v.index in seen:continue
        components+=1;stack=[v]
        while stack:
            n=stack.pop()
            if n.index in seen:continue
            seen.add(n.index);stack.extend(e.other_vert(n) for e in n.link_edges)
    mn=[min(p[a] for p in points) for a in range(3)];mx=[max(p[a] for p in points) for a in range(3)]
    zero=sum(t.area<1e-14 for t in mesh.loop_triangles)
    inward=sum(Vector(t.normal).dot(sum((mesh.corner_normals[i].vector for i in t.loops),Vector()))<=0 for t in mesh.loop_triangles)
    report={'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),'quads':sum(len(p.vertices)==4 for p in mesh.polygons),'triangle_caps':sum(len(p.vertices)==3 for p in mesh.polygons),'components':components,'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'duplicate_faces':sum(v-1 for v in facekeys.values()),'duplicate_vertices':len(points)-len(set(tuple(p) for p in points)),'zero_area_triangles':zero,'inward_triangles':inward,'signed_volume':bm.calc_volume(signed=True),'nonadjacent_intersections':len(pairs),'dimensions':[mx[a]-mn[a] for a in range(3)],'center':[(mx[a]+mn[a])/2 for a in range(3)],'scale':list(obj.scale),'determinant':obj.matrix_world.determinant(),'hard_edges':sum(e.use_edge_sharp for e in mesh.edges)}
    write(name,report);bm.free()
    assert components==1 and report['nonmanifold_edges']==0 and report['duplicate_faces']==0 and zero==0 and inward==0 and not pairs and report['signed_volume']>0,report
    return report
def inspect():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));inspect_mesh(bpy.data.objects['Droplet_Rebuilt'],'reopened-geometry.json')
def render():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));s=bpy.context.scene;obj=bpy.data.objects['Droplet_Rebuilt']
    s.render.engine='CYCLES';s.cycles.samples=128;s.cycles.use_denoising=False
    s.render.resolution_x=1440;s.render.resolution_y=1080;s.render.resolution_percentage=100
    s.view_settings.view_transform='Standard';s.view_settings.look='None';s.view_settings.exposure=0;s.view_settings.gamma=1
    world=bpy.data.worlds.new('Inspection_Stripe_Environment');s.world=world;world.use_nodes=True
    n=world.node_tree.nodes;l=world.node_tree.links;n.clear()
    tex=n.new('ShaderNodeTexCoord');sep=n.new('ShaderNodeSeparateXYZ');l.new(tex.outputs['Normal'],sep.inputs[0])
    mul=n.new('ShaderNodeMath');mul.operation='MULTIPLY';mul.inputs[1].default_value=13;l.new(sep.outputs['Z'],mul.inputs[0])
    sine=n.new('ShaderNodeMath');sine.operation='SINE';l.new(mul.outputs[0],sine.inputs[0])
    ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.06;ramp.color_ramp.elements[0].color=(.035,.035,.035,1);ramp.color_ramp.elements[1].position=.15;ramp.color_ramp.elements[1].color=(.65,.65,.65,1);l.new(sine.outputs[0],ramp.inputs[0])
    bg=n.new('ShaderNodeBackground');l.new(ramp.outputs[0],bg.inputs['Color']);out=n.new('ShaderNodeOutputWorld');l.new(bg.outputs[0],out.inputs[0])
    camd=bpy.data.cameras.new('InspectionCamera');cam=bpy.data.objects.new('InspectionCamera',camd);s.collection.objects.link(cam);s.camera=cam;camd.type='ORTHO';camd.ortho_scale=3.35
    ld=bpy.data.lights.new('InspectionKey','AREA');light=bpy.data.objects.new('InspectionKey',ld);s.collection.objects.link(light);light.location=(1,4,2);light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler();ld.energy=180;ld.shape='DISK';ld.size=5
    mirror=bpy.data.materials.new('Rebuilt_Inspection_Mirror');mirror.use_nodes=True;bs=mirror.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.88,.88,.88,1);bs.inputs['Metallic'].default_value=1;bs.inputs['Roughness'].default_value=.035
    p=ART/'Previews';p.mkdir(exist_ok=True);files=[]
    views=[('Gray-Side',(5,0,0),False),('Gray-ThreeQuarter',(4,1.4,2.2),False),('Gray-Head',(1,.4,5),False),('Gray-Tail',(1,.4,-5),False),('Mirror-Side',(5,0,0),True),('Mirror-ThreeQuarter',(4,1.4,2.2),True),('Mirror-Opposite',(-4,1,-2),True),('Mirror-Head',(1,.4,5),True),('Mirror-Tail',(1,.4,-5),True)]
    grey=obj.data.materials[0]
    for name,direction,metal in views:
        obj.data.materials[0]=mirror if metal else grey
        cam.location=direction;cam.rotation_euler=camera_rotation(direction).to_euler()
        light.hide_render=metal
        s.render.film_transparent=True
        s.render.filepath=str(p/(name+'.png'));bpy.ops.render.render(write_still=True);files.append(s.render.filepath)
    # Oblique pole detail resolves whether tiny dark rings are reflections or creases.
    target=Vector((0,0,-1.195));cam.location=target+Vector((.08,.025,-.035));cam.rotation_euler=camera_rotation(cam.location-target).to_euler();camd.ortho_scale=.055
    s.render.filepath=str(p/'Mirror-Tail-Detail.png');bpy.ops.render.render(write_still=True);files.append(s.render.filepath);camd.ortho_scale=3.35
    # A sequence of actual views: reflection motion is inspected, not a static decal.
    for i in range(7):
        angle=math.radians(-35+i*12);cam.location=(5*math.cos(angle),.8,5*math.sin(angle));cam.rotation_euler=camera_rotation(cam.location).to_euler()
        s.render.filepath=str(p/f'Mirror-Sweep-{i:02}.png');bpy.ops.render.render(write_still=True);files.append(s.render.filepath)
    # Save only an additional inspection file; main source stays a simple editable asset.
    obj.data.materials[0]=mirror;cam.location=(4,1.4,2.2);cam.rotation_euler=camera_rotation(cam.location).to_euler();s.render.film_transparent=False
    bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Droplet_Rebuilt_Inspection.blend'))
    write('renders.json',{'engine':'Cycles','samples':128,'denoise':False,'bloom':False,'motion_blur':False,'material':'temporary neutral gray / metal roughness .035','environment':'procedural directional gray stripes, no image textures','camera_views':views,'files':files})
def export():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));obj=bpy.data.objects['Droplet_Rebuilt'];mesh=obj.data.copy();mesh.transform(C)
    ns=[C.to_3x3()@n.vector for n in obj.data.corner_normals];mesh.normals_split_custom_set(ns)
    s=bpy.data.scenes.new('Export_Copy_Only');s.unit_settings.system='METRIC';s.unit_settings.scale_length=1;bpy.context.window.scene=s
    copy=bpy.data.objects.new('Droplet_Rebuilt',mesh);s.collection.objects.link(copy);copy.select_set(True);bpy.context.view_layer.objects.active=copy
    tri=copy.modifiers.new('ExportTriangulation','TRIANGULATE');tri.quad_method='FIXED'
    if hasattr(tri,'keep_custom_normals'):tri.keep_custom_normals=True
    options=dict(use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=True)
    bpy.ops.export_scene.fbx(filepath=str(FBX),**options)
    write('export.json',{'source_sha256':sha(SOURCE),'fbx_sha256':sha(FBX),'source_head':'+Z','source_up':'+Y','staging_head':'+Y','staging_up':'+Z','conversion_determinant':C.determinant(),'matrix':[list(r) for r in C],'options':{k:sorted(v) if isinstance(v,set) else v for k,v in options.items()},'export_objects':1,'marker_policy':'Unity derives named empty marker positions from the actual imported mesh endpoints; avoids known FBX Empty axis issue'})
def roundtrip():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE));o=bpy.data.objects['Droplet_Rebuilt'];expected=[C@v.co for v in o.data.vertices];normals={loop.vertex_index:C.to_3x3()@o.data.corner_normals[i].vector for i,loop in enumerate(o.data.loops)}
    reset();bpy.ops.import_scene.fbx(filepath=str(FBX),use_anim=False,use_custom_normals=True,use_image_search=False);objs=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(objs)==1;obj=objs[0]
    tree=KDTree(len(expected))
    for i,p in enumerate(expected):tree.insert(p,i)
    tree.balance();errs=[];angles=[];mapping={}
    for v in obj.data.vertices:
        _,idx,d=tree.find(obj.matrix_world@v.co);errs.append(d);mapping[v.index]=idx
    for i,loop in enumerate(obj.data.loops):
        n=(obj.matrix_world.to_3x3().inverted().transposed()@obj.data.corner_normals[i].vector).normalized();angles.append(math.degrees(n.angle(normals[mapping[loop.vertex_index]],0)))
    report=inspect_mesh(obj,'fbx-geometry.json');report.update(max_vertex_error=max(errs),max_normal_degrees=max(angles),mesh_count=len(objs),fbx_sha256=sha(FBX));write('roundtrip.json',report)
    assert max(errs)<1e-5 and max(angles)<.12
    bpy.ops.wm.save_as_mainfile(filepath=str(E/'Droplet_Rebuilt_FBX_Roundtrip.blend'))
def diagnose_old():
    path=ROOT/'ArtSource/Blender/Droplet/PerfectDroplet/PerfectDroplet.blend';bpy.ops.wm.open_mainfile(filepath=str(path));obj=bpy.data.objects['PerfectDroplet_Game'];pts=[obj.matrix_world@v.co for v in obj.data.vertices]
    report=inspect_mesh(obj,'previous-geometry.json');report.update(file=str(path),hash=sha(path),positive_tip_zone_radius=max(math.hypot(p.x,p.y) for p in pts if p.z>1.176),negative_tip_zone_radius=max(math.hypot(p.x,p.y) for p in pts if p.z< -1.176));write('previous-diagnosis.json',report)
def details():
    bpy.ops.wm.open_mainfile(filepath=str(ART/'Droplet_Rebuilt_Inspection.blend'));s=bpy.context.scene;cam=s.camera;obj=bpy.data.objects['Droplet_Rebuilt'];cam.data.clip_start=.00001;cam.data.ortho_scale=.024
    if not bpy.data.materials.get('Rebuilt_Inspection_Gray'):
        gray=bpy.data.materials.new('Rebuilt_Inspection_Gray');gray.use_nodes=True;bs=gray.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.42,.42,.42,1);bs.inputs['Roughness'].default_value=.48
    target=Vector((0,0,-1.198));s.render.film_transparent=False
    for name,direction,gray in [('Gray-Tail-Detail',(.08,.025,-.025),True),('Mirror-Tail-Detail',(.08,.025,-.025),False),('Mirror-Tail-Detail-Other',(-.08,.025,-.025),False)]:
        cam.location=target+Vector(direction);cam.rotation_euler=camera_rotation(Vector(direction)).to_euler();obj.data.materials[0]=bpy.data.materials['Rebuilt_Inspection_Gray' if gray else 'Rebuilt_Inspection_Mirror'];bpy.data.objects['InspectionKey'].hide_render=not gray
        s.render.filepath=str(ART/'Previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
    write('pole-detail.json',{'clip_start':cam.data.clip_start,'orthographic_width_m':.024,'target':list(target),'tail_analytic_curvature_radius_m':(5*.02*RS)**2/(20*.7),'files':['Gray-Tail-Detail.png','Mirror-Tail-Detail.png','Mirror-Tail-Detail-Other.png'],'note':'Previous too-close detail camera near plane clipped the tip; corrected inspection camera only, geometry unchanged.'})
if __name__=='__main__':globals()[sys.argv[sys.argv.index('--')+1]]()
