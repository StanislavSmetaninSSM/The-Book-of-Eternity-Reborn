#!/usr/bin/env python3
"""Consumes actual C# RunAsync output; no synthetic substitute for serialized wire DATA."""
import json, pathlib, sys, unittest
import relay_phase_evidence as evidence
wire,request,kind=sys.argv[1:]
sys.argv=sys.argv[:1]
class ActualOutcomeWire(unittest.TestCase):
    def test_actual_serialized_outcome(self):
        def unique(pairs):
            result={}
            for key,value in pairs:
                if key in result:raise ValueError('Duplicate wire field')
                result[key]=value
            return result
        value=json.loads(pathlib.Path(wire).read_bytes(),object_pairs_hook=unique)
        actual=json.loads(pathlib.Path(request).read_bytes(),object_pairs_hook=unique)
        self.assertEqual(evidence.checked_mortal_outcome(value,actual),value)
        self.assertEqual(value['kind'],kind)
if __name__=='__main__':unittest.main(verbosity=2)
