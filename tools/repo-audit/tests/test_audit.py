from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
import sys
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "audit.py"
SPEC = importlib.util.spec_from_file_location("repo_audit", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
repo_audit = importlib.util.module_from_spec(SPEC)
sys.modules["repo_audit"] = repo_audit
SPEC.loader.exec_module(repo_audit)


class RepositoryAuditTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        (self.repo / "scripts").mkdir()
        (self.repo / ".github" / "workflows").mkdir(parents=True)
        (self.repo / "deploy" / "environments").mkdir(parents=True)
        (self.repo / "webUI").mkdir()
        (self.repo / "deploy" / "environments" / "common.json").write_text(
            json.dumps(
                {
                    "project_id": "example-project",
                    "region": "example-region",
                    "artifact_repository": "example-repository",
                    "terraform": {"backend": {"bucket_variable": "TF_STATE_BUCKET"}},
                }
            ),
            encoding="utf-8",
        )
        (self.repo / "global.json").write_text(json.dumps({"sdk": {"version": "9.0.100"}}), encoding="utf-8")
        (self.repo / "webUI" / ".nvmrc").write_text("22.0.0\n", encoding="utf-8")
        self.config_path = Path(__file__).resolve().parents[1] / "audit-config.json"
        self.config = repo_audit.read_json(self.config_path)
        self.config_digest = repo_audit.sha256_file(self.config_path)

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def test_exact_duplicates_and_script_references_are_recorded(self) -> None:
        script = self.repo / "scripts" / "check.py"
        script.write_text("print('same')\n", encoding="utf-8")
        (self.repo / "scripts" / "copy.py").write_text("print('same')\n", encoding="utf-8")
        (self.repo / ".github" / "workflows" / "ci.yml").write_text("run: python scripts/check.py\n", encoding="utf-8")

        model = repo_audit.build_model(self.repo, self.config, self.config_digest)

        duplicate_paths = [set(group["paths"]) for group in model["duplicate_groups"]]
        self.assertIn({"scripts/check.py", "scripts/copy.py"}, duplicate_paths)
        scripts = {item["path"]: item for item in model["script_inventory"]}
        self.assertEqual("WORKFLOW_REFERENCED", scripts["scripts/check.py"]["status"])
        self.assertEqual("NO_STATIC_REFERENCE_FOUND", scripts["scripts/copy.py"]["status"])

    def test_python_imports_are_recorded_as_script_references(self) -> None:
        helper = self.repo / "scripts" / "helper_module.py"
        helper.write_text("VALUE = 1\n", encoding="utf-8")
        consumer = self.repo / "scripts" / "consumer.py"
        consumer.write_text("from helper_module import VALUE\nprint(VALUE)\n", encoding="utf-8")

        model = repo_audit.build_model(self.repo, self.config, self.config_digest)
        scripts = {item["path"]: item for item in model["script_inventory"]}
        references = [
            item for item in model["script_references"] if item["reference_path"] == "scripts/helper_module.py"
        ]

        self.assertEqual("AUTOMATION_REFERENCED", scripts["scripts/helper_module.py"]["status"])
        self.assertTrue(any(item["match_kind"] == "python-import" for item in references))

    def test_environment_values_are_not_read_or_emitted(self) -> None:
        (self.repo / ".env.example").write_text("KNOWN_VALUE=example\n", encoding="utf-8")
        (self.repo / "scripts" / "env.py").write_text(
            "import os\nprint(os.getenv('KNOWN_VALUE'))\nprint(os.getenv('SECRET_VALUE'))\n",
            encoding="utf-8",
        )

        model = repo_audit.build_model(self.repo, self.config, self.config_digest)
        variables = {item["variable"]: item for item in model["environment_variables"]}

        self.assertEqual(1, variables["KNOWN_VALUE"]["definition_count"])
        self.assertEqual(0, variables["SECRET_VALUE"]["definition_count"])
        serialized = repo_audit.stable_json(model["environment_variables"])
        self.assertNotIn("example\n", serialized)

    def test_model_is_deterministic(self) -> None:
        (self.repo / "scripts" / "a.sh").write_text("#!/bin/sh\necho ok\n", encoding="utf-8")
        first = repo_audit.build_model(self.repo, self.config, self.config_digest)
        second = repo_audit.build_model(self.repo, self.config, self.config_digest)
        self.assertEqual(repo_audit.stable_json(first), repo_audit.stable_json(second))

    def test_generated_and_dataset_categories_are_explicit(self) -> None:
        migration = self.repo / "src" / "Example" / "Migrations" / "One.cs"
        migration.parent.mkdir(parents=True)
        migration.write_text("class One {}\n", encoding="utf-8")
        dataset = self.repo / "data" / "baseline" / "sample.csv"
        dataset.parent.mkdir(parents=True)
        dataset.write_text("x,y\n1,2\n", encoding="utf-8")

        model = repo_audit.build_model(self.repo, self.config, self.config_digest)
        categories = {item["path"]: item["category"] for item in model["files"]}

        self.assertEqual("generated", categories["src/Example/Migrations/One.cs"])
        self.assertEqual("dataset", categories["data/baseline/sample.csv"])


if __name__ == "__main__":
    unittest.main()
