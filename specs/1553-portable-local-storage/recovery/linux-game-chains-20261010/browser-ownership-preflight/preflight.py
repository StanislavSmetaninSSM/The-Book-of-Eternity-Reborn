import ast,json,os,time
from pathlib import Path
from playwright.sync_api import sync_playwright
p=Path('/workspace/boe-1553-storage-migration/tests/fixtures/LinuxGameChains/browser_chain.py');module=ast.parse(p.read_text());selected=ast.Module(body=[n for n in module.body if isinstance(n,ast.FunctionDef) and n.name in ['chromium_pids','exited']],type_ignores=[]);exec(compile(selected,str(p),'exec'))
with sync_playwright() as driver:
 browser=driver.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox','--disable-dev-shm-usage']);page=browser.new_page();assert page.url=='about:blank';pids=chromium_pids(browser);assert pids;assert all((Path('/proc')/str(pid)).exists() for pid in pids);browser.close();deadline=time.monotonic()+5
 while not all(exited(pid) for pid in pids):
  assert time.monotonic()<deadline;time.sleep(.05)
 result={'Scope':'fixture browser ownership only; no game process/command/GM','Inventory':'real Chromium CDP SystemInfo.getProcessInfo; proc PID existence before/exit after','Pids':pids,'AllExited':True,'PASS':True};Path('/tmp/boe_browser_ownership_preflight4.json').write_text(json.dumps(result));print(json.dumps(result))
