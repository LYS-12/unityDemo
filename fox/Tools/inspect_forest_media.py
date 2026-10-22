"""Inspect delivered recordings and original synthesized assets without changing them."""
from pathlib import Path
import json, math, re, struct, subprocess, sys, wave
sys.path.insert(0,str(Path(__file__).resolve().parent/'.media'))
import imageio_ffmpeg

root=Path(__file__).resolve().parents[1]
encoder=imageio_ffmpeg.get_ffmpeg_exe()
result={'recordings':[],'synthesizedEffects':[],'errors':[]}
for movie in sorted((root/'Docs/Videos').glob('*.mp4')):
    if movie.stem.endswith('-silent'): continue
    output=subprocess.run([encoder,'-i',str(movie)],capture_output=True,text=True,encoding='utf-8',errors='replace').stderr
    duration=re.search(r'Duration: ([\d:.]+)',output)
    size=re.search(r'Video:.*? (\d{3,4})x(\d{3,4})',output)
    data={'path':movie.relative_to(root).as_posix(),'bytes':movie.stat().st_size,'duration':duration[1] if duration else None,'dimensions':list(size.groups()) if size else None,'hasAudio':'Audio: aac' in output}
    if not data['hasAudio'] or not size: result['errors'].append('Invalid recording: '+movie.name)
    wav=movie.with_suffix('.wav')
    if wav.exists():
        with wave.open(str(wav),'rb') as source:
            values=[v[0]/32768 for v in struct.iter_unpack('<h',source.readframes(source.getnframes()))]
            data['audio']={'channels':source.getnchannels(),'sampleRate':source.getframerate(),'seconds':source.getnframes()/source.getframerate(),'peak':max(map(abs,values),default=0),'rms':math.sqrt(sum(v*v for v in values)/max(1,len(values))),'clippedSamples':sum(abs(v)>=.9999 for v in values)}
            if data['audio']['peak']==0: result['errors'].append('Silent capture: '+wav.name)
    result['recordings'].append(data)
for sound in sorted((root/'Assets/_Project/Audio/SFX/ForestParty').glob('*.wav')):
    with wave.open(str(sound),'rb') as source:
        values=[v[0]/32768 for v in struct.iter_unpack('<h',source.readframes(source.getnframes()))]
        result['synthesizedEffects'].append({'name':sound.stem,'seconds':source.getnframes()/source.getframerate(),'peak':max(map(abs,values),default=0)})
(root/'Docs/forest-media-validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'recordings':result['recordings'],'effects':len(result['synthesizedEffects']),'errors':result['errors']},ensure_ascii=False,indent=2))
raise SystemExit(bool(result['errors']))
