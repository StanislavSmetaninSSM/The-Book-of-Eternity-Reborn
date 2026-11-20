"""T024 finite console route DATA. Never issues authority or accepts a game turn.

Inputs are fresh observed UI/canonical DATA, not permission to mutate. The driver
must still use the ordinary console action and independently check its result.
"""
import re
import json
import hashlib
import unicodedata

def checked_route(value):
    if value is None:return {'Route':'birth'}
    if type(value) is not dict:raise ValueError('Finite route object required')
    route=value.get('Route')
    fields={'birth':{'Route'},'ordinary-save':{'Route'},'self-save-load-turn':{'Route'},
            'permission-with-denial':{'Route','StorageId','ItemId'},
            'storage-local-proof':{'Route','StorageId','ItemId'},
            'ordinary-save-with-storage-retrieve':{'Route','LocationId','StorageId','ItemId'}}.get(route)
    if fields is None or set(value)!=fields:raise ValueError('Unknown route or extra input program')
    for key in fields-{'Route'}:
        if type(value[key]) is not str or not value[key].strip() or any(ord(c)<32 for c in value[key]):
            raise ValueError('Exact canonical target identity required')
    if route=='ordinary-save-with-storage-retrieve' and any(value[key]!=value[key].strip() for key in fields-{'Route'}):
        raise ValueError('Exact storage retrieve identity required')
    return dict(value)

def storage_target(location,storage_id,item_id,accessible):
    if type(location) is not dict or type(location.get('locationId')) is not str or not location['locationId']:
        raise ValueError('Accepted current location identity required')
    storages=location.get('locationStorages')
    if type(storages) is not list:raise ValueError('Current storage list required')
    matches=[s for s in storages if type(s) is dict and s.get('storageId')==storage_id]
    if len(matches)!=1:raise ValueError('Storage identity absent or ambiguous')
    storage=matches[0];name=storage.get('name')
    if type(name) is not str or not name.strip() or any(ord(c)<32 for c in name):
        raise ValueError('Storage label unavailable')
    if sum(s.get('name')==name for s in storages if type(s) is dict)!=1:
        raise ValueError('Storage label ambiguous')
    if storage.get('hasFullAccess') is not accessible:raise ValueError('Observed permission does not match route boundary')
    items=storage.get('contents')
    if type(items) is not list:raise ValueError('Storage items unavailable')
    accepted=[i for i in items if type(i) is dict and type(i.get('itemId')) is str and i['itemId']]
    found=[i for i in accepted if i['itemId']==item_id]
    if len(found)!=1:raise ValueError('Exact accepted item absent or ambiguous')
    def label(item):
        name=item.get('name',item.get('itemName'))
        if type(name) is not str or not name.strip() or any(ord(c)<32 for c in name):
            raise ValueError('Item label unavailable')
        count=str(item.get('quantity',item.get('count',1)))
        return name if count=='1' else name+' ×'+count
    item_label=label(found[0])
    if sum(label(i)==item_label for i in accepted)!=1:raise ValueError('Item label ambiguous')
    storage_label=(f'📦 {name} ({len(accepted)} пр.) → управление' if accessible
                   else f'📦 🔒 {name} ({len(accepted)} пр.)')
    return {'LocationId':location['locationId'],'StorageId':storage_id,'ItemId':item_id,
            'StorageLabel':storage_label,'StorageName':name,'ItemLabel':item_label,
            'ContentsCount':len(accepted)}

def fresh_selection(raw,cut,title,label):
    """Exact emitted captured-pipe frame only, never a reconstructed terminal screen."""
    if type(raw) is not bytes or type(cut) is not int or cut<0 or cut>len(raw):return False
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return False
    # These observations do not respond to escape queries or admit alternate buffers.
    def ansi(match):
        args,final=match.group(1),match.group(2)
        if final in ('h','l') and args=='?25':return ''
        if '?' in args or final not in 'mABCDGHfKJ':raise ValueError('Unsupported prompt control')
        if final=='m' and any(int(v or 0) not in (0,1,2,3,4,9,22,23,24,29) for v in args.split(';')):
            raise ValueError('Unsupported prompt decoration')
        return ''
    try:text=re.sub(r'\x1b\[([0-9;?]*)([A-Za-z])',ansi,text)
    except ValueError:return False
    if '\x1b' in text:return False
    at=text.rfind(title)
    if at<0:return False
    lines=text[at+len(title):].replace('\r','').splitlines()
    options=[line.strip() for line in lines if line.strip()]
    occurrences=[line for line in options if line.removeprefix('> ').strip()==label]
    return len(occurrences)==1 and occurrences[0]=='> '+label

