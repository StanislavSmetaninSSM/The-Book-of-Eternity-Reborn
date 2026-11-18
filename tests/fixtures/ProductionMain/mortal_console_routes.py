"""T024 finite console route DATA. Never issues authority or accepts a game turn.

Inputs are fresh observed UI/canonical DATA, not permission to mutate. The driver
must still use the ordinary console action and independently check its result.
"""
import re

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

def selected_frame(raw, cut, title):
    """Return the one selected emitted row from the newest complete fixed menu.

    This is captured output, not a terminal-screen reconstruction. Pagination,
    missing/ambiguous selected rows and incomplete output cannot select anything.
    """
    if type(raw) is not bytes or type(cut) is not int or not 0<=cut<=len(raw):return None
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return None
    # Same supported non-concealing output surface as the inert pipe/xterm proof.
    text=re.sub(r'\x1b\[(?:\?25[hl]|[0-9;]*[mABCDGHfKJ])','',text)
    if '\x1b' in text or not text.endswith('\r'):return None
    if 'reveal more items' in text or 'More choices' in text:return None
    at=text.rfind(title)
    if at<0:return None
    rows=text[at+len(title):].replace('\r','').splitlines()
    selected=[row.strip()[2:] for row in rows if row.strip().startswith('> ')]
    if len(selected)!=1:return None
    label=selected[0]
    return label if fresh_selection(raw,cut,title,label) else None

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
