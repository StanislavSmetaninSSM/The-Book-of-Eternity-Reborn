import sys,json,time
from pathlib import Path
for line in sys.stdin:
 v=json.loads(line); print('received '+str(v['sequence']),file=sys.stderr,flush=True)
 if v['sequence']==1:Path(sys.argv[1]).write_text('inert notification')
 time.sleep(1 if v['sequence']==1 else .05)
 print(json.dumps({'sequence':v['sequence'],'ok':True}),flush=True)
