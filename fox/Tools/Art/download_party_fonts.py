from pathlib import Path
import urllib.request, time
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root=Path(__file__).resolve().parents[2]/'Assets/ThirdParty/ForestPartyFonts'
sources={
 'ZCOOLKuaiLe/ZCOOLKuaiLe-Regular.ttf':'https://raw.githubusercontent.com/google/fonts/main/ofl/zcoolkuaile/ZCOOLKuaiLe-Regular.ttf',
 'ZCOOLKuaiLe/OFL.txt':'https://raw.githubusercontent.com/google/fonts/main/ofl/zcoolkuaile/OFL.txt',
 'NotoSansSC/NotoSansSC-Variable.ttf':'https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf',
 'NotoSansSC/OFL.txt':'https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/OFL.txt',
}
for filename,url in sources.items():
    path=root/filename; path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists():
        request=urllib.request.Request(url,headers={'User-Agent':'ForestPartyAssetSetup/1.0'})
        for attempt in range(6):
            try:
                with urllib.request.urlopen(request,timeout=90) as response: path.write_bytes(response.read())
                break
            except OSError:
                if attempt==5: raise
                time.sleep(2)
    print(filename,path.stat().st_size)
# Unity's bundled FreeType supports a static instance more reliably than a variable TTF.
variable=root/'NotoSansSC/NotoSansSC-Variable.ttf'
static=root/'NotoSansSC/NotoSansSC-Regular.ttf'
if not static.exists():
    font=instantiateVariableFont(TTFont(variable),{'wght':400},inplace=True)
    font.save(static)
    variable.rename(root/'NotoSansSC/NotoSansSC-Variable.ttf.source~')
(root/'SOURCES.txt').write_text('\n'.join(f'{name}: {url}' for name,url in sources.items())+'\nNoto Sans SC: static weight-400 instance generated with fontTools. Font names unchanged.\n',encoding='utf-8')
