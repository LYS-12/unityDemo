"""Snapshot the complete authored project before the forest-party upgrade."""
from pathlib import Path
import hashlib, json, zipfile

root = Path(__file__).resolve().parents[1]
destination = root / '.organize-backup/forest-party-20261022.zip'
if destination.exists():
    raise SystemExit('Existing forest-party snapshot retained.')
files = []
for directory in ('Assets', 'ProjectSettings', 'Packages', 'Tools', 'Docs'):
    files.extend(p for p in (root / directory).rglob('*') if p.is_file())
baseline = {}
with zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED, compresslevel=3) as archive:
    for path in files:
        relative = path.relative_to(root).as_posix()
        data = path.read_bytes()
        baseline[relative] = hashlib.sha256(data).hexdigest()
        archive.writestr(relative, data)
(destination.with_suffix('.json')).write_text(json.dumps(baseline, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Snapshot: {len(files)} files, {destination.stat().st_size} bytes')
