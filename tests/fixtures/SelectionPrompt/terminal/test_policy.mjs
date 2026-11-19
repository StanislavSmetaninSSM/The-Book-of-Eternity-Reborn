// SOURCE ONLY until review: no package code imported by these admission controls.
import assert from 'node:assert/strict';
import {Admission} from './policy.mjs';
const text='⚙️ Игровое меню\r\n⚔️ 📦 é 中文\x1b[6n';
const raw=Buffer.from(text);
for(let split=0;split<=raw.length;split++) {
 const a=new Admission();const first=a.feed(raw.subarray(0,split));const next=a.feed(raw.subarray(split));
 assert.equal(Buffer.concat([first.bytes,next.bytes]).toString(),text);
 assert.equal(first.queries+next.queries,1);assert.equal(next.pending,false);a.feed(Buffer.alloc(0),true);
}
for(const bad of ['\x1b[?1049h','\x1b[8m','\x1b]52;c;x\x07','\x1b[5n','\x1b[999A','\x1bPfoo','\x00']) {
 assert.throws(()=>new Admission().feed(Buffer.from(bad)));
}
for(const partial of [Buffer.from('\x1b['),Buffer.from([0xf0,0x9f])]) {
 const a=new Admission();assert.equal(a.feed(partial).pending,true);assert.throws(()=>a.feed(Buffer.alloc(0),true));
}
// Summary follows all assertions.
// console.log(JSON.stringify({Suite:'terminal-admission',Success:true,Utf8Splits:raw.length+1,RejectedControls:7,PartialEOF:2}));

// BOM is valid DATA and must never disappear at a chunk boundary.
for(const text of ['\ufeffabc','a\ufeffb','\ufeff\x1b[6n\ufeff']) {
 const bytes=Buffer.from(text);
 for(let split=0;split<=bytes.length;split++) {
  const a=new Admission();const x=a.feed(bytes.subarray(0,split));const y=a.feed(bytes.subarray(split));
  assert.deepEqual(Buffer.concat([x.bytes,y.bytes,a.feed(Buffer.alloc(0),true).bytes]),bytes);
 }
}
console.log(JSON.stringify({Suite:'terminal-admission',Success:true,Utf8Splits:raw.length+1,RejectedControls:7,PartialEOF:2,BomCorpus:3}));