def require_local_unchanged(before,after):
    for key in ('StorySHA256','History','QueuePins'):
        if key not in before or key not in after or before[key]!=after[key]:
            raise ValueError('Local console route changed original '+key)

def client_environment(environment):
    """Isolated client console capability; no mutation of bridge/daemon/RPC env."""
    return dict(environment, TERM='xterm-256color')

OWNED_PTY_LOAD_PROFILE='owned-pty-100x25-noecho-load78'

def fixed_menu(header, choices, page_size, profile=None):
    """Closed, source-specific finite menu DATA; never authorizes a game mutation."""
    if type(header) is not str or type(choices) is not list or not choices or type(page_size) is not int:
        raise ValueError('Explicit finite menu required')
    if len(choices)>page_size or len(set(choices))!=len(choices):raise ValueError('Pagination or duplicate choices')
    if profile is not None and (profile!=OWNED_PTY_LOAD_PROFILE or header!='Выберите сохранение:'
                                or page_size!=15 or len(choices)!=2 or choices[-1]!='↩️ Назад'
                                or re.fullmatch(r's \| Ход [0-9]+ \| .+ \| [0-9]{2}\.[0-9]{2}\.[0-9]{4} [0-9]{2}:[0-9]{2} \| [0-9]{1,5}KB',choices[0]) is None):
        raise ValueError('Unsupported closed Load geometry profile')
    limit=78 if profile is not None else 76
    def safe(line):
        return bool(line) and not any(ord(c)<32 or c=='\x7f' or unicodedata.category(c)=='Cf' for c in line) and sum(2 if unicodedata.east_asian_width(c) in 'WF' else 1 for c in line)<=limit
    if not all(safe(line) for line in header.split('\n')) or not all(type(x) is str and safe(x) for x in choices):
        raise ValueError('Unsupported control or wrapped menu')
    footer=choices[-1]
    if any(x.startswith(footer) or footer.startswith(x) for x in choices[:-1]):
        raise ValueError('Terminal row prefix collision')
    value={'header':header,'choices':tuple(choices),'page_size':page_size}
    if profile is not None:value['profile']=profile
    return value

