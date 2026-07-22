import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))

from scripts.operations.consolidate_test_results import (  # noqa: E402
    consolidate,
    parse_junit_xml,
    parse_trx_xml,
)


def write_xml(path: Path, content: str):
    path.write_text(content, encoding="utf-8")


class TestConsolidateTestResults(unittest.TestCase):
    def test_parse_junit_simple(self):
        xml = """<?xml version="1.0" encoding="UTF-8"?>
        <testsuite name="test" tests="5" failures="1" errors="0" skipped="0">
        </testsuite>"""
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "test.xml"
            write_xml(p, xml)
            result = parse_junit_xml(p)
        self.assertEqual(result, {"tests": 5, "passed": 4, "failed": 1, "skipped": 0})

    def test_parse_junit_with_skipped(self):
        xml = """<?xml version="1.0" encoding="UTF-8"?>
        <testsuite name="test" tests="10" failures="2" errors="1" skipped="3">
        </testsuite>"""
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "test.xml"
            write_xml(p, xml)
            result = parse_junit_xml(p)
        self.assertEqual(result, {"tests": 10, "passed": 4, "failed": 3, "skipped": 3})

    def test_parse_junit_failure_and_error(self):
        xml = """<?xml version="1.0" encoding="UTF-8"?>
        <testsuite name="test" tests="3" failures="1" errors="1" skipped="0">
        </testsuite>"""
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "test.xml"
            write_xml(p, xml)
            result = parse_junit_xml(p)
        self.assertEqual(result, {"tests": 3, "passed": 1, "failed": 2, "skipped": 0})

    def test_parse_junit_invalid_xml(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "invalid.xml"
            write_xml(p, "not xml")
            result = parse_junit_xml(p)
        self.assertIsNone(result)

    def test_parse_junit_empty_file(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "empty.xml"
            write_xml(p, "")
            result = parse_junit_xml(p)
        self.assertIsNone(result)

    def test_parse_trx_namespaced(self):
        xml = """<?xml version="1.0" encoding="UTF-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions />
          <Results>
            <UnitTestResult testName="t1" outcome="Passed" />
            <UnitTestResult testName="t2" outcome="Failed" />
            <UnitTestResult testName="t3" outcome="Skipped" />
          </Results>
        </TestRun>"""
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "test.trx"
            write_xml(p, xml)
            result = parse_trx_xml(p)
        self.assertEqual(result, {"tests": 3, "passed": 1, "failed": 1, "skipped": 1})

    def test_consolidate_multiple_jobs(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            (root_path / "job1").mkdir()
            write_xml(
                root_path / "job1" / "result.xml",
                """<?xml version="1.0"?>
                <testsuite name="a" tests="5" failures="1" errors="0" skipped="0"/>
                """,
            )
            (root_path / "job2").mkdir()
            write_xml(
                root_path / "job2" / "result.xml",
                """<?xml version="1.0"?>
                <testsuite name="b" tests="3" failures="0" errors="0" skipped="1"/>
                """,
            )
            result = consolidate(root_path)
        self.assertEqual(result["totals"]["tests"], 8)
        self.assertEqual(result["totals"]["passed"], 6)
        self.assertEqual(result["totals"]["failed"], 1)
        self.assertEqual(result["totals"]["skipped"], 1)
        self.assertEqual(len(result["jobs"]), 2)
        self.assertEqual(len(result["filesRead"]), 2)

    def test_consolidate_job_without_xml(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            (root_path / "job1").mkdir()
            (root_path / "job1" / "readme.txt").write_text("hello", encoding="utf-8")
            result = consolidate(root_path)
        self.assertEqual(result["totals"]["tests"], 0)
        self.assertEqual(len(result["warnings"]), 1)
        self.assertIn("no XML files", result["warnings"][0])

    def test_consolidate_empty_directory(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            result = consolidate(root_path)
        self.assertEqual(result["totals"]["tests"], 0)
        self.assertEqual(result["schemaVersion"], 1)

    def test_consolidate_missing_directory(self):
        result = consolidate(Path("/nonexistent/path"))
        self.assertEqual(result["totals"]["tests"], 0)
        self.assertEqual(len(result["warnings"]), 1)

    def test_consolidate_duplicate_files_same_job(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            (root_path / "job1").mkdir()
            write_xml(
                root_path / "job1" / "a.xml",
                """<?xml version="1.0"?>
                <testsuite name="a" tests="2" failures="0" errors="0" skipped="0"/>
                """,
            )
            write_xml(
                root_path / "job1" / "b.xml",
                """<?xml version="1.0"?>
                <testsuite name="b" tests="3" failures="1" errors="0" skipped="0"/>
                """,
            )
            result = consolidate(root_path)
        self.assertEqual(result["totals"]["tests"], 5)
        self.assertEqual(result["totals"]["passed"], 4)
        self.assertEqual(result["totals"]["failed"], 1)

    def test_output_json_structure(self):
        with tempfile.TemporaryDirectory() as root:
            root_path = Path(root)
            (root_path / "job1").mkdir()
            write_xml(
                root_path / "job1" / "result.xml",
                """<?xml version="1.0"?>
                <testsuite name="a" tests="1" failures="0" errors="0" skipped="0"/>
                """,
            )
            result = consolidate(root_path)
        self.assertIn("schemaVersion", result)
        self.assertIn("jobs", result)
        self.assertIn("totals", result)
        self.assertIn("warnings", result)
        self.assertIn("filesRead", result)
        self.assertIn("filesIgnored", result)
