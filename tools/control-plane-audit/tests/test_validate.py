from __future__ import annotations

import contextlib
import importlib.util
import io
import shutil
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "validate.py"
spec = importlib.util.spec_from_file_location("control_plane_validate", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)


class ControlPlaneDecompositionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.repo = Path(__file__).resolve().parents[3]

    def test_repository_contract_passes(self) -> None:
        self.assertEqual(0, self._run(self.repo))

    def test_slice_mutation_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "repo"
            self._copy_contract_inputs(repo)
            target = (
                repo
                / "src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.RunTimings.cs"
            )
            target.write_text(
                target.read_text(encoding="utf-8").replace("return null;", "return null; // mutation", 1),
                encoding="utf-8",
            )
            self.assertEqual(1, self._run(repo))

    def test_public_contract_mutation_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            repo = Path(temp) / "repo"
            self._copy_contract_inputs(repo)
            target = repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs"
            text = target.read_text(encoding="utf-8").replace(
                "\n}\n", "\n    public Task UnexpectedAsync() => Task.CompletedTask;\n}\n"
            )
            target.write_text(text, encoding="utf-8")
            self.assertEqual(1, self._run(repo))

    def _copy_contract_inputs(self, destination: Path) -> None:
        for relative in [
            "config/quality/control-plane-decomposition.json",
            "src/NatureProtector.Backoffice.Api/ControlPlane/Services/IControlPlaneService.cs",
        ]:
            source = self.repo / relative
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
        services = self.repo / "src/NatureProtector.Backoffice.Api/ControlPlane/Services"
        target_services = destination / "src/NatureProtector.Backoffice.Api/ControlPlane/Services"
        target_services.mkdir(parents=True, exist_ok=True)
        for source in services.glob("PostgresControlPlaneService*.cs"):
            shutil.copy2(source, target_services / source.name)

    @staticmethod
    def _run(repo: Path) -> int:
        import sys

        old = sys.argv
        try:
            sys.argv = [str(MODULE_PATH), "--repo", str(repo)]
            with contextlib.redirect_stdout(io.StringIO()):
                return module.main()
        finally:
            sys.argv = old


if __name__ == "__main__":
    unittest.main()
