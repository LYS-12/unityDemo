"""One-time, GUID-preserving migration. Run snapshot, migrate, then verify.

Paths are constrained to this Unity project. Existing assets are never deleted.
The backup contains original text assets/settings; unchanged binaries can be moved
back using Docs/asset-migration.csv. Stop Unity before running migrate.
"""
from pathlib import Path
import csv
import hashlib
import json
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
DOCS = ROOT / 'Docs'
BACKUP = ROOT / '.organize-backup'
RENAMES = {
    'CarMo': 'CameraMove', 'LooadingUI': 'LoadingIndicator',
    'Loader': 'SceneLoader', 'LoadBack': 'LoadingSceneController',
    'Main': 'MainMenuBootstrap', 'Next': 'FoxPlacementGoal',
    'Next1': 'IngredientPlacementGoal', 'Final': 'FinalPlacementGoal',
    'ChoosePanel': 'LevelSelectPanel', 'SettingPanel': 'SettingsPanel',
    'FinalPanel': 'CompletionPanel', 'SubmissionCounter': 'OneShotContainerCounter',
    'TankBaseObj': 'CombatActor', 'MonsterObj': 'PatrolEnemy',
    'BulletObj': 'Projectile', 'WeaponObj': 'Weapon', 'JsonMgr': 'JsonDataStore',
}
COMBAT = {'TankBaseObj', 'MonsterObj', 'BulletObj', 'WeaponObj'}
PLAYER = {'Player', 'PlayerAnimator', 'CarMo', 'FoxLook'}
ITEMS = {'KitchenObject', 'KitchenObjectHolder', 'KitchenObjectSO'}
PROGRESSION = {'Next', 'Next1', 'Final', 'GameManager'}
SCENEFLOW = {'Loader', 'LoadBack', 'Main'}
THIRD = {'Quibli', 'Monsters Ultimate Pack 01 Cute Series', '像素幸存者资源包1.1'}
TEXT = {'.meta', '.cs', '.unity', '.prefab', '.asset', '.controller', '.anim',
        '.inputactions', '.json', '.txt', '.wlt', '.mat', '.rendertexture', '.lighting'}


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def decode(path):
    raw = path.read_bytes()
    for encoding in ('utf-8-sig', 'gb18030'):
        try:
            return raw.decode(encoding), encoding
        except UnicodeDecodeError:
            pass
    raise ValueError(f'Unrecognized source encoding: {path}')


def inventory():
    result = {}
    for path in sorted(ASSETS.rglob('*')):
        if not path.is_file() or path.suffix == '.meta':
            continue
        meta = Path(str(path) + '.meta')
        assert meta.exists(), f'Missing meta: {path}'
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text('utf-8'), re.M)
        assert match, f'Invalid meta: {meta}'
        guid = match[1]
        assert guid not in result, f'Duplicate GUID: {guid}'
        item = {'path': path.relative_to(ROOT).as_posix(), 'sha256': digest(path),
                'meta_sha256': digest(meta)}
        if path.suffix.lower() in TEXT:
            text, _ = decode(path)
            item['references'] = sorted(re.findall(r'guid: ([0-9a-f]{32})', text))
        result[guid] = item
    return result


