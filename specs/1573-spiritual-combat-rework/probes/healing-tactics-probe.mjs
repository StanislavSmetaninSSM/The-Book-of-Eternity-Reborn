// Reproducible T123 measurements. Stdout is JSONL only; raw belongs outside Git.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {seedFor} from './healing-counters.mjs';
import {attackPolicies,createOperation,createEncounter,runTrial} from './healing-tactics-model.mjs';

function permutations(values){return values.length?values.flatMap((v,i)=>permutations(values.filter((_,j)=>j!==i)).map(t=>[v,...t])):[[]];}
const orders={duel:permutations([0,1]),support:permutations([0,1,2]),group:permutations([0,1,2,3])};
const modes=['instant','long','adaptive'],builds=[{healingPercent:0,resistance:0},{healingPercent:25,resistance:25},{healingPercent:50,resistance:25}];
const targets=['patient','helper','switch'];
export function tacticsScenarios(matrix='duel') {
  const rows=[];
  function add(kind,build,harmonies,selectedModes,policies,options={}){
    for(const harmony of harmonies)for(const mode of selectedModes)for(const order of orders[kind])
      for(const attackPolicy of policies)rows.push({D:130,kind,...build,harmony,mode,order,attackPolicy,
        targetPolicy:'patient',helperAccess:true,defender:'passive',healerPolicy:'heal',reply:'guard',interrupt:true,mirror:false,...options});
  }
  function support(build,harmonies,selectedModes){
    for(const targetPolicy of targets)for(const helperAccess of [false,true])
      add('support',build,harmonies,selectedModes,['suppress_reserve'],{targetPolicy,helperAccess});
  }
  function group(build,harmonies,selectedModes,defenders,selectedTargets=targets){
    for(const defender of defenders)for(const targetPolicy of selectedTargets)
      add('group',build,harmonies,selectedModes,['suppress_reserve'],{targetPolicy,defender});
  }
  if(matrix==='duel'){
    for(const reply of ['guard','counter'])for(const healerPolicy of ['heal','cleanse_safe'])
      add('duel',builds[2],['normal','healer28','both36'],modes,attackPolicies,{reply,healerPolicy});
    for(const build of builds.slice(0,2))add('duel',build,['healer28','both36'],modes,attackPolicies);
    add('duel',builds[2],['healer28','both36'],modes,attackPolicies,{interrupt:false});
  }else if(matrix==='support'){
    support(builds[2],['normal','both36'],modes);
    for(const build of builds.slice(0,2))support(build,['both36'],['instant']);
  }else if(matrix==='group'){
    group(builds[2],['normal','both36'],modes,['passive','offensive']);
    for(const build of builds.slice(0,2))group(build,['both36'],['instant'],['offensive']);
    add('group',builds[2],['normal','both36'],modes,['suppress_reserve'],{mirror:true,targetPolicy:'switch',defender:'offensive'});
  }else if(matrix==='confirm'){
    add('duel',builds[2],['healer28','both36'],['instant','adaptive'],['fixed_suppress','greedy','reserve','suppress_reserve']);
    support(builds[2],['both36'],['instant']);
    group(builds[2],['normal','both36'],['instant'],['offensive'],['switch']);
    for(const row of rows)row.seedSuffix='T123-confirmation';
  }else throw Error('Unknown T123 matrix');
  return rows;
}

