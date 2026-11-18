"""T024 finite console route DATA. Never issues authority or accepts a game turn.

Inputs are fresh observed UI/canonical DATA, not permission to mutate. The driver
must still use the ordinary console action and independently check its result.
"""
import re
import unicodedata

def checked_route(value):
    if value is None:return {'Route':'birth'}
    if type(value) is not dict:raise ValueError('Finite route object required')
    route=value.get('Route')
    fields={'birth':{'Route'},
            'permission-with-denial':{'Route','StorageId','ItemId'},
            'storage-local-proof':{'Route','StorageId','ItemId'}}.get(route)
    if fields is None or set(value)!=fields:raise ValueError('Unknown route or extra input program')
    for key in fields-{'Route'}:
        if type(value[key]) is not str or not value[key].strip() or any(ord(c)<32 for c in value[key]):
            raise ValueError('Exact canonical target identity required')
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

def fixed_menu(header, choices, page_size):
    """Closed, source-specific finite menu DATA; never authorizes a game mutation."""
    if type(header) is not str or type(choices) is not list or not choices or type(page_size) is not int:
        raise ValueError('Explicit finite menu required')
    if len(choices)>page_size or len(set(choices))!=len(choices):raise ValueError('Pagination or duplicate choices')
    def safe(line):
        return bool(line) and not any(ord(c)<32 or c=='\x7f' or unicodedata.category(c)=='Cf' for c in line) and sum(2 if unicodedata.east_asian_width(c) in 'WF' else 1 for c in line)<=78
    if not all(safe(line) for line in header.split('\n')) or not all(type(x) is str and safe(x) for x in choices):
        raise ValueError('Unsupported control or wrapped menu')
    footer=choices[-1]
    if any(x.startswith(footer) or footer.startswith(x) for x in choices[:-1]):
        raise ValueError('Terminal row prefix collision')
    return {'header':header,'choices':tuple(choices),'page_size':page_size}

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

def preflight_local_route(inventory, location, player_status, storage_id, item_id, turn):
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
    local_load_menu(location['name'],turn,99999)


def selected_frame(raw, cut, menu):
    """Exact emitted frames; only SGR and the source-sized CR/cursor-up redraw prefix are accepted."""
    if type(raw) is not bytes or type(cut) is not int or not 0<=cut<=len(raw) or type(menu) is not dict:return None
    try:
        checked=fixed_menu(menu['header'],list(menu['choices']),menu['page_size'])
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
        # Cursor hiding is allowed only immediately before an exact header.
        part=part.replace('\x1b[?25l'+header_lines[0],header_lines[0],1)
        if '\x1b' in part:return None
        header='\n'.join(re.escape(line)+r' *' for line in header_lines)
        starts=list(re.finditer(r'(?:^|\n)\r?'+header+r'\r?\n',part))
        if not starts or (not first and starts[-1].start()!=0):return None
        body=part[starts[-1].end():]
        body=re.sub(r'^(?: *\r?\n)*','',body)
        if re.search(r'\r(?!\n|$)',body):return None
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
    if len(parts)>1 and parts[0]=='':parts=parts[1:]
    selected=None
    for index,part in enumerate(parts):
        selected=one(part,index==0)
        if selected is None:return None
    return selected


def local_load_menu(location_name, turn, size_kb, timestamp='18.11.2026 18:00'):
    """Fixed sole-save visible contract; size bound is operational, not a predicted ZIP size."""
    if type(size_kb) is not int or not 0<=size_kb<=99999:raise ValueError('Unsupported Save size display')
    return fixed_menu('Выберите сохранение:',[f's | Ход {turn} | {location_name} | {timestamp} | {size_kb}KB','↩️ Назад'],15)

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
