"""Measure unchanged-camera frozen SRP flare OFF/ON captures; never alters images."""
from pathlib import Path
from PIL import Image
import numpy as np,json,sys
folder=Path(sys.argv[1])
result={'scope':'Read-only pixel differences from actual frozen Game screenshots. No image processing applied to delivered screenshots. Occluded pair should have negligible flare contribution compared to the unobstructed pair.','pairs':{}}
for name in ('occluded','unoccluded'):
    off=np.asarray(Image.open(folder/f'flare-{name}-OFF.png').convert('RGB')).astype(np.float32)
    on=np.asarray(Image.open(folder/f'flare-{name}-ON.png').convert('RGB')).astype(np.float32)
    delta=np.abs(on-off)
    result['pairs'][name]={'meanAbsoluteChannelDifference255':float(delta.mean()),'maximumChannelDifference255':float(delta.max()),'pixelsDifferentOver2':int((delta.max(axis=2)>2).sum()),'width':off.shape[1],'height':off.shape[0]}
normal=result['pairs']['unoccluded']['meanAbsoluteChannelDifference255']
blocked=result['pairs']['occluded']['meanAbsoluteChannelDifference255']
result['occludedToUnoccludedDifferenceRatio']=blocked/normal if normal else None
result['occlusionComparisonPassed']=normal>.02 and blocked<normal*.05
(folder/'flare-pixel-comparison.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
