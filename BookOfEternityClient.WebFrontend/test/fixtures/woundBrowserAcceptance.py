# T126/T130 Linux rendered acceptance source. Requires existing Playwright, Chromium and loopback Vite.
# Run from a clean tree with an output directory outside the checkout; no backend/game state is used.
import argparse,asyncio,datetime,hashlib,json,os,pathlib,re,shutil,signal,socket,subprocess,sys,traceback,urllib.parse
from playwright.async_api import async_playwright,expect
p=argparse.ArgumentParser();p.add_argument('directory');p.add_argument('--remaining',action='store_true');p.add_argument('--actual-shell',choices=['desktop','mobile']);a=p.parse_args();r=pathlib.Path(__file__).resolve().parents[3];d=pathlib.Path(a.directory).resolve();
if a.actual_shell and a.remaining: p.error('--remaining belongs only to the original controlled-host mode')
if d.is_relative_to(r): p.error('Diagnostic output must be outside the checkout')
d.mkdir(parents=True,exist_ok=False);g=['git','--no-lazy-fetch','-c','gc.auto=0','-c','maintenance.auto=false'];assert not subprocess.check_output(g+['status','--porcelain'],cwd=r).strip();assert shutil.disk_usage(r).free>=(687865856+2908000000 if a.actual_shell else 128*2**20)
sha=subprocess.check_output(g+['rev-parse','HEAD'],cwd=r,text=True).strip();report={'source':sha,'part':'treatment residual' if a.remaining else 'full','started':datetime.datetime.now(datetime.timezone.utc).isoformat(),'budgetSeconds':60,'scope':'Production CommandResultView/PromptForm/BlockRenderer with typed controlled ShellContext and real browserApi fetch, intercepted public packet responses; not live backend/provider or full AppShell/save restart','requests':[],'checks':[],'errors':[]}
pins={str(p.relative_to(r)):hashlib.file_digest(p.open('rb'),'sha256').hexdigest() for root in ['BookOfEternityClient.WebFrontend/src','BookOfEternityClient.WebFrontend/test'] for p in (r/root).rglob('*') if p.is_file()};(d/'source-pins.json').write_text(json.dumps(pins,indent=2));(d/'shell.pid').write_text(str(os.getpid()));(d/'report.json').write_text(json.dumps(report,indent=2))
if a.actual_shell:
 report.update(part='actual-shell-'+a.actual_shell,scope='Ordinary Vite index/main/StrictMode/App/ShellProvider DOM and controlled HTTP emissions only; not backend authority, canonical nonmutation, GM/provider, saves/fresh-process or packaged hosting',reserveBytes=687865856,headroomBytes=2908000000)
 frontend=r/'BookOfEternityClient.WebFrontend'
 for source in [frontend/'index.html',frontend/'package.json',frontend/'package-lock.json',frontend/'vite.config.ts',r/'tests/categories.json',r/'tests/selection.json',*frontend.glob('tsconfig*.json'),*(frontend/'public').rglob('*'),*(frontend/'node_modules').rglob('*')]:
  if source.is_file() and not any(part in {'.vite','.vite-temp'} for part in source.relative_to(r).parts):
   pins[str(source.relative_to(r))]=hashlib.file_digest(source.open('rb'),'sha256').hexdigest()
 report['dependencyCacheExclusions']=['node_modules/.vite','node_modules/.vite-temp']
 (d/'source-pins.json').write_text(json.dumps(pins,indent=2));(d/'report.json').write_text(json.dumps(report,indent=2))
