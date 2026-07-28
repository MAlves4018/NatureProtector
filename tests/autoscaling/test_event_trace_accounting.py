from __future__ import annotations

import csv
import importlib.util
import tempfile
import unittest
from pathlib import Path


REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location(
    "event_trace", REPO / "scripts/autoscaling/reconcile-event-trace.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader
spec.loader.exec_module(module)


def write_stage(path: Path, event_ids: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["event_id"])
        writer.writeheader()
        for event in event_ids:
            writer.writerow({"event_id": event})


class EventTraceAccountingTests(unittest.TestCase):
    def test_zero_based_event_indexes_do_not_create_loss(self):
        event_ids = [f"00000000-0000-0000-0000-00000000000{i}" for i in range(5)]
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            for name in module.STAGE_FILES:
                write_stage(root / name, event_ids)

            summary = module.reconcile(root)

            self.assertEqual(5, summary["confirmed_distinct"])
            self.assertEqual(0, summary["event_loss"])
            self.assertTrue(summary["accounting_reconciled"])

    def test_real_gap_is_reported_by_event_id(self):
        confirmed = [
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
            "00000000-0000-0000-0000-000000000003",
        ]
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            for name in module.STAGE_FILES:
                write_stage(root / name, confirmed)
            write_stage(root / "final-effect-event-ids.csv", confirmed[:-1])

            summary = module.reconcile(root)

            self.assertEqual(1, summary["event_loss"])
            self.assertFalse(summary["accounting_reconciled"])
            missing = module.read_rows(root / "missing-event-ids.csv")
            self.assertEqual(confirmed[-1], missing[0]["event_id"])

    def test_duplicate_final_effect_is_rejected_even_without_loss(self):
        confirmed = [
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
        ]
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            for name in module.STAGE_FILES:
                write_stage(root / name, confirmed)
            write_stage(root / "final-effect-event-ids.csv", [confirmed[0], confirmed[1], confirmed[1]])

            summary = module.reconcile(root)

            self.assertEqual(0, summary["event_loss"])
            self.assertEqual(1, summary["unexpected_duplicate_effects"])
            self.assertFalse(summary["accounting_reconciled"])

    def test_missing_intermediate_stage_rejects_accounting_even_when_final_effect_exists(self):
        confirmed = [
            "00000000-0000-0000-0000-000000000001",
            "00000000-0000-0000-0000-000000000002",
        ]
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            for name in module.STAGE_FILES:
                write_stage(root / name, confirmed)
            write_stage(root / "processed-event-ids.csv", confirmed[:1])

            summary = module.reconcile(root)

            self.assertEqual(0, summary["event_loss"])
            self.assertEqual(1, summary["stage_missing_event_ids"]["processed-event-ids.csv"])
            self.assertFalse(summary["accounting_reconciled"])


if __name__ == "__main__":
    unittest.main()
