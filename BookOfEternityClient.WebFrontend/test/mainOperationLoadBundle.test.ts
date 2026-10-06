import { describe, expect, it } from 'vitest';
import { refreshShellAfterLoad } from '../src/hooks/refreshShellAfterLoad';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

const bundle = { establishedGeneration:'exact-generation', menu:{}, session:{}, game:{},
  noActiveSession:false, settings:{}, audio:{} };
const decision = (disposition: string) => ({ disposition, success:disposition==='Committed', error:'', menu:null,
  loadedSaveId:disposition==='Committed'?'save':'', selectedSourcePath:'/isolated/fixture.zip',
  establishedGeneration:disposition==='NotLoaded'?null:'exact-generation', needsFollowUp:false,
  continuationBlocked:false, state:bundle });

describe('F2 original Load guard publishes its same-response bundle',()=>{
  for(const component of ['settings','launcher'] as const) for(const disposition of ['Committed','RolledBack','NotLoaded']) {
    it(`${component} ${disposition}: forwards original bundle and makes no second load-state request`,async()=>{
      const h=createSettingsComponentHarness();const view=h[component]();let received:unknown;let reads=0;let published=0;
      h.shell.refreshAfterLoad=async(generation:string|null,current:()=>boolean,allow:boolean,state:unknown)=>{
        received=state;
        return (refreshShellAfterLoad as any)({ getLoadState:async()=>{reads++;return ok(bundle);} },
          ()=>{published++;},{current:0},generation,current,allow,state);
      };
      view.render();nodes(view.tree).find(n=>n.type==='button'&&n.props.children==='Загрузить сохранение')!.props.onClick();
      h.load.resolve(ok(decision(disposition)));await flushPromises();
      expect(received).toEqual(bundle);expect(reads).toBe(0);expect(published).toBe(1);
      expect(h.counts().loadPosts).toBe(1);expect(h.counts().navigations).toBe(disposition==='Committed'?1:0);
    });
  }
  for(const component of ['settings','launcher'] as const) for(const bad of ['missing','stale','incomplete']) {
    it(`${component} ${bad}: keeps committed decision without publishing, navigating or refetching`,async()=>{
      const h=createSettingsComponentHarness();const view=h[component]();let reads=0;let published=0;
      h.shell.refreshAfterLoad=async(generation:string|null,current:()=>boolean,allow:boolean,state:unknown)=>
        (refreshShellAfterLoad as any)({getLoadState:async()=>{reads++;return ok(bundle);}},()=>{published++;},{current:0},generation,current,allow,state);
      const result=decision('Committed');
      (result as any).state=bad==='missing'?null:bad==='stale'?{...bundle,establishedGeneration:'other'}:{...bundle,settings:null};
      view.render();nodes(view.tree).find(n=>n.type==='button'&&n.props.children==='Загрузить сохранение')!.props.onClick();
      h.load.resolve(ok(result));await flushPromises();
      expect(reads).toBe(0);expect(published).toBe(0);expect(h.counts().loadPosts).toBe(1);expect(h.counts().navigations).toBe(0);
    });
  }
});
