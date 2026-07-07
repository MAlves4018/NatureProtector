#!/usr/bin/env python3
"""Build the curated offline documentation portal.

Requires: mistune and jinja2. The static documentation validator remains the
CI authority when these optional presentation dependencies are unavailable.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
from pathlib import Path

try:
    import mistune
    from jinja2 import Template
except ImportError as exc:
    raise SystemExit("Install optional dependencies 'mistune' and 'jinja2' to build the portal") from exc

PAGES = [
    ("Início", "docs/index.md"),
    ("Estado do projeto", "docs/current-state/project-state.md"),
    ("Arquitetura e runtime", "docs/current-state/architecture-and-runtime.md"),
    ("Operations Control Plane", "docs/current-state/operations-control-plane.md"),
    ("Roles e UI", "docs/current-state/roles-capabilities-and-ui.md"),
    ("Qualidade e evidence", "docs/current-state/quality-evidence-and-testing.md"),
    ("Cloud e deployment", "docs/current-state/cloud-and-deployment.md"),
    ("Dados e fronteiras científicas", "docs/current-state/data-risk-and-scientific-boundaries.md"),
    ("Limitações e gates", "docs/current-state/limitations-and-open-gates.md"),
    ("Primeira run guiada", "docs/tutorials/first-guided-run.md"),
    ("Executar quality e ler evidence", "docs/how-to/run-quality-and-read-evidence.md"),
    ("Rever deployment de staging", "docs/how-to/review-a-staging-deployment.md"),
    ("Referência de roles", "docs/reference/roles-and-capabilities.md"),
    ("Catálogo de operações", "docs/reference/operation-catalog.md"),
    ("Evidence e autoridade", "docs/explanation/evidence-and-authority.md"),
    ("Portfolio de diagramas", "docs/architecture/diagrams/current/README.md"),
    ("Compêndio de estudo", "docs/study/NatureProtector-Complete-Study-Compendium.md"),
    ("Referência rápida da defesa", "docs/presentation/NatureProtector-Defence-Quick-Reference.md"),
    ("Delta do relatório", "docs/report/current-state-integration/report-delta.md"),
]

TEMPLATE = Template(r'''<!doctype html><html lang="pt-PT"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{title}} - NatureProtector</title><link rel="stylesheet" href="assets/site.css"></head><body><header><a href="index.html"><strong>Documentação NatureProtector</strong></a><input id="search" placeholder="Pesquisar documentação atual" aria-label="Pesquisar"></header><div class="layout"><nav>{% for label,href in nav %}<a href="{{href}}" {% if href==current %}class="active"{% endif %}>{{label}}</a>{% endfor %}</nav><main><div class="status">SNAPSHOT ATUAL · 2026-06-28 · implementação e prova mantidas separadas</div>{{content}}</main></div><script src="assets/search.js"></script></body></html>''')

CSS = r''':root{--ink:#142033;--nav:#0f3b5f;--accent:#0ea5e9;--soft:#f0f9ff;--line:#cbd5e1}*{box-sizing:border-box}body{margin:0;font:16px/1.55 system-ui,-apple-system,Segoe UI,sans-serif;color:var(--ink);background:#f8fafc}header{height:64px;display:flex;align-items:center;gap:32px;padding:0 24px;background:var(--nav);color:white;position:sticky;top:0;z-index:2}header a{color:white;text-decoration:none}header input{margin-left:auto;width:min(440px,45vw);padding:9px 12px;border-radius:8px;border:0}.layout{display:grid;grid-template-columns:270px minmax(0,1fr);max-width:1500px;margin:auto}nav{padding:22px 14px;position:sticky;top:64px;height:calc(100vh - 64px);overflow:auto;background:white;border-right:1px solid var(--line)}nav a{display:block;padding:8px 10px;border-radius:7px;color:#334155;text-decoration:none;margin:2px 0}nav a:hover,nav a.active{background:#e0f2fe;color:#075985}main{background:white;max-width:980px;width:100%;padding:36px 48px 80px;min-height:100vh}.status{font-size:12px;letter-spacing:.04em;color:#0369a1;background:var(--soft);border:1px solid #bae6fd;padding:8px 10px;border-radius:8px}h1,h2,h3{color:var(--nav);scroll-margin-top:80px}h1{font-size:2.2rem;border-bottom:2px solid #7dd3fc;padding-bottom:8px}h2{margin-top:2rem}a{color:#0369a1}table{width:100%;border-collapse:collapse;font-size:.94rem}th,td{border:1px solid var(--line);padding:8px;text-align:left;vertical-align:top}th{background:#e0f2fe}pre{background:#0f172a;color:#e2e8f0;padding:14px;border-radius:8px;overflow:auto}code{background:#f1f5f9;padding:1px 4px;border-radius:4px}pre code{background:transparent;padding:0}img{display:block;max-width:100%;margin:20px auto;border:1px solid #e2e8f0}.result{padding:8px;border-bottom:1px solid var(--line)}@media(max-width:800px){.layout{display:block}nav{position:static;height:auto;border-right:0;border-bottom:1px solid var(--line)}main{padding:24px 18px}header input{width:45vw}}'''

SEARCH_JS = r'''const input=document.querySelector('#search');let box;fetch('assets/search-index.json').then(r=>r.json()).then(index=>{input.addEventListener('input',()=>{const q=input.value.trim().toLowerCase();if(box)box.remove();if(q.length<2)return;box=document.createElement('div');box.style='position:fixed;right:24px;top:58px;width:min(520px,90vw);max-height:70vh;overflow:auto;background:white;border:1px solid #cbd5e1;box-shadow:0 12px 30px #0003;z-index:10';index.filter(x=>x.title.toLowerCase().includes(q)||x.text.toLowerCase().includes(q)).slice(0,12).forEach(x=>{const a=document.createElement('a');a.href=x.href;a.className='result';a.style='display:block;text-decoration:none';a.innerHTML='<strong>'+x.title+'</strong><br><small>'+x.text.slice(Math.max(0,x.text.toLowerCase().indexOf(q)-70),Math.max(0,x.text.toLowerCase().indexOf(q)-70)+180)+'</small>';box.appendChild(a)});document.body.appendChild(box)})});'''


def build(repo: Path, output: Path) -> None:
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)
    slugs = {rel: Path(rel).stem + ".html" for _, rel in PAGES}
    slugs["docs/index.md"] = "index.html"
    renderer = mistune.create_markdown(plugins=["table", "strikethrough", "task_lists", "url"])
    search = []
    for title, rel in PAGES:
        source = repo / rel
        text = source.read_text(encoding="utf-8", errors="ignore")
        text = re.sub(r"^---\n.*?\n---\n", "", text, flags=re.S)
        content = renderer(text)
        for target, destination in slugs.items():
            content = content.replace(f'href="{target}"', f'href="{destination}"')
        for _, other_rel in PAGES:
            relative = Path(os.path.relpath(repo / other_rel, source.parent)).as_posix()
            content = content.replace(f'href="{relative}"', f'href="{slugs[other_rel]}"')
        content = re.sub(r'(?:href|src)="(?:\.\./)+architecture/diagrams/current/render/([^"]+)"', lambda m: m.group(0).split('=')[0] + f'="assets/diagrams/{m.group(1)}"', content)
        content = content.replace('href="render/', 'href="assets/diagrams/').replace('href="sidecars/', 'href="assets/sidecars/')
        content = content.replace('href="../structurizr/workspace.dsl"', 'href="assets/workspace.dsl"')
        body = TEMPLATE.render(title=title, content=content, nav=[(label, slugs[path]) for label, path in PAGES], current=slugs[rel])
        (output / slugs[rel]).write_text(body, encoding="utf-8")
        plain = re.sub(r"\s+", " ", re.sub("<[^>]+>", " ", content))[:20000]
        search.append({"title": title, "href": slugs[rel], "text": plain})

    assets = output / "assets"
    assets.mkdir()
    (assets / "site.css").write_text(CSS, encoding="utf-8")
    (assets / "search-index.json").write_text(json.dumps(search, ensure_ascii=False), encoding="utf-8")
    (assets / "search.js").write_text(SEARCH_JS, encoding="utf-8")
    shutil.copytree(repo / "docs/architecture/diagrams/current/render", assets / "diagrams")
    shutil.copytree(repo / "docs/architecture/diagrams/current/sidecars", assets / "sidecars")
    shutil.copytree(repo / "docs/study/exports", assets / "study-exports")
    for exported_html in (assets / "study-exports").glob("*.html"):
        exported_html.write_text(exported_html.read_text(encoding="utf-8").replace("../architecture/diagrams/current/render/", "../diagrams/"), encoding="utf-8")
    shutil.copy2(repo / "docs/documentation-manifest.yml", output / "documentation-manifest.yml")
    shutil.copytree(repo / "docs/reference/generated", output / "generated")
    shutil.copy2(repo / "docs/structurizr/workspace.dsl", assets / "workspace.dsl")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output", type=Path, default=Path("artifacts/documentation/portal"))
    args = parser.parse_args()
    build(args.repo.resolve(), args.output.resolve())
    print(args.output.resolve())
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
