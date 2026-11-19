"""T034 one literal actual-UI route; no game authority, generic key program or DSR replies."""
import json
import pathlib
import re
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / 'ProductionMain'))
import mortal_console_routes as routes

LOCATION = 'Хозяйственный двор у нижней лестницы'
STORAGE = 'Запертая кладовая разметочных материалов'
STORAGE_ID = 'storage_t034_geometry'
CHALK_ID = 'itm_t034_chalk'



OPTIONS = ('💾 Сохранить', '📂 Загрузить', '⚙️ Опции', '🏠 Выйти в главное меню', '↩️ Назад')
DESCRIPTIONS = ('Создать сохранение текущего цикла', 'Загрузить существующее сохранение',
                'Открыть клиентские настройки', 'Вернуться в главное меню', None)
OPTIONS_FOOTER = '↑/↓ или W/S — выбор • Enter — подтвердить • Esc — назад'
SAVE_WARNING = 'Требуется служебная проверка хранилища сохранений.'

def require_time(deadline, now=None):
    if (time.monotonic() if now is None else now) >= deadline:
        raise TimeoutError('Original full-route deadline exhausted')

def wait_observation(predicate, pump, deadline, label):
    while True:
        require_time(deadline)
        value = predicate()
        require_time(deadline)
        if value:
            return value
        pump()

def send_current(send, key, label, deadline):
    require_time(deadline)
    return send(key, label)

def ordered_transition(frames, prior, expected):
    if not frames:
        return False
    moved = False
    for row in frames:
        if row not in (prior, expected) or (moved and row != expected):
            raise ValueError('Skipped/backward transition')
        moved |= row == expected
    return moved

def retained_selection(raw, proof):
    # A prior validated frame is usable only if no newer bytes exist. A partial
    # newest draw never falls back; complete appended draws must retain selection.
    if not raw.startswith(proof['raw']):
        raise ValueError('Capture prefix changed')
    if len(raw) == len(proof['raw']):
        return proof['selected']
    frames = routes.selected_frames(raw, len(proof['raw']), proof['menu'])
    if not frames:
        return None
    if any(row != proof['selected'] for row in frames):
        raise ValueError('Unrequested selection after retained frame')
    return proof['selected']

def options_frame(raw, cut, selected, *, initial):
    if selected not in OPTIONS or not 0 <= cut <= len(raw):
        return False
    try:
        text = raw[cut:].decode('utf-8')
        def style(match):
            if any(int(v or 0) not in (0,1,2,3,4,9,22,23,24,29) for v in match[1].split(';')):
                raise ValueError('Unsupported Options decoration')
            return ''
        text = re.sub(r'\x1b\[([0-9;]*)m', style, text)
    except (UnicodeError, ValueError):
        return False
    starts = list(re.finditer(r'(?:^|\n|(?<=H)) *[➤◆] '+re.escape(OPTIONS[0])+r' *\r?\n', text))
    if not starts:
        return False
    start = starts[-1].start()
    prefix, body = text[:start], text[start:]
    if '\x1b' in body:
        return False
    lines = [line.strip(' \r') for line in body.split('\n') if line.strip(' \r')]
    expected = []
    for label, description in zip(OPTIONS, DESCRIPTIONS):
        expected.append(('➤ ' if label == selected else '◆ ') + label)
        if description:
            expected.append(description)
    expected.append(OPTIONS_FOOTER)
    if lines != expected:
        return False
    if initial:
        # Actual static Rule and Esc line must both precede the first complete grid.
        if not re.search(r'(?:^|\n|(?<=H))[─ ]*⚙️ Игровое меню[─ ]*\r?\n', prefix):
            return False
        if not re.search(r'(?:^|\n)Esc — назад *\r?\n', prefix):
            return False
    # Cursor positioning/space clearing belongs only before this newly emitted body.
    # Screen.feed independently rejects unsupported controls/DSR on the entire stream.
    return True

def clean_save_interval(text):
    return text.count('✅ Игра сохранена') == 1 and SAVE_WARNING not in text


class AcknowledgedTerminalView:
    """Read-only callback-fenced terminal evidence for this one literal route."""
    def __init__(self, terminal, captured):
        self.terminal=terminal;self.captured=captured;self.cut=0;self.seq=0
        self.pending=True;self.lines=[];self.modes={}
    def observe(self, raw):
        value=self.terminal.feed(raw)
        self.pending=value['pending'];self.modes=value['modes'];self.seq=value['seq'];self.cut=len(self.captured)
        if not self.pending:self.lines=self.terminal.snapshot()['lines']
    @property
    def keyboard(self):return {'cursor':self.modes.get('applicationCursorKeysMode',False),'keypad':self.modes.get('applicationKeypadMode',False)}
    def arrow(self, up=False):
        if self.pending:raise ValueError('Unacknowledged terminal prefix')
        return (b'\x1bO' if self.modes['applicationCursorKeysMode'] else b'\x1b[')+(b'A' if up else b'B')
    def prompt(self, text, after):
        return not self.pending and self.cut==len(self.captured) and self.cut>after and any(text in line for line in self.lines)
    def options(self, after, selected, initial):
        if self.pending or self.cut!=len(self.captured) or self.cut<=after:return False
        return options_frame(bytes(self.captured),after,selected,initial=initial) and options_cells(self.lines,selected)

