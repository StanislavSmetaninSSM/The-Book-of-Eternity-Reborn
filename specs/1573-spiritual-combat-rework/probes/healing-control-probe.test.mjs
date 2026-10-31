import test from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import * as probe from './healing-control-probe.mjs';

test('T129 covers504 exact disjoint duel/stress/group cells with independent confirmation',()=>{
  assert.equal(typeof probe.controlScenarios,'function');const rows=probe.controlScenarios();
  assert.equal(rows.length,504);assert.equal(new Set(rows.map(x=>JSON.stringify(x))).size,504);
  assert.deepEqual(Object.fromEntries(['duel','r-stress','group'].map(k=>[k,rows.filter(x=>x.slice===k).length])),{duel:336,'r-stress':56,group:112});
  assert.ok(rows.every(x=>x.controlRules==='control-v1'&&x.periodicAttack==='suppress_needed'&&x.cleansePolicy==='first'&&x.level===5));
  assert.deepEqual(probe.controlScenarios('confirm').map(({seedSuffix,...x})=>x),rows.map(({seedSuffix,...x})=>x));
});
test('full parameter seed is stable and independent of confirmation/sample count',()=>{
  const cell=probe.controlScenarios()[0],x=probe.simulateControl({...cell,n:1,horizon:3});
  assert.deepEqual(x,probe.simulateControl({...cell,n:1,horizon:3}));
  assert.notEqual(x.seed,probe.simulateControl({...probe.controlScenarios('confirm')[0],n:1,horizon:3}).seed);
  assert.notEqual(x.seed,probe.simulateControl({...cell,n:2,horizon:3}).seed);
});
test('all504 main/confirm smoke and three-shard union preserve complete exact results',()=>{
  function cli(args){const r=spawnSync(process.execPath,[fileURLToPath(new URL('./healing-control-probe.mjs',import.meta.url)),
    '--samples=1','--horizon=3',...args],{encoding:'utf8',maxBuffer:40*1024*1024});assert.equal(r.status,0,r.stderr);assert.equal(r.stderr,'');
    return r.stdout.trim().split('\n').map(JSON.parse);}
  const records=cli([]),full=records.filter(x=>x.type==='cell');assert.equal(full.length,504);assert.equal(records.at(-1).trajectories,504);
  const shards=[0,1,2].flatMap(i=>cli(['--shard-count=3',`--shard-index=${i}`]).filter(x=>x.type==='cell')).sort((a,b)=>a.index-b.index);
  const strip=({elapsedMs,...x})=>x;assert.deepEqual(shards.map(strip),full.map(strip));
  assert.equal(cli(['--matrix=confirm']).filter(x=>x.type==='cell').length,504);
  for(const x of full){assert.equal(x.outcomes.team0+x.outcomes.team1+x.outcomes.horizon,1);
    for(const e of Object.values(x.endpoints))assert.equal(e.achieved+e.horizon+e.noEvent,1);}
});
test('invalid control runner flags fail before metadata',()=>{
  for(const args of [['--matrix=bad'],['--samples=0'],['--samples=1.5'],['--horizon=0'],['--shard-count=0'],
    ['--shard-count=505'],['--shard-count=3','--shard-index=3'],['--matrix=main','--matrix=confirm'],['--unknown=1']]){
    const r=spawnSync(process.execPath,[fileURLToPath(new URL('./healing-control-probe.mjs',import.meta.url)),...args],{encoding:'utf8'});
    assert.notEqual(r.status,0);assert.equal(r.stdout,'');assert.match(r.stderr,/Invalid|Unknown|Duplicate/);}
});
