from pathlib import Path
import json,hashlib
repo=Path(__file__).resolve().parents[4];base=repo/'specs/1536-complete-wound-materialization';r=base/'recovery'
h=lambda b:hashlib.sha256(b).hexdigest()
names=['t179pd-ni-iobs-l01-source-packet-20261018.json','t148-m01-source-packet-20261018.json']
paths=set();bindings=[]
for name in names:
 p=json.loads((r/name).read_text());deltas=[]
 for x in p['sourcePins']:
  paths.add(x['path']);data=(repo/x['path']).read_bytes()
  if len(data)!=x['bytes'] or h(data)!=x['sha256']:deltas.append(dict(path=x['path'],historicalSha256=x['sha256'],current=dict(path=x['path'],bytes=len(data),sha256=h(data))))
 bindings.append(dict(path=str((r/name).relative_to(repo)),originalPins=len(p['sourcePins']),unchangedPins=len(p['sourcePins'])-len(deltas),explicitCurrentDeltas=deltas,qualification='Historical original compiled provenance only; no current byte equivalence claim for changed source.'))
 paths.add(str((r/name).relative_to(repo)))
paths.update(str(p.relative_to(repo)) for p in (r/'l01-delivery-d01').iterdir() if p.is_file())
paths.add(str((r/'l01-managed-stack-capability-inventory-20261018.json').relative_to(repo)))
paths.update(str(p.relative_to(repo)) for p in [r/'l01-delivery-d01-generator-initial-path-error-20261018.json',r/'l01-delivery-d01-recover-20261018.py',repo/'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs',r/'l01-live-delivery-plan-independent-review-20261018.json',r/'l01-delivery-d01-baseline-evidence-20261018.json',r/'l01-delivery-d01-baseline-originals-20261018.zip',r/'l01-delivery-d01-nonlive-evidence-20261018.json',r/'l01-delivery-d01-nonlive-originals-20261018.zip'])
rows=[dict(path=p,bytes=len((repo/p).read_bytes()),sha256=h((repo/p).read_bytes())) for p in sorted(paths)]
packet=dict(kind='D01_CURRENT_TEST_ONLY_SOURCE_CLOSURE',base='1817ae5814f06a45cd6df975a9211c380a256df8',planAccepted='b411999b4c1c4cd55c96f8d3edfc608c99f15dd7',status='WIP_UNCOMPILED_WIRING_SOURCE_REVIEW_REQUIRED',sourcePins=rows,sourcePinDigest=h(json.dumps(rows,sort_keys=True,separators=(',',':')).encode()),historicalPinSets=bindings,category='fr063-joint-interface-crash-diagnostic',gameplayRelease='NONE',nativeFixtureRelease='NONE',scope='Current source preparation only. Source guard bounds old packets as immutable original provenance, not rewritten/current qualification.')
(r/'l01-delivery-d01-source-packet-20261018.json').write_text(json.dumps(packet,indent=2)+'\n')
print('D01 packet',len(rows),'pins',[(x['originalPins'],x['unchangedPins'],len(x['explicitCurrentDeltas'])) for x in bindings])