def options_cells(lines, selected):
    if len(lines)!=25 or selected not in OPTIONS:return False
    visible=[line.strip() for line in lines if line.strip()]
    expected=[]
    for label,description in zip(OPTIONS,DESCRIPTIONS):
        expected.append(('➤ ' if label==selected else '◆ ')+label)
        if description:expected.append(description)
    expected.append(OPTIONS_FOOTER)
    return len(visible)==len(expected)+2 and re.fullmatch(r'[─ ]*⚙️ Игровое меню[─ ]*',visible[0]) is not None and visible[1]=='Esc — назад' and visible[2:]==expected

# Exact standalone production outcome; only known nonconcealing edge decorations.
_EDGE=rb'(?:\x1b\[(?:0|1|2|3|4|9|22|23|24|29)(?:;(?:0|1|2|3|4|9|22|23|24|29))*m)*'
_SUCCESS=re.compile(rb'[ \r\n]*'+_EDGE+'✅ Игра сохранена'.encode()+_EDGE+rb' *\r?\n[ \r\n]*')
def save_outcome(raw):
    return _SUCCESS.fullmatch(raw) is not None

def returned_save_boundary(raw, cut, menu):
    match=_SUCCESS.match(raw[cut:])
    if match is None:return None
    boundary=cut+match.end()
    # Unlike the generic menu grammar, Save return permits NO plain prefix.
    # Real Spectre may hide the cursor and decorate the exact first header line.
    first=menu['header'].split('\n')[0].encode()
    entry=rb'(?:\x1b\[\?25l)?'+_EDGE+re.escape(first)+_EDGE+rb' *\r?\n'
    if re.match(entry,raw[boundary:]) is None:return None
    frames=routes.selected_frames(raw,boundary,menu)
    return boundary if frames else None

def complete_save_observation(view, raw, cut):
    return view.prompt('✅ Игра сохранена',cut) and save_outcome(raw[cut:])

def fresh_route_key(terminal, view, drain, key, label, predicate, deadline):
    """Drain+revalidate before resolving a mode-dependent key intent exactly once."""
    require_time(deadline);drain();require_time(deadline)
    if view.pending or view.cut!=len(view.captured) or not predicate():
        raise ValueError('Fresh complete intended UI not proved before input')
    resolved=key() if callable(key) else key
    require_time(deadline);terminal.key(resolved,label)
    return resolved


