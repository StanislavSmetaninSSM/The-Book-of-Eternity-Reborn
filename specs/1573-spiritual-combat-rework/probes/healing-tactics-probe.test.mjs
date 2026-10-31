import test from 'node:test';
import assert from 'node:assert/strict';
import {tacticsScenarios,simulateTactics,summarizeEndpoint} from './healing-tactics-probe.mjs';
test('preregistered matrix counts, all permutations, policies, access controls and independent confirmation',()=>{
  const expected={duel:756,support:288,group:1152,confirm:116};
  for(const [matrix,n] of Object.entries(expected)){
    const rows=tacticsScenarios(matrix);assert.equal(rows.length,n);
    assert.equal(new Set(rows.map(x=>JSON.stringify(x))).size,n);
    assert.ok(rows.every(x=>x.D===130));
  }
  const duel=tacticsScenarios('duel'),support=tacticsScenarios('support'),group=tacticsScenarios('group');
  assert.equal(new Set(duel.map(x=>x.attackPolicy)).size,7);
  assert.equal(new Set(support.map(x=>x.order.join())).size,6);
  assert.equal(new Set(group.map(x=>x.order.join())).size,24);
  for(const row of support.filter(x=>x.helperAccess))assert.ok(support.some(x=>JSON.stringify({...x,helperAccess:true})===JSON.stringify(row)&&x.helperAccess===false));
  assert.equal(group.filter(x=>x.mirror).length,144);
  assert.ok(tacticsScenarios('confirm').every(x=>x.seedSuffix==='T123-confirmation'));
  assert.equal((756+288+1152)*1000+116*10000,3356000);
});
test('endpoint quantiles retain competing no-event outcomes and horizon censoring',()=>{
  const summary=summarizeEndpoint([{time:2,kind:'event'},{time:3,kind:'no-event'},{time:5,kind:'horizon'},{time:4,kind:'event'}]);
  assert.equal(summary.achieved,2);assert.equal(summary.horizon,1);assert.equal(summary.noEvent,1);
  assert.equal(summary.median,4);assert.equal(summary.p95,null);assert.equal(summary.conditionalMedian,2);
});
test('simulation replays seeds deterministically with complete per-person accounting and censored quantiles',()=>{
  const options={kind:'group',mode:'adaptive',n:5,horizon:2,seedSuffix:'cheap'};
  const a=simulateTactics(options),b=simulateTactics(options);assert.deepEqual(a,b);
  assert.equal(a.trials,5);assert.equal(a.outcomes.horizon+a.outcomes.team0+a.outcomes.team1,5);
  assert.equal(a.endpoints.total.median,null);assert.equal(a.endpoints.total.p95,null);
  assert.ok(a.counts.byActor.A.attacks>0);assert.ok(a.counts.byActor.C.healingMains>0);
  assert.notEqual(a.seed,simulateTactics({...options,seedSuffix:'independent'}).seed);
});
