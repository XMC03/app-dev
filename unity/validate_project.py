"""Check serialized references without claiming to import the project in Unity."""
import json
import re
from pathlib import Path

project = Path(__file__).resolve().parent / 'Nightfall'
assets = project / 'Assets'
guids = {}
for asset in assets.rglob('*'):
    if asset.suffix == '.meta':
        continue
    meta = Path(str(asset) + '.meta')
    assert meta.is_file(), f'Missing metadata: {asset}'
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(), re.M)
    assert match, f'Invalid GUID: {meta}'
    guid = match.group(1)
    assert guid not in guids, f'Duplicate GUID: {meta}'
    guids[guid] = asset
scene = assets / 'Scenes' / 'Nightfall.unity'
scene_text = scene.read_text()
script_guid = re.search(r'm_Script:.*guid: ([0-9a-f]{32})', scene_text).group(1)
assert guids[script_guid] == assets / 'Scripts' / 'NightfallGame.cs', 'Scene references the wrong script'
build = (project / 'ProjectSettings' / 'EditorBuildSettings.asset').read_text()
scene_guid = re.search(r'guid: ([0-9a-f]{32})', build).group(1)
assert guids[scene_guid] == scene, 'Build references the wrong scene'
assert 'path: Assets/Scenes/Nightfall.unity' in build
assert 'activeInputHandler: 0' in (project / 'ProjectSettings' / 'ProjectSettings.asset').read_text()
manifest = json.loads((project / 'Packages' / 'manifest.json').read_text())
assert manifest['dependencies']['com.unity.modules.audio'] == '1.0.0'
assert manifest['dependencies']['com.unity.modules.imgui'] == '1.0.0'
assert (project / 'ProjectSettings' / 'ProjectVersion.txt').read_text().startswith('m_EditorVersion: 6000.0.')
print(f'PASS Unity project structure, {len(guids)} metadata GUIDs, scene script, build scene, input and package declarations.')
print('Unity Editor import, Play mode, rendering, audio and executable builds are not validated by this check.')
