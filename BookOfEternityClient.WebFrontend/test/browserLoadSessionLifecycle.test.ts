import { describe, expect, it } from 'vitest';
import { createSettingsComponentHarness, flushPromises, nodes, ok } from './helpers/settingsComponentHarness';

describe('actual Load handlers await original fresh-launch receipt after applying refresh', () => {
  for (const component of ['launcher', 'settings'] as const) {
    it(`${component}: failed original stop names the GM and never refreshes/restarts`, async()=>{
      const h=createSettingsComponentHarness();const view=h[component]();
      nodes(view.tree).find(n=>n.type==='button' && n.props.children==='Загрузить сохранение')!.props.onClick();
      h.load.resolve(ok({success:false,disposition:'NotLoaded',error:'',loadedSaveId:'',selectedSourcePath:null,establishedGeneration:null,
        needsFollowUp:true,continuationBlocked:true,lifecycleOperationId:h.loadRequest().operationId,mainSessionState:'Uncertain',freshLaunchRequired:false}));
      await flushPromises();
      expect(h.counts()).toMatchObject({refreshes:0,loadCompletions:0,navigations:0,loadCancels:1});
      expect(h.shell.loadContinuationNotice.message).toContain('ГМ');
    });
    for(const fault of ['refresh-failed','stale-owner','wrong-generation','confirmed','no-active'] as const) {
      it(`${component}: ${fault} keeps one original dispatch and exact fresh decision`, async()=>{
        const h=createSettingsComponentHarness();const view=h[component]();
        const button=nodes(view.tree).find(n=>n.type==='button' && n.props.children==='Загрузить сохранение')!;
        button.props.onClick();button.props.onClick();
        if(fault==='refresh-failed')h.setRefreshFailure();
        if(fault==='stale-owner')view.unmount();
        const result={success:true,disposition:'Committed',error:'',loadedSaveId:'save',selectedSourcePath:'/isolated/source.zip',
          establishedGeneration:'loaded-generation',needsFollowUp:false,continuationBlocked:false,lifecycleOperationId:h.loadRequest().operationId,
          state:{establishedGeneration:'loaded-generation',menu:h.shell.menu,session:{},game:null,noActiveSession:true,settings:h.persistedSettings,audio:h.initialAudio},
          mainSessionState:fault==='no-active'?'NoActiveSession':'Stopped',freshLaunchRequired:fault!=='no-active'};
        h.load.resolve(ok(result));await flushPromises();
        if(fault==='confirmed'||fault==='wrong-generation') {
          h.loadComplete.resolve(ok({...result,establishedGeneration:fault==='wrong-generation'?'newer-generation':result.establishedGeneration,
            freshLaunchRequired:false,mainSessionState:'Running'}));await flushPromises();
        }
        const success=fault==='confirmed'||fault==='no-active';
        expect(h.counts().loadPosts).toBe(1);expect(h.counts().navigations).toBe(success?1:0);
        expect(h.counts().loadCompletions).toBe(fault==='confirmed'||fault==='wrong-generation'?1:0);
        if(!success)expect(h.shell.loadContinuationNotice).toMatchObject({disposition:'Committed',establishedGeneration:'loaded-generation',continuationBlocked:true});
        if(fault==='stale-owner'||fault==='refresh-failed')expect(h.counts().loadCancels).toBe(1);
      });
    }
    for (const fault of ['missing-source', 'lost-restart'] as const) {
      it(`${component}: ${fault} preserves Committed and blocks fresh continuation`, async () => {
        const h=createSettingsComponentHarness();const view=h[component]();
        nodes(view.tree).find(n=>n.type==='button' && n.props.children==='Загрузить сохранение')!.props.onClick();
        const result={ success:true,disposition:'Committed',error:'',loadedSaveId:'save',selectedSourcePath:'/isolated/source.zip',
          establishedGeneration:'loaded-generation',needsFollowUp:false,continuationBlocked:false,
          lifecycleOperationId:h.loadRequest().operationId,mainSessionState:'Stopped',freshLaunchRequired:true,
          state:{establishedGeneration:'loaded-generation',menu:h.shell.menu,session:{},game:null,noActiveSession:true,settings:h.persistedSettings,audio:h.initialAudio}};
        h.load.resolve(ok(result));await flushPromises();
        if(fault==='missing-source')h.loadComplete.resolve(ok({...result,selectedSourcePath:null,freshLaunchRequired:false,mainSessionState:'Running'}));
        else h.loadComplete.reject(new Error('controlled lost restart response'));
        await flushPromises();
        expect(h.counts().navigations).toBe(0);
        expect(h.shell.loadContinuationNotice).toMatchObject({disposition:'Committed',continuationBlocked:true,selectedSourcePath:'/isolated/source.zip'});
        expect(h.shell.loadContinuationNotice.message).toContain('ГМ');
        expect(h.counts().loadCompletions).toBe(1);
      });
    }
    it(`${component}: cannot navigate while fresh launch remains unconfirmed`, async () => {
      const h = createSettingsComponentHarness(); const view = h[component]();
      const button = nodes(view.tree).find(n => n.type === 'button' && n.props.children === 'Загрузить сохранение')!;
      button.props.onClick();
      h.load.resolve(ok({ success: true, disposition: 'Committed', error: '', loadedSaveId: 'save',
        selectedSourcePath: '/isolated/source.zip', establishedGeneration: 'loaded-generation',
        needsFollowUp: false, continuationBlocked: false, lifecycleOperationId: h.loadRequest().operationId, state: { establishedGeneration: 'loaded-generation', menu: h.shell.menu, session: {}, game: null, noActiveSession: true, settings: h.persistedSettings, audio: h.initialAudio },
        mainSessionState: 'Stopped', freshLaunchRequired: true }));
      await flushPromises();
      expect(h.counts().refreshes).toBe(1);
      expect(h.counts().navigations).toBe(0);
      expect(h.counts().loadCompletions).toBe(1);
    });
  }
});
