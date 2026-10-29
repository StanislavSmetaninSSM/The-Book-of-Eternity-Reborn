// #1573 T065-T067: standalone exploratory arithmetic, NOT production combat.
// No game imports, filesystem writes, future-roll lookahead, or diagnostic quotas.
// Run with Node; redirect stdout outside the checkout. Scenario horizon is explicit.
import assert from 'node:assert/strict';

const samples = 10000;
const horizon = 2000;
const round = (n, d) => Number((2n * n + d) / (2n * d));
function rng(seed) {
  let state = seed | 0;
  return () => { state ^= state << 13; state ^= state >>> 17; state ^= state << 5; return state >>> 0; };
}
function seedFor(name) {
  let hash = 0x1573065;
  for (const char of name) hash = Math.imul(hash ^ char.charCodeAt(0), 16777619);
  return (hash >>> 0) || 1;
}
function profile(D) {
  const X = D <= 50 ? 7 + 3 * D / 10 : 22 + 5 * (D - 50) / 10;
  const S = D <= 50 ? 5 + 3 * D / 10 : 20 + 5 * (D - 50) / 10;
  const a2 = BigInt(Math.min(40 + D, 2 * X));
  const scale = BigInt(50 + D);
  const f = BigInt(X);
  const hp = round((200n + 3n * a2) * scale, 100n);
  const heal = round(22n * (200n + 3n * a2) * scale, 10000n);
  const costs = [8, 16, 24, 28].map(k => 2 * k * (50 + D));
  const attacks = [28, 36, 44].map(B => {
    const fullN = BigInt(B) * (200n + 3n * a2) * scale * (200n + 2n * a2);
    const fullD = 10000n * (200n + 3n * a2);
    const threshold = 20 - Number(5n * f / (100n + f));
    const values = [];
    for (let a = 1; a <= 20; a++) for (let b = 1; b <= 20; b++) {
      let value;
      if (a >= threshold || (a !== 1 && a - b >= 10)) {
        value = round(fullN * (150n + 2n * f), fullD * (100n + f));
      } else if (a === 1 || a - b < 0) value = 0;
      else value = round(fullN, fullD * (a - b < 5 ? 2n : 1n));
      values.push(value);
    }
    return { values, full: round(fullN, fullD), mean: values.reduce((a,b)=>a+b,0)/400 };
  });
  return { D, X, S, H:S, hp, heal, costs, attacks,
    max: 80 * (50 + D) + 100 * S,
    regen: 8 * (50 + D) + 20 * S, gain: 32 * (50 + D) };
}
const profiles = [0, 50, 130, 210, 1000].map(profile);

// Reproduce the preceding v1 experiment, including its continuous seed stream.
function baseline() {
  const random = rng(0x1573064), result = [];
  for (const p of profiles) for (const B of [0,20,22]) {
    const healing = round(BigInt(B) * BigInt(200+3*Math.min(40+p.D,2*p.X)) * BigInt(50+p.D),10000n);
    const durations = [];
    for (let trial=0; trial<samples; trial++) {
      let hp=p.hp, a=p.max, d=p.max, turns=0;
      while(hp>0) {
        turns++;
        if(a>=p.costs[0]) { a-=p.costs[0]; hp-=p.attacks[0].values[random()%400]; }
        else a+=p.gain;
        a=Math.min(p.max,a+p.regen);
        if(hp<=0) break;
        if(B>0) {
          if(hp<p.hp && d>=p.costs[3]) { hp=Math.min(p.hp,hp+healing); d-=p.costs[3]; }
          else d+=p.gain;
        }
        d=Math.min(p.max,d+p.regen);
      }
      durations.push(turns);
    }
    durations.sort((a,b)=>a-b);
    result.push({D:p.D,B,mean:durations.reduce((a,b)=>a+b,0)/samples,p95:durations[9499],max:durations[9999]});
  }
  assert.deepEqual(result.filter(r=>r.B===22).map(r=>[r.mean,r.max]),
    [[35.5184,256],[46.2882,358],[46.9178,493],[42.7929,361],[30.6163,223]]);
  return result;
}

