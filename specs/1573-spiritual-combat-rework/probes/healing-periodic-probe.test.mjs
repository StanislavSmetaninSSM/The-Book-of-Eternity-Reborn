import test from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import * as probe from './healing-periodic-probe.mjs';

test('T128 matrix covers exact 684 unique cells and named disjoint slices',()=>{
  assert.equal(typeof probe.periodicScenarios,'function');const rows=probe.periodicScenarios();
  assert.equal(rows.length,684);assert.equal(new Set(rows.map(x=>JSON.stringify(x))).size,684);
  assert.deepEqual(Object.fromEntries(['core','r90','tiers','scaling','offensive'].map(k=>[k,rows.filter(x=>x.slice===k).length])),
    {core:468,r90:48,tiers:64,scaling:32,offensive:72});
  assert.ok(rows.every(x=>x.healingRules==='periodic-v1'&&!x.interrupt));
  assert.deepEqual(probe.periodicScenarios('confirm').map(({seedSuffix,...x})=>x),rows.map(({seedSuffix,...x})=>x));
});
test('seeds include full parameters and independent confirmation, excluding shard position',()=>{
  const a=probe.periodicScenarios()[0],b=probe.periodicScenarios('confirm')[0];
  const x=probe.simulatePeriodic({...a,n:1,horizon:3}),y=probe.simulatePeriodic({...a,n:1,horizon:3});
  assert.deepEqual(x,y);assert.notEqual(x.seed,probe.simulatePeriodic({...b,n:1,horizon:3}).seed);
  assert.notEqual(x.seed,probe.simulatePeriodic({...a,n:2,horizon:3}).seed);
  assert.equal(x.outcomes.team0+x.outcomes.team1+x.outcomes.horizon,1);
  for(const row of Object.values(x.endpoints))assert.equal(row.achieved+row.horizon+row.noEvent,1);
});
test('strict CLI three-shard union exactly reproduces unsharded all-cell smoke',()=>{
  function run(args){const r=spawnSync(process.execPath,[fileURLToPath(new URL('./healing-periodic-probe.mjs',import.meta.url)),
    '--samples=1','--horizon=3',...args],{encoding:'utf8',maxBuffer:40*1024*1024});assert.equal(r.status,0,r.stderr);return r.stdout.trim().split('\n').map(JSON.parse);}
  const full=run([]).filter(x=>x.type==='cell');assert.equal(full.length,684);
  const shards=[0,1,2].flatMap(i=>run(['--shard-count=3',`--shard-index=${i}`]).filter(x=>x.type==='cell')).sort((a,b)=>a.index-b.index);
  const strip=({elapsedMs,...x})=>x;assert.deepEqual(shards.map(strip),full.map(strip));
  assert.equal(run(['--matrix=confirm']).filter(x=>x.type==='cell').length,684);
});
test('invalid runner inputs fail before producing metadata',()=>{
  for(const args of [['--matrix=duel'],['--samples=0'],['--samples=1.5'],['--horizon=0'],['--shard-count=0'],
    ['--shard-count=685'],['--shard-count=3','--shard-index=3'],['--shard-index=-1'],['--matrix=main','--matrix=confirm'],['--bad=1']]){
    const r=spawnSync(process.execPath,[fileURLToPath(new URL('./healing-periodic-probe.mjs',import.meta.url)),...args],{encoding:'utf8'});
    assert.notEqual(r.status,0);assert.equal(r.stdout,'');assert.match(r.stderr,/Unknown|Invalid|Duplicate/);
  }
});
