import test from 'node:test';
import assert from 'node:assert/strict';
import {simulateModes,strikeAttack} from './healing-modes-probe.mjs';
import {attackProfile,utilityReport,amplificationScenarios} from './healing-amplification.mjs';
import {actor,begin} from './healing-modes-model.mjs';
import {price} from './reaction-model.mjs';

test('strong and large attacks use existing B/cost and cannot inherit suppression for free',()=>{
  assert.deepEqual(attackProfile('ordinary'),{B:28,K:8});
  assert.deepEqual(attackProfile('strong'),{B:36,K:16});
  assert.deepEqual(attackProfile('large'),{B:44,K:24});
  assert.throws(()=>simulateModes({strike:'strong',style:'suppress',n:1}));
  assert.throws(()=>simulateModes({strike:'invented',n:1}));
});
test('runner exercises stronger damage and correctly pays the bigger main price',()=>{
  for(const strike of ['strong','large']){
    const r=simulateModes({strike,D:130,harmony:'both36',n:100});
    assert.equal(r.strike,strike);assert.equal(r.attackPaid,r.counts.attacks*price(actor(130,'a'),attackProfile(strike).K));
    assert.ok(r.counts.damageToB/r.counts.attacks>125);
    const starved=simulateModes({strike,D:0,harmony:'healer28',mode:'long',n:100});
    assert.ok(starved.counts.recoverA>0);
  }
});
test('unaffordable strong strike cannot borrow end energy, damage target or spend Main',()=>{
  const a=actor(130,'a'),t=actor(130,'t');begin(a);a.energy=price(a,16)-1;
  assert.equal(strikeAttack(a,t,{damage:100,quality:2},'strong'),false);
  assert.equal(a.energy,5759);assert.equal(a.used,false);assert.equal(t.hp,1030);
  a.energy++;assert.equal(strikeAttack(a,t,{damage:100,quality:2},'strong'),true);
  assert.equal(a.energy,0);assert.equal(t.hp,930);assert.equal(a.used,true);
});
test('instant rescues a burst victim while long earns its delay over two legal critical hits',()=>{
  const r=utilityReport();assert.equal(r.burst.damage,382);
  assert.equal(r.burst.withoutHealing,0);assert.equal(r.burst.instant,10);
  assert.equal(r.twoHits.instant,0);assert.equal(r.twoHits.long,1);
  assert.equal(r.twoHits.instantPaid,r.twoHits.longPaid);assert.equal(r.twoHits.main,2);
});
test('ally revival remains useful, modifiers reach real threshold and suppression still matters',()=>{
  const r=utilityReport().revival,find=(mode,bonus,suppression)=>r.find(x=>x.mode===mode&&x.bonus===bonus&&x.suppression===suppression);
  assert.equal(find('instant',0,0).firstHeal,72);assert.equal(find('instant',0,0).mainsToRevival,2);
  assert.equal(find('instant',50,0).firstHeal,108);assert.equal(find('instant',50,0).mainsToRevival,1);
  assert.equal(find('long',0,0).firstHeal,165);assert.equal(find('long',0,0).mainsToRevival,2);
  assert.equal(find('long',0,50).firstHeal,82);assert.equal(find('long',0,50).mainsToRevival,4);
  assert.equal(find('long',25,50).firstHeal,103);assert.equal(find('long',25,50).mainsToRevival,2);
});
test('matrix covers approved countermeasures and keeps support/scale/confirmation explicit',()=>{
  const rows=amplificationScenarios('main');assert.ok(rows.some(r=>r.strike==='strong'&&r.reply==='guard'));
  assert.ok(rows.some(r=>r.strike==='large'));assert.ok(rows.some(r=>r.policy==='cleanse_safe'));
  assert.equal(new Set(rows.map(r=>JSON.stringify(r))).size,rows.length);
  assert.equal(new Set(amplificationScenarios('support').map(r=>r.order.join())).size,6);
  assert.deepEqual([...new Set(amplificationScenarios('scaling').map(r=>r.D))],[0,1000]);
  assert.ok(amplificationScenarios('confirm').every(r=>r.seedSuffix==='T122-confirmation'));
});
