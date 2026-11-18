#!/usr/bin/python3
"""Inert original-stdio peer. Never opens a game or contacts a provider."""
import json,os,sys
from pathlib import Path
mode=os.environ['BOE_READY_TEST_MODE']; ledger=Path(os.environ['BOE_READY_TEST_LEDGER'])
identity={'rootKey':'/owned/inert','runId':'1'*32,'generationId':'2'*32,'epoch':1,'backend':2,'hostInstanceId':'3'*32,'bootId':'test-boot'}
projection={'pinId':'4'*32,'closeId':'5'*32,'operationId':'6'*32,'identity':identity,'outcome':0,'closingFailed':False}
def record(x):
 with ledger.open('a') as f:f.write(json.dumps(x)+'\n')
def reply(x):
 print(json.dumps(x),flush=True)
ready={'ok':True,'state':'active','sequence':0,'originalClose':projection}
if mode=='command-timeout':
 reply(ready)
 line=sys.stdin.readline();record({'command':json.loads(line) if line else None})
 # Never respond to this dispatched command. Closing stdin is NOT cancellation.
 for line in sys.stdin:record({'unexpected':line})
 record({'eof':True});sys.exit(0)
line=sys.stdin.readline()
if not line:record({'eofBeforeCancellation':True});sys.exit(0)
command=json.loads(line);record({'cancel':command})
if mode=='drain-timeout':
 for line in sys.stdin:record({'unexpected':line})
 record({'eof':True});sys.exit(0)
reply(ready)
if mode=='eof':
 record({'peerClosedWithoutReceipt':True});sys.exit(0)
if mode!='eof':
 close=dict(projection,outcome=2)
 if mode=='wrong-close':close['closeId']='7'*32
 reply({'ok':True,'state':'closed-observed','sequence':1,'completionKind':'original-main','localScopeCompleted':False,'effectiveOutcome':2,'terminalClose':close,'closeObserved':True,'continuationFailed':False})
for line in sys.stdin:record({'unexpected':line})
record({'eof':True})