def run(root, captured, screen, pump, send, result, deadline, initial_cut, profile):
    """All waits share the original bounded deadline. No retry after unknown UI."""
    game = root / 'isolated-data' / 'game_session'
    cut = initial_cut
    previous = None
    previous_selected = None
    retained = None

    def wait(predicate, label):
        return wait_observation(predicate, pump, deadline, label)

    original_send = send
    def send(key, label, predicate):
        require_time(deadline)
        return original_send(key,label,predicate)

    def data(path):
        return json.loads((game / path).read_text(encoding='utf-8-sig'))

    def stage(name):
        def observed():
            p = root / 'stage.json'
            return p.exists() and json.loads(p.read_text())['Stage'] == name
        wait(observed, 'Actual fixture stage ' + name)
        result.setdefault('ActualStages', []).append(name)

    def inventory():
        return routes.inventory_menu(data('game_state/inventory/items.json'),
                                     data('game_state/world/current_location.json'), {})

    def choose(menu, target, *, enter=True, up=False):
        nonlocal cut, previous, previous_selected, retained
        if target not in menu['choices']:
            raise ValueError('Exact target absent')
        observed_menu = routes.after_menu(menu, previous, previous_selected) if previous else menu
        if retained is not None:
            if retained['menu'] != menu:
                raise ValueError('Retained selection belongs to another menu')
            selected = wait(lambda: retained_selection(bytes(captured), retained), 'Complete current retained frame')
            retained = None
        else:
            frames = wait(lambda: routes.selected_frames(bytes(captured), cut, observed_menu), 'Complete fresh menu')
            if len(set(frames)) != 1:
                raise ValueError('Unrequested selection change')
            selected = frames[-1]
        visited = {selected}
        while selected != target:
            index = menu['choices'].index(selected)
            next_index = index - 1 if up else index + 1
            if not 0 <= next_index < len(menu['choices']):
                raise ValueError('Unexpected navigation cycle')
            expected = menu['choices'][next_index]
            boundary = len(captured)
            proof={'raw':bytes(captured),'menu':menu,'selected':selected}
            send(lambda: screen.arrow(up), 'Up' if up else 'Down', lambda: retained_selection(bytes(captured),proof)==selected)
            def transition():
                fresh = routes.selected_frames(bytes(captured), boundary, menu)
                return ordered_transition(fresh, selected, expected)
            wait(transition, 'Exact neighbor after one key')
            selected = expected
            if selected in visited:
                raise ValueError('True menu cycle')
            visited.add(selected)
            cut = boundary
        if enter:
            cut = len(captured)
            proof={'raw':bytes(captured),'menu':menu,'selected':selected}
            send(b'\r', 'Enter exact ' + target, lambda: retained_selection(bytes(captured),proof)==selected)
            previous = menu
            previous_selected = target
        else:
            retained = {'raw': bytes(captured), 'menu': menu, 'selected': selected}
            previous = None
        result.setdefault('Selected', []).append(target)

    def panel(count, inventory_count):
        rows = []
        if inventory_count:
            rows.append(f'📥 Положить предмет в хранилище ({inventory_count} в инвентаре)')
        if count:
            rows.append(f'📤 Забрать предмет из хранилища ({count} внутри)')
        return routes.fixed_menu(f'📦 {STORAGE}\n  Предметов внутри: {count}', rows + ['← Назад к инвентарю'], 10)

    def open_options(name, number, selected):
        nonlocal previous, cut
        stage(name)
        wait(lambda: screen.options(cut,'💾 Сохранить',True), 'Fresh complete Options custom frame')
        options_cut=cut
        send(str(number).encode(), 'Options numeric selection', lambda: screen.options(options_cut,'💾 Сохранить',True))
        cut = len(captured)
        wait(lambda: screen.options(cut,selected,False), 'Fresh complete Options selected redraw')
        options_cut=cut
        send(b'\r', 'Options Enter', lambda: screen.options(options_cut,selected,False))
        cut = len(captured); previous = None

    stage('locked-denial')
    menu = inventory()
    locked = f'📦 🔒 {STORAGE} (1 пр.)'
    choose(menu, locked)
    choose(menu, '← Назад')
    stage('retrieve')
    menu = inventory()
    choose(menu, f'📦 {STORAGE} (1 пр.) → управление')
    choose(panel(1, 1), '📤 Забрать предмет из хранилища (1 внутри)')
    choose(routes.fixed_menu('Выберите предмет для извлечения из хранилища:',
                             ['Кусочек разметочного мела', '← Отмена'], 20), 'Кусочек разметочного мела')
    # Actual success text precedes the returned panel. Do not discard arbitrary prefix.
    result['TransferIntervalCut'] = cut
    # Decoder admits only known prompt-close/metadata; unsupported message framing refuses.
    choose(panel(0, 2), '← Назад к инвентарю')
    choose(inventory(), '← Назад')
    open_options('save', 1, '💾 Сохранить')
    prompt_cut=cut
    wait(lambda: screen.prompt('Название сохранения:',prompt_cut), 'Actual Save name composer')
    send(b's\r','Save replacement text s and Enter',lambda: screen.prompt('Название сохранения:',prompt_cut))
    prompt_cut=len(captured)
    wait(lambda: screen.prompt('Описание (необязательно):',prompt_cut), 'Actual Save description composer')
    send(b'\r','Empty description Enter',lambda: screen.prompt('Описание (необязательно):',prompt_cut))
    save_cut=len(captured)
    wait(lambda: complete_save_observation(screen,bytes(captured),save_cut), 'Complete standalone actual Save outcome')
    send(b'\r','Save acknowledgement',lambda: screen.prompt('✅ Игра сохранена',save_cut) and save_outcome(bytes(captured)[save_cut:]))
    stage('deposit');previous=None
    deposit_menu=inventory()
    cut=wait(lambda: returned_save_boundary(bytes(captured),save_cut,deposit_menu),'Clean Save through actual returned inventory')
    choose(inventory(), f'📦 {STORAGE} (0 пр.) → управление')
    choose(panel(0, 2), '📥 Положить предмет в хранилище (2 в инвентаре)')
    choose(routes.fixed_menu('Выберите предмет для перемещения в хранилище:',
                             ['Древний меч', 'Кусочек разметочного мела', '← Отмена'], 20), 'Кусочек разметочного мела')
    choose(panel(1, 1), '← Назад к инвентарю')
    choose(inventory(), '← Назад')
    open_options('load-focus-back', 2, '📂 Загрузить')
    archive = json.loads((root / 'archive.json').read_text())
    label = archive['LoadLabel']
    menu = routes.fixed_menu('Выберите сохранение:', [label, '↩️ Назад'], 15, profile)
    menu = routes.entry_menu(menu, load_clear=True)
    choose(menu, '↩️ Назад', enter=False)
    choose(menu, label, enter=False, up=True)
    choose(menu, '↩️ Назад')
    wait(lambda: (root / 'completed.json').exists(), 'Actual returned Back-only Load')
    completed = json.loads((root / 'completed.json').read_text())
    if completed['Success'] is not True or completed['LoadCommitAttempted'] or completed['HostedReplacementClaimed']:
        raise ValueError('Fixture outcome boundary mismatch')
    result['ActualFullRoute'] = completed