def snapshot():
    assert not BACKUP.exists(), 'Baseline already exists; do not overwrite it.'
    BACKUP.mkdir()
    baseline = inventory()
    (BACKUP / 'baseline.json').write_text(json.dumps(baseline, ensure_ascii=False, indent=2), 'utf-8')
    with zipfile.ZipFile(BACKUP / 'original-text.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for directory in (ASSETS, ROOT / 'ProjectSettings', ROOT / 'Packages'):
            for path in directory.rglob('*'):
                if path.is_file() and (directory != ASSETS or path.suffix.lower() in TEXT):
                    archive.write(path, path.relative_to(ROOT).as_posix())
    print(f'Snapshot: {len(baseline)} assets; original text/settings backed up.')


def destination(old):
    p = Path(old)
    parts = p.parts[1:]
    top = parts[0]
    tail = Path(*parts[1:])
    base = Path('Assets/_Project')
    ext = p.suffix.lower()
    if top in THIRD:
        return Path('Assets/ThirdParty') / top / tail
    if top == 'Scripts':
        if parts[1:3] == ('Json', 'LitJson'):
            return Path('Assets/ThirdParty/LitJson') / Path(*parts[3:])
        stem = p.stem
        name = RENAMES.get(stem, stem) + '.cs'
        if stem in COMBAT:
            return base / 'Prototypes/Combat/Scripts' / name
        module = ('Input' if stem == 'GameInput' else 'Player' if stem in PLAYER else
                  'Items' if stem in ITEMS else 'Progression' if stem in PROGRESSION else
                  'SceneFlow' if stem in SCENEFLOW else 'Audio' if parts[1] == 'Data' else
                  'Persistence' if parts[1] == 'Json' else 'Interaction' if parts[1] == 'Counter' else 'UI')
        return base / 'Scripts' / module / name
    if top in {'GameControl.cs', 'GameControl.inputactions'}:
        return base / 'Scripts/Input' / top
    if top in {'Scenes', 'Settings'}:
        return base / top / tail
    if top == 'ScriptObject':
        return base / 'Data/Items' / tail
    if top == 'Resources':
        return base / 'Resources' / tail.with_name(RENAMES.get(p.stem, p.stem) + p.suffix)
    if top == 'Fight':
        return base / 'Prototypes/Combat/Prefabs' / tail
    if top == '_Assets':
        categories = {'Meshes': 'Art/Models', 'Materials': 'Art/Materials',
                      'Textures': 'Art/Textures', 'Animations': 'Art/Animations',
                      'Font': 'Fonts', 'PrefabsVisuals': 'Prefabs/Visuals', 'Sounds': 'Audio'}
        category = parts[1]
        rest = Path(*parts[2:])
        if category == 'Sounds':
            return base / 'Audio' / rest.parts[0] / 'KitchenAssets' / Path(*rest.parts[1:])
        if category == 'Meshes' and ext == '.mat':
            return base / 'Art/Materials/KitchenAssets' / rest
        if category == 'Meshes' and ext in {'.png', '.tif', '.exr', '.jpg'}:
            return base / 'Art/Textures/KitchenAssets' / rest
        return base / categories[category] / 'KitchenAssets' / rest
    if top == 'Prefabs':
        category = parts[1] if len(parts) > 2 else ''
        art_tail = Path(*[part for part in tail.parts if part not in {'Models', 'Materials', 'Textures', 'Prefb'}])
        if ext == '.fbx':
            return base / 'Art/Models/Environment' / art_tail
        if ext == '.mat':
            return base / 'Art/Materials/Environment' / art_tail
        if ext in {'.png', '.exr', '.jpg', '.tif'}:
            return base / 'Art/Textures/Environment' / art_tail
        if ext == '.rendertexture':
            return base / 'Art/Textures/RenderTargets' / p.name
        if ext == '.prefab':
            group = ('Counters' if category == 'Counters' else 'Items' if category == 'KitchenObjects'
                     or p.stem.lower().startswith('butterfly') else 'Characters' if p.stem.lower() == 'fox'
                     else 'Environment')
            # Keep distinct variants in source groups; do not merge duplicate-looking assets.
            rest = tail
            if category in {'Counters', 'KitchenObjects', 'Models'}:
                rest = Path(*parts[2:])
            if category == 'New Prefab':
                source = {'gross': 'Plants', 'tree': 'Trees', 'object': 'Objects', 'butterfly': 'Butterflies'}
                rest = Path('Variants') / source.get(parts[2], parts[2]) / Path(*parts[3:])
            if p.stem == 'Cheesse':
                rest = rest.with_name('Cheese.prefab')
            return base / 'Prefabs' / group / rest
        return base / 'Art/Models/Environment' / tail
    if top == 'Animations':
        return base / 'Art/Animations' / tail
    if top == '角色':
        return base / 'Art/Sprites/角色' / tail
    if top == 'UIB':
        return base / 'Art/Sprites/UI' / tail
    if top == 'Font':
        return base / 'Fonts' / tail
    if top == 'Videos':
        if ext == '.rendertexture':
            return base / 'Art/Textures/RenderTargets' / p.name
        target = ('Audio/Music' if ext in {'.mp3', '.wav'} and ('轻音乐' in p.name or '仙女棒' in p.name)
                  else 'Audio/SFX' if ext in {'.mp3', '.wav'} else 'Art/Textures/Video'
                  if ext in {'.jpg', '.png'} else 'Art/Materials/Video' if ext == '.mat' else 'Videos')
        return base / target / tail
    return p


def migrate():
    assert (BACKUP / 'baseline.json').exists(), 'Run snapshot first.'
    assert not (DOCS / 'asset-migration.csv').exists(), 'Migration already ran.'
    baseline = json.loads((BACKUP / 'baseline.json').read_text('utf-8'))
    rows = []
    destinations = set()
    for guid, item in baseline.items():
        old = Path(item['path'])
        new = destination(item['path'])
        assert new.as_posix().casefold() not in destinations, f'Path collision: {new}'
        destinations.add(new.as_posix().casefold())
        rows.append({'guid': guid, 'old_path': old.as_posix(), 'new_path': new.as_posix(),
                     'original_sha256': item['sha256']})
    DOCS.mkdir(exist_ok=True)
    with (DOCS / 'asset-migration.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    # Packs move as whole directories, including every internal folder .meta.
    for name in THIRD:
        old, new = ASSETS / name, ASSETS / 'ThirdParty' / name
        assert old.resolve().is_relative_to(ASSETS.resolve()) and new.resolve().is_relative_to(ASSETS.resolve())
        assert not new.exists()
        new.parent.mkdir(parents=True, exist_ok=True)
        old.rename(new)
        Path(str(old) + '.meta').rename(Path(str(new) + '.meta'))
    litjson = ASSETS / 'Scripts/Json/LitJson'
    newlitjson = ASSETS / 'ThirdParty/LitJson'
    litjson.rename(newlitjson)
    Path(str(litjson) + '.meta').rename(Path(str(newlitjson) + '.meta'))
    for row in rows:
        if row['old_path'] == row['new_path']:
            continue
        if Path(row['new_path']).parts[1] == 'ThirdParty':
            continue
        old, new = ROOT / row['old_path'], ROOT / row['new_path']
        assert old.resolve().is_relative_to(ASSETS.resolve()) and new.resolve().is_relative_to(ASSETS.resolve())
        assert not new.exists(), f'Destination exists: {new}'
        new.parent.mkdir(parents=True, exist_ok=True)
        old.rename(new)
        Path(str(old) + '.meta').rename(Path(str(new) + '.meta'))
    # Preserve the legacy folder GUIDs even when folders were split.
    archive = ASSETS / '_Project/LegacyFolders'
    original_folders = [p for p in ASSETS.iterdir() if p.is_dir() and p.name not in
                        {'_Project', 'ThirdParty', 'TextMesh Pro', 'TutorialInfo'}]
    for folder in original_folders:
        if any(p.is_file() and p.suffix != '.meta' for p in folder.rglob('*')):
            continue
        assert folder.resolve().is_relative_to(ASSETS.resolve())
        target = archive / folder.name
        target.parent.mkdir(parents=True, exist_ok=True)
        assert not target.exists()
        folder.rename(target)
        meta = Path(str(folder) + '.meta')
        if meta.exists():
            meta.rename(Path(str(target) + '.meta'))
    # Retain generated wrapper name, and make its source/output paths explicit.
    wrapper = ASSETS / '_Project/Scripts/Input/GameControl.inputactions.meta'
    text = wrapper.read_text('utf-8')
    text = text.replace('wrapperCodePath: ', 'wrapperCodePath: Assets/_Project/Scripts/Input/GameControl.cs')
    text = text.replace('wrapperClassName: ', 'wrapperClassName: GameControl')
    wrapper.write_text(text, 'utf-8')
    build = ROOT / 'ProjectSettings/EditorBuildSettings.asset'
    build.write_text(build.read_text('utf-8').replace('Assets/Scenes/', 'Assets/_Project/Scenes/'), 'utf-8')
    pattern = re.compile(r'\b(' + '|'.join(sorted(RENAMES, key=len, reverse=True)) + r')\b')
    encodings = []
    for script in (ASSETS / '_Project').rglob('*.cs'):
        text, encoding = decode(script)
        text = pattern.sub(lambda match: RENAMES[match[0]], text)
        text = re.sub(r'\bJsonUtlity\b', 'JsonUtility', text)
        script.write_text(text, 'utf-8', newline='\n')
        if encoding != 'utf-8-sig':
            encodings.append({'path': script.relative_to(ROOT).as_posix(), 'source_encoding': encoding})
    (DOCS / 'source-encodings.json').write_text(json.dumps(encodings, ensure_ascii=False, indent=2), 'utf-8')
    (DOCS / 'type-renames.json').write_text(json.dumps(RENAMES, ensure_ascii=False, indent=2), 'utf-8')
    print(f'Migrated {sum(r["old_path"] != r["new_path"] for r in rows)} assets with their meta files.')


def verify():
    baseline = json.loads((BACKUP / 'baseline.json').read_text('utf-8'))
    current = inventory()
    failures = []
    original_meta_guids = set()
    with zipfile.ZipFile(BACKUP / 'original-text.zip') as archive:
        for name in archive.namelist():
            if name.startswith('Assets/') and name.endswith('.meta'):
                match = re.search(r'^guid: ([0-9a-f]{32})\r?$', archive.read(name).decode('utf-8'), re.M)
                if match:
                    original_meta_guids.add(match[1])
    current_meta_guids = set()
    for meta in ASSETS.rglob('*.meta'):
        match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text('utf-8'), re.M)
        if match:
            if match[1] in current_meta_guids:
                failures.append(f'Duplicate asset/folder GUID: {meta}')
            current_meta_guids.add(match[1])
    for guid in original_meta_guids - current_meta_guids:
        failures.append(f'Lost asset/folder GUID: {guid}')
    allowed_changes = {'.cs', '.unity', '.inputactions', '.prefab'}
    for guid, old in baseline.items():
        new = current.get(guid)
        if not new:
            failures.append(f'Lost GUID {guid}: {old["path"]}')
            continue
        if Path(old['path']).suffix.lower() not in allowed_changes and old['sha256'] != new['sha256']:
            failures.append(f'Unexpected content change: {new["path"]}')
        if 'references' in old:
            # New script references are allowed; original reference counts must remain intact.
            from collections import Counter
            removed = Counter(old['references']) - Counter(new['references'])
            if removed:
                failures.append(f'Removed references: {new["path"]}: {dict(removed)}')
    result = {'original_assets': len(baseline), 'current_assets': len(current),
              'retained_original_guids': len(set(baseline) & set(current)),
              'original_asset_and_folder_guids': len(original_meta_guids),
              'retained_original_asset_and_folder_guids': len(original_meta_guids & current_meta_guids),
              'failures': failures}
    (DOCS / 'static-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if failures:
        sys.exit(1)


if __name__ == '__main__':
    {'snapshot': snapshot, 'migrate': migrate, 'verify': verify}[sys.argv[1]]()
