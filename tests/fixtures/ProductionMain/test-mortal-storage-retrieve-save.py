import copy
import hashlib
import json
import unittest
import mortal_console_routes as routes

def after_grant(*args,**kw):
    if not hasattr(routes,"after_grant"): raise AssertionError("after_grant route gate missing")
    return routes.after_grant(*args,**kw)

def checked_retrieve_route(value):
    return routes.checked_route(value)

class RetrieveData(unittest.TestCase):
    def setUp(self):
        self.route={'Route':'ordinary-save-with-storage-retrieve','LocationId':'yard','StorageId':'locker','ItemId':'chalk'}
        self.verified_birth={key:self.route[key] for key in ('LocationId','StorageId','ItemId')}
        self.request={'sessionId':'s','requestId':'r','turnNumber':45}
        self.decision={**self.request,'Decision':'Grant'}
        self.recipient=[{'playerId':'player_current','playerName':'Пепельная Искра'}]
        self.owner={'ownerType':'Faction','ownerId':'council','ownerName':'Совет'}
        self.patch={'targetLocationId':'yard','storageId':'locker','update':{'newAuthorizedUsers':self.recipient,'newHasFullAccess':True}}
        self.body={'Writes':[{'Path':'game_state/world/world_map.json','Data':{'worldMapUpdates':{'storageUpdates':[self.patch]}}}]}
        self.accepted={**self.request,'AcceptedGameTurns':1}
        self.before={'locationId':'yard','storageId':'locker','owner':self.owner,'hasFullAccess':False,'authorizedUsers':[]}
        self.entry={**self.before,'hasFullAccess':True,'authorizedUsers':copy.deepcopy(self.recipient)}
        self.current={'locationId':'yard','locationStorages':[{**copy.deepcopy(self.entry),'name':'Шкаф','contents':[{'itemId':'chalk','name':'Мел','existedId':'chalk','count':1}]}]}
        self.access={'entries':[copy.deepcopy(self.entry)],'grantStorageAccess':[{'storageId':'locker'}]}
        self.inventory={'items':[]}

    def call(self, rebind=True):
        response=json.dumps(self.body).encode()
        if rebind:self.accepted['ResponseSHA256']=hashlib.sha256(response).hexdigest()
        return after_grant(self.route,self.request,self.accepted,self.decision,response,self.before,self.current,self.access,self.inventory,'Пепельная Искра',verified_birth=self.verified_birth)

    def test_exact_route(self):
        self.assertEqual(checked_retrieve_route(self.route),self.route)
        with self.assertRaises(ValueError):checked_retrieve_route(dict(self.route,Keys=['Enter']))

    def test_grant_yields_only_retrieve_target(self):
        target=self.call()
        self.assertIsInstance(target,dict)
        self.assertEqual((target['LocationId'],target['StorageId'],target['ItemId']),('yard','locker','chalk'))

    def test_defer_and_denial_stop(self):
        for decision in ['Defer','Deny']:
            self.decision['Decision']=decision
            self.body['Writes']=[]
            self.assertIsNone(self.call())

    def test_decision_wrong_turn_refuses(self):
        self.decision['turnNumber']+=1
        with self.assertRaises(ValueError):self.call()

    def test_consumed_packet_contents_must_match_joined_hash(self):
        self.call()
        self.patch['update']['newHasFullAccess']=False
        with self.assertRaisesRegex(ValueError,'response'):self.call(False)

    def test_pending_only_is_not_a_grant(self):
        self.body['Writes']=[]
        with self.assertRaises(ValueError):self.call()

    def test_no_permission_fabrication_from_old_effective_flag(self):
        self.before['hasFullAccess']=True
        with self.assertRaises(ValueError):self.call()

    def test_no_offscreen_retrieve(self):
        self.current['locationId']='mira'
        with self.assertRaises(ValueError):self.call()

    def test_no_duplicate_in_player_inventory(self):
        self.inventory['items']=[{'itemId':'chalk','name':'Мел'}]
        with self.assertRaises(ValueError):self.call()

    def test_exact_effective_projection_matches_location(self):
        self.access['entries'][0]['hasFullAccess']=False
        with self.assertRaises(ValueError):self.call()

    def test_wrong_quantity_refuses(self):
        self.current['locationStorages'][0]['contents'][0]['count']=2
        with self.assertRaises(ValueError):self.call()

    def test_self_consistent_wrong_route_refuses_against_verified_birth(self):
        self.verified_birth['ItemId']='original_chalk_item'
        with self.assertRaisesRegex(ValueError,'birth identity'):self.call()

    def test_effective_false_with_prior_recipient_is_not_ungranted_baseline(self):
        self.before['authorizedUsers']=self.recipient
        with self.assertRaisesRegex(ValueError,'locked coordinate'):self.call()

    def test_denial_with_grant_packet_is_inconsistent(self):
        self.decision['Decision']='Deny'
        with self.assertRaises(ValueError):self.call()

