#!/usr/bin/env python3
"""Build a cross-phase evidence index, integrity audit and report scorecard.

The collector is read-only with respect to existing phase outputs. It scans one
report-evidence baseline, verifies available SHA-256 manifests, resolves claim
lineage, inventories report assets and creates a Phase 10 governance package.
It never promotes missing runtime evidence and never edits prior phase outputs.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import mimetypes
import re
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

SCRIPT_VERSION = "1.2.0"
RUN_RE = re.compile(r"^\d{8}T\d{6}Z(?:[-_A-Za-z0-9]+)?$")
SUCCESS_STATUSES = {
    "PASS",
    "PASSED",
    "PASS_COMPLETE_REPORT_PACKAGE",
    "PASS_PARTIAL_REPORT_PACKAGE",
    "PASS_EXPLORATORY_VALIDATION",
    "PASS_WITH_LIMITATIONS",
    "PASS_GAP_CLOSURE_READY",
    "PASS_EVIDENCE_COMPLETE",
    "PLAN_READY_EVIDENCE_INCOMPLETE",
    "PARTIAL_PASS_BLOCKED_ENVIRONMENT",
}


def utc_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: Iterable[dict[str, Any]], fieldnames: list[str] | None = None) -> None:
    materialized = list(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        fieldnames = list(materialized[0]) if materialized else []
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(materialized)


def read_json(path: Path, default: Any = None) -> Any:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return default


def md_table(rows: list[dict[str, Any]], columns: list[tuple[str, str]]) -> str:
    if not rows:
        return "_Sem registos._\n"

    def esc(value: Any) -> str:
        return str(value if value is not None else "").replace("|", "\\|").replace("\n", " ")

    lines = [
        "| " + " | ".join(label for _, label in columns) + " |",
        "|" + "|".join("---" for _ in columns) + "|",
    ]
    lines.extend("| " + " | ".join(esc(row.get(key, "")) for key, _ in columns) + " |" for row in rows)
    return "\n".join(lines) + "\n"


def safe_float(value: Any, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def phase_for(relative: Path, phase_dirs: dict[str, str]) -> str:
    first = relative.parts[0] if relative.parts else ""
    for phase, directory in phase_dirs.items():
        if first == directory:
            return phase
    return "other"


def run_id_for(relative: Path) -> str | None:
    for part in relative.parts[1:3]:
        if RUN_RE.match(part):
            return part
    return None


def artifact_role(path: Path) -> str:
    name = path.name.lower()
    suffix = path.suffix.lower()
    parts = {part.lower() for part in path.parts}
    if name == "sha256sums.txt":
        return "integrity_manifest"
    if name == "latest.txt":
        return "latest_pointer"
    if "claims" in parts or "claim" in name:
        return "claim_register"
    if "figures" in parts or suffix in {".svg", ".png", ".jpg", ".jpeg", ".webp"}:
        return "figure"
    if "tables" in parts or suffix == ".tex":
        return "table"
    if "logs" in parts or suffix in {".log"} or name.endswith(("stdout.txt", "stderr.txt")):
        return "log"
    if "summary" in name:
        return "summary"
    if "manifest" in name:
        return "manifest"
    if "provenance" in name or "source-inventory" in name:
        return "provenance"
    if suffix == ".csv":
        return "dataset"
    if suffix == ".json":
        return "structured_evidence"
    if suffix in {".md", ".txt"}:
        return "narrative"
    if suffix in {".dot"}:
        return "diagram_source"
    return "other"


def parse_integrity_manifests(baseline_root: Path, excluded_root: Path) -> tuple[dict[str, dict[str, Any]], list[dict[str, Any]]]:
    coverage: dict[str, dict[str, Any]] = {}
    audit: list[dict[str, Any]] = []
    for manifest in sorted(baseline_root.rglob("SHA256SUMS.txt")):
        if excluded_root == manifest or excluded_root in manifest.parents:
            continue
        try:
            lines = manifest.read_text(encoding="utf-8").splitlines()
        except OSError as exc:
            audit.append({
                "manifest": manifest.relative_to(baseline_root).as_posix(),
                "path": "",
                "expectedSha256": "",
                "actualSha256": "",
                "status": "MANIFEST_UNREADABLE",
                "detail": str(exc),
            })
            continue
        for line in lines:
            if not line.strip():
                continue
            match = re.match(r"^([0-9a-fA-F]{64})\s+\*?(.*)$", line.strip())
            if not match:
                audit.append({
                    "manifest": manifest.relative_to(baseline_root).as_posix(),
                    "path": "",
                    "expectedSha256": "",
                    "actualSha256": "",
                    "status": "INVALID_MANIFEST_LINE",
                    "detail": line[:200],
                })
                continue
            expected, raw = match.group(1).lower(), match.group(2).strip()
            target = (manifest.parent / raw).resolve()
            try:
                target.relative_to(baseline_root.resolve())
            except ValueError:
                status, actual, detail = "PATH_ESCAPE", "", raw
            else:
                if not target.is_file():
                    status, actual, detail = "MISSING", "", ""
                else:
                    actual = sha256(target)
                    status = "VERIFIED" if actual == expected else "MISMATCH"
                    detail = ""
            try:
                rel = target.relative_to(baseline_root.resolve()).as_posix()
            except ValueError:
                rel = raw
            record = {
                "manifest": manifest.relative_to(baseline_root).as_posix(),
                "path": rel,
                "expectedSha256": expected,
                "actualSha256": actual,
                "status": status,
                "detail": detail,
            }
            audit.append(record)
            current = coverage.get(rel)
            if current is None or current.get("status") != "VERIFIED":
                coverage[rel] = record
    return coverage, audit


def resolve_latest(phase_root: Path) -> tuple[str | None, Path | None, str]:
    latest = phase_root / "LATEST.txt"
    if latest.is_file():
        raw = latest.read_text(encoding="utf-8").strip()
        candidate = phase_root / Path(raw).name
        if candidate.is_dir():
            return candidate.name, candidate, "VALID"
        return raw or None, None, "BROKEN"
    runs = sorted((p for p in phase_root.iterdir() if p.is_dir() and RUN_RE.match(p.name)), key=lambda p: p.name) if phase_root.is_dir() else []
    if runs:
        return runs[-1].name, runs[-1], "INFERRED"
    return None, None, "NOT_APPLICABLE" if phase_root.is_dir() else "MISSING_PHASE"


def find_summary(root: Path | None, candidates: list[str]) -> Path | None:
    if root is None:
        return None
    for candidate in candidates:
        direct = root / candidate
        if direct.is_file():
            return direct
    for candidate in candidates:
        matches = sorted(root.rglob(candidate))
        if matches:
            return matches[0]
    return None


def summary_status(summary: Any) -> str:
    if not isinstance(summary, dict):
        return "NO_SUMMARY"
    for key in ("status", "overall_status", "phaseStatus", "phase_status", "currentRuntimeExecutionStatus", "currentReliabilityStatus"):
        if summary.get(key) is not None:
            return str(summary[key]).upper()
    return "UNKNOWN"


def resolve_source_path(repo: Path, baseline_root: Path, source: str) -> Path:
    source_path = Path(source)
    if source_path.is_absolute():
        return source_path
    direct = repo / source_path
    if direct.exists():
        return direct
    marker = Path("artifacts") / "report-evidence" / baseline_root.name
    parts = source_path.parts
    marker_parts = marker.parts
    for i in range(0, max(0, len(parts) - len(marker_parts) + 1)):
        if parts[i : i + len(marker_parts)] == marker_parts:
            return baseline_root.joinpath(*parts[i + len(marker_parts) :])
    return baseline_root / source_path


def find_current_campaign(baseline_root: Path, run_id: str) -> tuple[Path | None, dict[str, Any] | None]:
    candidate = baseline_root / "08-campaign" / run_id / "campaign-summary.json"
    if candidate.is_file():
        return candidate, read_json(candidate, {})
    phase_root = baseline_root / "08-campaign"
    _, latest_dir, _ = resolve_latest(phase_root)
    candidate = latest_dir / "campaign-summary.json" if latest_dir else None
    return (candidate, read_json(candidate, {})) if candidate and candidate.is_file() else (None, None)


def build_svg_bar(path: Path, title: str, rows: list[tuple[str, float]], subtitle: str = "") -> None:
    width, height = 1000, max(340, 120 + len(rows) * 62)
    left, right, top = 280, 70, 110
    plot_width = width - left - right
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="white"/>',
        f'<text x="40" y="46" font-family="Arial, sans-serif" font-size="28" font-weight="700">{title}</text>',
    ]
    if subtitle:
        lines.append(f'<text x="40" y="76" font-family="Arial, sans-serif" font-size="16" fill="#444">{subtitle}</text>')
    for index, (label, value) in enumerate(rows):
        y = top + index * 62
        bounded = max(0.0, min(100.0, value))
        bar_width = plot_width * bounded / 100.0
        lines.extend([
            f'<text x="{left-15}" y="{y+22}" text-anchor="end" font-family="Arial, sans-serif" font-size="17">{label}</text>',
            f'<rect x="{left}" y="{y}" width="{plot_width}" height="30" rx="5" fill="#e8edf2"/>',
            f'<rect x="{left}" y="{y}" width="{bar_width:.1f}" height="30" rx="5" fill="#52677c"/>',
            f'<text x="{min(left+bar_width+10, width-45):.1f}" y="{y+22}" font-family="Arial, sans-serif" font-size="16" font-weight="700">{value:.1f}</text>',
        ])
    lines.append('</svg>')
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_phase_svg(path: Path, rows: list[dict[str, Any]]) -> None:
    labels = [(row["phase"], 100.0 if row["summaryAvailable"] else (50.0 if row["directoryAvailable"] else 0.0)) for row in rows]
    build_svg_bar(path, "Cobertura das fases de evidência", labels, "100 = diretório e resumo disponíveis; 50 = apenas diretório")


def build_claim_dot(path: Path, claim_rows: list[dict[str, Any]]) -> None:
    lines = [
        "digraph EvidenceClaimLineage {",
        '  graph [rankdir=LR, bgcolor="transparent", nodesep=0.35, ranksep=0.7];',
        '  node [shape=box, style="rounded,filled", fontname="Arial", fontsize=10, color="#52677c", fillcolor="#f5f7f9"];',
        '  edge [color="#7a8793", arrowsize=0.7];',
    ]
    phase_nodes: set[str] = set()
    for row in claim_rows:
        claim_id = re.sub(r"[^A-Za-z0-9_]", "_", row["claimId"] or "claim")
        label = (row["claim"] or row["claimId"] or "claim").replace('"', "'")
        if len(label) > 90:
            label = label[:87] + "..."
        source_phase = row.get("sourcePhase") or "unknown"
        phase_id = re.sub(r"[^A-Za-z0-9_]", "_", source_phase)
        if phase_id not in phase_nodes:
            lines.append(f'  phase_{phase_id} [label="{source_phase}", shape=folder, fillcolor="#e8edf2"];')
            phase_nodes.add(phase_id)
        fill = "#e7f3ea" if row.get("traceable") else "#f8e8e8"
        lines.append(f'  claim_{claim_id} [label="{row["claimId"]}: {label}", fillcolor="{fill}"];')
        lines.append(f"  phase_{phase_id} -> claim_{claim_id};")
    lines.append("}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def render_dot(dot_path: Path) -> list[str]:
    outputs: list[str] = []
    dot = shutil.which("dot")
    if not dot:
        return outputs
    for fmt in ("svg", "png"):
        target = dot_path.with_suffix(f".{fmt}")
        process = subprocess.run([dot, f"-T{fmt}", str(dot_path), "-o", str(target)], check=False, capture_output=True, text=True)
        if process.returncode == 0 and target.is_file():
            outputs.append(target.name)
    return outputs


def create_png_fallback(svg_path: Path, png_path: Path) -> bool:
    png_path.parent.mkdir(parents=True, exist_ok=True)
    try:
        import cairosvg  # type: ignore

        cairosvg.svg2png(url=str(svg_path), write_to=str(png_path), output_width=1800)
        return png_path.is_file() and png_path.stat().st_size > 0
    except Exception:
        pass
    converter = shutil.which("magick") or shutil.which("convert")
    if converter:
        command = [converter, str(svg_path), str(png_path)]
        process = subprocess.run(command, check=False, capture_output=True, text=True)
        return process.returncode == 0 and png_path.is_file() and png_path.stat().st_size > 0
    return False


def write_hash_manifest(root: Path) -> int:
    manifest = root / "SHA256SUMS.txt"
    files = sorted(p for p in root.rglob("*") if p.is_file() and p != manifest)
    manifest.write_text("\n".join(f"{sha256(p)}  {p.relative_to(root).as_posix()}" for p in files) + "\n", encoding="utf-8")
    return len(files)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd())
    parser.add_argument("--baseline-id", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--config", default="config/evidence/evidence-governance.json")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    repo = args.repo.resolve()
    baseline_root = repo / "artifacts" / "report-evidence" / args.baseline_id
    if not baseline_root.is_dir():
        raise SystemExit(f"Baseline not found: {baseline_root}")
    config_path = (repo / args.config).resolve()
    config = read_json(config_path)
    if not isinstance(config, dict):
        raise SystemExit(f"Invalid configuration: {config_path}")
    output = (args.output or baseline_root / "10-evidence-intelligence" / args.run_id).resolve()
    try:
        output.relative_to(baseline_root.resolve())
    except ValueError:
        raise SystemExit("Output must remain inside the selected baseline root")
    if output.exists() and any(output.iterdir()):
        if not args.overwrite:
            raise SystemExit(f"Output is not empty: {output}; use --overwrite")
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)

    phase_dirs: dict[str, str] = config.get("phaseDirectories", {})
    summary_candidates: dict[str, list[str]] = config.get("summaryCandidates", {})
    integrity_coverage, integrity_audit = parse_integrity_manifests(baseline_root, output)

    artifact_rows: list[dict[str, Any]] = []
    for path in sorted(p for p in baseline_root.rglob("*") if p.is_file() and not (output == p or output in p.parents)):
        relative = path.relative_to(baseline_root)
        integrity = integrity_coverage.get(relative.as_posix(), {})
        mime, _ = mimetypes.guess_type(path.name)
        artifact_rows.append({
            "path": relative.as_posix(),
            "phase": phase_for(relative, phase_dirs),
            "runId": run_id_for(relative) or "",
            "role": artifact_role(relative),
            "extension": path.suffix.lower().lstrip("."),
            "mimeType": mime or "application/octet-stream",
            "sizeBytes": path.stat().st_size,
            "sha256": sha256(path),
            "integrityStatus": integrity.get("status", "NOT_MANIFESTED"),
            "integrityManifest": integrity.get("manifest", ""),
        })

    campaign_path, campaign = find_current_campaign(baseline_root, args.run_id)
    selected_steps = list((campaign or {}).get("selectedSteps", []))
    campaign_step_status = {
        str(row.get("step")): str(row.get("status", "UNKNOWN"))
        for row in (campaign or {}).get("steps", [])
        if isinstance(row, dict)
    }

    phase_rows: list[dict[str, Any]] = []
    phase_summary_paths: dict[str, Path] = {}
    for phase, directory in phase_dirs.items():
        phase_root = baseline_root / directory
        if phase in {"phase1"}:
            latest_id, latest_dir, pointer_status = "", phase_root if phase_root.is_dir() else None, "NOT_APPLICABLE"
        else:
            latest_id, latest_dir, pointer_status = resolve_latest(phase_root)
            if phase == "phase8" and (phase_root / args.run_id).is_dir():
                latest_id, latest_dir, pointer_status = args.run_id, phase_root / args.run_id, "CURRENT_RUN"
        summary_path = find_summary(latest_dir, summary_candidates.get(phase, []))
        summary = read_json(summary_path, {}) if summary_path else {}
        if summary_path:
            phase_summary_paths[phase] = summary_path
        phase_artifacts = [row for row in artifact_rows if row["phase"] == phase]
        status = "PASS" if phase == "phase1" and summary_path else summary_status(summary)
        selected = phase in selected_steps or phase == "phase8"
        phase_rows.append({
            "phase": phase,
            "directory": directory,
            "selectedInCampaign": selected,
            "campaignStepStatus": campaign_step_status.get(phase, "NOT_RECORDED"),
            "directoryAvailable": phase_root.is_dir(),
            "latestRunId": latest_id or "",
            "latestPointerStatus": pointer_status,
            "summaryAvailable": bool(summary_path),
            "summaryPath": summary_path.relative_to(baseline_root).as_posix() if summary_path else "",
            "summaryStatus": status,
            "artifactCount": len(phase_artifacts),
            "figureCount": sum(1 for row in phase_artifacts if row["role"] == "figure"),
            "tableCount": sum(1 for row in phase_artifacts if row["role"] == "table"),
            "manifestedCount": sum(1 for row in phase_artifacts if row["integrityStatus"] == "VERIFIED"),
        })

    claim_register_candidates = sorted(baseline_root.glob("07-report-integration/*/claims/claim-evidence-register.json"))
    claim_register = claim_register_candidates[-1] if claim_register_candidates else None
    claims = read_json(claim_register, []) if claim_register else []
    claim_rows: list[dict[str, Any]] = []
    allowed_classes = set(config.get("claimRules", {}).get("allowedEvidenceClasses", []))
    for claim in claims if isinstance(claims, list) else []:
        source = str(claim.get("source", ""))
        source_path = resolve_source_path(repo, baseline_root, source)
        source_exists = source_path.is_file()
        try:
            source_relative = source_path.relative_to(baseline_root).as_posix()
        except ValueError:
            source_relative = source
        integrity = integrity_coverage.get(source_relative, {})
        source_phase = phase_for(Path(source_relative), phase_dirs)
        wording_complete = bool(claim.get("allowed_wording")) and bool(claim.get("prohibited_wording"))
        evidence_class = str(claim.get("evidence_class", ""))
        traceable = source_exists and integrity.get("status") == "VERIFIED" and evidence_class in allowed_classes and wording_complete
        claim_rows.append({
            "claimId": claim.get("claim_id", ""),
            "claim": claim.get("claim", ""),
            "evidenceClass": evidence_class,
            "source": source,
            "resolvedSource": source_relative,
            "sourcePhase": source_phase,
            "sourceExists": source_exists,
            "integrityStatus": integrity.get("status", "NOT_MANIFESTED"),
            "wordingBoundaryComplete": wording_complete,
            "traceable": traceable,
            "allowedWording": claim.get("allowed_wording", ""),
            "prohibitedWording": claim.get("prohibited_wording", ""),
        })

    figure_groups: dict[str, dict[str, Any]] = {}
    for row in artifact_rows:
        if row["role"] != "figure":
            continue
        path = Path(row["path"])
        key = path.with_suffix("").as_posix()
        group = figure_groups.setdefault(key, {
            "figureBase": key,
            "phase": row["phase"],
            "formats": set(),
            "files": [],
            "totalBytes": 0,
            "allIntegrityVerified": True,
        })
        group["formats"].add(row["extension"])
        group["files"].append(row["path"])
        group["totalBytes"] += int(row["sizeBytes"])
        if row["integrityStatus"] != "VERIFIED":
            group["allIntegrityVerified"] = False
    figure_rows: list[dict[str, Any]] = []
    preferred = set(config.get("presentation", {}).get("preferredFigureFormats", ["svg", "png"]))
    fallback_root = output / "report-ready" / "figure-fallbacks"
    for group in figure_groups.values():
        formats = sorted(group["formats"])
        has_vector = "svg" in formats
        has_raster = bool({"png", "jpg", "jpeg", "webp"} & set(formats))
        generated_fallback = ""
        if has_vector and not has_raster:
            svg_source = baseline_root / f"{group['figureBase']}.svg"
            fallback_path = fallback_root / f"{group['figureBase']}.png"
            if svg_source.is_file() and create_png_fallback(svg_source, fallback_path):
                generated_fallback = fallback_path.relative_to(output).as_posix()
                has_raster = True
        effective_formats = set(formats) | ({"png"} if generated_fallback else set())
        figure_rows.append({
            "figureBase": group["figureBase"],
            "phase": group["phase"],
            "formats": ",".join(formats),
            "files": ";".join(sorted(group["files"])),
            "totalBytes": group["totalBytes"],
            "allIntegrityVerified": group["allIntegrityVerified"],
            "hasVector": has_vector,
            "hasRaster": has_raster,
            "generatedFallback": generated_fallback,
            "preferredFormatCoverage": round(100 * len(preferred & effective_formats) / max(1, len(preferred)), 1),
        })
    figure_rows.sort(key=lambda row: (row["phase"], row["figureBase"]))

    report_asset_candidates = sorted(baseline_root.glob("07-report-integration/*/report-ready/report-asset-manifest.json"))
    report_assets_path = report_asset_candidates[-1] if report_asset_candidates else None
    report_assets = read_json(report_assets_path, []) if report_assets_path else []
    if isinstance(report_assets, dict):
        report_assets = report_assets.get("assets", report_assets.get("items", []))
    report_asset_rows: list[dict[str, Any]] = []
    report_asset_base = report_assets_path.parent.parent if report_assets_path else baseline_root
    for asset in report_assets if isinstance(report_assets, list) else []:
        raw = str(asset.get("file", asset.get("path", asset.get("asset", asset.get("source", "")))))
        candidate = (report_asset_base / raw).resolve() if raw else report_asset_base / "__missing__"
        if raw and not candidate.exists():
            candidate = resolve_source_path(repo, baseline_root, raw)
        exists = candidate.is_file()
        try:
            rel = candidate.relative_to(baseline_root).as_posix()
        except ValueError:
            rel = raw
        report_asset_rows.append({
            "asset": raw,
            "resolvedPath": rel,
            "exists": exists,
            "integrityStatus": integrity_coverage.get(rel, {}).get("status", "NOT_MANIFESTED"),
            "type": asset.get("type", asset.get("kind", asset.get("evidence_class", ""))),
            "target": asset.get("target", asset.get("chapter", asset.get("recommended_location", ""))),
        })

    phase7_run_root = report_assets_path.parent.parent if report_assets_path else None
    report_area_rows: list[dict[str, Any]] = []
    if phase7_run_root and (phase7_run_root / "tables" / "evidence-status.csv").is_file():
        with (phase7_run_root / "tables" / "evidence-status.csv").open(encoding="utf-8", newline="") as handle:
            report_area_rows = list(csv.DictReader(handle))

    imported_gap_rows: list[dict[str, Any]] = []
    if phase7_run_root and (phase7_run_root / "tables" / "remaining-gaps.csv").is_file():
        with (phase7_run_root / "tables" / "remaining-gaps.csv").open(encoding="utf-8", newline="") as handle:
            imported_gap_rows = list(csv.DictReader(handle))

    gaps: list[dict[str, Any]] = []
    for imported in imported_gap_rows:
        priority = str(imported.get("priority", "P1")).upper()
        gaps.append({
            "gapId": f"GAP-REPORT-{len(gaps)+1:03d}",
            "severity": "MEDIUM" if priority in {"P0", "P1"} else "LOW",
            "area": "report-completeness",
            "gap": imported.get("gap", "Lacuna importada da Fase 7"),
            "impact": imported.get("report_effect", "Limita a completude do relatório."),
            "requiredAction": imported.get("needed", "Recolher a evidência em falta."),
        })
    for row in phase_rows:
        if row["selectedInCampaign"] and not row["summaryAvailable"]:
            gaps.append({"gapId": f"GAP-{row['phase'].upper()}-SUMMARY", "severity": "HIGH", "area": row["phase"], "gap": "Resumo da fase selecionada não está disponível.", "impact": "Impede provar o resultado da fase.", "requiredAction": "Reexecutar ou recuperar o coletor e respetivo verificador."})
        if row["latestPointerStatus"] == "BROKEN":
            gaps.append({"gapId": f"GAP-{row['phase'].upper()}-LATEST", "severity": "MEDIUM", "area": row["phase"], "gap": "LATEST.txt não resolve para um diretório existente.", "impact": "Pode fazer a integração consumir a run errada.", "requiredAction": "Reescrever LATEST.txt apenas com o identificador portátil da run."})
    for audit in integrity_audit:
        if audit["status"] not in {"VERIFIED"}:
            gaps.append({"gapId": f"GAP-INTEGRITY-{len(gaps)+1:03d}", "severity": "HIGH" if audit["status"] in {"MISMATCH", "PATH_ESCAPE"} else "MEDIUM", "area": "integrity", "gap": f"{audit['status']}: {audit['path'] or audit['manifest']}", "impact": "A integridade ou completude do pacote não pode ser confirmada.", "requiredAction": "Regenerar o manifesto ou recuperar o ficheiro correto."})
    for row in claim_rows:
        if not row["traceable"]:
            gaps.append({"gapId": f"GAP-CLAIM-{row['claimId'] or len(gaps)+1}", "severity": "HIGH", "area": "claims", "gap": f"Claim sem rastreabilidade completa: {row['claimId']}", "impact": "A afirmação não deve ser promovida para o relatório.", "requiredAction": "Corrigir fonte, hash, classe de evidência ou limite de linguagem."})
    for row in report_asset_rows:
        if not row["exists"]:
            gaps.append({"gapId": f"GAP-ASSET-{len(gaps)+1:03d}", "severity": "MEDIUM", "area": "presentation", "gap": f"Asset de relatório ausente: {row['asset']}", "impact": "O capítulo pode ficar com figura ou tabela em falta.", "requiredAction": "Regenerar o asset através da fase produtora."})
    for row in figure_rows:
        if row["hasVector"] and not row["hasRaster"]:
            gaps.append({"gapId": f"GAP-FIGURE-FALLBACK-{len(gaps)+1:03d}", "severity": "LOW", "area": "presentation", "gap": f"Figura sem fallback raster: {row['figureBase']}", "impact": "Algumas ferramentas de conversão podem não preservar o SVG.", "requiredAction": "Gerar PNG a partir do mesmo source, sem alterar os dados."})

    checked = [row for row in integrity_audit if row["status"] not in {"INVALID_MANIFEST_LINE"}]
    verified = sum(1 for row in checked if row["status"] == "VERIFIED")
    integrity_score = 100.0 * verified / len(checked) if checked else 0.0
    selected_phase_rows = [row for row in phase_rows if row["selectedInCampaign"]]
    selected_complete = sum(1 for row in selected_phase_rows if row["summaryAvailable"] and row["summaryStatus"] in SUCCESS_STATUSES)
    selected_completeness = 100.0 * selected_complete / len(selected_phase_rows) if selected_phase_rows else 0.0
    covered_report_areas = sum(1 for row in report_area_rows if str(row.get("result", "")).upper() not in {"NO_SOURCE", "BLOCKED", "FAIL", "UNKNOWN", ""})
    report_area_coverage = 100.0 * covered_report_areas / len(report_area_rows) if report_area_rows else selected_completeness
    completeness_score = (selected_completeness + report_area_coverage) / 2.0
    traceable_count = sum(1 for row in claim_rows if row["traceable"])
    traceability_score = 100.0 * traceable_count / len(claim_rows) if claim_rows else 0.0
    reproducibility_checks = [
        config_path.is_file(),
        campaign_path is not None and campaign_path.is_file(),
        any(row["role"] == "provenance" for row in artifact_rows),
        any(row["role"] == "integrity_manifest" for row in artifact_rows),
        all(row["latestPointerStatus"] != "BROKEN" for row in phase_rows),
    ]
    reproducibility_score = 100.0 * sum(reproducibility_checks) / len(reproducibility_checks)
    minimum_figures = int(config.get("presentation", {}).get("minimumReportFigures", 5))
    minimum_tables = int(config.get("presentation", {}).get("minimumReportTables", 5))
    report_figures = sum(1 for row in figure_rows if row["phase"] == "phase7")
    report_tables = sum(1 for row in artifact_rows if row["phase"] == "phase7" and row["role"] == "table")
    presentation_checks = [
        min(1.0, report_figures / max(1, minimum_figures)),
        min(1.0, report_tables / max(1, minimum_tables)),
        1.0 if report_asset_rows and all(row["exists"] for row in report_asset_rows) else (0.5 if report_asset_rows else 0.0),
        1.0 if figure_rows and any(row["hasVector"] for row in figure_rows) else 0.0,
    ]
    presentation_score = 100.0 * sum(presentation_checks) / len(presentation_checks)

    weights = config.get("scoring", {}).get("weights", {})
    component_scores = {
        "integrity": round(integrity_score, 1),
        "completeness": round(completeness_score, 1),
        "traceability": round(traceability_score, 1),
        "reproducibility": round(reproducibility_score, 1),
        "presentation": round(presentation_score, 1),
    }
    overall = sum(component_scores[key] * safe_float(weights.get(key), 0.0) for key in component_scores)
    if sum(safe_float(v) for v in weights.values()) <= 0:
        overall = sum(component_scores.values()) / len(component_scores)
    overall = round(overall, 1)
    ready = safe_float(config.get("scoring", {}).get("readyThreshold"), 90)
    caveat = safe_float(config.get("scoring", {}).get("shareWithCaveatsThreshold"), 70)
    coverage_ready = safe_float(config.get("scoring", {}).get("minimumReportAreaCoverageForReady"), 90)
    coverage_caveat = safe_float(config.get("scoring", {}).get("minimumReportAreaCoverageForCaveats"), 50)
    if any(gap["severity"] == "HIGH" for gap in gaps):
        status = "NEEDS_REVISION"
    elif report_area_coverage >= coverage_ready and overall >= ready and not any(gap["severity"] == "MEDIUM" for gap in gaps):
        status = "READY_TO_SHARE"
    elif report_area_coverage >= coverage_caveat and overall >= caveat:
        status = "SHARE_WITH_CAVEATS"
    else:
        status = "NEEDS_REVISION"

    score_rows = [{"dimension": key, "score": value, "weight": safe_float(weights.get(key), 0.0)} for key, value in component_scores.items()]
    scorecard = {
        "generatedAtUtc": utc_iso(),
        "scriptVersion": SCRIPT_VERSION,
        "baselineId": args.baseline_id,
        "runId": args.run_id,
        "status": status,
        "overallScore": overall,
        "governanceQualityScore": overall,
        "evidenceCoveragePercent": round(report_area_coverage, 1),
        "selectedPhaseCompletionPercent": round(selected_completeness, 1),
        "componentScores": component_scores,
        "counts": {
            "artifacts": len(artifact_rows),
            "integrityChecks": len(checked),
            "integrityVerified": verified,
            "phases": len(phase_rows),
            "claims": len(claim_rows),
            "traceableClaims": traceable_count,
            "figures": len(figure_rows),
            "reportAssets": len(report_asset_rows),
            "reportAreas": len(report_area_rows),
            "coveredReportAreas": covered_report_areas,
            "gaps": len(gaps),
            "highSeverityGaps": sum(1 for gap in gaps if gap["severity"] == "HIGH"),
        },
        "claimBoundary": "Phase 10 assesses evidence governance quality separately from actual report-area coverage. The governance score must never be presented as percentage of evidence collected.",
    }

    write_json(output / "evidence-index.json", artifact_rows)
    write_csv(output / "evidence-index.csv", artifact_rows)
    write_json(output / "integrity-audit.json", integrity_audit)
    write_csv(output / "integrity-audit.csv", integrity_audit)
    write_json(output / "phase-scorecard.json", phase_rows)
    write_csv(output / "phase-scorecard.csv", phase_rows)
    write_json(output / "claim-lineage.json", claim_rows)
    write_csv(output / "claim-lineage.csv", claim_rows)
    write_json(output / "figure-inventory.json", figure_rows)
    write_csv(output / "figure-inventory.csv", figure_rows)
    write_json(output / "report-asset-audit.json", report_asset_rows)
    write_csv(output / "report-asset-audit.csv", report_asset_rows)
    write_json(output / "report-area-coverage.json", report_area_rows)
    write_csv(output / "report-area-coverage.csv", report_area_rows)
    write_json(output / "evidence-gap-register.json", gaps)
    write_csv(output / "evidence-gap-register.csv", gaps, ["gapId", "severity", "area", "gap", "impact", "requiredAction"])
    write_json(output / "phase10-summary.json", scorecard)
    write_csv(output / "evidence-quality-scorecard.csv", score_rows, ["dimension", "score", "weight"])

    tables = output / "report-ready" / "tables"
    figures = output / "report-ready" / "figures"
    tables.mkdir(parents=True, exist_ok=True)
    figures.mkdir(parents=True, exist_ok=True)
    (tables / "evidence-quality-scorecard.md").write_text(md_table(score_rows, [("dimension", "Dimensão"), ("score", "Pontuação"), ("weight", "Peso")]), encoding="utf-8")
    (tables / "phase-coverage.md").write_text(md_table(phase_rows, [("phase", "Fase"), ("selectedInCampaign", "Selecionada"), ("summaryStatus", "Estado"), ("artifactCount", "Artefactos"), ("figureCount", "Figuras")]), encoding="utf-8")
    (tables / "claim-lineage.md").write_text(md_table(claim_rows, [("claimId", "Claim"), ("evidenceClass", "Classe"), ("sourcePhase", "Fonte"), ("integrityStatus", "Integridade"), ("traceable", "Rastreável")]), encoding="utf-8")
    (tables / "evidence-gaps.md").write_text(md_table(gaps, [("severity", "Severidade"), ("area", "Área"), ("gap", "Lacuna"), ("requiredAction", "Ação")]), encoding="utf-8")
    (tables / "report-area-coverage.md").write_text(md_table(report_area_rows, [("area", "Área"), ("result", "Resultado"), ("evidence_class", "Classe"), ("claim_ceiling", "Limite")]), encoding="utf-8")

    quality_svg = figures / "evidence-quality-scorecard.svg"
    phase_svg = figures / "phase-coverage.svg"
    build_svg_bar(quality_svg, "Qualidade e prontidão da evidência", [(row["dimension"].capitalize(), row["score"]) for row in score_rows], f"Pontuação global: {overall:.1f}/100 — {status}")
    build_phase_svg(phase_svg, phase_rows)
    create_png_fallback(quality_svg, quality_svg.with_suffix(".png"))
    create_png_fallback(phase_svg, phase_svg.with_suffix(".png"))
    claim_dot = figures / "claim-lineage.dot"
    build_claim_dot(claim_dot, claim_rows)
    rendered = render_dot(claim_dot)

    summary_md = [
        "# Fase 10 — governação e inteligência da evidência",
        "",
        f"- Baseline: `{args.baseline_id}`",
        f"- Run: `{args.run_id}`",
        f"- Estado: **{status}**",
        f"- Pontuação global: **{overall:.1f}/100**",
        f"- Artefactos indexados: **{len(artifact_rows)}**",
        f"- Claims rastreáveis: **{traceable_count}/{len(claim_rows)}**",
        f"- Verificações de integridade: **{verified}/{len(checked)}**",
        f"- Lacunas de severidade alta: **{sum(1 for gap in gaps if gap['severity'] == 'HIGH')}**",
        "",
        "## Pontuações",
        "",
        md_table(score_rows, [("dimension", "Dimensão"), ("score", "Pontuação"), ("weight", "Peso")]),
        "## Limite de afirmação",
        "",
        "A Fase 10 mede a qualidade, rastreabilidade e prontidão de apresentação do pacote. Não transforma evidência estática em execução atual, nem produz novos resultados científicos ou operacionais.",
        "",
    ]
    (output / "phase10-summary.md").write_text("\n".join(summary_md), encoding="utf-8")

    at_glance = [
        "# Evidence at a glance",
        "",
        f"**Estado:** {status} — **{overall:.1f}/100**",
        "",
        "## O que pode ser apresentado",
        "",
        f"- {traceable_count} claims possuem fonte existente, classe permitida, limite de linguagem e cobertura de integridade.",
        f"- {report_figures} grupos de figuras e {report_tables} artefactos tabulares foram encontrados na fase de integração do relatório.",
        f"- {covered_report_areas}/{len(report_area_rows)} áreas do relatório possuem evidência atual ou analítica promovível." if report_area_rows else "- A matriz de cobertura do relatório não estava disponível.",
        f"- {verified} entradas de manifesto SHA-256 foram verificadas.",
        "",
        "## O que continua a faltar",
        "",
    ]
    if gaps:
        for gap in gaps[:12]:
            at_glance.append(f"- **{gap['severity']} — {gap['area']}:** {gap['gap']}")
    else:
        at_glance.append("- Não foram encontradas lacunas automáticas.")
    at_glance += ["", "## Regra de utilização", "", "Cada número ou afirmação deve manter a ligação ao claim register e à fonte hashada. Resultados de uma run não devem ser misturados com outra sem uma comparação explícita.", ""]
    (output / "report-ready" / "evidence-at-a-glance.md").write_text("\n".join(at_glance), encoding="utf-8")

    asset_manifest = []
    for path in sorted((output / "report-ready").rglob("*")):
        if path.is_file():
            asset_manifest.append({
                "path": path.relative_to(output).as_posix(),
                "role": artifact_role(path.relative_to(output)),
                "sha256": sha256(path),
                "sizeBytes": path.stat().st_size,
            })
    write_json(output / "report-ready" / "phase10-report-asset-manifest.json", asset_manifest)
    write_csv(output / "report-ready" / "phase10-report-asset-manifest.csv", asset_manifest)

    hashed = write_hash_manifest(output)
    phase_root = baseline_root / "10-evidence-intelligence"
    phase_root.mkdir(parents=True, exist_ok=True)
    (phase_root / "LATEST.txt").write_text(args.run_id + "\n", encoding="utf-8")

    print(f"PHASE_10_STATUS={status}")
    print(f"PHASE_10_SCORE={overall:.1f}")
    print(f"PHASE_10_OUTPUT={output}")
    print(f"PHASE_10_HASHED_FILES={hashed}")
    if rendered:
        print(f"PHASE_10_RENDERED_DIAGRAMS={','.join(rendered)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
