"""Closed source-shaped main/player predicates for foreground same-PTY client."""
import re
MAIN_CHOICES=('▶️ Продолжить','🌟 Новая игра','📂 Загрузить профиль','Тренировка QTE','Вылазка Дарена','⚙️ Опции','ℹ️ Об игре','🚪 Выход')
MAIN_HEADING='Выберите следующий шаг'
MAIN_FOOTER='↑/↓ • W/S • 1-8 • Enter'
_EDGE=r'(?:\x1b\[(?:0|1|2|3|4|9|22|23|24|29)(?:;(?:0|1|2|3|4|9|22|23|24|29))*m)*'
def _line(text):return r' *'+_EDGE+r' *'+re.escape(text)+r' *'+_EDGE+r' *\r?\n'
def main_ready(view,raw,cut,selected):
    if view.pending or view.cut!=len(raw) or not 0<=cut<len(raw) or selected not in (0,7):return False
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return False
    # Dynamic hero/status precede the grid. A complete fresh source grid is mandatory.
    rows=[]
    for i,title in enumerate(MAIN_CHOICES):
        marker='➤ ' if i==selected else '◆ '
        # Spectre styles marker/number separately on unselected rows.
        rows.append(r' *'+_EDGE+r' *'+re.escape(marker.rstrip())+_EDGE+r' '+_EDGE+re.escape(str(i+1)+'. '+title)+r' *'+_EDGE+r' *\r?\n')
    grid=_line(MAIN_HEADING)+r'(?: *\r?\n)*'+''.join(rows)+r' *'+_EDGE+r' *'+re.escape(MAIN_FOOTER)+r' *'+_EDGE+r' *(?:\r?\n)?$'
    match=re.search(grid,text)
    if not match:return False
    expected=[MAIN_HEADING]+[('➤ ' if i==selected else '◆ ')+str(i+1)+'. '+title for i,title in enumerate(MAIN_CHOICES)]+[MAIN_FOOTER]
    cells=[s.strip() for s in view.lines if s.strip()]
    return cells[-len(expected):]==expected

def player_ready(view,raw,cut):
    if view.pending or view.cut!=len(raw) or not 0<=cut<len(raw):return False
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return False
    if 'Сохранённый черновик' in text or '│' in text[-20:]:return False
    heading=text.rfind('✦ Ваш ход ✦')
    if heading<0:return False
    # Actual System input emits SaveCursor immediately after TextComposer's empty prompt.
    if re.search(r'(?:\r?\n)'+_EDGE+r' > '+_EDGE+r' \x1b\[s$',text) is None:return False
    state=view.cursor
    if state.get('x')!=4 or not 0<=state.get('y',-1)<25:return False
    return view.lines[state['y']]==' >' and any('✦ Ваш ход ✦' in line for line in view.lines)
