from __future__ import annotations
import importlib.util,unittest
from pathlib import Path
REPO=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('verify',REPO/'scripts/autoscaling/verify-scaling-experiment.py');m=importlib.util.module_from_spec(spec);assert spec.loader;spec.loader.exec_module(m)
class ScalingVerifierAdversarialTests(unittest.TestCase):
 def test_absurd_constant_one_replica_matrix_is_rejected(self):
  rows=[{'experiment':f'S{i}','publisher_rate':'100','replicas':'1','processed_rate':'1','p95_ms':'999999','backlog_end':'999999','correctness_pass':'true'} for i in range(1,9)]
  self.assertTrue(m.validate(rows))
 def test_missing_experiments_are_rejected(self):
  rows=[{'experiment':'S1','publisher_rate':'1','replicas':'1','processed_rate':'1','p95_ms':'10','backlog_end':'0','correctness_pass':'true'}]
  self.assertTrue(m.validate(rows))
if __name__=='__main__':unittest.main()
