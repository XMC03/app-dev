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
print(f'PASS {count} nonempty atlas cells, four evolution forms, six actions/four directions, seven distinct enemies, two 16:9 illustrations, and pixel-safe import settings.')
print('These are asset/data checks; Unity Play mode has not been run.')
