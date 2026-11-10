#!/usr/bin/env python3
"""Read only frozen independent evidence; no live proc, fixture, or native actions."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

BASE = Path('/tmp/1536-cloud-tools/review')
def ref(p):
    b = p.read_bytes()
    return {'path': str(p), 'bytes': len(b), 'sha256': hashlib.sha256(b).hexdigest()}
def read(name, expected):
    p = BASE / name
    r = ref(p)
    assert r['sha256'] == expected, r
    return json.loads(p.read_text()), r
def utc(s):
    return datetime.fromisoformat(s)

o, oref = read('t179pd-ni-iobs-l01-diagnostic2-outcome-independent-check-d65035c3-20261018.json',
              'c5a482e9f9cfc6fe4f39f922788320ed218a5d8d50bf490b7101fd624430a114')
a, aref = read('t179pd-ni-iobs-l01-diagnostic2-accounting-scope-corrected-independent-check-d65035c3-20261018.json',
              '74a2957d9a9155b27e6da8979b3742343bad166bbabe953926b041abb1325621')
assert not o['findings'] and not a['findings'] and o['nativeExit'] == a['nativeExit'] == 1
assert o['qualifiedBeforeAndAfter'] == a['qualifiedRemains'] == 52
resumer, = [x for x in a['identities'] if x['pid'] == 68412 and x['startTick'] == 2979925]
diag, = o['streamedDiagnostics']
assert diag['identity'] == {'pid': 68412, 'birthTick': 2979925}
last = diag['events'][-1]
assert (last['Stage'], last['Detail'], last['ElapsedMilliseconds']) == (
    'finalization-return', 'MortalJointCustodyCaptureCompletedBeforePreparation', 15304)
contained = [x for x in resumer['intervals'] if utc(x['startUtc']) >= utc(last['Utc'])]
assert contained and all(x['classificationEligible'] for x in contained)
for l, r in zip(contained, contained[1:]):
    assert l['lastScan'] == r['firstScan'] and l['endUtc'] == r['startUtc']
cpu = sum(x['cpuSeconds'] for x in contained)
wall = sum(x['wallSeconds'] for x in contained)
io = {k: sum(x['ioDeltas'][k] for x in contained) for k in contained[0]['ioDeltas']}
identity_summaries = []
for x in a['identities']:
    eligible = [w for w in x['windows'] if w['classificationEligible']]
    assert all(not m['assessment']['established'] for m in x['membershipMappings'])
    identity_summaries.append({
        'pid': x['pid'], 'startTick': x['startTick'], 'role': x['binding']['role'],
        'samples': x['samples'], 'endpointTotals': x['endpointTotals'],
        'intervals': len(x['intervals']),
        'eligibleIntervals': sum(i['classificationEligible'] for i in x['intervals']),
        'maxEndpointSpacingSeconds': max(i['wallSeconds'] for i in x['intervals']),
        'windowsAtLeast30Seconds': len(x['windows']), 'eligibleWindows': len(eligible),
        'activeWindows': sum(w['meetsActiveCpuHeuristic'] for w in eligible),
        'lowCpuWindows': sum(w['meetsLowCpuHeuristic'] for w in eligible),
        'eligibleAverageCoresRange': [min(w['averageCores'] for w in eligible), max(w['averageCores'] for w in eligible)],
        'taskStateSnapshots': x['taskStateSnapshots'], 'taskWchanSnapshots': x['taskWchanSnapshots'],
        'membershipMappings': x['membershipMappings']})
cg = a['cgroupIntervals']
cgtotals = {group: {key: sum(x['deltas'][group][key] for x in cg)
                   for key in cg[0]['deltas'][group]}
            for group in cg[0]['deltas']}
memory = [v for x in cg for v in x['memoryCurrent']]
out = {
    'kind': 'INDEPENDENT_FROZEN_DIAGNOSTIC2_FINITE_ACCOUNTING_SUPPLEMENT',
    'checkedUtc': datetime.now(timezone.utc).isoformat(),
    'reader': ref(Path(__file__)), 'inputs': [oref, aref], 'carrier': o['carrier'],
    'sourcePinDigest': o['pinDigest'], 'recordSha256': o['recordSha256'],
    'identities': identity_summaries,
    'lastCustodyToLastSample': {
        'lastScalar': last, 'lastSampleUtc': resumer['lastUtc'],
        'eventToLastSampleUtcSeconds': (utc(resumer['lastUtc']) - utc(last['Utc'])).total_seconds(),
        'firstIncludedEndpointUtc': contained[0]['startUtc'],
        'lastIncludedEndpointUtc': contained[-1]['endUtc'],
        'whollyContainedConsecutiveEligibleIntervals': len(contained),
        'endpointWallSeconds': wall, 'cpuSeconds': cpu, 'averageCores': cpu / wall,
        'ioDeltas': io,
        'limits': ['Unclosed trace interval: last sample is not a returned stage or completion.',
                   'Only wholly contained, consecutive, attributed, gap-free intervals counted; no edge extrapolation.',
                   'CPU and syscall traffic do not establish useful progress or identify managed work/lock ownership.']},
    'sampledSharedCgroup': {
        'environment': a['environment']['values'], 'intervals': len(cg),
        'eligibleIntervals': sum(x['classificationEligible'] for x in cg),
        'allEndpointDeltasMayBridgeIneligibleIntervals': cgtotals,
        'memoryCurrentMinMaxBytes': [min(memory), max(memory)],
        'targetGovernanceEstablished': False,
        'limits': ['Raw mount root /.. is namespace-relative/ambiguous; no normalization into proven target governance.',
                   'Aggregate shared-node counters neither attribute child CPU nor identify resource exhaustion.',
                   'No subtraction of throttle time from child wall time; no extrapolated completion bound.']},
    'scanOverhead': a['scanOverhead'], 'profileTotalElapsedSeconds': a['profileFinish']['elapsedSeconds'],
    'rawScanGaps': len(a['allRawScanGaps']), 'headerGaps': len(a['headerGaps']),
    'findings': [], 'coverageRemains': 52, 'runtimeRelease': False}
p = BASE / 't179pd-ni-iobs-l01-diagnostic2-joint-metrics-independent-check-20261018.json'
assert not p.exists(), 'Preserve existing independent derived output'
p.write_text(json.dumps(out, indent=2) + '\n')
print(json.dumps({'output': ref(p), 'lastCustodyToLastSample': out['lastCustodyToLastSample'],
                  'findings': out['findings']}, indent=2))
