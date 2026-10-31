import test from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {createOperation} from './healing-tactics-model.mjs';

import * as probe from './healing-interruption-probe.mjs';
const runner=fileURLToPath(new URL('./healing-interruption-probe.mjs',import.meta.url));
function cli(...args){return spawnSync(process.execPath,[runner,...args],{encoding:'utf8'});}
function successful(...args){
  const out=cli(...args);assert.equal(out.status,0,out.stderr);assert.equal(out.stderr,'');
  return out.stdout.trim()?out.stdout.trim().split(/\r?\n/).map(line=>JSON.parse(line)):[];
}
const parameters=['--samples=1','--horizon=3'];

test('main and confirmation cover exactly the preregistered 120 conditions with distinct fixed seed suffixes',()=>{
  const main=probe.interruptionScenarios?.('main'),confirm=probe.interruptionScenarios?.('confirm');
  assert.equal(main?.length,120);assert.equal(confirm?.length,120);
  assert.equal(new Set(main.map(row=>JSON.stringify(row))).size,120);
  for(const harmony of ['healer28','both36'])for(const order of ['0,1','1,0'])
    for(const mode of ['instant','long','adaptive'])for(const attackPolicy of ['fixed_large','suppress_reserve'])
      for(const interruptPolicy of ['never','always','reserve_strong','reserve_large','release_reserve_large'])
        assert.equal(main.filter(row=>row.harmony===harmony&&row.order.join()===order&&row.mode===mode&&
          row.attackPolicy===attackPolicy&&row.interruptPolicy===interruptPolicy).length,1);
  for(let i=0;i<120;i++){
    const {seedSuffix,...cell}=main[i],{seedSuffix:other,...same}=confirm[i];
    assert.equal(seedSuffix,'T124-main-v1');assert.equal(other,'T124-confirm-v1');assert.deepEqual(cell,same);
    assert.equal(cell.D,130);assert.equal(cell.kind,'duel');assert.equal(cell.healingPercent,50);
    assert.equal(cell.resistance,25);assert.equal(cell.reply,'guard');assert.equal(cell.healerPolicy,'heal');
    assert.equal(cell.interrupt,true);assert.equal(cell.defender,'passive');assert.equal(cell.mirror,false);
  }
  assert.throws(()=>probe.interruptionScenarios?.('group'));
});

test('CLI preserves full cell parameters, independent seeds, accounting, completeness and operation reuse',()=>{
  const main=successful('--matrix=main',...parameters),confirm=successful('--matrix=confirm',...parameters);
  for(const [matrix,records] of [['main',main],['confirm',confirm]]){
    assert.equal(records.length,122);
    const metadata=records[0],complete=records.at(-1),cells=records.slice(1,-1);
    assert.equal(metadata.type,'metadata');assert.equal(metadata.version,'T124-v1');assert.equal(metadata.matrix,matrix);
    assert.equal(metadata.cells,120);assert.equal(metadata.samples,1);assert.equal(metadata.horizon,3);
    assert.equal(metadata.shardIndex,0);assert.equal(metadata.shardCount,1);
    assert.deepEqual(metadata.indices,Array.from({length:120},(_,i)=>i));
    assert.equal(cells.length,120);assert.equal(complete.type,'complete');assert.equal(complete.complete,true);
    assert.equal(complete.cells,120);assert.equal(complete.trajectories,120);assert.deepEqual(complete.indices,metadata.indices);
    assert.equal(complete.formulaReuse.damageComputed,8);assert.ok(complete.formulaReuse.damageHits>0);
    assert.ok(complete.formulaReuse.healing.hits>0);
    for(let i=0;i<120;i++){
      const result=cells[i];assert.equal(result.type,'cell');assert.equal(result.index,i);assert.equal(result.complete,true);
      assert.equal(result.seedSuffix,matrix==='main'?'T124-main-v1':'T124-confirm-v1');
      assert.equal(result.trials,1);assert.equal(result.n,1);assert.equal(result.horizon,3);
      assert.equal(result.outcomes.team0+result.outcomes.team1+result.outcomes.horizon,1);
      assert.equal(result.counts.interruptDecisions,Object.entries(result.counts)
        .filter(([key])=>key.startsWith('interruptDecision_')).reduce((sum,[,n])=>sum+n,0));
      assert.equal(result.counts.interruptions,result.counts.interruptDecision_attempt);
      assert.equal(result.counts.interruptPaid,result.counts.interruptions*2880);
      assert.ok(result.counts.byActor.A);assert.ok(result.counts.byActor.B);
      assert.equal(result.endpoints.total.achieved+result.endpoints.total.horizon,1);
      if(result.endpoints.total.horizon)assert.equal(result.endpoints.total.p95,null);
    }
  }
  for(let i=1;i<=120;i++)assert.notEqual(main[i].seed,confirm[i].seed);
  // The new runner must feed exactly the old simulator, including per-cell seed identity.
  const cell=probe.interruptionScenarios('main')[23];
  const expected=simulateTactics({...cell,n:1,horizon:3,operation:createOperation()});
  const {type,index,elapsedMs,complete,...actual}=main[24];assert.deepEqual(actual,expected);
});

test('shards form a disjoint complete union with original cell indices and identical unsharded results',()=>{
  const full=successful('--matrix=main',...parameters).slice(1,-1);
  assert.equal(full.length,120);
  const union=[];
  for(let shard=0;shard<3;shard++){
    const records=successful('--matrix=main',...parameters,`--shard-index=${shard}`,'--shard-count=3');
    assert.equal(records[0].shardIndex,shard);assert.equal(records[0].shardCount,3);
    const cells=records.slice(1,-1);assert.equal(cells.length,40);
    assert.deepEqual(records[0].indices,cells.map(row=>row.index));assert.equal(records.at(-1).trajectories,40);
    assert.deepEqual(records.at(-1).indices,records[0].indices);
    for(const cell of cells){
      assert.equal(cell.index%3,shard);
      const {elapsedMs,...actual}=cell,{elapsedMs:ignored,...expected}=full[cell.index];assert.deepEqual(actual,expected);
      union.push(cell.index);
    }
  }
  assert.deepEqual(union.sort((a,b)=>a-b),Array.from({length:120},(_,i)=>i));
});

test('invalid matrices, samples, horizons, shards and duplicate or unknown flags fail before emitting metadata',()=>{
  for(const args of [['--matrix=duel'],['--samples=0'],['--samples=1.5'],['--horizon=0'],
    ['--shard-count=0'],['--shard-count=2','--shard-index=2'],['--shard-index=-1'],
    ['--shard-count=1.5'],['--shard-count=121'],['--shard-index=9007199254740992'],
    ['--matrix=main','--matrix=confirm'],['--unknown=1']]){
    const out=cli(...args);assert.notEqual(out.status,0);assert.equal(out.stdout,'');
    assert.match(out.stderr,/Unknown|Invalid|Duplicate/);
  }
});
