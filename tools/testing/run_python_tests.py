#!/usr/bin/env python3
from __future__ import annotations
import argparse
import subprocess
import sys
from pathlib import Path

SUITES = [
    "tests/evidence",
    "tests/runtime",
    "tests/autoscaling",
    "tests/data",
    "tests/observability",
    "tests/local",
    "tests/operations",
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    a = ap.parse_args()
    failed = []
    for suite in SUITES:
        p = a.repo / suite
        if not p.is_dir():
            continue
        r = subprocess.run(
            [sys.executable, "-m", "unittest", "discover", "-s", str(p), "-p", "test_*.py", "-v"], cwd=a.repo
        )
        if r.returncode:
            failed.append(suite)
    print("PYTHON_TEST_AUTHORITY_PASS" if not failed else "PYTHON_TEST_AUTHORITY_FAIL " + ",".join(failed))
    return 0 if not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
