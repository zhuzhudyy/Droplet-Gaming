"""Launch the actual visible game for a reproducible, opt-in rendered comparison."""
from pathlib import Path
import subprocess, sys
root=Path(__file__).resolve().parents[2]
label=sys.argv[1] if len(sys.argv)>1 else 'After'
scene='FleetAssault_Droplet_Rebuilt' if label.lower().startswith('before') else 'FleetAssault_VisualUpgrade'
exe=root/'Builds/Windows-VisualUpgrade-20260908/DropletPrototype.exe'
output=root/'docs/verification/VisualUpgrade/Player'
output.mkdir(parents=True,exist_ok=True)
args=[str(exe),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080',
      '--visual-upgrade-validation',str(output),'--visual-upgrade-label',label,
      '--visual-upgrade-scene',scene,'--visual-upgrade-quit','-logFile',str(output/f'{label}-player.log')]
process=subprocess.Popen(args,cwd=exe.parent)
print(f'Visible rendered Player pid={process.pid}, scene={scene}',flush=True)
code=process.wait()
print(f'Player exit={code}',flush=True)
sys.exit(code)
