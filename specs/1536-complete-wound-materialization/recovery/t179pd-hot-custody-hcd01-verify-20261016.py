import os,json,gzip,hashlib,subprocess
from pathlib import Path
root=Path('/workspace/The-Book-of-Eternity-Reborn');tmp=Path('/tmp');base='132ffd72c67daeccd1c6639472ecca4b585e92ab';head='d149d37577a3367ae6ca249640353a194934300e'
env=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0');rec='specs/1536-complete-wound-materialization/recovery/'
def git(*args):return subprocess.check_output(['git',*args],cwd=root,env=env)
def h(b):return hashlib.sha256(b).hexdigest()
def pin(path):
 b=git('show',head+':'+path);return {'path':path,'commit':head,'bytes':len(b),'sha256':h(b),'git_blob':hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()}
files=git('diff','--name-only',base,head).decode().splitlines();assert len(files)==15
assert all(p.startswith('specs/1536-complete-wound-materialization/') for p in files)
scoped=git('diff','--binary',base,head,'--','BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts');assert not scoped
assert not git('diff','--check',base,head)
mdold=tmp/'t179pd-hot-custody-engineering-independent-review-20261016.md';jsold=mdold.with_suffix('.json')
assert h(mdold.read_bytes())=='dd0f5e4cf1370026d1ea8137f7db630c59271539c15f4c35166d50977a9e7829'
assert h(jsold.read_bytes())=='0cfd75f40ec106a1b2d19bf6ec26d62cb305efc1b8ec54c383b5513832786fd2'
old=json.loads(jsold.read_text());preserved=[]
for p in [mdold,jsold]+[Path(a['path']) for a in old['supporting_artifacts']]+[Path('/tmp/t179pd-hot-custody-engineering-design-recovery-20261016.json')]:
 path=rec+p.name+('.gz' if p.suffix=='.diff' else '')
 committed=git('show',head+':'+path);raw=gzip.decompress(committed) if path.endswith('.gz') else committed
 assert raw==p.read_bytes(),path
 preserved.append({'local':str(p),'committed_path':path,'raw_sha256':h(raw),'raw_bytes':len(raw),'stored_sha256':h(committed),'stored_bytes':len(committed),'equal':True})
projections=[]
phrase='Actual custody capture completes and consumes Capturing before CreateJointRepairPreparationAsync; the genuine continuation is then built and its existing reservation acquired.'
for path in ['specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md']:
 txt=git('show',head+':'+path).decode();lines=txt.splitlines(keepends=True)
 assert phrase in txt and 'Genuine continuation/reservation can precede this capture:' not in txt
 assert 'This acceptance releases test-first source engineering only, not runtime execution.' in txt
 assert 'Implement only after preserved causal RED' in txt
 assert '- [ ] T179-Pd.3b:' in txt
 block=''.join(lines[:21]);projections.append({'path':path,'start':1,'end':21,'sha256':h(block.encode()),'text':block})
canonical='specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md';txt=git('show',head+':'+canonical).decode();lines=txt.splitlines(keepends=True);block=''.join(lines[312:320])
assert 'bounded engineering DESIGN accepted' in block and 'actual execution still requires' in block and 'HS01–HS16 are future coverage obligations' in block
projections.append({'path':canonical,'start':313,'end':320,'sha256':h(block.encode()),'text':block})
diff=git('diff','--binary',base,head);diffp=tmp/'t179pd-hot-custody-hcd01-verification-20261016.diff';assert not diffp.exists();diffp.write_bytes(diff)
reportmd=tmp/'t179pd-hot-custody-hcd01-verification-20261016.md';reportjs=reportmd.with_suffix('.json');assert not reportmd.exists() and not reportjs.exists()
body=f'''# HC-D01 affected verification

**ACCEPTED — HC-D01 is closed.** Reviewed exact Git candidate `{head}` against `{base}`. This is a narrow documentation/preservation verification, not a repeated engineering or runtime review.

Both `plan.md:18` and `tasks.md:18` now state: actual custody capture completes and consumes Capturing before `CreateJointRepairPreparationAsync`; the genuine continuation is then built and its existing reservation acquired. The contradictory earlier sentence is removed. This matches the previously accepted detailed design; no source contract is changed.

The new top status and canonical implementation-plan subsection accurately record bounded engineering DESIGN acceptance. Test-first source work is the only release: future HS01–16 remain UNRUN, product success logic follows preserved causal RED, actual execution still requires fresh preparation/SOURCE/first protected Late2/separate EVIDENCE, and full S/B/history/P-A/M3/coldP0/F1/T197/public activation and other qualification limits remain open. The Pd.3b task checkbox remains unchecked. Older frozen-for-review journal entries are historical beneath the newer explicit status.

All 15 changed paths are task documentation or preserved review/recovery artifacts. The scoped production/test/catalog/script diff is empty; `git diff --check {base} {head}` passed. Independently compared the committed prior MD/JSON, eight supporting artifacts (including four lossless gzip diffs), and the 990-file historical recovery receipt with their existing /tmp originals: 11/11 exact raw-byte matches. The prior MD/JSON hashes remain `dd0f5e4cf1370026d1ea8137f7db630c59271539c15f4c35166d50977a9e7829` / `0cfd75f40ec106a1b2d19bf6ec26d62cb305efc1b8ec54c383b5513832786fd2`. No claim is made that the historical132 recovery receipt describes a d149 clone.

The paired JSON retains all 15 current Git/blob/SHA pins, exact changed text projections, preservation comparisons, and exact diff argv/hash. Verification uses pinned Git objects and does not require the later live worktree to remain unchanged. No new source inspection, build/discovery/test/application/native execution, network/fetch/push, repo/OS changes, or delegation occurred. Original review reports remain immutable.

Retained separate configured reviewer: `gpt-6-astra`, `xhigh`; serving-backend telemetry unavailable. No residual HC-D01 finding. No new runtime release or broader qualification.

FINALSTOP.
'''
reportmd.write_text(body)
report={'task':'HC-D01 narrow affected verification','verdict':'ACCEPTED_HC_D01_CLOSED','base':base,'candidate':head,'observed_live_head':git('rev-parse','HEAD').decode().strip(),'live_worktree_required_unchanged':False,'configuration':{'requested_and_retained_configured_model':'gpt-6-astra','reasoning_effort':'xhigh','backend_telemetry':'unavailable'},'findings':[],'closed_findings':['HC-D01'],'pins':[pin(p) for p in files],'projections':projections,'preserved_artifacts':preserved,'preserved_artifact_count':len(preserved),'scoped_diff':{'argv':['git','diff','--binary',base,head,'--','BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts'],'bytes':len(scoped),'sha256':h(scoped)},'full_diff':{'path':str(diffp),'argv':['git','diff','--binary',base,head],'bytes':len(diff),'sha256':h(diff)},'diff_check':'PASS','review_script':{'path':str(Path(__file__)),'sha256':h(Path(__file__).read_bytes())},'report_markdown':{'path':str(reportmd),'sha256':h(reportmd.read_bytes())},'implementation_design_changed':False,'runtime_release':False,'prior_engineering_acceptance_scope_unchanged':True,'runtime_build_discovery_network_delegation_counts':0,'repo_writes':0,'prior_reports_modified':False,'finalstop':True}
reportjs.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'verdict':report['verdict'],'paths_changed':len(files),'preserved':len(preserved),'md':{'path':str(reportmd),'sha256':h(reportmd.read_bytes())},'json':{'path':str(reportjs),'sha256':h(reportjs.read_bytes())},'diff':report['full_diff']},indent=2))
