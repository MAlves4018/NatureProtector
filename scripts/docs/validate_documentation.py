#!/usr/bin/env python3
import json
import os
import re
import sys
import urllib.parse
from pathlib import Path

root = Path(__file__).resolve().parents[2]
errors = []
excluded_dirs = {'.git', '.nuget', '.testbin', 'artifacts', 'bin', 'node_modules', 'obj'}
excluded_paths = {
    ('docs', 'RepositorioDocumental'),
    ('docs', 'planning', 'V0'),
    ('docs', 'planning', 'V1'),
    ('docs', 'planning', 'V3'),
}


def is_excluded(md):
    parts = md.relative_to(root).parts
    return excluded_dirs.intersection(parts) or any(parts[:len(path)] == path for path in excluded_paths)


def iter_markdown_files():
    for current, dirnames, filenames in os.walk(root, followlinks=False):
        current_path = Path(current)
        rel_parts = current_path.relative_to(root).parts
        dirnames[:] = [
            name for name in dirnames
            if name not in excluded_dirs and not any((rel_parts + (name,))[:len(path)] == path for path in excluded_paths)
        ]
        for filename in filenames:
            if filename.endswith('.md'):
                yield current_path / filename


mds = sorted(iter_markdown_files())
pat = re.compile(r'(?<!!)\[[^\]]*\]\(([^)]+)\)|!\[[^\]]*\]\(([^)]+)\)')
for md in mds:
    txt = md.read_text(errors='ignore')
    for m in pat.finditer(txt):
        target = (m.group(1) or m.group(2)).strip().strip('<>')
        if not target or target.startswith(('http://', 'https://', 'mailto:', '#', 'data:')):
            continue
        if ' "' in target:
            target = target.split(' "', 1)[0]
        pth = urllib.parse.unquote(target.split('#', 1)[0])
        if not pth:
            continue
        if re.match(r'^[A-Za-z]:[\\/]', pth):
            errors.append(f'{md.relative_to(root)}: absolute local link: {target}')
            continue
        p = (md.parent / pth).resolve()
        if not p.exists():
            errors.append(f'{md.relative_to(root)}: missing: {target}')
# required portfolio triples
portfolio = root / 'docs/architecture/diagrams/current'
for src in (portfolio / 'src').glob('*.dot'):
    if src.name.endswith('-16x9.dot') or src.name.endswith('-a4.dot'):
        continue
    stem = src.stem
    for rel in [f'render/{stem}.svg', f'render/{stem}.png', f'render/{stem}-16x9.svg', f'sidecars/{stem}.json']:
        if not (portfolio / rel).exists():
            errors.append(f'missing diagram artifact: {rel}')
# canonical files
for rel in [
    'docs/index.md',
    'docs/documentation-manifest.yml',
    'docs/study/NatureProtector-Complete-Study-Compendium.md',
    'docs/structurizr/workspace.dsl',
]:
    if not (root / rel).exists():
        errors.append(f'missing canonical file: {rel}')
print(json.dumps({'markdown_files': len(mds), 'errors': errors, 'status': 'PASS' if not errors else 'FAIL'}, indent=2))
sys.exit(1 if errors else 0)
