import { writeFile } from 'node:fs/promises';
export async function connectCdp(webSocketUrl) {
  const socket = new WebSocket(webSocketUrl);
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, {once:true}); socket.addEventListener('error', reject, {once:true}); });
  const pending = new Map(); const events = []; let serial = 0;
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data);
    if (message.id) {
      const waiter = pending.get(message.id); if (!waiter) return;
      pending.delete(message.id); clearTimeout(waiter.timer);
      message.error ? waiter.reject(new Error(JSON.stringify(message.error))) : waiter.resolve(message.result);
    } else if (['Runtime.exceptionThrown','Log.entryAdded','Network.responseReceived','Network.requestWillBeSent','Network.loadingFailed'].includes(message.method)) {
      events.push(message);
    }
  });
  socket.addEventListener('close', () => { for (const waiter of pending.values()) { clearTimeout(waiter.timer); waiter.reject(new Error('CDP connection closed')); } pending.clear(); });
  function call(method, params={}) {
    return new Promise((resolve,reject) => {
      const id=++serial; const timer=setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); },15000);
      pending.set(id,{resolve,reject,timer}); socket.send(JSON.stringify({id,method,params}));
    });
  }
  async function evaluate(expression) {
    const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
  }
  async function until(expression, timeoutMs=15000) {
    const end=Date.now()+timeoutMs; let last;
    while(Date.now()<end) { last=await evaluate(expression); if(last) return last; await new Promise(resolve=>setTimeout(resolve,150)); }
    throw new Error(`DOM condition timed out: ${expression}; last=${JSON.stringify(last)}`);
  }
  async function screenshot(path) { const result=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true}); await writeFile(path,Buffer.from(result.data,'base64')); }
  return {call,evaluate,until,screenshot,events,close:()=>socket.close()};
}
