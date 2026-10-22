"""Read-only asset/GUID audit against the local pre-feature backup.

Run after Unity has imported the project and saved generated metadata.
The JSON output complements Unity reference validation and runtime tests.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GUID = re.compile(rb'^guid: ([0-9a-f]{32})\r?$', re.M)
REFERENCE = re.compile(rb'guid: ([0-9a-f]{32})')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    failures, changed, current = [], [], {}
    forest_upgrade = (ROOT / '.organize-backup/forest-party-20261022.json').exists()
    forest_reference_changes = {
        'Assets/_Project/Scenes/StartScene.unity',
        *(f'Assets/_Project/Scenes/GameScene{i}.unity' for i in range(1, 4)),
        *(f'Assets/_Project/Resources/UI/{name}.prefab' for name in ('StartPanel','SettingsPanel','LevelSelectPanel','CompletionPanel','OrderPanel1','OrderPanel2','OrderPanel3')),
        'Assets/_Project/Prefabs/Gameplay/GameplayHUD.prefab',
        'Assets/_Project/Prefabs/Enemies/RockEnemy.prefab',
    } if forest_upgrade else set()
    for meta in (ROOT / 'Assets').rglob('*.meta'):
        match = GUID.search(meta.read_bytes())
        if not match:
            failures.append('Invalid metadata: ' + str(meta.relative_to(ROOT)))
            continue
        guid = match[1].decode()
        if guid in current:
            failures.append('Duplicate GUID: ' + guid)
        current[guid] = meta
    for asset in (ROOT / 'Assets').rglob('*'):
        if asset.is_file() and not asset.name.endswith('~') and asset.suffix != '.meta' and not Path(str(asset) + '.meta').exists():
            failures.append('Missing metadata: ' + str(asset.relative_to(ROOT)))

    retained = 0
    with zipfile.ZipFile(ROOT / '.organize-backup/inventory-20261020.zip') as backup:
        for name in backup.namelist():
            original = backup.read(name)
            path = ROOT / name
            if name.endswith('.meta'):
                match = GUID.search(original)
                if match and match[1].decode() in current:
                    retained += 1
                elif match:
                    failures.append('Lost pre-feature GUID: ' + name)
            if not path.is_file():
                failures.append('Lost pre-feature file: ' + name)
                continue
            data = path.read_bytes()
            if sha(original) != sha(data):
                changed.append(name)
                allowed = (name.startswith('Assets/_Project/Scripts/') or
                           name.startswith('Assets/_Project/Editor/') or
                           name.startswith('Assets/_Project/Data/Items/') or
                           name.startswith('Assets/_Project/Prefabs/Items/') or
                           name.startswith('Assets/_Project/Scenes/GameScene') or
                           name in ('Assets/_Project/Prefabs/Characters/Variants/Objects/fox.prefab',
                                    'ProjectSettings/TagManager.asset', 'ProjectSettings/DynamicsManager.asset') or
                           forest_upgrade and (name in forest_reference_changes or name=='ProjectSettings/ProjectSettings.asset'))
                if not allowed:
                    failures.append('Unexpected change from pre-feature backup: ' + name)
            if name.startswith('Assets/') and Path(name).suffix in ('.prefab', '.unity'):
                removed = Counter(REFERENCE.findall(original)) - Counter(REFERENCE.findall(data))
                if removed and name not in forest_reference_changes:
                    failures.append('Removed pre-feature references: ' + name + ' ' + str(removed))

    # The original migration baseline also covers binary media and all third-party files.
    baseline = json.loads((ROOT / '.organize-backup/baseline.json').read_text('utf-8'))
    original_meta_guids = set()
    with zipfile.ZipFile(ROOT / '.organize-backup/original-text.zip') as backup:
        for name in backup.namelist():
            if name.startswith('Assets/') and name.endswith('.meta'):
                match = GUID.search(backup.read(name))
                if match:
                    original_meta_guids.add(match[1].decode())
    for guid in original_meta_guids - set(current):
        failures.append('Lost original asset/folder GUID: ' + guid)
    media_checked = 0
    for guid, original in baseline.items():
        meta = current.get(guid)
        if meta is None:
            failures.append('Lost original GUID: ' + guid)
            continue
        asset = Path(str(meta)[:-5])
        is_media = asset.suffix.lower() in {'.fbx', '.png', '.jpg', '.jpeg', '.tga', '.wav', '.mp3', '.mp4', '.ttf', '.otf', '.mat', '.controller', '.anim'}
        is_vendor = '/ThirdParty/' in asset.as_posix()
        if is_media or is_vendor:
            media_checked += 1
            if sha(asset.read_bytes()) != original['sha256']:
                failures.append('Changed original media/vendor content: ' + str(asset.relative_to(ROOT)))
    result = dict(retained_pre_feature_guids=retained, original_assets=len(baseline),
                  retained_original_asset_guids=len(set(baseline) & set(current)),
                  original_asset_and_folder_guids=len(original_meta_guids),
                  retained_original_asset_and_folder_guids=len(original_meta_guids & set(current)),
                  unchanged_original_media_or_vendor_files=media_checked,
                  intentional_changed_files=changed, errors=failures)
    (ROOT / 'Docs/gameplay-asset-validation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({key: value for key, value in result.items() if key != 'intentional_changed_files'}, ensure_ascii=False, indent=2))
    raise SystemExit(bool(failures))


if __name__ == '__main__':
    main()