def record(name,**extra):report['checks'].append({'name':name,**extra});(d/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));print('PASS',name,flush=True)
async def main():
 async with async_playwright() as pw:
  browser=await pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True)
  context=await browser.new_context(viewport={'width':1440,'height':1000},locale='ru-RU')
  await context.tracing.start(screenshots=True,snapshots=True,sources=False)
  page=await context.new_page();page.on('pageerror',lambda error:report['errors'].append(str(error)))
  requests=asyncio.Queue()
  async def route_api(route):
   req=route.request;report['requests'].append({'url':req.url,'method':req.method,'body':req.post_data_json});await requests.put(route)
  await page.route(re.compile(r'^http://127\.0\.0\.1:5177/api/'),route_api)
  async def case(name):
   await page.evaluate('async(name)=>{window.woundFixture.setCase(name);await new Promise(resolve=>requestAnimationFrame(resolve));}',name)
   await expect(page.locator('h1')).to_be_visible()
  async def packet(name):return await page.evaluate('(name)=>window.woundFixture.result(name)',name)
  async def reply(route,name,status=200):
   body=await packet(name) if status==200 else {'error':'Controlled refusal'}
   async with page.expect_event('requestfinished',predicate=lambda request:request==route.request):
    await route.fulfill(status=status,content_type='application/json',body=json.dumps(body))
   await page.evaluate('()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))')
  async def take():return await asyncio.wait_for(requests.get(),5)
  try:
   await page.goto('http://127.0.0.1:5177/test/fixtures/wounds.html',wait_until='networkidle');await expect(page.get_by_role('heading',name='Активные раны',exact=True)).to_be_focused()
   if not a.remaining:
    for width,label in [(1440,'desktop'),(390,'mobile')]:
     await page.set_viewport_size({'width':width,'height':1000 if width==1440 else 844});await case('active')
     await expect(page.get_by_text('Боль <b>не становится разметкой</b>.',exact=True)).to_be_visible()
     assert await page.locator('.wound-command-view b').count()==0
     assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth')
     await page.get_by_role('button',name='История заживших ран',exact=True).click();await expect(page.get_by_text('Заживший ожог — II степень. Рана зажила.',exact=True)).to_be_visible()
     assert 'Требуется лечение' not in await page.locator('body').inner_text()
     await page.screenshot(path=str(d/(label+'-history.png')),full_page=True)
     await page.get_by_role('button',name='Активные раны',exact=True).click();await expect(page.get_by_role('heading',name='Активные раны',exact=True)).to_be_focused()
     assert not report['requests'];record(label+' active/literal/History/back/layout')
    await case('active');await page.evaluate('window.woundFixture.holdActions=true');commands=await page.evaluate('window.woundFixture.commands.length')
    await page.get_by_role('button',name='История заживших ран',exact=True).evaluate('(e)=>{e.click();e.click();}')
    await expect(page.get_by_role('button',name='← Назад к сцене',exact=True)).to_be_disabled();assert await page.evaluate('window.woundFixture.commands.length')==commands+1
    await page.evaluate('window.woundFixture.holdActions=false;window.woundFixture.releaseAction()');await expect(page.get_by_text('Заживший ожог — II степень. Рана зажила.',exact=True)).to_be_visible();record('synchronous duplicate action guard and busy exit ownership')
   else: await page.set_viewport_size({'width':390,'height':844})
   await case('target');await expect(page.get_by_role('heading',name='Лечение ран',exact=True)).to_be_focused();await page.keyboard.press('Tab')
   assert await page.locator('select').evaluate('(e)=>document.activeElement===e')
   assert await page.locator('select').evaluate('(e)=>getComputedStyle(e).outlineStyle!="none"')
   await page.locator('select').select_option('opaque-1');await page.keyboard.press('Tab');assert await page.get_by_role('button',name='Отправить форму',exact=True).evaluate('(e)=>document.activeElement===e')
   await expect(page.get_by_text('Пока недоступно: нет согласия на помощь.',exact=False)).to_be_visible();assert await page.locator('option[value="opaque-2"]').evaluate('(e)=>e.disabled && e.matches(":disabled")');await page.locator('select').focus();await page.keyboard.press('ArrowDown');await expect(page.locator('select')).to_have_value('opaque-1');await page.keyboard.press('Tab')
   await page.keyboard.press('Enter');route=await take();assert route.request.post_data_json=={'sessionId':'opaque-target','ownerId':'opaque-owner','answers':{'wound_target':'opaque-1'}}
   await reply(route,'confirm');await expect(page.get_by_role('checkbox')).to_be_visible();assert not await page.get_by_role('checkbox').is_checked()
   # Typing/selection must not refocus the heading or dispatch any additional request.
   await page.locator('select').select_option('opaque-1');await page.get_by_role('checkbox').check();before=len(report['requests'])
   await page.locator('form').evaluate('(form)=>{form.dispatchEvent(new Event("submit",{bubbles:true,cancelable:true}));form.dispatchEvent(new Event("submit",{bubbles:true,cancelable:true}));}')
   route=await take();assert route.request.post_data_json['answers']=={'wound_selection':'opaque-1','confirm_wound_treatment':True};assert len(report['requests'])==before+1
   await expect(page.get_by_role('button',name='Отменить выбор',exact=True)).to_be_disabled();await reply(route,'pending');await expect(page.get_by_text('Попытка передана ГМу. Результат ещё не принят.',exact=True)).to_be_visible()
   assert await page.locator('form').count()==0;record('keyboard target-to-confirm; explicit answers; synchronous duplicate submit guard; pending distinct')
   await case('confirm');await page.locator('select').select_option('opaque-1');await page.get_by_role('checkbox').check()
   await page.get_by_role('button',name='Отправить форму',exact=True).click();route=await take();await reply(route,'result');await expect(page.get_by_text('Рана ослаблена до I степени. Мир совершил один цикл.',exact=True)).to_be_visible();assert await page.locator('form').count()==0;record('confirmed packet renders actual result without fabricated success')
   await case('confirm');clears=await page.evaluate('window.woundFixture.clears');await page.get_by_role('button',name='Отменить выбор',exact=True).evaluate('(e)=>{e.click();e.click();document.querySelector("form").dispatchEvent(new Event("submit",{bubbles:true,cancelable:true}));}')
   route=await take();assert route.request.url.endswith('/cancel');assert route.request.post_data_json=={'sessionId':'opaque-confirm','ownerId':'opaque-owner'};assert requests.empty();assert await page.evaluate('window.woundFixture.clears')==clears
   await reply(route,'cancelled');await expect(page.get_by_text('Лечение отменено. Бросок и ход не начинались.',exact=True)).to_be_visible();await page.get_by_role('button',name='← Назад к сцене',exact=True).click();await expect(page.locator('h1')).to_have_count(0);assert await page.evaluate('window.woundFixture.clears')==clears+1;record('cancel-first Back waits; duplicate cancel/submit overlap suppressed; close after response')
   for failure in ['http','lost']:
    await case('confirm');await page.locator('select').select_option('opaque-1');await page.get_by_role('button',name='Отменить выбор',exact=True).click();route=await take()
    if failure=='http':await reply(route,'stale',409)
    else:await route.abort('failed')
    await expect(page.get_by_role('alert')).to_be_visible();await expect(page.locator('select')).to_have_value('opaque-1');await expect(page.get_by_role('button',name='Отменить выбор',exact=True)).to_be_enabled();record('cancel '+failure+' retains live form and answers')
   # A delayed reply from an old parent cannot overwrite/clear a newer form or its answers.
   await case('confirm');await page.get_by_role('button',name='Отменить выбор',exact=True).click();old=await take();await case('target');await page.locator('select').select_option('opaque-1');await page.get_by_role('button',name='Отправить форму',exact=True).click();new=await take();refreshes=await page.evaluate('window.woundFixture.refreshes');await reply(old,'cancelled');await expect(page.get_by_role('button',name='Отправляем…',exact=True)).to_be_disabled();await expect(page.locator('select')).to_have_value('opaque-1');assert await page.evaluate('window.woundFixture.refreshes')==refreshes;await reply(new,'confirm');await expect(page.get_by_role('checkbox')).to_be_visible();record('late old cancel cannot overwrite new command, reset answers, release new operation or refresh')
   await case('confirm');await page.get_by_role('button',name='Отменить выбор',exact=True).click();old=await take();refreshes=await page.evaluate('window.woundFixture.refreshes');await page.evaluate('window.woundFixture.unmountView()');await expect(page.locator('h1')).to_have_count(0);await reply(old,'cancelled');await case('active');assert await page.evaluate('window.woundFixture.refreshes')==refreshes;record('disposed component ignores delayed prompt reply without local follow-up')
   for name,needle in [('empty','Активных ран сейчас нет.'),('unavailable','Сведения о ранах временно недоступны.'),('foreign','Цель: Ива'),('stale','Согласие изменилось.')]:
    await case(name);await expect(page.get_by_text(needle,exact=False)).to_be_visible();assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth');assert await page.locator('form').count()==0
   await page.screenshot(path=str(d/'mobile-stale.png'),full_page=True);record('empty/unavailable/foreign consent/stale read-only distinctions')
   await case('confirm');body=await page.locator('body').inner_text();assert not any(s in body for s in ['opaque-','SECRET_PRIVATE_PATH','hidden_route']);await page.screenshot(path=str(d/'mobile-confirm.png'),full_page=True);record('visible text excludes private values; mobile confirmation layout')
   await page.set_viewport_size({'width':1440,'height':1000});await case('confirm');assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth');await expect(page.get_by_role('checkbox')).to_be_visible();await page.screenshot(path=str(d/'desktop-confirm.png'),full_page=True);record('desktop confirmation layout over actual shared form')
   assert not report['errors'],report['errors'];assert requests.empty();report['passed']=True
  finally:
   report['ended']=datetime.datetime.now(datetime.timezone.utc).isoformat();report['fixtureCounters']=await page.evaluate('({commands:window.woundFixture?.commands,clears:window.woundFixture?.clears,refreshes:window.woundFixture?.refreshes})')
   await context.tracing.stop(path=str(d/'trace.zip'));await context.close();await browser.close()