const quantile=(sorted,q)=>sorted.length?sorted[Math.ceil(sorted.length*q)-1]:null;
/** Unconditional ranks require the event in enough trials; competing outcomes never become horizon times. */
export function summarizeEndpoint(values) {
  const observed=values.filter(x=>x.kind==='event').map(x=>x.time).sort((a,b)=>a-b);
  const countsByActor={};for(const value of values)if(value.kind==='event'&&value.actorId)countsByActor[value.actorId]=(countsByActor[value.actorId]??0)+1;
  const rank=q=>observed.length>=Math.ceil(values.length*q)?observed[Math.ceil(values.length*q)-1]:null;
  return {achieved:observed.length,fraction:observed.length/values.length,horizon:values.filter(x=>x.kind==='horizon').length,
    noEvent:values.filter(x=>x.kind==='no-event').length,median:rank(.5),p95:rank(.95),
    conditionalMedian:quantile(observed,.5),conditionalP95:quantile(observed,.95),countsByActor};
}
function mergeCounts(to,from){
  for(const [key,value] of Object.entries(from)){
    if(typeof value==='number')to[key]=(to[key]??0)+value;
    else{to[key]??={};mergeCounts(to[key],value);}
  }
}
export function simulateTactics(options={}) {
  const {n=1000,horizon=2000,operation=createOperation(),seedVersion='T123-v1',...cell}=options;
  assert.ok(Number.isInteger(n)&&n>0&&Number.isInteger(horizon)&&horizon>0);
  const seed=seedFor(JSON.stringify({version:seedVersion,...cell,n,horizon}));
  const roster=createEncounter({...cell,horizon,operation}).actors.map(a=>({id:a.id,team:a.team,role:a.role,HP:a.maxHp,
    maxDE:a.p.max/100,regenDE:a.regen/100,healingPercent:a.healingPercent,resistance:a.resistance,
    attackable:a.attackable,canHeal:a.role==='healer',canCleanse:['healer','helper'].includes(a.role),
    canInterrupt:cell.interrupt!==false&&cell.healingRules!=='periodic-v1',reaction:1,ordinaryAttack:true,
    ...(cell.healingRules==='periodic-v1'?{effectResistance:a.effectResistance,healingRules:cell.healingRules,
      healingMode:cell.periodicMode,level:cell.level,activeGuard:cell.activeGuard,cleansePolicy:cell.cleansePolicy}:{}),
    ...(cell.controlRules==='control-v1'?{controlRules:cell.controlRules,controlProfile:cell.controlProfile,
      controlPolicy:cell.controlPolicy,helperControlRelease:cell.helperControlRelease,controlResistance:a.effectResistance}:{}),
    mainAttackPolicy:a.role==='primary'?(cell.healingRules==='periodic-v1'?cell.periodicAttack:
      cell.jointPolicy!==undefined?'joint_pressure_v1':cell.attackPolicy??'suppress_reserve'):'fixed_ordinary'}));
  const endpoints={total:[],firstPatientFall:[],firstAnyFall:[]},counts={},outcomes={team0:0,team1:0,horizon:0};
  for(let trial=0;trial<n;trial++){
    const result=runTrial({...cell,horizon,operation,seed:seedFor(`${seed}|trial|${trial}`)});
    outcomes[result.winner===null?'horizon':`team${result.winner}`]++;
    for(const key of Object.keys(endpoints))endpoints[key].push(result.endpoints[key]);
    mergeCounts(counts,result.counts);
  }
  const actionsPerTrial={};mergeCounts(actionsPerTrial,counts);
  for(const key of Object.keys(actionsPerTrial))if(typeof actionsPerTrial[key]==='number')actionsPerTrial[key]/=n;
  else for(const a of Object.values(actionsPerTrial[key]))for(const k of Object.keys(a))a[k]/=n;
  return {...cell,n,horizon,seed,trials:n,roster,outcomes,endpoints:Object.fromEntries(Object.entries(endpoints).map(([k,v])=>[k,summarizeEndpoint(v)])),
    counts,actionsPerTrial};
}

if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const args=process.argv.slice(2),matrix=args.find(x=>x.startsWith('--matrix='))?.split('=')[1]??'duel';
  const n=Number(args.find(x=>x.startsWith('--samples='))?.split('=')[1]??(matrix==='confirm'?10000:1000));
  const horizon=Number(args.find(x=>x.startsWith('--horizon='))?.split('=')[1]??2000);
  assert.ok(Number.isInteger(n)&&n>0&&Number.isInteger(horizon)&&horizon>0);
  const rows=tacticsScenarios(matrix),started=performance.now(),operation=createOperation();
  console.log(JSON.stringify({type:'metadata',version:'T123-v1',matrix,cells:rows.length,samples:n,horizon,node:process.version,
    attackPolicies,seedScheme:'FNV cell parameters/sample/horizon -> independent trial -> separate damage/reaction streams',
    limitations:'Visible-state heuristics; not optimal AI. Unequal roles and explicitly declared healing/resistance bonuses.',
    reserve:'Pay now; min(MaxDE,current-cost+ownRegen)>=16P',suppression:'strength<50 or every 50-mark expires<=target.started',
    groupDefeat:'all team members collapsed',endpoint:'total separate from firstPatientFall/firstAnyFall; no-event distinct from horizon'}));
  for(let index=0;index<rows.length;index++){
    const start=performance.now(),result=simulateTactics({...rows[index],n,horizon,operation});
    console.log(JSON.stringify({type:'cell',index,elapsedMs:performance.now()-start,...result}));
  }
  console.log(JSON.stringify({type:'complete',matrix,cells:rows.length,trajectories:rows.length*n,elapsedMs:performance.now()-started,
    formulaReuse:{...operation.stats,healing:operation.calculate.stats}}));
}