def inventory_menu(inventory, location, player_status):
    """Small supported fixture inventory; unsupported canonical labels refuse before input."""
    items=inventory.get('items');equipped=inventory.get('equippedItems',{});storages=location.get('locationStorages',[])
    if type(items) is not list or type(equipped) is not dict or type(storages) is not list:raise ValueError('Inventory surfaces missing')
    slots={'MainHand':'⚔️ Основная рука','OffHand':'🛡️ Вторая рука','Head':'🪖 Голова','Chest':'🛡️ Грудь','Legs':'🦵 Ноги','Feet':'👢 Ступни','Accessory1':'🔹 Аксессуар 1','Accessory2':'🔹 Аксессуар 2'}
    rows=[]
    for slot,identity in equipped.items():
        if identity is None:continue
        matches=[i for i in items if i.get('itemId')==identity]
        if slot not in slots or len(matches)!=1:raise ValueError('Unsupported equipment shape')
        rows.append('⚔ '+slots[slot]+': '+matches[0]['name'])
    for i in items:
        if not i.get('itemId') or type(i.get('name')) is not str:raise ValueError('Accepted item label missing')
        kind=i.get('type','');count=str(i.get('count',i.get('quantity',1)))
        if kind and (type(kind) is not str or kind.isascii()):raise ValueError('Unsupported translated item type')
        rows.append('📦 '+i['name']+(' x'+count if count!='1' else '')+(' ['+kind+']' if kind else ''))
    money=player_status.get('money',0)
    if type(money) is not int:raise ValueError('Unsupported money')
    if money<=0:money=inventory.get('money',0)
    if type(money) is not int:raise ValueError('Unsupported money')
    prefix=['💰 Деньги: '+str(money)] if money>0 else []
    commodities=inventory.get('resources',{})
    if type(commodities) is not dict:raise ValueError('Unsupported commodity shape')
    for key,value in commodities.items():
        if key in ('money','gold','coins'):continue
        if type(value) not in (int,str):raise ValueError('Unsupported commodity')
        if str(value) not in ('','0'):prefix.append('💎 '+key+': '+str(value))
    rows=prefix+rows
    for st in storages:
        if type(st.get('hasFullAccess')) is not bool or type(st.get('contents')) is not list:raise ValueError('Unsupported storage')
        n=len(st['contents'])
        if any(not i.get('itemId') for i in st['contents']):raise ValueError('Unsupported storage item')
        rows.append('📦 '+('' if st['hasFullAccess'] else '🔒 ')+st['name']+f' ({n} пр.)'+(' → управление' if st['hasFullAccess'] else ''))
    weight=''
    if 'totalWeight' in inventory:
        weight='  (⚖ '+str(inventory['totalWeight'])+'/'+str(inventory.get('maxWeight','?'))+' кг'+(' — ПЕРЕГРУЗКА!' if inventory.get('isOverloaded') is True else '')+')'
    return fixed_menu('🎒 📦 Инвентарь'+weight+'  (выберите для просмотра / управления)',rows+['← Назад'],20)

def preflight_local_route(inventory, location, player_status, storage_id, item_id, turn, *, profile=None):
    """Checks both finite move menus and the worst supported Load label before owners start.

    These copies predict only visible choice membership. Fresh real bytes remain
    mandatory after each action; no copied DATA is publication permission.
    """
    import copy
    inv=copy.deepcopy(inventory);loc=copy.deepcopy(location)
    target=storage_target(loc,storage_id,item_id,True)
    storage=next(st for st in loc['locationStorages'] if st['storageId']==storage_id)
    for retrieve in (True,False):
        inventory_menu(inv,loc,player_status)
        actions=[]
        if inv['items']:actions.append(f"📥 Положить предмет в хранилище ({len(inv['items'])} в инвентаре)")
        if storage['contents']:actions.append(f"📤 Забрать предмет из хранилища ({len(storage['contents'])} внутри)")
        fixed_menu('📦 '+target['StorageName']+'\n  Предметов внутри: '+str(len(storage['contents'])),actions+['← Назад к инвентарю'],10)
        source=storage['contents'] if retrieve else inv['items']
        labels=[i['name']+(' ×'+str(i.get('quantity',i.get('count',1))) if str(i.get('quantity',i.get('count',1)))!='1' else '') for i in source]
        title='Выберите предмет для извлечения из хранилища:' if retrieve else 'Выберите предмет для перемещения в хранилище:'
        fixed_menu(title,labels+['← Отмена'],20)
        item=next(i for i in source if i['itemId']==item_id);source.remove(item)
        (inv['items'] if retrieve else storage['contents']).append(item)
    local_load_menu(location['name'],turn,99999,profile=profile)


def after_menu(menu, previous, selected=None):
    """Record a source-bound close, optionally with the exact committed selection."""
    value=dict(menu,previous_height=len(previous['header'].split('\n'))+len(previous['choices']))
    if selected is not None:
        prior=fixed_menu(previous['header'],list(previous['choices']),previous['page_size'],previous.get('profile'))
        if selected not in prior['choices']:raise ValueError('Unknown committed predecessor selection')
        value.update(previous_menu=prior,previous_selected=selected)
    return value

def entry_menu(menu, command=None, load_clear=False):
    """Known console-entry preamble, never cursor controls within a menu frame."""
    if command not in (None,'/storage_move'):raise ValueError('Unsupported entry command')
    return dict(menu,command_echo=command,load_clear=load_clear)

