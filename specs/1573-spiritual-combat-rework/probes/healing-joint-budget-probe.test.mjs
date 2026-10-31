import test from 'node:test';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {jointBudgetScenarios} from './healing-joint-budget-probe.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {createOperation} from './healing-tactics-model.mjs';

const path=fileURLToPath(new URL('./healing-joint-budget-probe.mjs',import.meta.url));
function cli(args){return spawnSync(process.execPath,[path,...args],{encoding:'utf8'});}
function successful(...args){
  const result=cli(['--samples=1','--horizon=3',...args]);assert.equal(result.status,0,result.stderr);assert.equal(result.stderr,'');
  return result.stdout.trim()?result.stdout.trim().split(/\r?\n/).map(JSON.parse):[];
}

test('108-cell cross product fixes six tactics, profiles, orders and modes without mixing confirmation',()=>{
  const main=jointBudgetScenarios('main'),confirm=jointBudgetScenarios('confirm');
  assert.equal(main.length,108);assert.equal(confirm.length,108);assert.equal(new Set(main.map(JSON.stringify)).size,108);
  for(const benchmarkProfile of ['fortified_healer28','fortified_both36','unboosted_normal'])
    for(const order of ['0,1','1,0'])for(const mode of ['instant','long','adaptive'])
      for(const tactic of ['legacy_large_never','legacy_large_always','legacy_suppress_reserve',
        'planner_never','planner_always','planner_joint'])
        assert.equal(main.filter(row=>row.benchmarkProfile===benchmarkProfile&&row.order.join()===order&&row.mode===mode&&row.tactic===tactic).length,1);
  for(let i=0;i<108;i++){
    const {seedSuffix,...cell}=main[i],{seedSuffix:other,...same}=confirm[i];assert.deepEqual(cell,same);
    assert.equal(seedSuffix,'T125-main-v1');assert.equal(other,'T125-confirm-v1');
    assert.equal(cell.D,130);assert.equal(cell.kind,'duel');assert.equal(cell.reply,'guard');assert.equal(cell.healerPolicy,'heal');
    assert.equal(cell.healingPercent,cell.benchmarkProfile==='unboosted_normal'?0:50);
    assert.equal(cell.resistance,cell.benchmarkProfile==='unboosted_normal'?0:25);
    if(cell.jointPolicy!==undefined){assert.equal(cell.attackPolicy,'fixed_large');assert.equal(cell.interruptPolicy,undefined);}
  }
  assert.throws(()=>jointBudgetScenarios('support'));
});

test('CLI emits complete parameters, opt-in planner roster, normalized accounting, independent seeds and reuse',()=>{
  const sets=[successful('--matrix=main'),successful('--matrix=confirm')];
  for(const [setIndex,records] of sets.entries()){
    assert.equal(records.length,110);const header=records[0],complete=records.at(-1),cells=records.slice(1,-1);
    assert.equal(header.type,'metadata');assert.equal(header.version,'T125-v1');assert.equal(header.plannerHorizon,3);
    assert.equal(header.cells,108);assert.equal(header.samples,1);assert.equal(header.horizon,3);
    assert.deepEqual(header.indices,Array.from({length:108},(_,i)=>i));
    assert.equal(complete.complete,true);assert.equal(complete.cells,108);assert.equal(complete.trajectories,108);
    assert.deepEqual(complete.indices,header.indices);assert.equal(complete.formulaReuse.joint.computed,3);
    assert.ok(complete.formulaReuse.joint.hits>0);assert.ok(complete.formulaReuse.joint.lookups>0);
    for(const [i,row] of cells.entries()){
      assert.equal(row.type,'cell');assert.equal(row.index,i);assert.equal(row.complete,true);
      assert.equal(row.seedSuffix,setIndex?'T125-confirm-v1':'T125-main-v1');
      assert.equal(row.trials,1);assert.equal(row.horizon,3);
      assert.equal(row.outcomes.team0+row.outcomes.team1+row.outcomes.horizon,1);
      assert.equal(row.endpoints.total.achieved+row.endpoints.total.horizon,1);
      if(row.jointPolicy!==undefined){
        assert.equal(row.roster[0].mainAttackPolicy,'joint_pressure_v1');
        const count=row.counts;assert.equal(count.jointDecisions,Object.entries(count).filter(([k])=>k.startsWith('jointDecision_')).reduce((s,[,v])=>s+v,0));
        assert.equal(count.interruptions,count.jointDecision_attempt);assert.equal(count.interruptPaid,count.interruptions*2880);
        assert.equal(count.jointMainDecisions,count.byActor.A.attacks+count.byActor.A.recover);
      }else assert.equal(Object.hasOwn(row.counts,'jointDecisions'),false);
    }
  }
  for(let i=1;i<=108;i++)assert.notEqual(sets[0][i].seed,sets[1][i].seed);
  const cell=jointBudgetScenarios('main')[3],expected=simulateTactics({...cell,n:1,horizon:3,operation:createOperation()});
  const {type,index,complete,elapsedMs,...actual}=sets[0][4];assert.deepEqual(actual,expected);
});

test('deterministic shards retain original indices, seed identities and exact unsharded outputs',()=>{
  const full=successful('--matrix=main').slice(1,-1);assert.equal(full.length,108);const union=[];
  for(let shard=0;shard<2;shard++){
    const records=successful('--matrix=main',`--shard-index=${shard}`,'--shard-count=2');
    assert.equal(records[0].shardIndex,shard);assert.equal(records[0].shardCount,2);
    const cells=records.slice(1,-1);assert.equal(cells.length,54);assert.equal(records.at(-1).trajectories,54);
    assert.deepEqual(cells.map(row=>row.index),records[0].indices);
    for(const cell of cells){
      assert.equal(cell.index%2,shard);const {elapsedMs,...actual}=cell,{elapsedMs:ignored,...expected}=full[cell.index];
      assert.deepEqual(actual,expected);union.push(cell.index);
    }
  }
  assert.deepEqual(union.sort((a,b)=>a-b),Array.from({length:108},(_,i)=>i));
});

test('invalid flags, matrix and shard ranges fail before metadata',()=>{
  for(const args of [['--matrix=duel'],['--samples=0'],['--samples=1.5'],['--horizon=0'],
    ['--shard-count=0'],['--shard-count=109'],['--shard-count=2','--shard-index=2'],['--shard-index=-1'],
    ['--shard-count=1.5'],['--matrix=main','--matrix=confirm'],['--unknown=1']]){
    const out=cli(args);assert.notEqual(out.status,0);assert.equal(out.stdout,'');assert.match(out.stderr,/Invalid|Unknown|Duplicate/);
  }
});
