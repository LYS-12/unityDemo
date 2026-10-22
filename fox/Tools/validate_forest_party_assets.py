"""Audit the explicitly approved upgrade against its complete pre-change snapshot."""
from pathlib import Path
import hashlib, json, re, zipfile

root=Path(__file__).resolve().parents[1]
baseline=json.loads((root/'.organize-backup/forest-party-20261022.json').read_text(encoding='utf-8'))
allowed_prefixes=('Assets/_Project/Scripts/','Assets/_Project/Editor/','Assets/_Project/Resources/UI/')
allowed_files={
 'Assets/_Project/Scenes/StartScene.unity',
 *(f'Assets/_Project/Scenes/GameScene{i}.unity' for i in range(1,4)),
 'Assets/_Project/Prefabs/Gameplay/GameplayHUD.prefab',
 'Assets/_Project/Prefabs/Enemies/RockEnemy.prefab',
 'Assets/_Project/Data/Navigation/GameScene1Combat.asset',
 'ProjectSettings/TagManager.asset', 'ProjectSettings/ProjectSettings.asset',
}
errors=[]; changed=[]; guid_count=0; protected=0
with zipfile.ZipFile(root/'.organize-backup/forest-party-20261022.zip') as snapshot:
    for name,checksum in baseline.items():
        if not name.startswith(('Assets/','ProjectSettings/','Packages/')): continue
        path=root/name
        if not path.exists(): errors.append('Missing original file: '+name); continue
        data=path.read_bytes()
        if name.endswith('.meta'):
            before=re.search(rb'^guid: ([0-9a-f]{32})',snapshot.read(name),re.M)
            after=re.search(rb'^guid: ([0-9a-f]{32})',data,re.M)
            if not before or not after or before[1]!=after[1]: errors.append('GUID changed: '+name)
            guid_count+=1
        if hashlib.sha256(data).hexdigest()==checksum: protected+=1; continue
        changed.append(name)
        if name not in allowed_files and not name.startswith(allowed_prefixes): errors.append('Unexpected original-file change: '+name)

# A literal missing nested prefab is never treated as an authorized quest change.
known=json.loads((root/'Docs/known-missing-assets.json').read_text(encoding='utf-8'))
meta_guids=set()
for meta in (root/'Assets').rglob('*.meta'):
    match=re.search(r'^guid: ([0-9a-f]{32})',meta.read_text(encoding='utf-8',errors='replace'),re.M)
    if match: meta_guids.add(match[1])
known_pairs={(asset['path'],asset['guid']) for asset in known['assets']}
for prefab in (root/'Assets/_Project').rglob('*.prefab'):
    name=prefab.relative_to(root).as_posix()
    for guid in re.findall(r'm_SourcePrefab: \{fileID: [-\d]+, guid: ([0-9a-f]{32})',prefab.read_text(encoding='utf-8')):
        if guid not in meta_guids and (name,guid) not in known_pairs: errors.append('New missing nested prefab: '+name+' '+guid)
result={'errors':errors,'preservedOriginalGuids':guid_count,'unchangedOriginalFiles':protected,'authorizedChangedFiles':changed,'knownMissingNestedPrefabs':len(known_pairs)}
(root/'Docs/forest-asset-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({key:value for key,value in result.items() if key!='authorizedChangedFiles'},ensure_ascii=False,indent=2))
raise SystemExit(bool(errors))