def selected_frame(raw, cut, menu):
    frames=selected_frames(raw,cut,menu)
    return frames[-1] if frames else None

def selected_frames(raw, cut, menu):
    """Exact emitted frames; only SGR and the source-sized CR/cursor-up redraw prefix are accepted."""
    if type(raw) is not bytes or type(cut) is not int or not 0<=cut<=len(raw) or type(menu) is not dict:return None
    try:
        checked=fixed_menu(menu['header'],list(menu['choices']),menu['page_size'],menu.get('profile'))
    except (ValueError,KeyError,TypeError,UnicodeError):return None
    if 'previous_menu' in menu or 'previous_selected' in menu:
        try:
            prior=menu['previous_menu']
            prior=fixed_menu(prior['header'],list(prior['choices']),prior['page_size'],prior.get('profile'))
            target=menu['previous_selected']
            height=len(prior['header'].split('\n'))+len(prior['choices'])
            if target not in prior['choices'] or menu.get('previous_height')!=height:return None
            close=b'\r'+b'\x1b[2K\x1b[1A'*height+b'\x1b[2K\x1b[?25h'
            tail=raw[cut:]
            at=tail.find(close)
            if at<0:return None
            if at:
                frames=selected_frames(tail[:at],0,prior)
                if not frames or any(value!=target for value in frames):return None
            # Only complete, exact selected predecessor frames were consumed.
            # Retain the actual close and every successor byte for normal decoding.
            cut+=at
        except (ValueError,KeyError,TypeError,UnicodeError):return None
    return decode_menu_frames(raw,cut,checked,menu)

def decode_menu_frames(raw, cut, checked, menu):
    """Decode exact raw frame DATA for a descriptor and its transition metadata.

    This does not admit a live menu or authorize input. Live consumers must use
    selected_frames, which applies fixed_menu before reaching this decoder.
    """
    if type(raw) is not bytes or type(cut) is not int or not 0<=cut<=len(raw) or type(menu) is not dict:return None
    try:
        text=raw[cut:].decode('utf-8')
        def decoration(match):
            if any(int(v or 0) not in (0,1,2,3,4,9,22,23,24,29) for v in match.group(1).split(';')):raise ValueError('Unsupported decoration')
            return ''
        text=re.sub(r'\x1b\[([0-9;]*)m',decoration,text)
    except (ValueError,KeyError,TypeError,UnicodeError):return None
    header_lines=checked['header'].split('\n')
    height=len(header_lines)+len(checked['choices'])
    redraw='\r\x1b['+str(height)+'A'
    parts=text.split(redraw)
    def one(part, first):
        # Only the first new header can follow a prior prompt close or known entry echo.
        header=r'\r?\n'.join(re.escape(line)+r' *' for line in header_lines)
        starts=list(re.finditer(r'(?:^|\n|(?<=\x1b\[\?25h))\r?(?:\x1b\[\?25l)?'+header+r'\r?\n',part))
        if not starts:return None
        prefix=part[:starts[-1].start()]
        if not first and prefix:return None
        if first:
            previous=menu.get('previous_height')
            if previous is not None:
                if type(previous) is not int or not 1<=previous<=22:return None
                close='\r'+('\x1b[2K\x1b[1A'*previous)+'\x1b[2K\x1b[?25h'
                prefix=prefix.replace(close,'',1)
            if menu.get('load_clear') is True and '\x1b[2J\x1b[3J\x1b[1;1H' in prefix:
                prefix=prefix.replace('\x1b[2J\x1b[3J\x1b[1;1H','',1)
                if '📂 Загрузка игры' not in prefix:return None
            command=menu.get('command_echo')
            if command is not None:
                if command!='/storage_move':return None
                prefix=prefix.removeprefix('\x1b[s')
                for end in range(len(command),0,-1):
                    literal=command[:end]
                    prefix=prefix.replace('\x1b[u\x1b[J'+literal,'').replace('\x1b[u'+literal,'')
            if '\x1b' in prefix:return None
        body=part[starts[-1].end():]
        body=re.sub(r'^(?: *\r?\n)*','',body)
        if '\x1b' in body or re.search(r'\r(?!\n|$)',body):return None
        rows=body.replace('\r','').split('\n')
        if len(rows)!=len(checked['choices']):return None
        selected=[]
        for index,(row,label) in enumerate(zip(rows,checked['choices'])):
            match=re.fullmatch(r'(> |  )'+re.escape(label)+r' *',row)
            if not match:return None
            if match.group(1)=='> ':selected.append(index)
        return checked['choices'][selected[0]] if len(selected)==1 else None
    # A fresh cut can start at the redraw prefix. Coalesced old frames must also
    # be complete; a newest partial draw can never fall back to an earlier one.
    if len(parts)>1 and not parts[0].strip(' '):parts=parts[1:]
    selections=[]
    for index,part in enumerate(parts):
        selected=one(part,index==0)
        if selected is None:return None
        selections.append(selected)
    return selections