// A strategy sees current state only. 'finish' uses full ordinary-success large damage,
// not a future critical/roll; 'alternate' changes grade only after an actual attack.
function attackGrade(policy, me, other, p) {
  if(policy==='strong') return 1;
  if(policy==='large') return 2;
  if(policy==='alternate') return me.attackCount%2 ? 2 : 0;
  if(policy==='finish') return other.hp<=p.attacks[2].full && me.ap>=p.costs[2] ? 2 : 0;
  return 0;
}
function run(p, aPolicy, bPolicy, {first=0, interrupt=null, highHarmony=false}={}) {
  const key=`${p.D}|${aPolicy}|${bPolicy}|${first}|${JSON.stringify(interrupt)}|${highHarmony}`;
  const random=rng(seedFor(key)), durations=[], wins=[0,0], totals={attack:[0,0],heal:[0,0],recover:[0,0],prepare:0,interrupt:0,cancel:0};
  let censored=0;
  for(let trial=0;trial<samples;trial++) {
    const actors=[0,1].map(i=>({hp:p.hp,ap:p.max,attackCount:0,prepared:false,prepareNext:false,
      regen:highHarmony && i===1 ? p.costs[3] : p.regen}));
    let turns=0, winner=-1;
    for(;turns<horizon && winner<0;) {
      turns++;
      let reactionUsed=false;
      for(const id of [first,1-first]) {
        const me=actors[id], other=actors[1-id], policy=id===0?aPolicy:bPolicy;
        me.prepared=false; // Prepared reply expires at the beginning of its owner's next turn.
        const threshold=policy.startsWith('mix')?Number(policy.slice(3))/100:null;
        const wantsHeal=policy==='heal' ? me.hp<p.hp : threshold!==null && me.hp<=threshold*p.hp;
        if(wantsHeal) {
          if(me.ap>=p.costs[3]) {
            me.ap-=p.costs[3]; totals.heal[id]++;
            let cancelled=false;
            if(id===1 && interrupt) {
              const price=2*interrupt.cost*(50+p.D);
              const prepared=interrupt.mode==='prepared' && other.prepared;
              const immediate=interrupt.mode==='reaction' && !reactionUsed && other.ap>=price;
              if(prepared || immediate) {
                other.prepared=false; reactionUsed=true;
                if(immediate) other.ap-=price;
                totals.interrupt++;
                cancelled=random()%100<interrupt.chance;
                if(cancelled) totals.cancel++;
              }
            }
            if(!cancelled) me.hp=Math.min(p.hp,me.hp+p.heal);
          } else { me.ap+=p.gain; totals.recover[id]++; }
        } else if(policy==='heal') { me.ap+=p.gain; totals.recover[id]++; }
        else if(id===0 && interrupt?.mode==='prepared' && me.prepareNext) {
          const price=2*interrupt.cost*(50+p.D);
          if(me.ap>=price) {me.ap-=price; me.prepared=true;me.prepareNext=false;totals.prepare++;}
          else {me.ap+=p.gain;totals.recover[id]++;}
        } else {
          const grade=attackGrade(policy,me,other,p);
          if(me.ap>=p.costs[grade]) {
            me.ap-=p.costs[grade]; me.attackCount++;totals.attack[id]++;
            other.hp-=p.attacks[grade].values[random()%400];
            if(id===0 && interrupt?.mode==='prepared') me.prepareNext=true;
          } else {me.ap+=p.gain;totals.recover[id]++;}
        }
        me.ap=Math.min(p.max,me.ap+me.regen);
        if(other.hp<=0) {winner=id;break;}
      }
    }
    if(winner<0) censored++; else wins[winner]++;
    durations.push(turns);
  }
  durations.sort((a,b)=>a-b);
  return {D:p.D,a:aPolicy,b:bPolicy,first,interrupt,highHarmony,n:samples,
    meanObserved:durations.reduce((a,b)=>a+b,0)/samples,median:durations[4999],p95:durations[9499],
    maxObserved:durations[9999],censored,winA:wins[0]/samples,winB:wins[1]/samples,
    actionsPerTrial:Object.fromEntries(Object.entries(totals).map(([k,v])=>[k,Array.isArray(v)?v.map(n=>n/samples):v/samples]))};
}
function throughput(p,grade) {
  let ap=p.max,step=0,count=0;
  const seen=new Map();
  while(!seen.has(ap)) {
    seen.set(ap,[step,count]); step++;
    if(ap>=p.costs[grade]) {ap-=p.costs[grade];count++;}else ap+=p.gain;
    ap=Math.min(p.max,ap+p.regen);
  }
  const [start,casts]=seen.get(ap);
  return {D:p.D,B:[28,36,44][grade],fraction:(count-casts)/(step-start),
    damagePerTurn:p.attacks[grade].mean*(count-casts)/(step-start)};
}

const started=performance.now();
// Additional no-healing duel control can be run without repeating the main matrix.
if(process.argv.includes('--controls')) {
  const rows=profiles.flatMap(p=>[0,1].map(first=>run(p,'ordinary','ordinary',{first})));
  console.log(JSON.stringify({version:1,samples,horizon,elapsedMs:performance.now()-started,rows},null,2));
  process.exit(0);
}
const old=baseline();
const rows=[];
for(const p of profiles) for(const first of [0,1]) {
  for(const attack of ['ordinary','strong','large','alternate','finish']) rows.push(run(p,attack,'heal',{first}));
  for(const mix of ['mix25','mix50','mix75']) {
    for(const attack of ['ordinary','strong','large']) rows.push(run(p,attack,mix,{first}));
    rows.push(run(p,mix,mix,{first}));
  }
  for(const mode of ['prepared','reaction']) for(const cost of [4,8,16]) for(const chance of [25,50,75,100]) {
    rows.push(run(p,'ordinary','heal',{first,interrupt:{mode,cost,chance}}));
  }
}
for(const first of [0,1]) for(const attack of ['ordinary','strong','large']) {
  rows.push(run(profiles[2],attack,'heal',{first,highHarmony:true}));
  rows.push(run(profiles[2],attack,'heal',{first,highHarmony:true,interrupt:{mode:'reaction',cost:8,chance:50}}));
}
const cycle=profiles.flatMap(p=>[0,1,2].map(g=>throughput(p,g)));
console.log(JSON.stringify({version:1,samples,horizon,seed:'xorshift32; FNV-like per-scenario seedFor',
  elapsedMs:performance.now()-started,baseline:old,profiles:profiles.map(({attacks,...p})=>p),cycle,rows},null,2));
