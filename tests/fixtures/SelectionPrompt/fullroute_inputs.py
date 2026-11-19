"""T034 one literal actual-UI route; no game authority, generic key program or DSR replies."""
import json
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1] / 'ProductionMain'))
import mortal_console_routes as routes

LOCATION = 'Хозяйственный двор у нижней лестницы'
STORAGE = 'Запертая кладовая разметочных материалов'
STORAGE_ID = 'storage_t034_geometry'
CHALK_ID = 'itm_t034_chalk'


def run(root, captured, screen, pump, send, result, deadline, initial_cut, profile):
    """All waits share the original bounded deadline. No retry after unknown UI."""
    game = root / 'isolated-data' / 'game_session'
    cut = initial_cut
    previous = None

    def wait(predicate, label):
        while True:
            value = predicate()
            if value:
                return value
            if time.monotonic() >= deadline:
                raise TimeoutError(label)
            pump()

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
        nonlocal cut, previous
        if target not in menu['choices']:
            raise ValueError('Exact target absent')
        observed_menu = routes.after_menu(menu, previous) if previous else menu
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
                if not fresh:
                    return False
                if any(row not in (selected, expected) for row in fresh):
                    raise ValueError('Skipped/backward transition')
                moved = False
                for row in fresh:
                    if moved and row != expected:
                        raise ValueError('Transition reversed in coalesced frame')
                    moved |= row == expected
                return moved
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
            # Retain only the complete current frame interval, not pre-transition history.
            previous = None
        result.setdefault('Selected', []).append(target)

    def panel(count, inventory_count):
        rows = []
        if inventory_count:
            rows.append(f'📥 Положить предмет в хранилище ({inventory_count} в инвентаре)')
        if count:
            rows.append(f'📤 Забрать предмет из хранилища ({count} внутри)')
        return routes.fixed_menu(f'📦 {STORAGE}\n  Предметов внутри: {count}', rows + ['← Назад к инвентарю'], 10)

    def options(selected):
        if screen.pending or screen.decoder.getstate()[0] or screen.invalid:
            return False
        lines = screen.lines()
        labels = ['💾 Сохранить', '📂 Загрузить', '⚙️ Опции', '🏠 Выйти в главное меню', '↩️ Назад']
        # Actual custom Options grid, not a substituted SelectionPrompt.
        for label in labels:
            matches = [line.strip() for line in lines if label in line]
            if len(matches) != 1:
                return False
            if ('➤ ' + label in matches[0]) != (label == selected):
                return False
        return any('↑/↓ или W/S — выбор • Enter — подтвердить • Esc — назад' in line for line in lines)

    def open_options(name, number, selected):
        nonlocal previous, cut
        stage(name)
        wait(lambda: options('💾 Сохранить'), 'Actual full Options custom loop')
        cut = len(captured)
        send(str(number).encode(), 'Options numeric selection')
        wait(lambda: len(captured) > cut and options(selected), 'Fresh Options selected row')
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
    if 'Требуется служебная проверка хранилища сохранений.' in screen.plain:
        raise ValueError('Save committed with follow-up, not clean route')
    cut = len(captured); send(b'\r', 'Save acknowledgement')
    stage('deposit')
    previous = None
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
