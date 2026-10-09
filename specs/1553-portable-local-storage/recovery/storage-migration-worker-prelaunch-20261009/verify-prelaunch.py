from pathlib import Path
import json, base64, hashlib, struct, subprocess, sys, xml.etree.ElementTree as E

phase, prefix, sha = sys.argv[1:]
green = phase.startswith('green')
r = Path.cwd()
p, = (r / 'TestResults/test-categories').glob(prefix + '*')
s = json.loads((p / 'summary.json').read_text())
assert s['Revision']['Head'] == sha and s['Revision']['ChangedFiles'] == 0
assert s['OwnedTreeCleanupSucceeded'] and s['RuntimeCleanupSucceeded']
assert not s['TimedOut'] and not s['DuplicateTests'] and s['Selection']['Complete']
assert s['Tests'] == {'Total': 5, 'Executed': 5, 'Passed': 5 if green else 2, 'Failed': 0 if green else 3}
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
rows, cleanup, native = [], [], []
def b64(value): return base64.b64decode(value)
def decode(value): return json.loads(b64(value).decode('utf-8-sig'))
def digest(value): return hashlib.sha256(value).hexdigest()
for trx in p.glob('*.trx'):
    for test in E.parse(trx).findall('.//t:UnitTestResult', ns):
        for line in test.findtext('t:Output/t:StdOut', default='', namespaces=ns).splitlines():
            try: d = json.loads(line)
            except (ValueError, TypeError): continue
            if not isinstance(d, dict): continue
            if 'CleanupOwnedRoot' in d:
                assert d['OwnedFixtureRemoved'] and d['CleanupFailure'] is None and not Path(d['CleanupOwnedRoot']).exists()
                cleanup.append(d['CleanupOwnedRoot'])
            if 'Scenario' not in d: continue
            w, g = d['Scenario']['WorkerStorage'], d['Guardian']
            mode, c = w['mode'], w['Cut']
            unknown = mode.endswith('_unknown')
            expected_pass = green or not unknown
            assert test.get('outcome') == ('Passed' if expected_pass else 'Failed')
            assert g['echild'] and not g['emergencySignals'] and not g['failures'] and not g['deadline']
            assert g['driverExitCode'] == (0 if expected_pass else 1)
            assert d['Scenario']['OriginalWorkerMainRetired'] and w['DispatchSettled']
            assert w['AdmissionCloseFailure'] is None and w['MainStopFailure'] is None
            assert w['factories'] == 1 and not w['forbidden']
            assert w['PinCountAfter'] == w['EntryCount'] == w['OwnedCapacity'] == 0 and not w['SlotRetained']
            assert w['prior'] == w['afterImages']
            generation, = (decode(v)['GenerationId'] for k, v in w['prior'].items() if k.endswith('/session-generation/current.json'))
            assert generation == w['currentGeneration']
            default = mode == 'public_linux_refusal'
            assert w['reservationEntries'] == w['slotEntries'] == (0 if default else 1)
            if not default: assert w['pinHeldAtReservation'] and w['capacityHeldAtReservation'] and w['slotHeldAtReservation']
            if unknown:
                assert c['Cuts'] == 1 and c['Index'] == 0 and c['ActualUncertainty']
                journal = b64(c['JournalAtCut'])
                assert journal == b64(c['RetainedJournal']) and digest(journal).upper() == c['JournalSha256']
                assert b64(c['RetainedTarget']) == b'foreign-worker-publication-image'
                metadata = json.loads(journal[16:16+struct.unpack('<q', journal[8:16])[0]] if journal.startswith(b'BOELP2\r\n') else journal)
                assert not metadata['Committed']
                member = metadata['Members'][0]
                assert member['Path'] == c['Target'] and member['After']['Exists']
                assert digest(b64(c['PublishedBytes'])).lower() == member['After']['Sha256'].lower()
                assert c['PriorAtCut'] == c['AfterImages']
                assert not c['LaterReads'] and not c['LaterMutations'] and c['LaterRecovery'] == c['LaterPublications'] == 0 and not c['PendingClosingLease']
                assert c['LaterLeases'] == (0 if green or mode == 'known_diagnostic_unknown' else 1)
                if mode != 'known_diagnostic_unknown':
                    task = decode(w['selectedTaskBytes'])
                    assert task['sessionGeneration'] == generation and task['workerId'] == w['selectedTask']['WorkerId']
                    assert task['sourceTurn'] == {'sessionId': 'prelaunch-storage', 'requestId': 'original-worker-request', 'turnNumber': 7}
                    context = task['contextFiles'][0]
                    weather, = (v for k, v in w['prior'].items() if k.endswith('/' + context['path']))
                    assert digest(b64(weather)) == context['sha256']
                    if mode == 'reservation_unknown': assert c['PublishedBytes'] == w['selectedTaskBytes']
                    else:
                        assert w['reservationCommittedAtCut']
                        task_bytes, = (v for k, v in c['PriorAtCut'].items() if k.endswith('/' + task['taskId'] + '/task.json'))
                        assert task_bytes == w['selectedTaskBytes']
                        audit = decode(c['PublishedBytes'])
                        assert audit['eventType'] == 'task-dispatched' and audit['taskId'] == task['taskId'] and audit['workerId'] == task['workerId']
                else:
                    audit = decode(c['PublishedBytes'])
                    assert w['knownHits'] == 1 and audit['eventType'] == 'task-failed' and audit['taskId'] and audit['workerId'] == 'analysis_codex'
                    assert 'known original task reservation refusal' in audit['summary']
                assert w['SameOriginalUncertainty'] == green
                assert w['OriginalCauseRetained'] == (green and mode == 'known_diagnostic_unknown')
                if green: assert w['Failure'] and 'CoordinatedStatePublicationUncertainException' in w['Failure'] and not w['ResponseReturned'] and w['result'] is None
                else: assert w['Failure'] is None and w['ResponseReturned'] and w['Outcome'] == 'WorkerFailed'
            else:
                assert c['Cuts'] == 0 and c['ActualUncertainty'] is None and c['RetainedJournal'] is None
                assert w['Failure'] is None and w['ResponseReturned'] and w['Outcome'] == 'WorkerFailed'
                # Historical raw null/null ReferenceEquals is deliberately not an uncertainty witness.
                if default: assert w['result']['FallbackReason'] == w['ExpectedBackendRefusal'] and w['knownHits'] == 0
                else:
                    assert w['knownHits'] == 1 and w['AuditAfter'] == w['AuditCommitted'] and w['AuditAfter']
                    audit = decode(w['AuditAfter'])
                    assert audit['eventType'] == 'task-failed' and audit['taskId'] == w['result']['TaskId'] and audit['workerId'] == w['result']['WorkerId']
                    assert audit['summary'] == w['result']['FallbackReason'] == 'known original task reservation refusal'
            folder = Path(d['FixtureFolder'])
            provenance = json.loads((folder / 'build-provenance.json').read_text())
            assert len(provenance['assets']) == 3
            for a in provenance['assets']:
                assert digest(subprocess.check_output(['git', 'show', sha + ':' + a['source']])) == a['sourceSha256']
                assert digest((folder / a['binary']).read_bytes()) == a['binarySha256']
            native.append(str(folder))
            rows.append({'mode': mode, 'outcome': test.get('outcome'), 'cuts': c['Cuts'], 'laterOrdinaryAdmissions': c['LaterLeases'], 'sameOriginalUncertainty': w['SameOriginalUncertainty'] if unknown else None, 'causeRetained': w['OriginalCauseRetained'], 'guardian': g})
assert len(rows) == len(cleanup) == len(native) == 5
assert {x['mode'] for x in rows} == {'reservation_unknown', 'dispatch_unknown', 'known_diagnostic_unknown', 'known_reservation', 'public_linux_refusal'}
receipt = {'source': sha, 'actual': p.name, 'tests': s['Tests'], 'rows': rows, 'removedRoots': cleanup, 'nativeFolders': native, 'scope': 'Original Bridge/prelaunch only; 3 genuine journal cuts and 2 known controls. No worker launch, durable Store/ACK, Windows or actual secondary lease-close fault qualification.'}
Path('/tmp/boe-1553-prelaunch-root-' + phase + '.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')
print('Verified original Bridge prelaunch:', s['Tests'], '3 authentic cuts, 5 ownership/guardian cleanups, 15 native source/binary pins')
