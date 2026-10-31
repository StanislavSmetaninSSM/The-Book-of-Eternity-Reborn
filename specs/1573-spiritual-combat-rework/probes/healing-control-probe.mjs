// T129 fixed investigation. JSONL stdout; full diagnostic records belong outside Git.
import assert from 'node:assert/strict';
import {pathToFileURL} from 'node:url';
import {resolve} from 'node:path';
import {createOperation} from './healing-tactics-model.mjs';
import {simulateTactics} from './healing-tactics-probe.mjs';
import {controlProfiles} from './healing-control-model.mjs';

const builds=Object.freeze({normal:{harmony:'normal',healingPercent:0,resistance:0},
  fortified_healer:{harmony:'healer28',healingPercent:50,resistance:25},
  fortified_both:{harmony:'both36',healingPercent:50,resistance:25}});
const tactics=Object.freeze([{controlProfile:'none',controlPolicy:'pressure'},
  ...['seal','stun','stun_long'].flatMap(controlProfile=>['pressure','window'].map(controlPolicy=>({controlProfile,controlPolicy})))].map(Object.freeze));
const groupOrders=Object.freeze([[0,1,3,2],[0,3,2,1],[2,3,0,1],[3,2,0,1]].map(Object.freeze));
export function controlScenarios(matrix='main'){
  assert.ok(['main','confirm'].includes(matrix),'Unknown T129 matrix');const rows=[];
  function add(slice,benchmarkProfile,tactic,R,activeGuard,order,defender,helperControlRelease=false){
    rows.push({slice,benchmarkProfile,D:130,kind:slice==='group'?'group':'duel',...builds[benchmarkProfile],
      healingRules:'periodic-v1',periodicMode:'combined',periodicAttack:'suppress_needed',level:5,effectResistance:R,
      cleansePolicy:'first',activeGuard,order:[...order],defender,interrupt:false,controlRules:'control-v1',...tactic,
      helperControlRelease,seedSuffix:matrix==='main'?'T129-main-v1':'T129-confirm-v1'});
  }
  for(const build of Object.keys(builds))for(const tactic of tactics)for(const R of [0,25])for(const guard of [false,true])
    for(const order of [[0,1],[1,0]])for(const defender of ['passive','offensive'])add('duel',build,tactic,R,guard,order,defender);
  for(const build of ['fortified_healer','fortified_both'])for(const tactic of tactics)for(const R of [50,90])
    for(const order of [[0,1],[1,0]])add('r-stress',build,tactic,R,true,order,'passive');
  for(const build of ['normal','fortified_both'])for(const tactic of tactics)for(const release of [false,true])
    for(const order of groupOrders)add('group',build,tactic,25,true,order,'offensive',release);
  return rows;
}
export function simulateControl(options={}){return simulateTactics({seedVersion:'T129-v1',...options});}

export function controlCommandOptions(args){
  const values={},allowed=['matrix','samples','horizon','shard-index','shard-count'];
  for(const arg of args){const match=/^--([^=]+)=(.+)$/.exec(arg);
    assert.ok(match&&allowed.includes(match[1]),`Unknown T129 argument: ${arg}`);
    assert.ok(!Object.hasOwn(values,match[1]),`Duplicate T129 argument: ${match[1]}`);values[match[1]]=match[2];}
  const matrix=values.matrix??'main',rows=controlScenarios(matrix);
  function integer(key,fallback,min,max=Number.MAX_SAFE_INTEGER){const raw=values[key]??String(fallback),value=Number(raw);
    assert.ok(/^\d+$/.test(raw)&&Number.isSafeInteger(value)&&value>=min&&value<=max,`Invalid ${key}: ${raw}`);return value;}
  const n=integer('samples',matrix==='confirm'?1000:100,1),horizon=integer('horizon',2000,1);
  const shardCount=integer('shard-count',1,1,rows.length),shardIndex=integer('shard-index',0,0,shardCount-1);
  return {matrix,rows,n,horizon,shardCount,shardIndex};
}
if(process.argv[1]&&pathToFileURL(resolve(process.argv[1])).href===import.meta.url){
  const {matrix,rows,n,horizon,shardCount,shardIndex}=controlCommandOptions(process.argv.slice(2));
  const indices=rows.map((_,i)=>i).filter(i=>i%shardCount===shardIndex),operation=createOperation(),started=performance.now();
  console.log(JSON.stringify({type:'metadata',version:'T129-v1',baseModel:'T128-v1',matrix,cells:rows.length,samples:n,horizon,
    shardIndex,shardCount,indices,shardCells:indices.length,node:process.version,profiles:controlProfiles,tactics,builds,groupOrders,
    seedScheme:'FNV full cell including policy/matrix suffix + samples/horizon/T129-v1; independent trial/damage/effect-save/control-save/break streams; no shard/index/time',
    scope:'Numerical experiment; control prices/durations are explicit probe assumptions. T127 healing/suppression unchanged; no Heal interruptions.',
    authority:'Seal forbids new healing/cleansing but permits break/attack/Recover/guard. Stun forbids Main and active reactions; free ordinary defense/dueHoT/regen continue.',
    control:'Exact source/art/target, weaker refresh cannot shorten, independent sources, no DR. Save after eligible hit even crit; partial stun has no control/save.',
    breaks:'One Main/8P; current Will modifier versus snapshotted source Will modifier; Favor controls crit. Success>=partial removes exactly one bond, no extra Main.',
    helper:'Existing group roles [primary,striker,healer,helper], teams[0,0,1,1]; free helper release before legacy cleanse/attack/Recover when enabled.',
    window:'No family control and (HP<=65% or damaged without activeHoT); pressure casts whenever family free. Both fall back to the same suppress_needed/ordinary.',
    units:'Paid/Gained counters in hundredths DE; HP counters in HP. controlPaid is included in attackPaid; controlBreakPaid is separate. allyControlBreaks counts paid attempts.',
    endpoint:'total/firstPatientFall/firstAnyFall separate; unconditional ranks null when insufficient events; horizon is not a win. Last current end effects finish before result.',
    limitations:'Visible-state heuristics, not optimal AI; fixed controlR=healer effectR assumption; equal-stat check table reuse; unequal build costs; no production implementation.'}));
  for(const index of indices){const start=performance.now(),result=simulateControl({...rows[index],n,horizon,operation});
    console.log(JSON.stringify({type:'cell',index,complete:true,elapsedMs:performance.now()-start,...result}));}
  console.log(JSON.stringify({type:'complete',complete:true,version:'T129-v1',matrix,cells:indices.length,matrixCells:rows.length,
    samples:n,horizon,shardIndex,shardCount,indices,trajectories:indices.length*n,elapsedMs:performance.now()-started,
    formulaReuse:{...operation.stats,healing:operation.calculate.stats}}));
}
