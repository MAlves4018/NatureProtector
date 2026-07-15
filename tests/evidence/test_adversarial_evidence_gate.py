from __future__ import annotations
import importlib.util,json,tempfile,unittest
from pathlib import Path
REPO=Path(__file__).resolve().parents[2]

def load(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);assert spec.loader;spec.loader.exec_module(m);return m

class AdversarialEvidenceGateTests(unittest.TestCase):
 def test_nominal_empty_accounting_is_rejected(self):
  m=load('proof',REPO/'scripts/evidence/proof_contracts.py')
  result=m.claim_assertions('E1',{'id':'x','kind':'api-run'}, {'state':'SystemCompleted','simulationRunId':'r','accounting':{'settled':True}}, {'id':'r'},{'cycle':'x'},{'duration':1})
  self.assertFalse(result['passed'])
  self.assertTrue(any('expected/accepted' in e for e in result['errors']))
 def test_live_case_requires_nonempty_domain_artifacts(self):
  m=load('proof2',REPO/'scripts/evidence/proof_contracts.py')
  with tempfile.TemporaryDirectory() as t:
   root=Path(t)
   for rel in m.REQUIRED_CASE_FILES['api-run']:
    p=root/rel;p.parent.mkdir(parents=True,exist_ok=True);p.write_text('{}')
   errors=m.validate_case_tree(root,'api-run')
   self.assertTrue(any('missing or empty rabbitmq/queue-metrics.json' in e for e in errors))

if __name__=='__main__':unittest.main()
