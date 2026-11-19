// One owned terminal DATA process. Python alone owns and writes the PTY.
import {createRequire} from 'node:module';
import {createInterface} from 'node:readline';
import {readFileSync,realpathSync} from 'node:fs';
import {resolve} from 'node:path';
import {Admission} from './policy.mjs';
const prefix=realpathSync(process.argv[2]);
const req=createRequire(resolve(prefix,'package.json'));
for(const [name,version] of [['@xterm/headless','6.0.0'],['@xterm/addon-unicode11','0.9.0']]) {
 const p=JSON.parse(readFileSync(resolve(prefix,'node_modules',name,'package.json'),'utf8'));
 if(p.name!==name||p.version!==version) throw Error('Dependency identity mismatch');
}
const {Terminal}=req('@xterm/headless');
const {Unicode11Addon}=req('@xterm/addon-unicode11');
const term=new Terminal({cols:100,rows:25,convertEol:false,allowProposedApi:true,scrollback:1000,logLevel:'off',windowOptions:{}});
const addon=new Unicode11Addon();term.loadAddon(addon);term.unicode.activeVersion='11';
if(term.unicode.activeVersion!=='11')throw Error('Unicode profile not active');
const admission=new Admission();let seq=0,closed=false,batch=null;
const emit=x=>process.stdout.write(JSON.stringify(x)+'\n');
const subscription=term.onData(data=>{
 if(!batch||batch.replies.length>=batch.queries) throw Error('Unsolicited terminal reply');
 const state={x:term.buffer.active.cursorX,y:term.buffer.active.cursorY};
 const m=/^\x1b\[(\d+);(\d+)R$/.exec(data);
 if(!m||+m[1]!==state.y+1||+m[2]!==state.x+1)throw Error('Upstream reply/state mismatch');
 batch.replies.push({bytes:Buffer.from(data,'utf8').toString('base64'),state});
});
const binary=term.onBinary(()=>{throw Error('Unexpected binary response');});
function dispose(){subscription.dispose();binary.dispose();term.dispose();closed=true;}
async function command(c) {
 if(closed||!Number.isSafeInteger(c.seq)||c.seq!==seq+1)throw Error('Protocol order');
 seq=c.seq;
 if(c.op==='WRITE') {
  if(typeof c.bytes!=='string'||c.bytes.length>262144)throw Error('Invalid bytes');
  const raw=Buffer.from(c.bytes,'base64');if(raw.toString('base64')!==c.bytes)throw Error('Noncanonical bytes');
  const admitted=admission.feed(raw); // Entire batch admitted BEFORE upstream auto replies.
  batch={queries:admitted.queries,replies:[]};
  if(admitted.bytes.length)await new Promise(done=>term.write(admitted.bytes,done));
  if(batch.replies.length!==batch.queries)throw Error('Missing terminal reply');
  emit({op:'WRITTEN',seq,replies:batch.replies,pending:admitted.pending,modes:term.modes,cursor:{x:term.buffer.active.cursorX,y:term.buffer.active.cursorY}});batch=null;
 } else if(c.op==='SNAPSHOT') {
  if(admission.pending||admission.byteTail.length)throw Error('Incomplete control prefix');
  emit({op:'SNAPSHOT',seq,cursor:{x:term.buffer.active.cursorX,y:term.buffer.active.cursorY},lines:Array.from({length:25},(_,y)=>term.buffer.active.getLine(term.buffer.active.viewportY+y)?.translateToString(true)||'')});
 } else if(c.op==='CLOSE') {
  const end=admission.feed(new Uint8Array(),true);if(end.bytes.length||end.queries)throw Error('Undrained end');
  dispose();emit({op:'CLOSED',seq});
 } else throw Error('Unknown command');
}
emit({op:'READY',headless:'6.0.0',unicodeAddon:'0.9.0',unicode:'11',cols:term.cols,rows:term.rows});
const lines=createInterface({input:process.stdin,crlfDelay:Infinity});
let chain=Promise.resolve();
lines.on('line',line=>{chain=chain.then(()=>{if(line.length>300000)throw Error('Oversized protocol');return command(JSON.parse(line));}).catch(fail);});
lines.on('close',()=>{chain.then(()=>{if(!closed)throw Error('Protocol EOF without CLOSE');}).catch(fail);});
function fail(e){process.stderr.write(String(e.stack||e)+'\n');if(!closed)dispose();process.exitCode=1;lines.close();}
chain.catch(fail);
