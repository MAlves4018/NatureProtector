from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FINAL_DIR = ROOT / "scripts/evidence/final"


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(path.parent))
    try:
        spec.loader.exec_module(module)
    finally:
        sys.path.pop(0)
    return module


collector = load("collect_final_execution", FINAL_DIR / "collect_final_execution.py")
common = load("final_common", FINAL_DIR / "final_common.py")


class FinalExecutionPhaseTests(unittest.TestCase):
    def _collect_full(self, temp_root: Path, phase8_status: str = "PASS_COMPLETE_REPORT_PACKAGE"):
        repo = temp_root / "repo"
        repo.mkdir()
        phase8 = temp_root / "phase8"
        portfolio = temp_root / "portfolio"
        long_run = temp_root / "long-run"
        screenshots = temp_root / "screenshots"
        for path in (phase8, portfolio, long_run, screenshots / "manual-captures"):
            path.mkdir(parents=True)
        (phase8 / "campaign-summary.json").write_text(
            json.dumps({"status": phase8_status}), encoding="utf-8"
        )
        (portfolio / "verdict.json").write_text(
            json.dumps({"status": "REPORT_EVIDENCE_PORTFOLIO_READY"}), encoding="utf-8"
        )
        (long_run / "summary.json").write_text(
            json.dumps({"status": "LONG_RUN_STABILITY_PASS"}), encoding="utf-8"
        )
        (screenshots / "manual-captures/capture-register.json").write_text(
            json.dumps([{"captureId": "one"}, {"captureId": "two"}]), encoding="utf-8"
        )
        ledger = temp_root / "command-ledger.csv"
        ledger.write_text("stage,status\nall-required-stages,PASS\n", encoding="utf-8")
        output = repo / "artifacts/report-evidence/baseline-x/13-final-execution/run-x"
        result = subprocess.run(
            [
                sys.executable,
                str(FINAL_DIR / "collect_final_execution.py"),
                "--repo", str(repo),
                "--baseline-id", "baseline-x",
                "--run-id", "run-x",
                "--mode", "full",
                "--output", str(output),
                "--phase8-root", str(phase8),
                "--portfolio-root", str(portfolio),
                "--long-run-root", str(long_run),
                "--screenshots-root", str(screenshots),
                "--command-ledger", str(ledger),
            ],
            text=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        return result, output

    def test_config_reuses_existing_evidence_entrypoints(self):
        config = json.loads((ROOT / "config/evidence/final-execution.json").read_text(encoding="utf-8"))
        self.assertEqual("phase13", config["phaseId"])
        for relative in config["canonicalInputs"].values():
            self.assertTrue((ROOT / relative).exists(), relative)
        self.assertNotIn("scripts/evidence-final", json.dumps(config))

    def test_status_normalization_never_promotes_blocked(self):
        self.assertEqual("BLOCKED", collector.normalize_status("NOT_EXECUTED"))
        self.assertEqual("BLOCKED", collector.normalize_status("ENVIRONMENT_BLOCKED"))
        self.assertEqual("FAIL", collector.normalize_status("FAIL"))
        self.assertEqual("LIMITED", collector.normalize_status("PASS_WITH_LIMITATIONS"))
        self.assertEqual("INCONCLUSIVE", collector.normalize_status("PASS_UNKNOWN_STATUS"))

    def test_hash_manifest_detects_extra_file(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "a.txt").write_text("a", encoding="utf-8")
            common.write_hash_manifest(root)
            self.assertEqual([], common.verify_hash_manifest(root))
            (root / "b.txt").write_text("b", encoding="utf-8")
            self.assertTrue(any("Unlisted file" in item for item in common.verify_hash_manifest(root)))

    def test_plan_collection_is_explicitly_incomplete(self):
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            repo = temp_root / "repo"
            repo.mkdir()
            output = repo / "artifacts/report-evidence/baseline-x/13-final-execution/run-x"
            ledger = temp_root / "ledger.csv"
            ledger.write_text("stage,status\nplan,PLANNED\n", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(FINAL_DIR / "collect_final_execution.py"),
                    "--repo", str(repo),
                    "--baseline-id", "baseline-x",
                    "--run-id", "run-x",
                    "--mode", "plan",
                    "--output", str(output),
                    "--command-ledger", str(ledger),
                ],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            self.assertEqual(0, result.returncode, result.stderr)
            summary = json.loads((output / "phase13-summary.json").read_text(encoding="utf-8"))
            self.assertEqual("PLAN_READY_EVIDENCE_INCOMPLETE", summary["status"])
            self.assertNotEqual("CURRENT_EXECUTION", summary["evidenceClass"])

    def test_failed_collection_preserves_orchestration_logs_and_failure_index(self):
        with tempfile.TemporaryDirectory() as temp:
            temp_root = Path(temp)
            repo = temp_root / "repo"
            repo.mkdir()
            phase8 = repo / "artifacts/report-evidence/baseline-x/08-campaign/run-x"
            phase8.mkdir(parents=True)
            (phase8 / "campaign-summary.json").write_text(
                json.dumps({"status": "PARTIAL"}), encoding="utf-8"
            )
            work = repo / "artifacts/evidence-orchestration/baseline-x/run-x"
            (work / "logs").mkdir(parents=True)
            (work / "states").mkdir()
            (work / "logs/phase8.stderr.log").write_text("", encoding="utf-8")
            (work / "logs/phase8.stdout.log").write_text("PHASE_8_STATUS=PARTIAL\n", encoding="utf-8")
            ledger = work / "command-ledger.csv"
            ledger.write_text(
                "stage,status,exitCode,limitation,stdout,stderr\n"
                "phase8-campaign,FAIL,1,Exit code 1,logs/phase8.stdout.log,logs/phase8.stderr.log\n",
                encoding="utf-8",
            )
            output = repo / "artifacts/report-evidence/baseline-x/13-final-execution/run-x"
            result = subprocess.run(
                [
                    sys.executable,
                    str(FINAL_DIR / "collect_final_execution.py"),
                    "--repo", str(repo),
                    "--baseline-id", "baseline-x",
                    "--run-id", "run-x",
                    "--mode", "quick",
                    "--output", str(output),
                    "--phase8-root", str(phase8),
                    "--command-ledger", str(ledger),
                ],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            self.assertEqual(1, result.returncode)
            self.assertTrue((output / "failures.csv").is_file())
            self.assertTrue((output / "orchestration/logs/phase8.stdout.log").is_file())
            self.assertTrue((output / "evidence-index.csv").is_file())
            self.assertTrue((output / "SHA256SUMS.txt").is_file())

    def test_clean_full_execution_is_the_only_strict_live_pass(self):
        with tempfile.TemporaryDirectory() as temp:
            result, output = self._collect_full(Path(temp))
            self.assertEqual(0, result.returncode, result.stderr)
            summary = json.loads((output / "phase13-summary.json").read_text(encoding="utf-8"))
            self.assertEqual("PASS", summary["status"])

            verified = subprocess.run(
                [
                    sys.executable,
                    str(FINAL_DIR / "verify_final_execution.py"),
                    str(output),
                    "--require-live",
                ],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            self.assertEqual(0, verified.returncode, verified.stdout + verified.stderr)

    def test_verifier_rejects_failed_command_hidden_by_rehashed_pass_summary(self):
        with tempfile.TemporaryDirectory() as temp:
            result, output = self._collect_full(Path(temp))
            self.assertEqual(0, result.returncode, result.stderr)
            summary_path = output / "phase13-summary.json"
            summary = json.loads(summary_path.read_text(encoding="utf-8"))
            summary["failedCommands"] = ["hidden-failure"]
            summary["commandStatusCounts"] = {"FAIL": 1}
            summary_path.write_text(json.dumps(summary), encoding="utf-8")
            common.write_hash_manifest(output)

            verified = subprocess.run(
                [sys.executable, str(FINAL_DIR / "verify_final_execution.py"), str(output)],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            self.assertEqual(1, verified.returncode)
            self.assertIn("failed commands", verified.stdout)

    def test_limited_source_cannot_satisfy_strict_live_gate(self):
        with tempfile.TemporaryDirectory() as temp:
            result, output = self._collect_full(Path(temp), "PASS_WITH_LIMITATIONS")
            self.assertEqual(0, result.returncode, result.stderr)
            summary = json.loads((output / "phase13-summary.json").read_text(encoding="utf-8"))
            self.assertEqual("PASS_WITH_LIMITATIONS", summary["status"])
            self.assertEqual("LIMITED", summary["inputs"][0]["normalizedStatus"])

            verified = subprocess.run(
                [
                    sys.executable,
                    str(FINAL_DIR / "verify_final_execution.py"),
                    str(output),
                    "--require-live",
                ],
                text=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
            self.assertEqual(1, verified.returncode)
            self.assertIn("Strict live Phase 13 gate not satisfied", verified.stdout)


if __name__ == "__main__":
    unittest.main()