def local_load_menu(location_name, turn, size_kb, timestamp='18.11.2026 18:00', *, profile=None):
    """Fixed sole-save visible contract; size bound is operational, not a predicted ZIP size."""
    if type(size_kb) is not int or not 0<=size_kb<=99999:raise ValueError('Unsupported Save size display')
    return fixed_menu('Выберите сохранение:',[f's | Ход {turn} | {location_name} | {timestamp} | {size_kb}KB','↩️ Назад'],15,profile)

def item_position(location,inventory,storage_id,item_id):
    """Exact observed target position, never permission to move an item."""
    storages=location.get('locationStorages') if type(location) is dict else None
    items=inventory.get('items') if type(inventory) is dict else None
    if type(storages) is not list or type(items) is not list:raise ValueError('Current item surfaces missing')
    matches=[s for s in storages if type(s) is dict and s.get('storageId')==storage_id]
    if len(matches)!=1 or matches[0].get('hasFullAccess') is not True:raise ValueError('Current storage access unavailable')
    storage=matches[0]
    if type(storage.get('contents')) is not list:raise ValueError('Storage contents missing')
    def count(values):return sum(type(i) is dict and i.get('itemId')==item_id for i in values)
    pair=(count(storage['contents']),count(items))
    if pair not in ((1,0),(0,1)):raise ValueError('Target item duplicated or absent')
    return 'storage' if pair==(1,0) else 'inventory'

def clean_save_result(text):
    """Clean returned Save interval only; committed-with-follow-up is not clean."""
    if type(text) is not str:return False
    success='✅ Игра сохранена'
    warning='Требуется служебная проверка хранилища сохранений.'
    if text.count(success)!=1 or warning in text:return False
    saved=text.index(success);heading=text.rfind('Ваш ход');prompt=text.rfind('\n >  ')
    return saved<heading<prompt


# #1515 T016, finite positive access and local Retrieve evidence.
_ROUTE='ordinary-save-with-storage-retrieve'
_ID_FIELDS=('LocationId','StorageId','ItemId')
_TURN_FIELDS=('sessionId','requestId','turnNumber')
_MAP='game_state/world/world_map.json'

def _unique(pairs):
    value={}
    for key,item in pairs:
        if any(key.casefold()==existing.casefold() for existing in value):raise ValueError('duplicate consumed response field')
        value[key]=item
    return value

