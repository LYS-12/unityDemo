"""Format only authored C# changed by this upgrade; leave generated input/vendor code alone."""
from pathlib import Path
import hashlib, json, os, subprocess

root=Path(__file__).resolve().parents[1]
baseline=json.loads((root/'.organize-backup/forest-party-20261022.json').read_text(encoding='utf-8'))
paths=[]
for folder in ('Assets/_Project/Scripts','Assets/_Project/Editor'):
    for path in (root/folder).rglob('*.cs'):
        relative=path.relative_to(root).as_posix()
        if path.name=='GameControl.cs': continue
        if baseline.get(relative)!=hashlib.sha256(path.read_bytes()).hexdigest(): paths.append(relative)
environment=dict(os.environ,DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_NOLOGO='1')
command=['dotnet','format','whitespace','.', '--folder','--include',*paths,'--verbosity','minimal']
subprocess.run(command,cwd=root,env=environment,check=True)
subprocess.run(command+['--verify-no-changes'],cwd=root,env=environment,check=True)
(root/'Docs/forest-code-formatting.json').write_text(json.dumps({'formatter':'dotnet format whitespace (.NET SDK 9.0.100)','verified':True,'files':paths,'generatedInputUnchangedByFormatter':True},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Formatted and verified {len(paths)} authored C# files.')