async def actual_shell():
 base='http://127.0.0.1:5177';frontend=r/'BookOfEternityClient.WebFrontend'
 get_paths={'/api/main-menu','/api/session','/api/game-screen','/api/audio/settings','/api/client/settings','/api/explorer/command-coverage'}
 expected_commands=['/раны','/раны история','/раны']
 phase='bootstrap';packets=None;command_index=0;entries={};progress=asyncio.Event();packets_ready=asyncio.Event()
 violations=[];vite=None;browser=None;context=None;page=None;trace_started=False;body_passed=False
 vite_log=(d/'vite.log').open('wb');report.update(requests=[],websockets=[],violations=violations,cleanup={})
 def save_report(): (d/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
 def refuse(entry,reason):
  entry['refused']=reason;violations.append({'id':entry['id'],'reason':reason});progress.set();save_report()
 def finished(request):
  entry=entries.get(request)
  if entry is not None: entry['completed']=True;progress.set()
 def failed(request):
  entry=entries.get(request)
  if entry is not None:
   entry.update(completed=True,failure=request.failure);progress.set()
   if entry.get('api'): refuse(entry,'API request failed')
 def response_seen(response):
  entry=entries.get(response.request)
  if entry is not None: entry['status']=response.status
 def static_allowed(path):
  if path in {'/','/index.html','/@vite/client','/@react-refresh','/favicon.ico'}: return True
  if path.startswith(('/src/','/node_modules/','/@id/')): return True
  if path in {'/test/helpers/woundActualShellFixtures.ts','/test/helpers/woundCommandFixtures.tsx','/test/helpers/woundHelpCoverage.ts'}: return True
  if path.startswith('/@fs/'):
   target=pathlib.Path(path[len('/@fs'):]).resolve()
   return target.is_relative_to(frontend.resolve())
  return (frontend/'public'/path.lstrip('/')).resolve().is_relative_to((frontend/'public').resolve()) and (frontend/'public'/path.lstrip('/')).is_file()
 async def route_request(route):
  nonlocal command_index
  request=route.request;url=urllib.parse.urlsplit(request.url);path=urllib.parse.unquote(url.path)
  entry={'id':len(report['requests'])+1,'url':request.url,'method':request.method,'body':request.post_data,'phase':phase,'completed':False}
  report['requests'].append(entry);entries[request]=entry
  if url.scheme!='http' or url.netloc!='127.0.0.1:5177' or '\\' in path or '..' in path.split('/') or path.lower()=='/assets' or path.lower().startswith('/assets/'):
   refuse(entry,'External, traversal or /assets proxy request');await route.abort('blockedbyclient');return
  if path=='/api' or path.startswith('/api/'):
   entry['api']=True
   if url.query or not ((request.method=='GET' and path in get_paths) or (request.method=='POST' and path=='/api/explorer/command')):
    refuse(entry,'Unexpected API method/path/query');await route.abort('blockedbyclient');return
   await packets_ready.wait()
   if request.method=='GET':
    same=[row for row in report['requests'] if row.get('api') and row['phase']==entry['phase'] and row['method']=='GET' and urllib.parse.urlsplit(row['url']).path==path]
    if len(same)>(2 if entry['phase']=='bootstrap' else 1):
     refuse(entry,'Unexpected duplicate refresh GET');await route.abort('blockedbyclient');return
    payload=packets['bootstrap'][path]
   else:
    try: body=request.post_data_json
    except Exception: body=None
    if command_index>=3 or entry['phase']!='command-'+str(command_index+1) or body!={'command':expected_commands[command_index],'advancedEnabled':False}:
     refuse(entry,'Unexpected command order/body/default mode');await route.abort('blockedbyclient');return
    entry['body']=body;payload=packets['commands'][command_index]['response'];command_index+=1
   entry['response']=payload;save_report()
   await route.fulfill(status=200,content_type='application/json',body=json.dumps(payload,ensure_ascii=False));return
  if request.method!='GET' or not static_allowed(path):
   refuse(entry,'Unexpected nonstatic request');await route.abort('blockedbyclient');return
  entry['static']=True;await route.continue_()
 async def route_websocket(route):
  url=urllib.parse.urlsplit(route.url)
  entry={'url':route.url,'allowed':url.scheme=='ws' and url.netloc=='127.0.0.1:5177' and url.path=='/'}
  report['websockets'].append(entry)
  if entry['allowed']: route.connect_to_server()
  else:
   violations.append({'reason':'Unexpected websocket','url':route.url});progress.set();await route.close(code=1008,reason='Unexpected websocket')
 async def refresh_barrier(label,bootstrap=False):
  while True:
   assert not violations,violations
   rows=[row for row in report['requests'] if row.get('api') and row['phase']==label]
   counts={path:sum(row['method']=='GET' and urllib.parse.urlsplit(row['url']).path==path for row in rows) for path in get_paths}
   rounds=set(counts.values())
   posts=[row for row in rows if row['method']=='POST']
   if len(rounds)==1 and next(iter(rounds)) in ({1,2} if bootstrap else {1}) and len(posts)==(0 if bootstrap else 1) and all(row['completed'] for row in rows):
    snapshot={'phase':label,'completedGETs':counts,'completedPOSTs':len(posts)}
    report['refreshes']=[row for row in report.get('refreshes',[]) if row['phase']!=label]+[snapshot];save_report();return
   progress.clear();await progress.wait()
 async def activate(locator):
  await expect(locator).to_have_count(1);await expect(locator).to_be_enabled()
  await locator.focus();await expect(locator).to_be_focused();await page.keyboard.press('Enter')
 async def screen(label):
  await page.wait_for_load_state('networkidle')
  content=page.locator('.content-area > div')
  await expect(content).to_have_count(1)
  await expect(content).to_have_css('opacity','1')
  await expect(content).to_have_css('filter','blur(0px)')
  body=await page.locator('body').inner_text()
  assert not any(value in body for value in ['ACTUAL_SHELL_PRIVATE_SENTINEL','hidden_route','opaque-'])
  assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth')
  assert not report['errors'],report['errors'];assert not violations,violations
  await page.screenshot(path=str(d/(label+'.png')),full_page=True)
 async def command_step(locator,index):
  nonlocal phase
  phase='command-'+str(index);await activate(locator);await refresh_barrier(phase)
 try:
  with socket.socket() as probe: probe.bind(('127.0.0.1',5177))
  node=shutil.which('node');assert node,'Node is required';assert (frontend/'node_modules/vite/bin/vite.js').is_file()
  vite=await asyncio.create_subprocess_exec(node,str(frontend/'node_modules/vite/bin/vite.js'),'--host','127.0.0.1','--port','5177','--strictPort',cwd=frontend,stdout=vite_log,stderr=asyncio.subprocess.STDOUT,start_new_session=True)
  report['vite']={'pid':vite.pid,'cwd':str(frontend),'host':'127.0.0.1','port':5177};(d/'vite.pid').write_text(str(vite.pid));save_report()
  while base+'/' not in (d/'vite.log').read_text(errors='replace'):
   assert vite.returncode is None,'Owned Vite exited during startup';await asyncio.sleep(0.05)
  async with async_playwright() as pw:
   try:
    browser=await pw.chromium.launch(executable_path='/usr/bin/chromium',headless=True)
    report['chromiumVersion']=browser.version
    context=await browser.new_context(viewport={'width':1440 if a.actual_shell=='desktop' else 390,'height':1000 if a.actual_shell=='desktop' else 844},locale='ru-RU',service_workers='block')
    await context.route('**/*',route_request);await context.route_web_socket('**/*',route_websocket)
    await context.tracing.start(screenshots=True,snapshots=True,sources=False);trace_started=True
    page=await context.new_page()
    page.on('pageerror',lambda error:report['errors'].append(str(error)))
    page.on('requestfinished',finished);page.on('requestfailed',failed);page.on('response',response_seen)
    await page.goto(base+'/',wait_until='domcontentloaded')
    packets=await page.evaluate("async()=>{const module=await import('/test/helpers/woundActualShellFixtures.ts');return module.createWoundActualShellPackets();}")
    assert set(packets['bootstrap'])==get_paths
    assert [row['command'] for row in packets['commands']]==expected_commands
    game=packets['bootstrap']['/api/game-screen']
    assert game['narrative']['imagePrompt']=='' and game['media']['sceneImagePrompt']=='' and game['media']['gallery']==[]
    coverage=packets['bootstrap']['/api/explorer/command-coverage']
    assert len(coverage['commands'])==1 and coverage['commands'][0]['handlerKind']=='Wounds'
    for row in packets['commands']:
     reply=row['response'];assert reply['command']==row['command'] and not reply.get('pendingGmAction') and reply['interactiveSession'] is None and not reply['prompts']
     assert any(block['kind']=='rawJson' and block['json'].get('hidden_route')=='ACTUAL_SHELL_PRIVATE_SENTINEL' for block in reply['blocks'])
    (d/'packets.json').write_text(json.dumps(packets,ensure_ascii=False,indent=2));packets_ready.set()
    await refresh_barrier('bootstrap',bootstrap=True)
    assert await page.evaluate("typeof window.woundFixture==='undefined'")
    await activate(page.get_by_role('button',name=re.compile('Продолжить главу')))
    await expect(page.locator('.scene-view')).to_be_visible();await screen('scene')
    await activate(page.get_by_role('tab',name='Помощь (3)',exact=True))
    await expect(page.locator('.help-category__label')).to_have_text('Раны')
    menu=page.get_by_role('button',name=re.compile('Раны и заживление'))
    await expect(menu).to_be_visible();await screen('menu')
    await refresh_barrier('bootstrap',bootstrap=True)
    await command_step(menu,1)
    await expect(page.get_by_role('heading',level=1,name='Активные раны',exact=True)).to_be_focused()
    await expect(page.get_by_text('Боль <b>не становится разметкой</b>.',exact=True)).to_be_visible()
    assert await page.locator('.wound-command-view b').count()==0
    await screen('active')
    history_button=page.get_by_role('button',name='История заживших ран',exact=True)
    await page.keyboard.press('Tab');await expect(history_button).to_be_focused()
    await command_step(history_button,2)
    await expect(page.get_by_role('heading',level=1,name='История заживших ран',exact=True)).to_be_focused()
    await expect(page.get_by_text('Заживший ожог — II степень. Рана зажила.',exact=True)).to_be_visible()
    assert 'Требуется лечение' not in await page.locator('.wound-command-view').inner_text()
    await screen('history')
    await command_step(page.get_by_role('button',name='Активные раны',exact=True),3)
    await expect(page.get_by_role('heading',level=1,name='Активные раны',exact=True)).to_be_focused()
    await screen('active-back')
    before=len([row for row in report['requests'] if row.get('api')])
    await activate(page.get_by_role('button',name='← Назад к сцене',exact=True))
    await expect(page.locator('.wound-command-view')).to_have_count(0);await expect(page.locator('.scene-view')).to_be_visible()
    await screen('scene-back')
    assert len([row for row in report['requests'] if row.get('api')])==before
    assert command_index==3 and vite.returncode is None
    assert all(row['completed'] for row in report['requests']),report['requests']
    record('actual '+a.actual_shell+' App/ShellProvider read-only menu/wounds/History/back',commands=expected_commands,refreshBatches=report['refreshes'])
    body_passed=True
   finally:
    cleanup_errors=[]
    if context is not None:
     if trace_started:
      try: await asyncio.wait_for(context.tracing.stop(path=str(d/'trace.zip')),5)
      except Exception as error: cleanup_errors.append('trace: '+repr(error))
     try: await asyncio.wait_for(context.close(),5)
     except Exception as error: cleanup_errors.append('context: '+repr(error))
    if browser is not None:
     try: await asyncio.wait_for(browser.close(),5)
     except Exception as error: cleanup_errors.append('browser: '+repr(error))
    report['cleanup'].update(browserClosed=browser is None or not browser.is_connected(),contextClosed=context is None or not context.pages,errors=cleanup_errors)
 finally:
  if vite is not None:
   if vite.returncode is None:
    assert os.getpgid(vite.pid)==vite.pid
    os.killpg(vite.pid,signal.SIGTERM)
    try: await asyncio.wait_for(vite.wait(),3)
    except asyncio.TimeoutError:
     assert os.getpgid(vite.pid)==vite.pid
     os.killpg(vite.pid,signal.SIGKILL);await asyncio.wait_for(vite.wait(),3)
   try: os.killpg(vite.pid,0);group_gone=False
   except ProcessLookupError: group_gone=True
   report['cleanup'].update(viteExited=vite.returncode is not None,viteExitCode=vite.returncode,viteGroupGone=group_gone)
  vite_log.close();report['ended']=datetime.datetime.now(datetime.timezone.utc).isoformat()
  report['freeAfter']=shutil.disk_usage(r).free;report['reserveRetained']=report['freeAfter']>=687865856
  report['passed']=body_passed and not violations and not report['errors'] and not report['cleanup'].get('errors') and report['cleanup'].get('browserClosed',False) and report['cleanup'].get('contextClosed',False) and report['cleanup'].get('viteGroupGone',False) and report['reserveRetained']
  save_report()
async def bounded():
 await asyncio.wait_for(actual_shell() if a.actual_shell else main(),timeout=60)
try:asyncio.run(bounded())
except BaseException as error:report['passed']=False;report['exception']=traceback.format_exc();raise
finally:
 report['freeAfter']=shutil.disk_usage(r).free;post={k:hashlib.file_digest((r/k).open('rb'),'sha256').hexdigest() for k in pins};report['sourceUnchanged']=pins==post;(d/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));(d/'driver.exit').write_text('0' if report.get('passed') and report['sourceUnchanged'] else '1')
