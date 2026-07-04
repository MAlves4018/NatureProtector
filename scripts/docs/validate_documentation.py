#!/usr/bin/env python3
from pathlib import Path
import re, sys, json, urllib.parse
root=Path(__file__).resolve().parents[2]
errors=[]
excluded_dirs={'.git','.nuget','.testbin','bin','node_modules','obj'}
mds=[md for md in root.rglob('*.md') if not excluded_dirs.intersection(md.relative_to(root).parts)]
pat=re.compile(r'(?<!!)\[[^\]]*\]\(([^)]+)\)|!\[[^\]]*\]\(([^)]+)\)')
for md in mds:
    txt=md.read_text(errors='ignore')
    for m in pat.finditer(txt):
        target=(m.group(1) or m.group(2)).strip().strip('<>')
        if not target or target.startswith(('http://','https://','mailto:','#','data:')): continue
        if ' "' in target: target=target.split(' "',1)[0]
        pth=urllib.parse.unquote(target.split('#',1)[0])
        if not pth: continue
        if re.match(r'^[A-Za-z]:[\\/]',pth):
            errors.append(f'{md.relative_to(root)}: absolute local link: {target}')
            continue
        p=(md.parent/pth).resolve()
        if not p.exists(): errors.append(f'{md.relative_to(root)}: missing: {target}')
# required portfolio triples
portfolio=root/'docs/architecture/diagrams/current'
for src in (portfolio/'src').glob('*.dot'):
    if src.name.endswith('-16x9.dot') or src.name.endswith('-a4.dot'): continue
    stem=src.stem
    for rel in [f'render/{stem}.svg',f'render/{stem}.png',f'render/{stem}-16x9.svg',f'sidecars/{stem}.json']:
        if not (portfolio/rel).exists(): errors.append(f'missing diagram artifact: {rel}')
# canonical files
for rel in ['docs/index.md','docs/documentation-manifest.yml','docs/study/NatureProtector-Complete-Study-Compendium.md','docs/structurizr/workspace.dsl']:
    if not (root/rel).exists(): errors.append(f'missing canonical file: {rel}')
print(json.dumps({'markdown_files':len(mds),'errors':errors,'status':'PASS' if not errors else 'FAIL'},indent=2))
sys.exit(1 if errors else 0)
