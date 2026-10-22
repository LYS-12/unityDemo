"""Archive only named generated intermediates and write a reviewable delivery manifest."""
from pathlib import Path
import hashlib, json, re, shutil

root=Path(__file__).resolve().parents[1]
baseline=json.loads((root/'.organize-backup/forest-party-20261022.json').read_text(encoding='utf-8'))
def checked(relative):
    path=(root/relative).resolve()
    if not path.is_relative_to(root): raise ValueError('Path leaves workspace: '+str(path))
    return path

for name in ('forest-intro','forest-reunion','ghost-animation'):
    source=checked('Docs/Videos/'+name+'-silent.mp4')
    destination=checked('TempCapture/Encoding/'+source.name)
    if source.exists(): destination.parent.mkdir(parents=True,exist_ok=True); source.replace(destination)
for name in ('ForestGhost.blend1','ForestPartyTitle.blend1'):
    source=checked('SourceArt/Ghost/'+name); destination=checked('TempCapture/BlenderBackups/'+name)
    if source.exists(): destination.parent.mkdir(parents=True,exist_ok=True); source.replace(destination)
source=checked('Docs/Images/movie-check.png')
if source.exists():
    destination=checked('TempCapture/OldPreview/movie-check.png'); destination.parent.mkdir(parents=True,exist_ok=True); source.replace(destination)
showcase=checked('TempCapture/MediaCheck/ghost-movement.png')
if showcase.exists(): shutil.copy2(showcase,checked('Docs/Images/ghost-motion-showcase.png'))

record=checked('Docs/forest-recording-result.txt')
if record.exists():
    lines=record.read_text(encoding='utf-8').splitlines(); latest={}
    for line in lines:
        if line.startswith('Docs/Videos/'): latest[line.split(':',1)[0]]=line
    record.write_text('\n'.join([lines[0],*latest.values(),'Ghost demonstration uses temporary scenery hiding and a presentation floor; authored scene assets retain the forest.'])+'\n',encoding='utf-8')

changed=[]; created=[]
for folder in ('Assets','ProjectSettings','Packages'):
    for path in (root/folder).rglob('*'):
        if not path.is_file() or path.suffix=='.meta' or path.name.endswith('~'): continue
        name=path.relative_to(root).as_posix(); checksum=hashlib.sha256(path.read_bytes()).hexdigest()
        if baseline.get(name)==checksum: continue
        item={'path':name,'sha256':checksum}
        meta=Path(str(path)+'.meta')
        if meta.exists():
            match=re.search(r'^guid: ([0-9a-f]{32})',meta.read_text(encoding='utf-8'),re.M)
            if match: item['guid']=match[1]
        (changed if name in baseline else created).append(item)
artifacts=[]
for folder in ('SourceArt/Ghost','Docs/Videos'):
    for path in (root/folder).glob('*'):
        if path.is_file(): artifacts.append({'path':path.relative_to(root).as_posix(),'bytes':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
result={'unity':'2022.3.62f3c1','changedOriginalFiles':changed,'newAssets':created,'sourceAndRecordings':artifacts,'buildDirectory':'Builds/WindowsDevelopment','reports':['Docs/森林派对操作与参数.md','Docs/森林派对架构与审查.md','Docs/森林派对验证结果.md','Docs/forest-test-results.xml','Docs/unity-reference-validation.json','Docs/windows-build-result.txt']}
checked('Docs/forest-delivery-manifest.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Delivery manifest: {len(changed)} authorized existing-file changes, {len(created)} new assets, {len(artifacts)} source/media artifacts.')
