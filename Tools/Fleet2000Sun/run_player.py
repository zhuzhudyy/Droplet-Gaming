"""Visible, native 1080p standalone validation plus WDDM process memory sampling."""
from pathlib import Path
import subprocess,sys,json,time
root=Path(__file__).resolve().parents[2]
label=sys.argv[1] if len(sys.argv)>1 else 'Final'
exe=root/'Builds/Windows-Fleet2000Sun-20260908/DropletPrototype.exe'
output=root/'docs/verification/Fleet2000Sun/Player'
output.mkdir(parents=True,exist_ok=True)
args=[str(exe),'-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','--fleet2000-validation',str(output),'--fleet2000-label',label,'--fleet2000-quit','-logFile',str(output/f'{label}-player.log')]
player=subprocess.Popen(args,cwd=exe.parent)
print(f'Visible standalone PID {player.pid}',flush=True)
sample=output/f'{label}-wddm-memory.csv'
error=(output/f'{label}-memory-error.log').open('w',encoding='utf-8')
startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
counter=subprocess.Popen(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'Tools/Fleet2000Sun/sample_gpu_memory.ps1'),'-PlayerProcessId',str(player.pid),'-OutputPath',str(sample)],stdout=subprocess.DEVNULL,stderr=error,startupinfo=startup)
(output/f'{label}-launch.json').write_text(json.dumps({'pid':player.pid,'argv':args,'memoryScope':'Windows WDDM GPU Process Memory counters filtered by this player PID; sum across adapters; dedicated GPU allocation, not adapter capacity or Unity CPU allocation. Counter polling runs in a separate low-rate process.'},ensure_ascii=False,indent=2),encoding='utf-8')
code=player.wait()
try: counter.wait(timeout=8)
except subprocess.TimeoutExpired: counter.terminate()
error.close()
print(f'Player exit {code}',flush=True)
sys.exit(code)