def after_grant(route, request, accepted, decision, response, before, current, access, inventory, player_name, *, verified_birth):
    """Return the exact selector, or None for denial/defer; caller owns all evidence joins."""
    route=checked_route(route)
    if route['Route']!=_ROUTE:raise ValueError('Exact storage retrieve route required')
    loc,locker,item=(route[key] for key in _ID_FIELDS)
    if type(verified_birth) is not dict or any(verified_birth.get(key)!=route[key] for key in _ID_FIELDS):
        raise ValueError('route differs from independently verified birth identity')
    turn=request.get('turnNumber')
    if type(turn) is not int or turn<1 or any(type(request.get(k)) is not str or not request[k] for k in _TURN_FIELDS[:2]):
        raise ValueError('actual request identity absent')
    for witness in accepted,decision:
        if type(witness.get('turnNumber')) is not int or any(witness.get(k)!=request[k] for k in _TURN_FIELDS):
            raise ValueError('decision/acceptance differs from actual turn')
    if type(accepted.get('AcceptedGameTurns')) is not int or accepted['AcceptedGameTurns']!=1:
        raise ValueError('one client-accepted turn required')
    if type(response) is not bytes or hashlib.sha256(response).hexdigest()!=accepted.get('ResponseSHA256'):
        raise ValueError('consumed response differs from verified relay evidence')
    packet=json.loads(response,object_pairs_hook=_unique)
    writes=packet['Writes']
    map_updates=[w['Data'].get('worldMapUpdates',{}) for w in writes if w['Path']==_MAP]
    if len(map_updates)>1:raise ValueError('ambiguous world map write')
    updates=map_updates[0].get('storageUpdates',[]) if map_updates else []
    if type(updates) is not list:raise ValueError('governed storage updates required')
    outcome=decision.get('Decision')
    if outcome in ('Deny','Defer'):
        if updates:raise ValueError('non-grant decision has storage updates')
        return None
    if outcome!='Grant' or type(player_name) is not str or not player_name.strip():
        raise ValueError('affirmative GM decision/recipient absent')
    recipient=[{'playerId':'player_current','playerName':player_name}]
    if len(updates)!=1:raise ValueError('one governed grant update required')
    patch=updates[0]
    fields=patch.get('update')
    if (set(patch)!={'targetLocationId','storageId','update'} or patch['targetLocationId']!=loc or patch['storageId']!=locker
        or type(fields) is not dict or set(fields)!={'newAuthorizedUsers','newHasFullAccess'}
        or fields['newAuthorizedUsers']!=recipient or fields['newHasFullAccess'] is not True):
        raise ValueError('exact governed grant pair required')
    if (before.get('locationId')!=loc or before.get('storageId')!=locker or before.get('hasFullAccess') is not False
        or before.get('authorizedUsers')!=[]):
        raise ValueError('signed original locked coordinate required')
    owner=before.get('owner')
    if type(owner) is not dict or owner.get('ownerType')!='Faction' or not owner.get('ownerId'):
        raise ValueError('original faction owner absent')
    if current.get('locationId')!=loc:raise ValueError('courtyard is not current')
    target=storage_target(current,locker,item,True)
    current_storage=next(s for s in current['locationStorages'] if s.get('storageId')==locker)
    entries=[e for e in access['entries'] if e.get('locationId')==loc and e.get('storageId')==locker]
    if len(entries)!=1:raise ValueError('exact effective entry absent or ambiguous')
    for observed in entries[0],current_storage:
        if observed.get('owner')!=owner or observed.get('hasFullAccess') is not True or observed.get('authorizedUsers')!=recipient:
            raise ValueError('current accepted access projections disagree')
    _require_canonical_stack(current_storage['contents'],item)
    if item_position(current,inventory,locker,item)!='storage':
        raise ValueError('one exact chalk stack in locker only required')
    return target

def _exact_row(rows, predicate, message):
    matches=[row for row in rows if type(row) is dict and predicate(row)]
    if len(matches)!=1:raise ValueError(message)
    return matches[0]

def _require_canonical_stack(rows, item):
    chalk=_exact_row(rows,lambda row:row.get('itemId')==item,'one exact chalk stack required')
    if chalk.get('existedId')!=item:raise ValueError('canonical chalk existedId differs from itemId')
    if type(chalk.get('count')) is not int or chalk['count']!=1:
        raise ValueError('canonical chalk count must be integer one')
    return chalk

def _same_data(a,b):
    return json.dumps(a,sort_keys=True)==json.dumps(b,sort_keys=True)

