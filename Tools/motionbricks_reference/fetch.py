"""Fetch only pinned inference assets. No datasets, deployment stack, or training."""
import argparse
import hashlib
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[2]
SOURCE = '7f151314d4d1606544bf249d2a7a1cb754c64582'
WEIGHTS = '6733128a3d8a523b1418b06bca3cdf61c8b0987f'

def fetch(url, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        temp = path.with_suffix(path.suffix + '.partial')
        urllib.request.urlretrieve(url, temp)
        temp.replace(path)
    with path.open('rb') as f:
        if f.read(100).startswith(b'version https://git-lfs'):
            raise ValueError(f'LFS pointer, not weights: {path}')
    return dict(url=url, path=path.relative_to(ROOT).as_posix(), size=path.stat().st_size,
                sha256=hashlib.file_digest(path.open('rb'), 'sha256').hexdigest())

def main():
    p = argparse.ArgumentParser(); p.add_argument('--backbone', action='store_true'); args = p.parse_args()
    artifacts = []
    hf=json.load(urllib.request.urlopen(f'https://huggingface.co/api/models/nvidia/GEAR-SONIC/revision/{WEIGHTS}?blobs=true'))
    planner_info=next(x['lfs'] for x in hf['siblings'] if x['rfilename']=='planner_sonic.onnx')
    for name in ('planner_sonic.onnx', 'LICENSE'):
        path = ROOT / ('Models' if name.endswith('.onnx') else 'External') / name
        entry=fetch(f'https://huggingface.co/nvidia/GEAR-SONIC/resolve/{WEIGHTS}/{name}', path)
        if name.endswith('.onnx'):
            if entry['sha256']!=planner_info['sha256'] or entry['size']!=planner_info['size']:raise ValueError('Planner publisher hash/size mismatch')
            entry['publisherSha256']=planner_info['sha256']
        artifacts.append(entry)
    tree = json.load(urllib.request.urlopen(f'https://api.github.com/repos/NVlabs/GR00T-WholeBodyControl/git/trees/{SOURCE}?recursive=1'))['tree']
    for item in tree:
        name = item['path']
        minimal = name in ('LICENSE', 'motionbricks/assets/skeletons/g1/g1_29dof.xml','motionbricks/assets/skeletons/g1/g1.xml')
        backbone = args.backbone and name.startswith('motionbricks/') and (
            name.endswith(('.py', '.yaml', '.npy', '.p')) or name.startswith('motionbricks/out/') and name.endswith('.ckpt'))
        if item['type'] != 'blob' or not (minimal or backbone): continue
        host = 'media.githubusercontent.com/media' if name.endswith('.ckpt') else 'raw.githubusercontent.com'
        entry=fetch(f'https://{host}/NVlabs/GR00T-WholeBodyControl/{SOURCE}/{name}', ROOT / 'External' / name)
        if name.endswith('.ckpt'):
            pointer=urllib.request.urlopen(f'https://raw.githubusercontent.com/NVlabs/GR00T-WholeBodyControl/{SOURCE}/{name}').read().decode()
            expected=next(line.split('sha256:')[1] for line in pointer.splitlines() if line.startswith('oid '))
            size=int(next(line.split()[1] for line in pointer.splitlines() if line.startswith('size ')))
            if entry['sha256']!=expected or entry['size']!=size:raise ValueError(f'Checkpoint publisher hash/size mismatch: {name}')
            entry['publisherSha256']=expected
        artifacts.append(entry)
    manifest = dict(schemaVersion=1, sourceRevision=SOURCE, artifactRevision=WEIGHTS,
                    selected='GEAR-SONIC G1 kinematic planner (not the MotionBricks Python backbone)',
                    codeLicense='Apache-2.0', weightsLicense='NVIDIA Open Model License', artifacts=artifacts)
    (ROOT / 'model-manifest.json').write_text(json.dumps(manifest, indent=2))
    print(f'Verified {len(artifacts)} pinned artifacts')

if __name__ == '__main__': main()
