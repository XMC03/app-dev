"""Validate PNGs against the Unity atlas contract, without claiming Unity rendering."""
from pathlib import Path
import json
import hashlib
from PIL import Image

root = Path(__file__).resolve().parents[2]
art = root / 'unity/SlimeAscent/Assets/Resources/Art'
manifest = json.loads((art / 'art-manifest.json').read_text())
count = 0
atlases = {}
for name, (cols, rows) in manifest['atlases'].items():
    image = Image.open(art / f'{name}.png')
    assert image.mode == 'RGBA', f'{name}: lost transparency'
    assert image.size == (cols*64, rows*64), f'{name}: wrong cell dimensions'
    for row in range(rows):
        for col in range(cols):
            alpha = image.getchannel('A').crop((col*64, row*64, (col+1)*64, (row+1)*64))
            assert alpha.getbbox(), f'{name}: empty cell {col}, {row}'
            count += 1
    metadata = (art / f'{name}.png.meta').read_text()
    for required in ['filterMode: 0', 'enableMipMap: 0', 'nPOTScale: 0', 'textureCompression: 0', 'alphaIsTransparency: 1']:
        assert required in metadata, f'{name}: importer can corrupt atlas ({required})'
    atlases[name] = image

def fingerprint(image, col, row):
    return hashlib.sha256(image.crop((col*64,row*64,(col+1)*64,(row+1)*64)).tobytes()).hexdigest()

slime = atlases['slime']
assert len({fingerprint(slime,0,form*4+2) for form in range(4)}) == 4, 'Evolution forms look identical'
for form in range(4):
    assert len({fingerprint(slime,0,form*4+direction) for direction in range(4)}) == 4, 'Missing distinct slime directions'
    for direction in range(4):
        for action in range(6):
            assert len({fingerprint(slime,action*4+frame,form*4+direction) for frame in range(4)}) >= 2, 'Static action animation'
enemies = atlases['enemies']
assert len({fingerprint(enemies,0,species*4+2) for species in range(7)}) == 7, 'Enemy silhouettes are identical'
for species in range(7):
    assert len({fingerprint(enemies,0,species*4+direction) for direction in range(4)}) == 4, 'Enemy facing cues are missing'
for name in ['title', 'ending']:
    image = Image.open(art / f'{name}.png')
    image.verify()
    image = Image.open(art / f'{name}.png')
    assert abs(image.width/image.height-16/9) < .04, 'Illustration aspect ratio is unsuitable'
    assert image.width >= 1280, 'Illustration is too small for the game'
# The Guardian's tell is round in top-down space, matching its circular damage area.
alpha = atlases['effects'].getchannel('A').crop((3*64,3*64,4*64,4*64))
box = alpha.getbbox()
assert abs((box[2]-box[0])-(box[3]-box[1])) <= 2, 'Guardian tell is flattened'
layout = json.loads((art / 'humans-layout.json').read_text())
humans = Image.open(art / 'humans.png')
assert humans.mode == 'RGBA' and humans.size == (layout['width'], layout['height']), 'Human atlas contract changed'
assert layout['classes'] == ['Warrior', 'Cleric', 'Ranger', 'Healer', 'Rogue'], 'Human role order differs from the game enum'
assert layout['columns'][0] == layout['rowEdges'][0] == 0
assert layout['columns'][-1] == humans.width and layout['rowEdges'][-1] == humans.height
assert all(a < b for a, b in zip(layout['columns'], layout['columns'][1:]))
assert all(a < b for a, b in zip(layout['rowEdges'], layout['rowEdges'][1:]))
for role in range(5):
    frames = set()
    for pose in range(6):
        left, right = layout['columns'][pose:pose+2]
        adjustment = next((a for a in layout.get('adjustments', []) if a['role'] == role and a['pose'] == pose), None)
        if adjustment:
            left, right = adjustment['left'], adjustment['right']
        top, bottom = layout['rowEdges'][role:role+2]
        if adjustment and 'bands' in adjustment:
            for band in adjustment['bands']:
                assert left <= band['left'] < band['right'] <= right
                assert top <= band['top'] < band['bottom'] <= bottom
        assert 0 <= left < right <= humans.width and 0 <= top < bottom <= humans.height
        assert left < layout['bodyCenters'][pose] < right and top < layout['feet'][role] < bottom, 'Invalid human anchor'
        frame = humans.crop((left, top, right, bottom))
        alpha = frame.getchannel('A')
        histogram = alpha.histogram()
        assert histogram[0] > frame.width*frame.height*.08, 'Human frame has an opaque background'
        assert sum(histogram[128:]) > 1500, 'Human frame is empty or only a ghost'
        frames.add(hashlib.sha256(frame.tobytes()).hexdigest())
    assert len(frames) >= 5, 'Human action poses are duplicated'
meta = (art / 'humans.png.meta').read_text()
for required in ['filterMode: 0', 'enableMipMap: 0', 'nPOTScale: 0', 'textureCompression: 0', 'alphaIsTransparency: 1']:
    assert required in meta, f'Human atlas importer is unsafe: {required}'
print(f'PASS {count} nonempty atlas cells, four evolution forms, six actions/four directions, seven distinct enemies, two 16:9 illustrations, and pixel-safe import settings.')
print('PASS 30 transparent human poses, five classes, explicit frame boundaries and valid foot anchors.')
print('These are asset/data checks; Unity Play mode has not been run.')
