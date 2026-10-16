import os,sys,json,hashlib,subprocess
from pathlib import Path
ROOT=Path('/workspace/The-Book-of-Eternity-Reborn')
OUT=Path('/tmp')
SOURCE='3fdc16e8a954458f1e56bafa42ca24a66c06d020'
BASE='d99e45102e2fb64088d771eb473de6671617f9b6'
HEAD='132ffd72c67daeccd1c6639472ecca4b585e92ab'
PREFIX='specs/1536-complete-wound-materialization/recovery/t179pd-hot-submitted-admission-engineering-design-20261016'
ENV=dict(os.environ,GIT_NO_LAZY_FETCH='1',GIT_OPTIONAL_LOCKS='0')
def git(*args,root=ROOT):return subprocess.check_output(['git',*args],cwd=root,env=ENV)
def sha(b):return hashlib.sha256(b).hexdigest()
def blob(b):return hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()
def pin(path,commit=HEAD):
 b=git('show',f'{commit}:{path}')
 return dict(path=path,commit=commit,bytes=len(b),sha256=sha(b),git_blob=blob(b),working_tree_equal=(ROOT/path).read_bytes()==b)
assert git('rev-parse','HEAD').decode().strip()==HEAD
assert not git('status','--porcelain')
design=json.loads((ROOT/(PREFIX+'.json')).read_text())
assert sha((ROOT/(PREFIX+'.md')).read_bytes())=='90f0ddc0c1fa3516231e62fcf9a8433e2f21b10b6a05219d6e24e105e5fb329f'
assert sha((ROOT/(PREFIX+'.json')).read_bytes())=='5429971dec24f96a022c3820c478a23884bf85a1982fde89408216d46e470d08'
sourcepins=[]
for row in design['source_pins']:
 p=pin(row['path'],row['commit'])
 for k in ['bytes','sha256','git_blob']: assert row[k]==p[k],(row['path'],k)
 sourcepins.append(p)
projections=[]
for row in design['source_projections']:
 b=git('show',f"{SOURCE}:{row['path']}")
 text=''.join(b.decode().splitlines(keepends=True)[row['start']-1:row['end']])
 assert text==row['text'],(row['path'],row['start'],'text')
 assert sha(text.encode())==row['sha256']
 projections.append(dict(path=row['path'],start=row['start'],end=row['end'],sha256=row['sha256'],matches=True))
leaves=[]
for row in design['historical_output_leaf_index']:
 lines=git('show',f"{SOURCE}:{row['path']}").decode().splitlines()
 for line,text in row['io']: assert lines[line-1].strip()==text.strip(),(row['name'],line)
 assert row['bounded_stage_disposition']=='NOT_IMPLEMENTED_IN_THIS_UNIT__FULL_S_UNAVAILABLE'
 leaves.append(dict(name=row['name'],path=row['path'],line=row['line'],end=row['end'],io_lines_checked=len(row['io']),disposition=row['bounded_stage_disposition']))
scoped=['BookOfEternityClient','BookOfEternityClient.Tests','BookOfEternityClient.IntegrationTests','tests','scripts']
diffs=[]
for name,args in [
 ('design-full',['diff','--binary',BASE,HEAD]),
 ('source-continuity',['diff','--binary',SOURCE,HEAD,'--',*scoped]),
 ('design-scoped',['diff','--binary',BASE,HEAD,'--',*scoped]),
 ('working',['diff','--binary',HEAD])]:
 data=git(*args);path=OUT/f't179pd-hot-custody-engineering-independent-{name}-20261016.diff'
 assert not path.exists(),str(path)
 path.write_bytes(data)
 if name!='design-full':assert not data,name
 diffs.append(dict(path=str(path),argv=['git',*args],bytes=len(data),sha256=sha(data)))
receipt_path=Path('/tmp/t179pd-hot-custody-engineering-design-recovery-20261016.json')
receipt=json.loads(receipt_path.read_text());clone=Path(receipt['directory'])
assert receipt['sha']==HEAD and receipt['count']==990 and len(receipt['files'])==990
assert git('rev-parse','HEAD',root=clone).decode().strip()==HEAD
assert not git('status','--porcelain',root=clone)
alternates=Path(git('rev-parse','--git-path','objects/info/alternates',root=clone).decode().strip())
if not alternates.is_absolute():alternates=clone/alternates
assert not alternates.exists()
tree={}
for record in git('ls-tree','-r','-z',HEAD).split(b'\0'):
 if not record:continue
 meta,p=record.split(b'\t',1);tree[p.decode()]=meta.decode().split()[2]
recovery=[]
for row in receipt['files']:
 b=(clone/row['path']).read_bytes();w=(ROOT/row['path']).read_bytes()
 assert b==w and sha(b)==row['sha256'] and len(b)==row['bytes'] and blob(b)==tree[row['path']],row['path']
 recovery.append(dict(**row,git_blob=blob(b),clone_writer_git_equal=True))
extra=['AGENTS.md','docs/development-workflow.md','docs/testing.md','.specify/memory/constitution.md',
 'specs/1536-complete-wound-materialization/spec.md','specs/1536-complete-wound-materialization/plan.md','specs/1536-complete-wound-materialization/tasks.md',
 'specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md',PREFIX+'.md',PREFIX+'.json',
 'BookOfEternityClient/Core/SessionOperationContext.cs','BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs','BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs',
 'BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs','BookOfEternityClient/Services/MortalJointRepairPublicationTransaction.cs']
report=dict(candidate=HEAD,source=SOURCE,base=BASE,observed_head=HEAD,source_pins=sourcepins,source_projection_checks=projections,output_leaf_checks=leaves,
 extra_pins=[pin(p) for p in extra],diffs=diffs,changed_paths=git('diff','--name-status',BASE,HEAD).decode(),
 recovery=dict(receipt_path=str(receipt_path),receipt_sha256=sha(receipt_path.read_bytes()),directory=str(clone),head=HEAD,count=len(recovery),clean=True,no_alternates=True,file_pins=recovery,
 transport_claim='Coordinator receipt reports new GitHub-only shallow filtered sparse clone; reviewer verifies local exact bytes/blob identities/clean HEAD, does not perform network fetch.',main=receipt['remoteReadback'].splitlines()[1]),
 counts=dict(source_pins=len(sourcepins),projections=len(projections),output_leaf_rows=len(leaves),leaf_io_lines=sum(r['io_lines_checked'] for r in leaves),recovery_files=len(recovery)),runtime_executed=0)
p=OUT/'t179pd-hot-custody-engineering-independent-inspection-20261016.json';assert not p.exists();p.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(dict(counts=report['counts'],changed_paths=report['changed_paths'],diffs=diffs,inspection=str(p),sha256=sha(p.read_bytes())),indent=2))
