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


def run(root, captured, screen, pump, send, result, deadline, initial_cut, profile):
    """All waits share the original bounded deadline. No retry after unknown UI."""
    game = root / 'isolated-data' / 'game_session'
    cut = initial_cut
    previous = None
    retained = None

    def wait(predicate, label):
        return wait_observation(predicate, pump, deadline, label)

    original_send = send
    def send(key, label):
        send_current(original_send, key, label, deadline)

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
        nonlocal cut, previous, retained
        if target not in menu['choices']:
            raise ValueError('Exact target absent')
        observed_menu = routes.after_menu(menu, previous) if previous else menu
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
            key = (b'\x1bOA' if screen.keyboard['cursor'] else b'\x1b[A') if up else screen.down()
            send(key, 'Up' if up else 'Down')
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
            send(b'\r', 'Enter exact ' + target)
            previous = menu
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
        wait(lambda: options_frame(bytes(captured), cut, '💾 Сохранить', initial=True), 'Fresh complete Options custom frame')
        cut = len(captured)
        send(str(number).encode(), 'Options numeric selection')
        wait(lambda: options_frame(bytes(captured), cut, selected, initial=False), 'Fresh complete Options selected redraw')
        cut = len(captured); previous = None
        send(b'\r', 'Options Enter')

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
    wait(lambda: 'Название сохранения:' in screen.plain and not screen.pending, 'Actual Save name composer')
    send(b's\r', 'Save replacement text s and Enter')
    wait(lambda: 'Описание (необязательно):' in screen.plain and not screen.pending, 'Actual Save description composer')
    save_cut = len(screen.plain)
    send(b'\r', 'Empty description Enter')
    wait(lambda: '✅ Игра сохранена' in screen.plain[save_cut:] and not screen.pending, 'Actual Save outcome')
    if not clean_save_interval(screen.plain[save_cut:]):
        raise ValueError('Save committed with follow-up, not clean route')
    cut = len(captured); send(b'\r', 'Save acknowledgement')
    stage('deposit')
    previous = None
    # Same ordered PTY stream must reach a complete new inventory, not merely the
    # external stage file, before deciding whether the entire Save was clean.
    deposit_menu = inventory()
    wait(lambda: routes.selected_frames(bytes(captured), cut, deposit_menu), 'Returned post-Save inventory boundary')
    if not clean_save_interval(screen.plain[save_cut:]):
        raise ValueError('Save follow-up observed through actual returned inventory')
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
