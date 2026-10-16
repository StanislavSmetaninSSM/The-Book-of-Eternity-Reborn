from pathlib import Path
import sys,json,subprocess,hashlib,gzip,shutil
name,mdhash,jsonhash=sys.argv[1:];r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery';h=lambda b:hashlib.sha256(b).hexdigest()
for ext,expected in [('md',mdhash),('json',jsonhash)]:assert h(Path('/tmp',name+'.'+ext).read_bytes())==expected
j=json.loads(Path('/tmp',name+'.json').read_text());counts=[0,0,0,0];extras={};diffs=[]
def walk(o):
 if isinstance(o,dict):
  revision=o.get('commit',o.get('source_commit',o.get('revision')))
  if revision and 'path'in o and 'sha256'in o:
   ref=revision+':'+o['path'];whole=subprocess.check_output(['git','show',ref],cwd=r)
   projection=('first_line'in o and 'last_line'in o) or ('start'in o and 'end'in o) or ('start_line'in o and 'end_line'in o) or ('startLine'in o and 'endLine'in o) or ('firstLine'in o and 'lastLine'in o)
   first=o.get('first_line',o.get('start',o.get('start_line',o.get('startLine',o.get('firstLine')))));last=o.get('last_line',o.get('end',o.get('end_line',o.get('endLine',o.get('lastLine')))))
   b=b''.join(whole.splitlines(keepends=True)[first-1:last]) if projection else whole
   assert h(b)==o['sha256'];assert len(b)==o.get('bytes',o.get('byte_count',len(b)))
   if projection and 'text'in o:assert b.decode()==o['text']
   blob=o.get('blob',o.get('git_blob',o.get('gitBlob')));assert not blob or subprocess.check_output(['git','rev-parse',ref],cwd=r,text=True).strip()==blob
   counts[3 if projection else 0]+=1
  if isinstance(o.get('argv'),list) and o['argv'][:2]==['git','diff'] and 'sha256'in o:
   b=subprocess.check_output(o['argv'],cwd=r);assert h(b)==o['sha256'];assert len(b)==o.get('bytes',o.get('byte_count',len(b)));counts[1]+=1
   path=o.get('path',o.get('artifact'))
   if path:assert Path(path).read_bytes()==b;diffs.append((Path(path),b,o['argv']))
  if str(o.get('path','')).startswith('/tmp/') and 'sha256'in o:
   p=Path(o['path']);assert h(p.read_bytes())==o['sha256'];extras[p]=p.read_bytes();counts[2]+=1
  for v in o.values():walk(v)
 elif isinstance(o,list):
  for v in o:walk(v)
walk(j)

print(name,'ROOT_VERIFIED_NO_REPO_WRITES',counts)
