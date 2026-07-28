from __future__ import annotations

import csv
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
ACCEPTANCE_CONFIG = json.loads((REPO / "config/acceptance/final-acceptance.json").read_text(encoding="utf-8"))
DELIVERY_CONFIG = json.loads((REPO / "config/acceptance/final-delivery.json").read_text(encoding="utf-8"))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_hash_manifest(root: Path) -> None:
    rows = []
    root_manifest = (root / "hashes.sha256").resolve()
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.resolve() != root_manifest:
            rows.append(f"{digest(path)}  {path.relative_to(root).as_posix()}")
    (root / "hashes.sha256").write_text("\n".join(rows) + "\n", encoding="utf-8")


def write_acceptance(root: Path, *, status: str = "PASS", secret: bool = False) -> None:
    root.mkdir(parents=True)
    stages = ACCEPTANCE_CONFIG["profiles"]["Full"]["stages"]
    stage_rows = [
        {
            "id": stage,
            "category": "synthetic",
            "status": "PASS",
            "exitCode": 0,
            "durationSeconds": 1,
            "evidence": f"components/{stage}",
            "detail": "synthetic",
        }
        for stage in stages
    ]
    summary = {
        "schemaVersion": 1,
        "runId": "synthetic-full",
        "profile": "Full",
        "status": status,
        "selectedStageCount": len(stages),
        "executedStageCount": len(stages),
        "passedStageCount": len(stages) if status == "PASS" else len(stages) - 1,
        "failedStageCount": 0 if status == "PASS" else 1,
        "blockedStageCount": 0,
        "harnessErrorStageCount": 0,
        "notSelectedStageCount": 0,
        "stages": stage_rows,
    }
    environment = {
        "gitCommit": "1" * 40,
        "gitBranch": "main",
        "gitSourceClean": True,
        "sourceFingerprint": "2" * 64,
    }
    run_spec = {
        "profile": "Full",
        "planOnly": False,
        "executeControlledValidationP3": True,
        "acknowledgeNonProduction": True,
        "p3AuthenticationConfigured": True,
    }
    (root / "summary.json").write_text(json.dumps(summary), encoding="utf-8")
    (root / "environment.json").write_text(json.dumps(environment), encoding="utf-8")
    (root / "run-spec.json").write_text(json.dumps(run_spec), encoding="utf-8")
    (root / "SUMMARY.md").write_text("Full PASS\n", encoding="utf-8")
    (root / "tests.csv").write_text("id,status\nsynthetic,PASS\n", encoding="utf-8")
    (root / "commands.csv").write_text("id,exitCode\nsynthetic,0\n", encoding="utf-8")
    (root / "blockers.csv").write_text("id,status\nnone,PASS\n", encoding="utf-8")
    evidence = root / "components/synthetic/evidence/result.json"
    evidence.parent.mkdir(parents=True)
    evidence.write_text(json.dumps({"status": "PASS"}), encoding="utf-8")
    if secret:
        (root / "components/synthetic/evidence/leak.log").write_text(
            "Authorization: Bearer abcdefghijklmnopqrstuvwxyz0123456789\n", encoding="utf-8"
        )
    with (root / "evidence-manifest.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["path", "sizeBytes", "sha256"])
        writer.writeheader()
        writer.writerow(
            {
                "path": evidence.relative_to(root).as_posix(),
                "sizeBytes": evidence.stat().st_size,
                "sha256": digest(evidence),
            }
        )
    write_hash_manifest(root)


def write_delivery(root: Path, acceptance_root: Path) -> None:
    root.mkdir(parents=True)
    release = root / "release/natureprotector-final.zip"
    release.parent.mkdir(parents=True)
    release.write_bytes(b"synthetic release")
    checksum = Path(str(release) + ".sha256")
    checksum.write_text(f"{digest(release)}  {release.name}\n", encoding="ascii")
    gate_ids = ["preflight"] + DELIVERY_CONFIG["requiredReleaseGates"]
    with (root / "delivery-gates.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["id", "status", "exitCode", "durationSeconds", "evidence", "detail", "command"],
        )
        writer.writeheader()
        for gate in gate_ids:
            writer.writerow(
                {
                    "id": gate,
                    "status": "PASS",
                    "exitCode": 0,
                    "durationSeconds": 1,
                    "evidence": gate,
                    "detail": "synthetic",
                    "command": gate,
                }
            )
    (root / "source-identity.json").write_text(
        json.dumps({"commit": "1" * 40, "clean": True, "sourceFingerprint": "2" * 64}), encoding="utf-8"
    )
    proof = root / "acceptance-proof"
    proof.mkdir()
    for name in (
        "environment.json",
        "run-spec.json",
        "summary.json",
        "SUMMARY.md",
        "tests.csv",
        "commands.csv",
        "blockers.csv",
        "evidence-manifest.csv",
        "hashes.sha256",
    ):
        shutil.copy2(acceptance_root / name, proof / name)
    (proof / "acceptance-verification.json").write_text(
        json.dumps(
            {
                "status": "PASS",
                "gitCommit": "1" * 40,
                "sourceFingerprint": "2" * 64,
                "sourceClean": True,
            }
        ),
        encoding="utf-8",
    )
    summary = {
        "status": "PASS",
        "gitCommit": "1" * 40,
        "sourceClean": True,
        "sourceFingerprint": "2" * 64,
        "acceptanceRoot": str(acceptance_root),
        "acceptanceProof": "acceptance-proof",
        "acceptanceProfile": "Full",
        "acceptanceStatus": "PASS",
        "releaseArchive": release.relative_to(root).as_posix(),
        "releaseArchiveChecksum": checksum.relative_to(root).as_posix(),
        "releaseArchiveSha256": digest(release),
        "selectedGateCount": len(gate_ids),
        "passedGateCount": len(gate_ids),
        "failedGateCount": 0,
    }
    (root / "final-delivery-summary.json").write_text(json.dumps(summary), encoding="utf-8")
    (root / "FINAL-DELIVERY.md").write_text("Final delivery PASS\n", encoding="utf-8")
    with (root / "delivery-manifest.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["path", "sizeBytes", "sha256"])
        writer.writeheader()
        delivery_manifest = (root / "delivery-manifest.csv").resolve()
        hash_manifest = (root / "hashes.sha256").resolve()
        for path in sorted(root.rglob("*")):
            if path.is_file() and path.resolve() not in {delivery_manifest, hash_manifest}:
                writer.writerow(
                    {
                        "path": path.relative_to(root).as_posix(),
                        "sizeBytes": path.stat().st_size,
                        "sha256": digest(path),
                    }
                )
    write_hash_manifest(root)


class FinalDeliveryContractTests(unittest.TestCase):
    def run_acceptance_verifier(self, root: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                "scripts/acceptance/verify_final_acceptance_evidence.py",
                str(root),
                "--config",
                "config/acceptance/final-acceptance.json",
            ],
            cwd=REPO,
            text=True,
            capture_output=True,
            check=False,
        )

    def run_delivery_verifier(self, root: Path) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                "scripts/release/verify_final_delivery.py",
                str(root),
                "--config",
                "config/acceptance/final-delivery.json",
                "--require-pass",
            ],
            cwd=REPO,
            text=True,
            capture_output=True,
            check=False,
        )

    def test_delivery_contract_is_closed_and_full_only(self) -> None:
        self.assertEqual(DELIVERY_CONFIG["requiredAcceptanceProfile"], "Full")
        self.assertEqual(
            DELIVERY_CONFIG["statuses"],
            ["PASS", "FAIL", "BLOCKED_PREREQUISITE", "HARNESS_ERROR", "PLAN_ONLY"],
        )
        self.assertTrue(DELIVERY_CONFIG["sourcePolicy"]["requireGitRepository"])
        self.assertTrue(DELIVERY_CONFIG["sourcePolicy"]["requireCleanWorkingTree"])

    def test_finalizer_orders_acceptance_before_packaging(self) -> None:
        source = (REPO / "scripts/release/Invoke-NP-FinalDelivery.ps1").read_text(encoding="utf-8")
        self.assertLess(source.index("acceptance-evidence-verification"), source.index("release-candidate-build"))
        self.assertLess(source.index("release-candidate-build"), source.index("clean-install"))
        for gate in DELIVERY_CONFIG["requiredReleaseGates"]:
            self.assertIn(gate, source)
        self.assertIn("requireCleanWorkingTree", source)
        self.assertIn("NP_RELIABILITY_AUTH_TOKEN", source)
        self.assertIn("--expected-commit", source)
        self.assertIn("--expected-source-fingerprint", source)
        self.assertIn("run-scoped child", source)
        acceptance_runner = (REPO / "scripts/acceptance/Invoke-NP-FinalAcceptance.ps1").read_text(encoding="utf-8")
        self.assertIn("gitSourceClean", acceptance_runner)
        self.assertIn("sourceFingerprint", acceptance_runner)

    def test_synthetic_full_acceptance_passes_strict_verifier(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "acceptance"
            write_acceptance(root)
            completed = self.run_acceptance_verifier(root)
            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)


    def test_acceptance_from_other_source_snapshot_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "acceptance"
            write_acceptance(root)
            completed = subprocess.run(
                [
                    sys.executable,
                    "scripts/acceptance/verify_final_acceptance_evidence.py",
                    str(root),
                    "--config",
                    "config/acceptance/final-acceptance.json",
                    "--expected-commit",
                    "3" * 40,
                    "--expected-source-fingerprint",
                    "4" * 64,
                ],
                cwd=REPO,
                text=True,
                capture_output=True,
                check=False,
            )
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("differs from the delivery source", completed.stdout)

    def test_non_full_or_non_pass_acceptance_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "acceptance"
            write_acceptance(root, status="FAIL")
            completed = self.run_acceptance_verifier(root)
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("Acceptance status is not PASS", completed.stdout)

    def test_acceptance_secret_leak_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "acceptance"
            write_acceptance(root, secret=True)
            completed = self.run_acceptance_verifier(root)
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("Potential secret material", completed.stdout)

    def test_synthetic_final_delivery_passes(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            acceptance = base / "acceptance"
            delivery = base / "delivery"
            write_acceptance(acceptance)
            write_delivery(delivery, acceptance)
            completed = self.run_delivery_verifier(delivery)
            self.assertEqual(completed.returncode, 0, completed.stdout + completed.stderr)

    def test_tampered_release_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            acceptance = base / "acceptance"
            delivery = base / "delivery"
            write_acceptance(acceptance)
            write_delivery(delivery, acceptance)
            (delivery / "release/natureprotector-final.zip").write_bytes(b"tampered")
            completed = self.run_delivery_verifier(delivery)
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("hash", completed.stdout.lower())

    def test_wrong_delivery_gate_sequence_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            base = Path(folder)
            acceptance = base / "acceptance"
            delivery = base / "delivery"
            write_acceptance(acceptance)
            write_delivery(delivery, acceptance)
            with (delivery / "delivery-gates.csv").open(encoding="utf-8") as handle:
                rows = list(csv.DictReader(handle))
            rows[1], rows[2] = rows[2], rows[1]
            with (delivery / "delivery-gates.csv").open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=rows[0].keys())
                writer.writeheader()
                writer.writerows(rows)
            # Refresh manifests so this fails for the semantic gate order, not merely stale hashes.
            with (delivery / "delivery-manifest.csv").open("w", encoding="utf-8", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=["path", "sizeBytes", "sha256"])
                writer.writeheader()
                delivery_manifest = (delivery / "delivery-manifest.csv").resolve()
                hash_manifest = (delivery / "hashes.sha256").resolve()
                for path in sorted(delivery.rglob("*")):
                    if path.is_file() and path.resolve() not in {delivery_manifest, hash_manifest}:
                        writer.writerow(
                            {
                                "path": path.relative_to(delivery).as_posix(),
                                "sizeBytes": path.stat().st_size,
                                "sha256": digest(path),
                            }
                        )
            write_hash_manifest(delivery)
            completed = self.run_delivery_verifier(delivery)
            self.assertNotEqual(completed.returncode, 0)
            self.assertIn("gate sequence", completed.stdout.lower())


if __name__ == "__main__":
    unittest.main()
