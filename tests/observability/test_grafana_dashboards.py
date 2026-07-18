import json
import unittest
from pathlib import Path


class GrafanaDashboardTests(unittest.TestCase):
    def test_provisioned_dashboards_have_datasources_and_queries(self) -> None:
        root = Path(__file__).resolve().parents[2]
        dashboard_dir = root / "infra" / "grafana" / "dashboards"
        dashboards = sorted(dashboard_dir.glob("natureprotector-*.json"))

        self.assertGreaterEqual(len(dashboards), 4)
        dashboards_with_queries = 0
        for dashboard in dashboards:
            payload = json.loads(dashboard.read_text(encoding="utf-8"))
            panels = payload.get("panels") or []
            self.assertTrue(payload.get("uid"), dashboard.name)
            self.assertTrue(payload.get("title"), dashboard.name)
            self.assertGreater(len(panels), 0, dashboard.name)
            dashboards_with_queries += int(any(_panel_has_query(panel) for panel in panels))

        self.assertGreaterEqual(dashboards_with_queries, 4)


def _panel_has_query(panel: dict) -> bool:
    for target in panel.get("targets") or []:
        if target.get("rawSql") or target.get("url_options"):
            return True
    return False


if __name__ == "__main__":
    unittest.main()