class ProductionRetrieveData(unittest.TestCase):
    def setUp(self):
        self.base=RetrieveData()
        self.base.setUp()
        self.chalk={'itemId':'chalk','existedId':'chalk','name':'Мел','count':1}
        self.base.current['locationStorages'][0]['contents']=[copy.deepcopy(self.chalk)]
        self.map={'realm':'mortal_world','locations':[{'locationId':'yard','locationStorages':[
            {k:copy.deepcopy(v) for k,v in self.base.current['locationStorages'][0].items() if k!='contents'}
        ]}]}
        self.offscreen={'schemaVersion':1,'entries':[]}
        self.offscreen_after=copy.deepcopy(self.offscreen)
        self.birth={'kind':'storage_addition','childId':'locker','entityId':'yard','sourceLocationId':'yard',
            'targetLocationId':'yard','turn':42,'transitionId':'birth','beforeState':{'state':'absent'},
            'afterState':{'storageId':'locker','hasFullAccess':False},'sourceAuthorityKind':'turn_outcome',
            'sourceAuthorityId':'turn_42','operationRef':'currentLocationData.locationStorages[0]'}
        self.location_before={'locationEntries':[{'locationId':'yard','transitions':[self.birth]}]}
        self.location_after=copy.deepcopy(self.location_before)
        self.location_after['locationEntries'][0]['transitions'].append({'kind':'storage_update','childId':'locker'})
        self.storage_carrier={'kind':'location_storage','ownerId':'yard','containerId':'locker','containerPath':[]}
        self.player_carrier={'kind':'player_inventory','ownerId':'player','containerId':None,'containerPath':[]}
        self.create={'transitionId':'created','kind':'create','turn':42,'sourceItemIds':[],
            'sourceCarrier':None,'destinationCarrier':self.storage_carrier,'quantityBefore':0,'quantityAfter':1,
            'authorityKind':'turn_outcome','authorityId':'turn_42'}
        self.transfer={'transitionId':'mitrn_'+'a'*32,'kind':'transfer','turn':42,'sourceItemIds':['chalk'],
            'sourceCarrier':self.storage_carrier,'destinationCarrier':self.player_carrier,'quantityBefore':1,
            'quantityAfter':1,'authorityKind':'local_storage_move','authorityId':'local_storage_move_'+'b'*32}
        self.item_before={'schemaVersion':1,'entries':[{'itemId':'chalk','receiptId':'receipt','state':'active',
            'currentCarrier':self.storage_carrier,'originMaterializationIds':['materialized'],'originCreationRefs':['creation'],
            'parentItemIds':[],'mergedIntoItemId':None,'transitions':[self.create]}]}
        self.item_after=copy.deepcopy(self.item_before)
        self.item_after['entries'][0]['currentCarrier']=self.player_carrier
        self.item_after['entries'][0]['transitions'].append(self.transfer)
        self.current_after=copy.deepcopy(self.base.current)
        self.current_after['locationStorages'][0]['contents']=[]
        self.inventory_after={'items':[copy.deepcopy(self.chalk)]}

    def witness(self):
        self.assertTrue(hasattr(routes,"require_retrieve_evidence"),"post-Retrieve route gate missing")
        return routes.require_retrieve_evidence(self.base.route,self.map,self.offscreen,self.offscreen_after,
            self.location_before,self.location_after,self.base.current,self.current_after,
            self.base.inventory,self.inventory_after,self.item_before,self.item_after)

    def test_birth_pin_binds_predecessor_request_and_cargo(self):
        prior={'turnNumber':42,'sessionId':'world','requestId':'birth-request'}
        source={'sessionId':'world','requestId':'birth-request','snapshotToken':'observed-test-snapshot'}
        self.birth['afterState']['cargoBirthEvidence']={
            'locationId':'yard','storageId':'locker','sourceTurn':42,'carrierKind':'location_storage',
            'sourceAuthority':source,'cargoMeasurements':[{'itemId':'chalk','count':1}]}
        expected={k:self.base.route[k] for k in ('LocationId','StorageId','ItemId')}
        self.assertEqual(routes.storage_birth_witness(self.base.route,self.location_before,prior),expected)
        source['requestId']='other-request'
        with self.assertRaisesRegex(ValueError,'accepted predecessor'):
            routes.storage_birth_witness(self.base.route,self.location_before,prior)

    def test_before_key_requires_real_receipt_and_current_carrier(self):
        item=self.base.current['locationStorages'][0]['contents'][0]
        item.update(materialization={'materializationId':'materialized'},materializationReceipt={'receiptId':'receipt'})
        def start():
            return routes.require_retrieve_start(self.base.route,self.map,self.offscreen,
                self.location_before,self.location_after,self.base.current,self.base.inventory,self.item_before)
        start()
        item['materializationReceipt']['receiptId']='unjoined'
        with self.assertRaisesRegex(ValueError,'chalk receipt'):start()

    def test_post_move_rejects_changed_item_birth_evidence(self):
        source=self.base.current['locationStorages'][0]['contents'][0]
        source['materializationReceipt']={'receiptId':'receipt'}
        self.inventory_after['items'][0]['materializationReceipt']={'receiptId':'foreign'}
        with self.assertRaisesRegex(ValueError,'immutable item birth evidence'):self.witness()

    def test_real_count_only_item_opens_preflight(self):
        try:
            target=self.base.call()
        except ValueError as exc:
            self.fail(f'Canonical count-only chalk must be accepted: {exc}')
        self.assertIsInstance(target,dict)

    def test_quantity_only_alias_is_not_canonical_count(self):
        observed=self.base.current['locationStorages'][0]['contents'][0]
        observed['quantity']=observed.pop('count')
        with self.assertRaisesRegex(ValueError,'count'):self.base.call()

    def test_bool_count_is_not_integer(self):
        self.base.current['locationStorages'][0]['contents'][0]['count']=True
        with self.assertRaisesRegex(ValueError,'count'):self.base.call()

    def test_wrong_existed_id_is_not_same_item(self):
        self.base.current['locationStorages'][0]['contents'][0]['existedId']='alias'
        with self.assertRaisesRegex(ValueError,'existedId'):self.base.call()

    def test_exact_carrier_transition_and_preserved_birth_pass(self):
        self.assertEqual(self.witness(),{'BirthTransitionId':'birth','ItemTransitionId':'mitrn_'+'a'*32})

    def test_mutating_birth_transition_refuses(self):
        self.location_after['locationEntries'][0]['transitions'][0]['afterState']['hasFullAccess']=True
        with self.assertRaisesRegex(ValueError,'birth transition'):self.witness()

    def test_canonical_map_disagrees_with_current_permission(self):
        self.map['locations'][0]['locationStorages'][0]['hasFullAccess']=False
        with self.assertRaisesRegex(ValueError,'canonical map'):self.witness()

    def test_offscreen_coordinate_must_not_duplicate_current(self):
        self.offscreen['entries']=[{'locationId':'yard','storageId':'locker','contents':[self.chalk]}]
        with self.assertRaisesRegex(ValueError,'offscreen'):self.witness()

    def test_transfer_preserves_prefix_and_appends_exactly_one(self):
        self.item_after['entries'][0]['transitions'][0]['transitionId']='replacement'
        with self.assertRaisesRegex(ValueError,'history prefix'):self.witness()

    def test_wrong_transfer_direction_refuses(self):
        self.transfer['destinationCarrier']=self.storage_carrier
        with self.assertRaisesRegex(ValueError,'local storage transfer'):self.witness()

    def test_move_does_not_use_new_gm_turn(self):
        self.transfer['turn']=45
        with self.assertRaisesRegex(ValueError,'local storage transfer'):self.witness()

    def test_post_inventory_must_be_one_exact_stack(self):
        self.inventory_after['items'][0]['count']=2
        with self.assertRaisesRegex(ValueError,'count'):self.witness()