def require_retrieve_start(route, world_map, offscreen, birth_index,
                           live_index, current, inventory, item_index):
    """Check current canonical location, birth and source carrier before any console input."""
    route=checked_route(route)
    loc,locker,item=(route[key] for key in _ID_FIELDS)
    if world_map.get('realm')!='mortal_world' or current.get('locationId')!=loc:
        raise ValueError('The exact Mortal courtyard is not current')
    mapped=_exact_row(world_map['locations'],lambda row:row.get('locationId')==loc,'exact canonical map courtyard required')
    select=lambda obj:_exact_row(obj['locationStorages'],lambda row:row.get('storageId')==locker,'exact courtyard storage required')
    metadata=select(mapped);holding=select(current)
    if (metadata.get('hasFullAccess') is not True or holding.get('hasFullAccess') is not True or
        any(metadata.get(k)!=holding.get(k) for k in ('owner','authorizedUsers'))):
        raise ValueError('canonical map metadata and current storage disagree')
    if any((entry.get('locationId'),entry.get('storageId'))==(loc,locker) for entry in offscreen['entries']):
        raise ValueError('current locker is duplicated in offscreen contents')
    stack=_require_canonical_stack(holding['contents'],item)
    if item_position(current,inventory,locker,item)!='storage':
        raise ValueError('one exact chalk stack in locker only required')
    born=storage_birth_row(route,birth_index)
    if not _same_data(born,storage_birth_row(route,live_index)):
        raise ValueError('immutable storage birth transition changed before Retrieve')
    entry=_exact_row(item_index['entries'],lambda row:row.get('itemId')==item,'one item identity entry required')
    source={'kind':'location_storage','ownerId':loc,'containerId':locker,'containerPath':[]}
    if (entry.get('state')!='active' or entry.get('currentCarrier')!=source or
        not entry['transitions'] or entry['transitions'][-1].get('destinationCarrier')!=source or
        not isinstance(stack.get('materialization'),dict) or
        not isinstance(stack.get('materializationReceipt'),dict) or
        entry.get('receiptId')!=stack['materializationReceipt'].get('receiptId') or not entry.get('receiptId')):
        raise ValueError('exact accepted source carrier and chalk receipt required before Retrieve')

def storage_birth_row(route, index):
    entry=_exact_row(index['locationEntries'],lambda row:row.get('locationId')==route['LocationId'],'exact birth location history required')
    return _exact_row(entry['transitions'],lambda row:row.get('kind')=='storage_addition' and row.get('childId')==route['StorageId'],
                      'one exact storage birth transition required')


