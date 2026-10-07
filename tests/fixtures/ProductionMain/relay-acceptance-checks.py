"""Exact real live trace through inert execution of actual driver acceptance/cleanup."""
import ast,copy,gzip,hashlib,json,re,sys,tempfile
from pathlib import Path
from types import SimpleNamespace
repo=Path(__file__).resolve().parents[3];source=Path(__file__).with_name('live-game-one-turn.py');tree=ast.parse(source.read_text());mode=sys.argv[1]
base=repo/'specs/1553-portable-local-storage/recovery/evidence/relay-gm/live-r2';manifest=json.loads((base/'manifest.json').read_text())
def pinned(name):
 item=next(x for x in manifest['Artifacts'] if x['Path']==name);stored=(base/item['StoredPath']).read_bytes();raw=gzip.decompress(stored)
 assert len(raw)==item['Bytes'] and hashlib.sha256(raw).hexdigest()==item['SHA256'] and hashlib.sha256(stored).hexdigest()==item['StoredSHA256'];return raw
actual_story=pinned('play/game_session/stories/chaos_sea.jsonl');actual_client=pinned('client.raw');actual_delivery=json.loads(pinned('queue/request-1-d80064ecbba34043b5dd3f08b47eb5ea/dispatch.json'))
assert actual_story.startswith(b'\xef\xbb\xbf'), 'Source must preserve observed .NET BOM'
entry=next(n for n in tree.body if isinstance(n,ast.Try) and any(isinstance(x,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='client' for t in x.targets) for x in n.body))
block=next(n for n in ast.walk(entry) if isinstance(n,ast.If) and isinstance(n.test,ast.Call) and isinstance(n.test.func,ast.Attribute) and isinstance(n.test.func.value,ast.Name) and n.test.func.value.id=='story' and n.test.func.attr=='exists')
accept=copy.deepcopy(block)
# Source loop's sole break exits our bounded function after actual acceptance.
for node in ast.walk(accept):
 for name,value in ast.iter_fields(node):
  if isinstance(value,list):setattr(node,name,[ast.Return(value=None) if isinstance(v,ast.Break) else v for v in value])
def compile_function(nodes,name,ns):
 assignments={n.id for node in nodes for n in ast.walk(node) if isinstance(n,ast.Name) and isinstance(n.ctx,ast.Store)}
 f=ast.FunctionDef(name=name,args=ast.arguments(posonlyargs=[],args=[],kwonlyargs=[],kw_defaults=[],defaults=[]),body=[ast.Global(names=sorted(assignments))]+nodes,decorator_list=[])
 exec(compile(ast.fix_missing_locations(ast.Module(body=[f],type_ignores=[])),str(source),'exec'),ns);return ns[name]
with tempfile.TemporaryDirectory(prefix='own-relay-acceptance-') as folder:
 session=Path(folder);story=session/'stories/chaos_sea.jsonl';story.parent.mkdir();story.write_bytes(actual_story if mode!='plain' else actual_story[3:])
 data=json.loads(actual_story.decode('utf-8-sig'));action=data['player'];text=actual_client.decode('utf8')
 if mode=='wrong-action':action='A different request, never replayed'
 if mode=='missing-prompt':text=text[:text.rfind('Ваш ход')]
 client=SimpleNamespace(capture=bytearray(text.encode()),process=SimpleNamespace(poll=lambda:None),text=lambda offset=0:text,send=lambda *a:(_ for _ in ()).throw(AssertionError('Unexpected cancel/input')))
 if mode=='pending':
  p=session/'input/turn_request.json';p.parent.mkdir();p.write_text('{}')
 original=actual_delivery['inputBindingId'];delivery=dict(actual_delivery)
 if mode=='foreign-delivery':delivery['inputBindingId']='foreign'
 result=dict(AcceptedGameTurns=0,OriginalTurnDelivery=delivery,OriginalRunningStatus=dict(status=dict(inputBindingId=original)))
 def fresh_player(peer,offset,seconds):
  if 'Ваш ход' not in peer.text(offset) or '🌊 > ' not in peer.text(offset):raise TimeoutError('Actual fresh player prompt missing')
 exits=[];ns=dict(json=json,story=story,session=session,action=action,client=client,offset=0,result=result,client_wait=True,client_prompt=False,fresh_player=fresh_player)
 try:compile_function([accept],'actual_acceptance',ns)()
 except (TimeoutError,AssertionError):
  if mode not in ['missing-prompt','pending']:raise
  assert result['AcceptedGameTurns']==0 and ns['client_wait'] and not ns['client_prompt']
 else:
  if mode=='wrong-action':assert result['AcceptedGameTurns']==0 and ns['client_wait']
  elif mode in ['missing-prompt','pending']:raise AssertionError('History alone falsely qualified turn')
  else:
   assert result['AcceptedGameTurns']==1 and result['AcceptedStory']==data and not ns['client_wait'] and ns['client_prompt']
   assert result['InputQualified']==(mode!='foreign-delivery')
   # Execute actual client cleanup branch; after qualification it must take normal exit,
   # without Escape, replay, or inventing an owner/stop proof.
   cleanup=next(n for n in entry.finalbody if isinstance(n,ast.If) and 'client is not None' in ast.unparse(n.test))
   ns.update(relay_closed=True,start=0,client_cleanup_seconds=680,current_client_failure=lambda *a:False,client_exit_attempted=False,exit_client=lambda peer:exits.append('normal'),Path=Path)
   compile_function([copy.deepcopy(cleanup)],'actual_cleanup',ns)();assert exits==['normal']
print(json.dumps(dict(Case=mode,Passed=True,ActualDriverAst=True,RealTraceBytes=True,ModelCalls=0,GameProcesses=0)))
