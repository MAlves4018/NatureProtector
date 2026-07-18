from __future__ import annotations
import importlib.util,tempfile,unittest
from pathlib import Path
REPO=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('ipma',REPO/'scripts/data/poll-ipma-observations.py');m=importlib.util.module_from_spec(spec);assert spec.loader;spec.loader.exec_module(m)
class CursorSafetyTests(unittest.TestCase):
 def test_atomic_state_round_trip(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t)/'cursor.json';m.atomic_write_json(p,{'watermarks':{'1':'2026-01-01T00:00:00Z'}})
   self.assertEqual(m.read_state(p)['watermarks']['1'],'2026-01-01T00:00:00Z')
   self.assertFalse((Path(t)/'cursor.json.tmp').exists())
 def test_corrupt_state_is_quarantined(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t)/'cursor.json';p.write_text('{bad')
   self.assertEqual(m.read_state(p),{'watermarks':{}})
   self.assertTrue(list(Path(t).glob('cursor.json.corrupt-*')))
if __name__=='__main__':unittest.main()
