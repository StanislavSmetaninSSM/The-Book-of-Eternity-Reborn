# T126/T130 Linux rendered acceptance source. Requires existing Playwright, Chromium and loopback Vite.
# Run from a clean tree with an output directory outside the checkout; no backend/game state is used.
import argparse,asyncio,datetime,hashlib,json,os,pathlib,re,shutil,subprocess,sys,traceback
from playwright.async_api import async_playwright,expect
p=argparse.ArgumentParser();p.add_argument('directory');p.add_argument('--remaining',action='store_true');a=p.parse_args();r=pathlib.Path(__file__).resolve().parents[3];d=pathlib.Path(a.directory).resolve();
if d.is_relative_to(r): p.error('Diagnostic output must be outside the checkout')
d.mkdir(parents=True,exist_ok=False);g=['git','--no-lazy-fetch','-c','gc.auto=0','-c','maintenance.auto=false'];assert not subprocess.check_output(g+['status','--porcelain'],cwd=r).strip();assert shutil.disk_usage(r).free>=128*2**20
sha=subprocess.check_output(g+['rev-parse','HEAD'],cwd=r,text=True).strip();report={'source':sha,'part':'treatment residual' if a.remaining else 'full','started':datetime.datetime.now(datetime.timezone.utc).isoformat(),'budgetSeconds':60,'scope':'Production CommandResultView/PromptForm/BlockRenderer with typed controlled ShellContext and real browserApi fetch, intercepted public packet responses; not live backend/provider or full AppShell/save restart','requests':[],'checks':[],'errors':[]}
pins={str(p.relative_to(r)):hashlib.file_digest(p.open('rb'),'sha256').hexdigest() for root in ['BookOfEternityClient.WebFrontend/src','BookOfEternityClient.WebFrontend/test'] for p in (r/root).rglob('*') if p.is_file()};(d/'source-pins.json').write_text(json.dumps(pins,indent=2));(d/'shell.pid').write_text(str(os.getpid()));(d/'report.json').write_text(json.dumps(report,indent=2))
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
async def bounded():
 await asyncio.wait_for(main(),timeout=60)
try:asyncio.run(bounded())
except BaseException as error:report['passed']=False;report['exception']=traceback.format_exc();raise
finally:
 report['freeAfter']=shutil.disk_usage(r).free;post={k:hashlib.file_digest((r/k).open('rb'),'sha256').hexdigest() for k in pins};report['sourceUnchanged']=pins==post;(d/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));(d/'driver.exit').write_text('0' if report.get('passed') and report['sourceUnchanged'] else '1')
