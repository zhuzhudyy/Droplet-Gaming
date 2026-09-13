"""Explicit first-time configuration authoring. Never overwrite an existing config."""
import json, math, random
from pathlib import Path
p=Path(__file__).with_name('layout_config.json')
if p.exists(): raise SystemExit('Configuration already exists; edit it directly, do not regenerate silently.')
rng=random.Random(908260)
cfg=dict(schemaVersion=1, seed=908260, auKm=149597870.7, metersPerUnit=1,
    battleRadiusAu=2.5,battlePhaseDeg=230,battleInclinationDeg=2,battleAscendingNodeDeg=0,
    localOrigin=[0,20,260],spawn=[0,8,0],proxyNear=14000,proxySpan=4000,skyRadius=22000,
    nearClip=.1,farClip=25000,fieldOfView=65,oldBoundaryRadius=730,boundaryRadius=5840,warningRadius=4880,
    reserveRadius=800,minimumRockSpacing=650,beltInnerAu=2.1,beltOuterAu=3.3,beltMaxInclinationDeg=8,
    macroMarkerCount=480,macroOverviewUnitsPerAu=10,
    coordinateContract='Macro heliocentric circular XZ orbits (Y up), local Unity metres. Blender (x,z,y) basis. Fixed design phases, not ephemerides.',
    bodies=[],rocks=[])
for name,a,phase,inc,node,rad,asset in [
 ('Sun',0,0,0,0,695700,'Sun'),('Mercury',.387,15,7,48,2439.7,''),
 ('Venus',.723,140,3.4,76,6051.8,''),('Earth',1,30,0,0,6371,'Earth'),
 ('Mars',1.524,285,1.85,49,3389.5,''),('Jupiter',5.203,95,1.3,100,69911,''),
 ('Saturn',9.537,205,2.49,114,58232,''),('Uranus',19.189,320,.77,74,25362,''),
 ('Neptune',30.070,70,1.77,132,24622,'')]:
 cfg['bodies'].append(dict(id=name,semiMajorAu=a,phaseDeg=phase,inclinationDeg=inc,ascendingNodeDeg=node,radiusKm=rad,assetId=asset,readabilityMultiplier=1))
positions=[[-1050,-230,560],[1150,420,900],[-430,540,-900]]
for i in range(32):
 if i<3: pos=positions[i]; scale=[22,28,18][i];group='ParallaxReferences'
 else:
  group='SparseMid' if i<12 else 'SparseOuter'
  lo,hi=(1800,3300) if i<12 else (3500,5500)
  for attempt in range(10000):
   a=rng.uniform(0,math.tau);y=rng.uniform(-.75,.75);d=rng.uniform(lo,hi);r=math.sqrt(1-y*y)
   pos=[d*r*math.cos(a),20+d*y,260+d*r*math.sin(a)]
   if all(math.dist(pos,x['position'])>=cfg['minimumRockSpacing'] for x in cfg['rocks']): break
  else: raise RuntimeError('spacing rejection exhausted')
  scale=rng.uniform(12,32)
 cfg['rocks'].append(dict(id=f'rock-{i+1:03}',assetId=f'Rock{1+i%8:02}',group=group,position=[round(v,5) for v in pos],scale=round(scale,5),rotationDeg=[round(rng.uniform(-180,180),4) for k in range(3)],materialVariant=i%2,coordinateSemantic='local-metre'))
cfg['cameras']=[
 dict(name='ReferenceCamera',position=[0,20,-380],target=[0,20,260],fov=55),
 dict(name='SpawnForward',position=[0,10.2,-8],target=[0,8,35],fov=65),
 dict(name='SpawnReverse',position=[0,10.2,-8],target=[0,10.2,-1000],fov=65),
 dict(name='FleetOutskirts',position=[650,150,600],target=[-1000,-200,560],fov=65),
 dict(name='OpenSpaceForward',position=[3200,800,-900],target=[3200,800,1000],fov=65),
 dict(name='OpenSpaceReverse',position=[3200,800,-900],target=[3200,800,-2900],fov=65),
 dict(name='ParallaxA',position=[-850,-230,250],target=[-1050,-230,560],fov=65),
 dict(name='ParallaxB',position=[-650,-230,250],target=[-850,-230,560],fov=65)]
p.write_text(json.dumps(cfg,indent=2),encoding='utf-8')
print(p)