def require_retrieve_evidence(route, world_map, offscreen_before, offscreen_after,
                              location_before, location_after, current_before, current_after,
                              inventory_before, inventory_after, index_before, index_after):
    """Compare detached fields from authenticated pre-/post-Retrieve observations."""
    route=checked_route(route)
    if route['Route']!=_ROUTE:raise ValueError('Exact storage retrieve route required')
    loc,locker,item=(route[key] for key in _ID_FIELDS)
    def locker_at(root):
        if root.get('locationId')!=loc:raise ValueError('courtyard is not current')
        return _exact_row(root['locationStorages'],lambda row:row.get('storageId')==locker,'exact courtyard storage required')
    metadata_location=_exact_row(world_map['locations'],lambda row:row.get('locationId')==loc,'exact canonical map courtyard required')
    metadata=locker_at(metadata_location)
    pre_locker=locker_at(current_before)
    post_locker=locker_at(current_after)
    for row in metadata,pre_locker,post_locker:
        if (row.get('hasFullAccess') is not True or row.get('owner')!=metadata.get('owner') or
            row.get('authorizedUsers')!=metadata.get('authorizedUsers')):
            raise ValueError('canonical map metadata and current storage disagree')
    for root in offscreen_before,offscreen_after:
        if any((entry.get('locationId'),entry.get('storageId'))==(loc,locker) for entry in root['entries']):
            raise ValueError('current locker is duplicated in offscreen contents')
    if (item_position(current_before,inventory_before,locker,item)!='storage' or
        item_position(current_after,inventory_after,locker,item)!='inventory'):
        raise ValueError('exact chalk did not leave storage for player inventory')
    before_stack=_require_canonical_stack(pre_locker['contents'],item)
    after_stack=_require_canonical_stack(inventory_after['items'],item)
    if any(not _same_data(before_stack.get(key),after_stack.get(key))
           for key in ('materialization','materializationReceipt')):
        raise ValueError('immutable item birth evidence changed')

    original_birth=storage_birth_row(route,location_before)
    if not original_birth.get('transitionId') or not _same_data(original_birth,storage_birth_row(route,location_after)):
        raise ValueError('immutable storage birth transition changed')

    old,new=(_exact_row(root['entries'],lambda row:row.get('itemId')==item,'one item identity entry required')
             for root in (index_before,index_after))
    source={'kind':'location_storage','ownerId':loc,'containerId':locker,'containerPath':[]}
    target={'kind':'player_inventory','ownerId':'player','containerId':None,'containerPath':[]}
    if old.get('state')!='active' or new.get('state')!='active' or old.get('currentCarrier')!=source or new.get('currentCarrier')!=target:
        raise ValueError('active carrier transition differs from storage-to-player')
    if not _same_data({k:v for k,v in old.items() if k not in ('currentCarrier','transitions')},
                      {k:v for k,v in new.items() if k not in ('currentCarrier','transitions')}):
        raise ValueError('item identity fields changed beyond carrier and history')
    prior=old['transitions'];after=new['transitions']
    if not prior or len(after)!=len(prior)+1 or not _same_data(after[:-1],prior):
        raise ValueError('exact history prefix plus one transition required')
    row=after[-1]
    expected={'kind':'transfer','turn':prior[-1]['turn'],'sourceItemIds':[item],
              'sourceCarrier':source,'destinationCarrier':target,'quantityBefore':1,
              'quantityAfter':1,'authorityKind':'local_storage_move'}
    transition_id=row.get('transitionId')
    authority_id=row.get('authorityId')
    if (set(row)!=set(expected)|{'transitionId','authorityId'} or
        not _same_data({key:row[key] for key in expected},expected) or
        not isinstance(transition_id,str) or re.fullmatch(r'mitrn_[0-9a-f]{32}',transition_id) is None or
        not isinstance(authority_id,str) or re.fullmatch(r'local_storage_move_[0-9a-f]{32}',authority_id) is None or
        any(row.get('transitionId')==transition_id or row.get('authorityId')==authority_id for row in prior)):
        raise ValueError('one new exact local storage transfer required')
    return {'BirthTransitionId':original_birth['transitionId'],'ItemTransitionId':transition_id}

def storage_birth_witness(route, index, prior_request):
    """Require the storage/item to be the actual earlier same-world birth."""
    route=checked_route(route)
    loc,locker,item=(route[key] for key in _ID_FIELDS)
    birth=storage_birth_row(route,index)
    cargo=birth['afterState']['cargoBirthEvidence']
    authority=cargo['sourceAuthority']
    if (type(birth.get('turn')) is not int or birth['turn']!=prior_request['turnNumber'] or
        birth.get('entityId')!=loc or cargo.get('locationId')!=loc or cargo.get('storageId')!=locker or
        cargo.get('carrierKind')!='location_storage' or cargo.get('sourceTurn')!=birth['turn'] or
        any(authority.get(k)!=prior_request.get(k) or not authority.get(k) for k in _TURN_FIELDS[:2])):
        raise ValueError('storage birth differs from accepted predecessor')
    row=_exact_row(cargo['cargoMeasurements'],lambda row:row.get('itemId')==item,'one exact born chalk cargo identity required')
    if type(row.get('count')) is not int or row['count']!=1:
        raise ValueError('canonical chalk birth count must be integer one')
    return {key:route[key] for key in _ID_FIELDS}

def storage_retrieve_save_proved(result):
    """A no-grant stop is deliberately not successful transfer evidence."""
    try:
        witness=result['StorageRetrieveProof']
        return (result['StorageDecision']=='Grant' and witness['Retrieved'] is True and
                all(type(witness[k]) is str and bool(witness[k]) for k in ('BirthTransitionId','ItemTransitionId')) and
                result['OrdinarySaveProof']['Turn']==result['AcceptedTurnNumber'])
    except (KeyError,TypeError):return False
