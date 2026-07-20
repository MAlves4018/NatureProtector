from __future__ import annotations

import importlib.util
import os
import sys
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/performance/run-http-workload.py"
spec = importlib.util.spec_from_file_location("run_http_workload", SCRIPT)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)


class RunHttpWorkloadTests(unittest.TestCase):
    def test_bearer_token_env_sets_authorization_without_persisting_token(self):
        args = module.parse_args(["--auth-required"])
        with patch.dict(os.environ, {"NP_PERFORMANCE_AUTH_TOKEN": "token-value"}, clear=False):
            headers, auth_mode = module.build_request_headers(args)

        self.assertEqual("bearer-env", auth_mode)
        self.assertEqual("Bearer token-value", headers["Authorization"])

    def test_auth_required_fails_without_credentials(self):
        args = module.parse_args(["--auth-required"])
        with patch.dict(
            os.environ,
            {
                "NP_PERFORMANCE_AUTH_TOKEN": "",
                "NP_PERFORMANCE_USERNAME": "",
                "NP_PERFORMANCE_PASSWORD": "",
            },
            clear=False,
        ):
            with self.assertRaises(RuntimeError):
                module.build_request_headers(args)


if __name__ == "__main__":
    unittest.main()
