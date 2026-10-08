"""Structural checks only; does not import or run Unity."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parent / 'SlimeAscent'
assets = root / 'Assets'
guids = {}
for asset in assets.rglob('*'):
    if asset.suffix == '.meta':
        continue
    meta = Path(str(asset) + '.meta')
    assert meta.is_file(), f'Missing metadata for {asset}'
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    assert match, f'Invalid GUID in {meta}'
    guid = match.group(1)
    assert guid not in guids, f'Duplicate GUID {guid}'
    guids[guid] = asset
scenes = ['Opening', 'Lower', 'Middle', 'Upper', 'HumanWorld']
build = (root / 'ProjectSettings/EditorBuildSettings.asset').read_text()
paths = re.findall(r'path: (.+)', build)
assert paths == [f'Assets/Scenes/{name}.unity' for name in scenes], 'Wrong scene list or order'
for name in scenes:
    scene = assets / 'Scenes' / f'{name}.unity'
    script_guid = re.search(r'm_Script:.*guid: ([0-9a-f]{32})', scene.read_text()).group(1)
    assert guids[script_guid] == assets / 'Scripts/SlimeGame.cs', f'Wrong startup script in {scene}'
for guid in re.findall(r'guid: ([0-9a-f]{32})', build):
    assert guids[guid].suffix == '.unity', 'Build GUID does not identify a scene'
manifest = json.loads((root / 'Packages/manifest.json').read_text())
for module in ['audio', 'imgui', 'jsonserialize']:
    assert manifest['dependencies'][f'com.unity.modules.{module}'] == '1.0.0'
assert (root / 'ProjectSettings/ProjectVersion.txt').read_text().strip() == 'm_EditorVersion: 6000.6.4f1'
settings = (root / 'ProjectSettings/ProjectSettings.asset').read_text()
assert 'defaultScreenWidth: 1280' in settings and 'defaultScreenHeight: 720' in settings
assert 'activeInputHandler: 0' in settings
runtime = (assets / 'Scripts/SlimeGame.cs').read_text()
assert 'DontDestroyOnLoad' in runtime and 'SceneManager.sceneLoaded-=SceneLoaded' in runtime
assert 'if(loading)return;' in runtime, 'Actor rendering must wait for the new floor scene'
assert 'FindAnyObjectByType' in runtime
print(f'PASS: {len(guids)} unique asset GUIDs; five startup scenes and build order; Unity 6.6; 16:9; input/audio/save modules; scene-transition guard.')
print('Unity Editor import, Play mode, rendered scenes, audio playback and standalone builds remain unverified.')
