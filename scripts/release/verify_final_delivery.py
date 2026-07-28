#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
from pathlib import Path

ALLOWED = {"PASS", "FAIL", "BLOCKED_PREREQUISITE", "HARNESS_ERROR", "PLAN_ONLY"}


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def verify_hashes(root: Path, errors: list[str]) -> None:
    path = root / "hashes.sha256"
    if not path.is_file():
        errors.append("Missing hashes.sha256")
        return
    entries: dict[str, str] = {}
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        match = re.fullmatch(r"([a-fA-F0-9]{64})\s{2}(.+)", line)
        if not match:
            errors.append(f"Invalid hash line {number}")
            continue
        expected, relative = match.groups()
        relative = relative.replace("\\", "/")
        candidate = (root / relative).resolve()
        try:
            candidate.relative_to(root)
        except ValueError:
            errors.append(f"Unsafe hash path: {relative}")
            continue
        entries[relative] = expected.lower()
        if not candidate.is_file():
            errors.append(f"Hashed file missing: {relative}")
        elif digest(candidate) != expected.lower():
            errors.append(f"Hash mismatch: {relative}")
    root_manifest = (root / "hashes.sha256").resolve()
    actual = {
        file.relative_to(root).as_posix()
        for file in root.rglob("*")
        if file.is_file() and file.resolve() != root_manifest
    }
    if actual != set(entries):
        missing = sorted(actual - set(entries))
        orphan = sorted(set(entries) - actual)
        if missing:
            errors.append("Files missing from hash manifest: " + ", ".join(missing))
        if orphan:
            errors.append("Orphan hash entries: " + ", ".join(orphan))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("delivery_root", type=Path)
    parser.add_argument("--require-pass", action="store_true")
    parser.add_argument("--config", type=Path, default=Path("config/acceptance/final-delivery.json"))
    args = parser.parse_args()

    root = args.delivery_root.resolve()
    config_path = args.config.resolve()
    errors: list[str] = []
    config: dict = {}
    if not config_path.is_file():
        errors.append(f"Missing final delivery config: {config_path}")
    else:
        try:
            config = json.loads(config_path.read_text(encoding="utf-8-sig"))
        except Exception as exc:
            errors.append(f"Invalid final delivery config: {exc}")
    required = (
        "final-delivery-summary.json",
        "FINAL-DELIVERY.md",
        "delivery-gates.csv",
        "source-identity.json",
        "delivery-manifest.csv",
    )
    for name in required:
        if not (root / name).is_file():
            errors.append(f"Missing {name}")

    summary: dict = {}
    summary_path = root / "final-delivery-summary.json"
    if summary_path.is_file():
        try:
            summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
        except Exception as exc:
            errors.append(f"Invalid final-delivery-summary.json: {exc}")
    status = str(summary.get("status", ""))
    if status not in ALLOWED:
        errors.append(f"Unsupported final delivery status: {status}")
    if args.require_pass and status != "PASS":
        errors.append(f"Final delivery is not PASS: {status}")

    if status == "PASS":
        if summary.get("acceptanceStatus") != "PASS" or summary.get("acceptanceProfile") != "Full":
            errors.append("PASS delivery does not reference a Full PASS acceptance campaign")
        if not summary.get("gitCommit"):
            errors.append("PASS delivery has no git commit")
        if summary.get("sourceClean") is not True:
            errors.append("PASS delivery source was not clean")
        if not summary.get("releaseArchive") or not summary.get("releaseArchiveSha256"):
            errors.append("PASS delivery has no release archive identity")
        proof_value = str(summary.get("acceptanceProof", ""))
        proof_root = (root / proof_value).resolve() if proof_value else None
        if proof_root is None:
            errors.append("PASS delivery has no acceptance proof directory")
        else:
            try:
                proof_root.relative_to(root)
            except ValueError:
                errors.append("Acceptance proof must remain inside the delivery root")
            required_proof = (
                "environment.json",
                "run-spec.json",
                "summary.json",
                "tests.csv",
                "commands.csv",
                "blockers.csv",
                "evidence-manifest.csv",
                "hashes.sha256",
                "acceptance-verification.json",
            )
            for name in required_proof:
                if not (proof_root / name).is_file():
                    errors.append(f"Acceptance proof is missing {name}")
            verification_path = proof_root / "acceptance-verification.json"
            if verification_path.is_file():
                try:
                    verification = json.loads(verification_path.read_text(encoding="utf-8-sig"))
                except Exception as exc:
                    errors.append(f"Invalid acceptance proof verification: {exc}")
                else:
                    if verification.get("status") != "PASS":
                        errors.append("Acceptance proof verification is not PASS")
                    if verification.get("gitCommit") != summary.get("gitCommit"):
                        errors.append("Acceptance proof commit differs from delivery commit")
                    if verification.get("sourceFingerprint") != summary.get("sourceFingerprint"):
                        errors.append("Acceptance proof fingerprint differs from delivery fingerprint")
        if int(summary.get("failedGateCount", -1) or 0) != 0:
            errors.append("PASS delivery reports failed gates")
        if int(summary.get("passedGateCount", -1) or 0) != int(summary.get("selectedGateCount", -2) or -2):
            errors.append("Not every selected delivery gate passed")

        gates_path = root / "delivery-gates.csv"
        if gates_path.is_file():
            with gates_path.open(encoding="utf-8-sig", newline="") as handle:
                gates = list(csv.DictReader(handle))
            bad = [str(row.get("id", "")) for row in gates if row.get("status") != "PASS" or row.get("exitCode") != "0"]
            if bad:
                errors.append("Non-passing delivery gates: " + ", ".join(bad))
            expected_gates = ["preflight"] + [str(value) for value in config.get("requiredReleaseGates", [])]
            actual_gates = [str(row.get("id", "")) for row in gates]
            if actual_gates != expected_gates:
                errors.append("Delivery gate sequence differs from the versioned contract")

        checksum_value = str(summary.get("releaseArchiveChecksum", ""))
        archive_value = str(summary.get("releaseArchive", ""))
        checksum_path = Path(checksum_value)
        archive_path = Path(archive_value)
        if not checksum_path.is_absolute():
            checksum_path = (root / checksum_path).resolve()
        if not archive_path.is_absolute():
            archive_path = (root / archive_path).resolve()
        try:
            checksum_path.relative_to(root)
            archive_path.relative_to(root)
        except ValueError:
            errors.append("Release archive paths must remain inside the delivery root")
        if not archive_path.is_file():
            errors.append("Referenced release archive does not exist")
        elif digest(archive_path) != str(summary.get("releaseArchiveSha256", "")).lower():
            errors.append("Referenced release archive hash differs from summary")
        if not checksum_path.is_file():
            errors.append("Referenced release archive checksum does not exist")
        elif archive_path.is_file():
            expected_line = f"{digest(archive_path)}  {archive_path.name}"
            if checksum_path.read_text(encoding="ascii", errors="replace").strip().lower() != expected_line.lower():
                errors.append("External release checksum is invalid")

    manifest_path = root / "delivery-manifest.csv"
    if manifest_path.is_file():
        with manifest_path.open(encoding="utf-8-sig", newline="") as handle:
            rows = list(csv.DictReader(handle))
        if not rows:
            errors.append("delivery-manifest.csv is empty")
        recorded: set[str] = set()
        for row in rows:
            relative = str(row.get("path", "")).replace("\\", "/")
            recorded.add(relative)
            candidate = root / relative
            if not candidate.is_file():
                errors.append(f"Delivery manifest file missing: {relative}")
                continue
            if digest(candidate) != str(row.get("sha256", "")).lower():
                errors.append(f"Delivery manifest hash mismatch: {relative}")
        root_delivery_manifest = (root / "delivery-manifest.csv").resolve()
        root_hash_manifest = (root / "hashes.sha256").resolve()
        actual_manifest_files = {
            path.relative_to(root).as_posix()
            for path in root.rglob("*")
            if path.is_file() and path.resolve() not in {root_delivery_manifest, root_hash_manifest}
        }
        if recorded != actual_manifest_files:
            missing = sorted(actual_manifest_files - recorded)
            orphan = sorted(recorded - actual_manifest_files)
            if missing:
                errors.append("Files missing from delivery manifest: " + ", ".join(missing))
            if orphan:
                errors.append("Orphan delivery manifest entries: " + ", ".join(orphan))

    if root.is_dir():
        verify_hashes(root, errors)
    result = {
        "status": "PASS" if not errors else "FAIL",
        "deliveryStatus": status,
        "deliveryRoot": str(root),
        "errors": errors,
    }
    print(json.dumps(result, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