class DriverStorageRoute(unittest.TestCase):
    def driver(self, name, ns):
        import ast
        from pathlib import Path
        source=Path(__file__).with_name('live-mortal-phase.py')
        nodes=[n for n in ast.parse(source.read_text()).body if isinstance(n,ast.FunctionDef) and n.name==name]
        self.assertEqual(len(nodes),1,'missing actual driver function '+name)
        exec(compile(ast.Module(body=nodes,type_ignores=[]),str(source),'exec'),ns)
        return ns[name]

    def test_driver_declines_without_move_or_save(self):
        import types
        actions=[]
        ns=dict(route={'Route':'ordinary-save-with-storage-retrieve'},result={},start=0,work_seconds=60,
            time=types.SimpleNamespace(monotonic=lambda:0),
            wait_daemon_consumer_returned=lambda _:actions.append('consumer'),
            accepted_storage_retrieve=lambda _:(actions.append('decision'),False)[1])
        self.driver('ordinary_save_after_acceptance',ns)()
        self.assertEqual(actions,['consumer','decision'])
        self.assertNotIn('OrdinarySaveProof',ns['result'])

    def test_positive_driver_checks_before_saving_same_turn(self):
        import types
        actions=[];snapshot={'Files':{'story':'6162'},'StoryPath':'story'}
        ns=dict(route={'Route':'ordinary-save-with-storage-retrieve'},result={},start=0,work_seconds=60,expected_turn=43,
             time=types.SimpleNamespace(monotonic=lambda:0),session=None,story=None,relay_queue=None,sha=lambda b:b.hex(),
             wait_daemon_consumer_returned=lambda _:actions.append('consumer'),
             accepted_storage_retrieve=lambda _:(actions.append('retrieve'),True)[1],
             save_current_turn=lambda turn:(actions.append(('save',turn)),{'ArchiveBytes':b'zip','CleanOutcomeBeforeFirstPlayerClear':True})[1],
             phase_evidence=types.SimpleNamespace(
                 ordinary_save_snapshot=lambda *a,**kw:(actions.append(('snapshot',kw)),snapshot)[1],
                 verify_ordinary_save_archive=lambda *_:actions.append('verify')))
        self.driver('ordinary_save_after_acceptance',ns)()
        self.assertEqual(actions,['consumer','retrieve',('snapshot',{'storage_retrieve':True}),('save',43),'verify',('snapshot',{'storage_retrieve':True})])

    def test_live_adapter_uses_retrieve_once_after_data_checks_and_never_calls_load(self):
        import ast, pathlib
        source=pathlib.Path(__file__).with_name('live-mortal-phase.py')
        functions={n.name:n for n in ast.parse(source.read_text()).body if isinstance(n,ast.FunctionDef)}
        accepted=ast.unparse(functions['accepted_storage_retrieve'])
        self.assertLess(accepted.index('after_grant('),accepted.index('require_retrieve_start('))
        self.assertLess(accepted.index('require_retrieve_start('),accepted.index('storage_ui_move(target, True)'))
        self.assertLess(accepted.index('storage_ui_move(target, True)'),accepted.index('require_retrieve_evidence('))
        self.assertEqual(accepted.count('storage_ui_move('),1)
        self.assertNotIn('load_created_save(',accepted)

    def test_final_result_does_not_confuse_denial_with_success(self):
        self.assertTrue(hasattr(routes,'storage_retrieve_save_proved'),'outcome proof missing')
        for decision in ('Deny','Defer','Grant'):
            with self.subTest(decision=decision):
                result={'StorageDecision':decision,'AcceptedTurnNumber':43}
                self.assertFalse(routes.storage_retrieve_save_proved(result))

if __name__=='__main__':unittest.main()
