// Focused guards for the exploratory model; not production game tests.
import test from 'node:test';
import assert from 'node:assert/strict';
import { makeActor, begin, end, mark, strength, healAmount, heal, cleanse, recover,
  attack, endBattle, sourceDefeated, profileFor, damageTable } from './suppression-model.mjs';

test('healing multiplies exact amount before the sole rounding and deficit cap', () => {
  const a=makeActor(130,'a'); a.hp=0;
  assert.equal(healAmount(a),227);
  mark(a,'s',2);assert.equal(healAmount(a),113);
  a.hp=a.p.hp-10;assert.equal(healAmount(a),10);
  a.hp=0;a.marks=[];mark(a,'s',1);assert.equal(healAmount(a),170);
});
test('full mark lasts through end events of two following target opportunities', () => {
  const a=makeActor(130,'a'); mark(a,'s',2);
  begin(a);assert.equal(strength(a),50);end(a);assert.equal(strength(a),50);
  begin(a);assert.equal(strength(a),50);end(a);assert.equal(strength(a),0);
});
test('application inside target opportunity excludes its remainder from duration', () => {
  const a=makeActor(130,'a');begin(a);mark(a,'s',1);end(a);
  assert.equal(strength(a),25);begin(a);assert.equal(strength(a),25);end(a);
  assert.equal(strength(a),0);
});
test('weak reapplication cannot prolong or downgrade own strong mark', () => {
  const a=makeActor(130,'a');mark(a,'s',2);begin(a);end(a);
  const expiry=a.marks[0].expires;mark(a,'s',1);
  assert.equal(a.marks.length,1);assert.equal(a.marks[0].expires,expiry);
  assert.equal(strength(a),50);begin(a);end(a);assert.equal(strength(a),0);
});
test('same strength refresh resets bounded deadline and strong replaces weak', () => {
  const a=makeActor(130,'a');mark(a,'s',1);mark(a,'s',2);
  begin(a);end(a);mark(a,'s',2);
  assert.equal(a.marks.length,1);assert.equal(a.marks[0].expires,3);
  begin(a);end(a);assert.equal(strength(a),50);begin(a);end(a);assert.equal(strength(a),0);
});
test('different sources use strongest only with independent deadlines', () => {
  const a=makeActor(130,'a');mark(a,'s',2);begin(a);end(a);begin(a);
  mark(a,'t',1);assert.equal(strength(a),50);end(a);
  assert.equal(strength(a),25);begin(a);end(a);assert.equal(strength(a),0);
});
test('cleanse removes this entire family on one target, not health', () => {
  const a=makeActor(130,'a');a.hp=100;mark(a,'s',2);mark(a,'t',1);begin(a);
  const energy=a.energy;assert.equal(cleanse(a,a),true);
  assert.equal(a.energy,energy-a.p.costs[0]);assert.equal(strength(a),0);assert.equal(a.hp,100);
  assert.equal(heal(a),false);end(a); // One Main: cleanse cannot also heal.
});
test('ally cleansing spends only the helper own scaled energy and Main', () => {
  const helper=makeActor(50,'helper'), patient=makeActor(130,'patient');mark(patient,'s',2);
  const patientEnergy=patient.energy;begin(helper);assert.equal(cleanse(helper,patient),true);
  assert.equal(helper.energy,helper.p.max-helper.p.costs[0]);assert.equal(patient.energy,patientEnergy);
  assert.equal(patient.started,0);assert.equal(strength(patient),0);end(helper);
});
test('insufficient energy cannot borrow end regeneration or consume Main', () => {
  const a=makeActor(130,'a');a.hp=1;a.energy=a.p.costs[3]-1;begin(a);
  const energy=a.energy;assert.equal(heal(a),false);assert.equal(a.energy,energy);
  assert.equal(a.used,false);assert.equal(recover(a),true);end(a);
  assert.equal(a.energy,Math.min(a.p.max,energy+a.p.gain+a.regen));
});
test('one end regeneration, controlled skip still expires, extra Main is unavailable', () => {
  const a=makeActor(130,'a');a.energy=0;mark(a,'s',1);begin(a);
  assert.throws(()=>begin(a));end(a);assert.equal(strength(a),0);assert.equal(a.energy,a.regen);
  assert.throws(()=>end(a));
});
test('damage fully absorbed after a hit does not prevent explicit rider', () => {
  const a=makeActor(130,'a'), b=makeActor(130,'b');begin(a);
  const hp=b.hp;assert.equal(attack(a,b,{damage:200,quality:2},'suppress',true),true);
  assert.equal(b.hp,hp);assert.equal(strength(b),50);end(a);
});
test('failure has no rider; critical rider remains full50 for two opportunities', () => {
  const a=makeActor(130,'a'), b=makeActor(130,'b');begin(a);
  attack(a,b,{damage:0,quality:0},'suppress');assert.equal(strength(b),0);end(a);
  begin(a);attack(a,b,{damage:1,quality:3},'suppress');assert.equal(strength(b),50);
  assert.equal(b.marks[0].expires,2);end(a);
});
test('source defeat keeps mark; confirmed target exit/end removes it', () => {
  const a=makeActor(130,'a'), b=makeActor(130,'b');mark(b,a.id,2);sourceDefeated(a);
  assert.equal(strength(b),50);endBattle(b);assert.equal(strength(b),0);
});
test('snapshot copy preserves duration without sharing mutable marks', () => {
  const a=makeActor(130,'a');mark(a,'s',2);const b=structuredClone(a);
  begin(b);end(b);assert.equal(a.started,0);assert.equal(a.marks[0].expires,2);
});
test('high Harmony pays V each opportunity without increasing stored maximum', () => {
  const a=makeActor(130,'a',true), ordinary=makeActor(130,'b');a.hp=1;
  assert.equal(a.p.max,ordinary.p.max);assert.equal(a.regen,a.p.costs[3]);
  begin(a);heal(a);end(a);assert.equal(a.energy,a.p.max);
});
test('new attack shares exact accepted damage outcomes; source stats are unchanged', () => {
  const p=profileFor(130), table=damageTable(p,24);
  assert.equal(table.length,400);assert.ok(table.every(o=>Number.isSafeInteger(o.damage)));
  assert.equal(table[0].quality,0);assert.equal(table[399].quality,3);
  assert.equal(p.hp,1030);assert.equal(p.heal,227);
});
