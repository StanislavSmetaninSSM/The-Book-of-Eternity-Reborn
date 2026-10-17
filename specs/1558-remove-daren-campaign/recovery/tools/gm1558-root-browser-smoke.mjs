// UNRUN preparation: execute only after the final source/actual gate is released.
import { mkdir, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import assert from 'node:assert/strict';
import { connectCdp } from './gm1558-root-cdp-client.mjs';

const [debugOrigin, appOrigin, outputDirectory] = process.argv.slice(2);
assert(debugOrigin && appOrigin && outputDirectory, 'debugOrigin appOrigin outputDirectory required');
assert(new URL(debugOrigin).hostname === '127.0.0.1');
assert(new URL(appOrigin).hostname === '127.0.0.1');
await mkdir(outputDirectory, { recursive: false });
const pages = await (await fetch(`${debugOrigin}/json/list`)).json();
const page = pages.find(p => p.type === 'page');
assert(page?.webSocketDebuggerUrl, 'owned Chromium page required');
const cdp = await connectCdp(page.webSocketDebuggerUrl);
const snapshots = [];
async function snapshot(label) {
  const state = await cdp.evaluate(`({url:location.href,title:document.title,text:document.body.innerText,buttons:[...document.querySelectorAll('button')].map(x=>({text:x.innerText,disabled:x.disabled})),catalogCards:document.querySelectorAll('.qte-practice-card').length})`);
  assert(!/Дарен|Daren|daren-showcase/.test(state.text), `${label}: campaign visible`);
  snapshots.push({ label, ...state });
  await cdp.screenshot(join(outputDirectory, `${label}.png`));
  return state;
}
async function clickExpression(expression) {
  await cdp.until(`Boolean((${expression}) && !(${expression}).disabled)`);
  const point = await cdp.evaluate(`(()=>{const e=(${expression});if(!e||e.disabled)throw Error('missing/enabled target');e.scrollIntoView({block:'center'});const r=e.getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2}})()`);
  await cdp.call('Input.dispatchMouseEvent', { type:'mouseMoved', ...point });
  await cdp.call('Input.dispatchMouseEvent', { type:'mousePressed', button:'left', clickCount:1, ...point });
  await cdp.call('Input.dispatchMouseEvent', { type:'mouseReleased', button:'left', clickCount:1, ...point });
}
const button = text => `[...document.querySelectorAll('button')].find(x=>x.innerText.trim()===${JSON.stringify(text)})`;
const resultButton = text => `[...document.querySelectorAll('.qte-practice-result button')].find(x=>x.innerText.trim()===${JSON.stringify(text)})`;
const practiceButton = "document.querySelector('button[data-launcher-mode=\"practice\"]')";
try {
  for (const method of ['Page.enable','Runtime.enable','Network.enable','Log.enable']) await cdp.call(method);
  await cdp.call('Emulation.setDeviceMetricsOverride', { width:1440,height:1000,deviceScaleFactor:1,mobile:false });
  await cdp.call('Page.navigate', {url:appOrigin});
  await cdp.until(`Boolean(${practiceButton})`);
  await snapshot('01-menu');
  await clickExpression(practiceButton);
  await cdp.until("document.querySelectorAll('.qte-practice-card').length===11");
  assert.equal((await snapshot('02-practice-catalog')).catalogCards, 11);
  await clickExpression("document.querySelector('.qte-practice-card .qte-practice-card__start')");
  await cdp.until(`Boolean(${button('Начать мини-игру')})`);
  await snapshot('03-ready');
  await clickExpression(button('Начать мини-игру'));
  await cdp.until(`Boolean(${button('Выбрать действие')})`);
  await snapshot('04-branch-choice');
  await clickExpression(button('Выбрать действие'));
  await cdp.until("Boolean(document.querySelector('.qte-practice-result'))");
  await snapshot('05-result');
  await clickExpression(resultButton('Повторить'));
  await cdp.until(`Boolean(${button('Начать мини-игру')})`);
  await clickExpression(button('Начать мини-игру'));
  await cdp.until(`Boolean(${button('Выбрать действие')})`);
  await clickExpression(button('Выбрать действие'));
  await cdp.until("Boolean(document.querySelector('.qte-practice-result'))");
  await clickExpression(resultButton('Выйти'));
  await cdp.until("document.querySelectorAll('.qte-practice-card').length===11");
  await snapshot('06-exit-catalog');
  await cdp.call('Page.navigate', {url:`${appOrigin}/daren-showcase`});
  await cdp.until(`Boolean(${practiceButton})`);
  await snapshot('07-stale-route-home');
  const actions=cdp.events.filter(e=>e.method==='Network.requestWillBeSent'&&e.params.request.url.endsWith('/api/qte/practice/action')).map(e=>JSON.parse(e.params.request.postData));
  assert.equal(actions.length,2); assert(actions.every(a=>a.grade===null));
  const requests=cdp.events.filter(e=>e.method==='Network.requestWillBeSent').map(e=>e.params.request);
  const starts=requests.filter(r=>r.url.endsWith('/api/qte/practice/start')).map(r=>JSON.parse(r.postData));
  assert.equal(starts.length,1); assert.equal(starts[0].typeId,'BranchChoice');
  assert.equal(requests.filter(r=>r.url.endsWith('/api/qte/practice/retry')).length,1);
  assert.equal(requests.filter(r=>r.url.endsWith('/api/qte/practice/exit')).length,1);
  assert(!requests.some(r=>r.url.includes('/api/qte/daren')));
  assert(!cdp.events.some(e=>e.method==='Runtime.exceptionThrown'), 'unhandled browser exception');
  await writeFile(join(outputDirectory,'result.json'),JSON.stringify({status:'PASS',snapshots,events:cdp.events,proofLimit:'Actual Chromium desktop BranchChoice through mouse input, retry/exit and stale SPA URL. Eleven catalog entries; not eleven interactive playthroughs or a Windows/mobile qualification.'},null,2)+'\n');
} catch(error) {
  await writeFile(join(outputDirectory,'FIRST-failure.json'),JSON.stringify({status:'FAIL',error:String(error),stack:error.stack,snapshots,events:cdp.events},null,2)+'\n');
  throw error;
} finally { cdp.close(); }
