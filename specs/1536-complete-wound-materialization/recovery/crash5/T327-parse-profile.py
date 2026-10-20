from pathlib import Path
from collections import defaultdict
import json,hashlib,zipfile,xml.etree.ElementTree as ET,sys

def analyze(evidence):
    load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
    outcome=load(evidence/'T327-profile-outcome.json')
    sha=lambda b:hashlib.sha256(b).hexdigest().upper()
    archive=evidence/outcome['Archive'];assert sha(archive.read_bytes())==outcome['SHA256']
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None and len(z.namelist())==len(set(z.namelist()))==len(outcome['Files'])
        for item in outcome['Files']:
            data=z.read(item['Path']);assert len(data)==item['Bytes'] and sha(data)==item['SHA256']
        diagnostics=[name for name in z.namelist() if name.startswith('TestResults/joint-proof-profile/') and name.endswith('.jsonl')]
        assert len(diagnostics)==1
        records=[json.loads(line) for line in z.read(diagnostics[0]).decode('utf-8-sig').splitlines() if line.strip()]
        trx_names=[name for name in z.namelist() if name.endswith('.trx')]
        assert len(trx_names)==1
        trx=ET.fromstring(z.read(trx_names[0]));ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        results=trx.findall('.//t:UnitTestResult',ns);assert len(results)==1
        stdout=results[0].find('t:Output/t:StdOut',ns)
        stream=[]
        for line in (stdout.text or '').splitlines():
            if line.startswith('{'):
                try: item=json.loads(line)
                except json.JSONDecodeError: continue
                if item.get('Boundary')=='joint_operation_timing':stream.append(item)
        timings=[item for item in records if item.get('Boundary')=='joint_operation_timing']
        duplicate_in_trx=stream==timings
        if not duplicate_in_trx:
            message=results[0].find('.//t:Message',ns)
            assert not stream and results[0].get('outcome')=='Failed' and message is not None
            assert 'System.IO.IOException' in message.text and diagnostics[0].split('/')[-1] in message.text
            assert 'being used by another process' in message.text
        error=results[0].find('.//t:Message',ns)
    assert len({x['PID'] for x in timings})==1
    assert [x['Sequence'] for x in timings]==list(range(1,len(timings)+1))
    assert all(timings[i]['TotalElapsedMilliseconds']<=timings[i+1]['TotalElapsedMilliseconds'] for i in range(len(timings)-1))
    stack=[];intervals=[]
    for item in timings:
        if item['State']=='begin':
            assert item['Milliseconds']==0
            stack.append({'Operation':item['Operation'],'BeginSequence':item['Sequence'],'StartMilliseconds':item['TotalElapsedMilliseconds'],'Parent':stack[-1]['Operation'] if stack else None,'ChildrenMilliseconds':0.0})
        else:
            assert item['State']=='end' and stack and stack[-1]['Operation']==item['Operation'],item
            current=stack.pop();elapsed=item['Milliseconds'];assert elapsed>=0
            current.update(EndSequence=item['Sequence'],Milliseconds=elapsed,EndMilliseconds=item['TotalElapsedMilliseconds'])
            residual=elapsed-current['ChildrenMilliseconds'];assert residual>=-0.01,(current,residual)
            current['OutsideMeasuredChildrenMilliseconds']=max(0,residual)
            intervals.append(current)
            if stack:stack[-1]['ChildrenMilliseconds']+=elapsed
    groups=defaultdict(list)
    for row in intervals:groups[row['Operation']].append(row)
    aggregates=[{'Operation':name,'Completed':len(rows),'InclusiveMilliseconds':sum(x['Milliseconds'] for x in rows),
                 'OutsideMeasuredChildrenMilliseconds':sum(x['OutsideMeasuredChildrenMilliseconds'] for x in rows),
                 'MaximumMilliseconds':max(x['Milliseconds'] for x in rows)} for name,rows in sorted(groups.items())]
    cleanup=[x for x in records if 'CleanupOwnedRoot' in x]
    setup=[x for x in records if x.get('Boundary')=='joint_profile_setup']
    oracle=[x for x in records if x.get('Boundary')=='joint_profile_readonly_oracle']
    costs=[x for x in records if x.get('Boundary')=='joint_profile_process_cost']
    result={'Run':outcome['Run'],'CompiledSource':outcome['CompiledSource'],'MetadataHead':outcome['MetadataHead'],
            'ActualTestOutcome':results[0].get('outcome'),'TestDuration':results[0].get('duration'),
            'RawSHA256':outcome['SHA256'],'IndexedFiles':len(outcome['Files']),'TimingRecords':len(timings),
            'TimingDuplicateInTRX':duplicate_in_trx,'ActualFailureMessage':error.text if error is not None else None,
            'CompletedIntervals':len(intervals),'Unfinished':stack,'Setup':setup,'ProcessCosts':costs,'ReadOnlyOracle':oracle,'Cleanup':cleanup,
            'InputAndBinariesUnchanged':outcome['Attempt']['InputsUnchanged'] and outcome['Attempt']['BinariesUnchanged'],
            'Aggregates':aggregates,'Intervals':sorted(intervals,key=lambda x:x['BeginSequence']),
            'Interpretation':'Inclusive timings overlap; do not add ancestors and descendants. OutsideMeasuredChildren includes diagnostic observer work, uninstrumented calls and scheduling; it is not CPU time. Process CPU/wall are separately measured observations, not proof of OS/scanner causality. No timing quota or threshold; original recovery remains FAIL/open, coldWindowsfive0/5/LinuxUNRUN.'}
    return result

def main():
    evidence=Path(__file__).resolve().parent
    # Installed canonical script accepts an explicit evidence directory; external preparation defaults to this checkout.
    if '--evidence' in sys.argv:evidence=Path(sys.argv[sys.argv.index('--evidence')+1])
    result=analyze(evidence);text=json.dumps(result,indent=2,ensure_ascii=False)+'\n';target=evidence/'T327-timings.json'
    if '--check' in sys.argv:assert target.read_text(encoding='utf-8')==text
    else:target.write_text(text,encoding='utf-8',newline='\n')
    print(json.dumps({k:v for k,v in result.items() if k not in ['Aggregates','Intervals']},ensure_ascii=False))
    print(json.dumps(sorted(result['Aggregates'],key=lambda x:x['OutsideMeasuredChildrenMilliseconds'],reverse=True)[:15],indent=2))
if __name__=='__main__':main()
