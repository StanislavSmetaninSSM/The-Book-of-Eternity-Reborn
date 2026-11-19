"""Exact cold Save43 admission/UI DATA. No authority, process or world actions on import."""
import hashlib, io, json, pathlib, re, stat, zipfile
from client_ui import _EDGE, _line

COLD_CHOICES=('🌟 Новая игра','📂 Загрузить профиль','Тренировка QTE','Вылазка Дарена','⚙️ Опции','ℹ️ Об игре','🚪 Выход')

def cold_main_ready(view,raw,cut,selected):
    if view.pending or view.cut!=len(raw) or not 0<=cut<len(raw) or selected not in (0,1):return False
    try:text=raw[cut:].decode('utf-8')
    except UnicodeError:return False
    rows=[]
    for i,title in enumerate(COLD_CHOICES):
        marker='➤ ' if i==selected else '◆ '
        rows.append(r' *'+_EDGE+r' *'+re.escape(marker.rstrip())+_EDGE+r' '+_EDGE+re.escape(str(i+1)+'. '+title)+r' *'+_EDGE+r' *\r?\n')
    grid=_line('Выберите следующий шаг')+r'(?: *\r?\n)*'+''.join(rows)+r' *'+_EDGE+r' *'+re.escape('↑/↓ • W/S • 1-7 • Enter')+r' *'+_EDGE+r' *(?:\r?\n)?$'
    if re.search(grid,text) is None:return False
    expected=['Выберите следующий шаг']+[('➤ ' if i==selected else '◆ ')+str(i+1)+'. '+title for i,title in enumerate(COLD_CHOICES)]+['↑/↓ • W/S • 1-7 • Enter']
    cells=[s.strip() for s in view.lines if s.strip()]
    return cells[-len(expected):]==expected

def clean_cold_load_message(text):
    # Sole success message, not a substring amid blocked/follow-up diagnostics.
    edge=r'(?:\x1b\[(?:0|32)m)*'
    return type(text) is str and re.fullmatch(r'(?:\r?\n)?'+edge+re.escape('Сохранение загружено.')+edge+r'\r?\n',text) is not None

def read_cold_archive(path,expected_name,expected_sha):
    path=pathlib.Path(path)
    if path.name!=expected_name or type(expected_sha) is not str or re.fullmatch('[0-9a-f]{64}',expected_sha) is None:raise ValueError('Selected archive identity mismatch')
    # Fresh nonfollowing namespace check, no ownership/authority assertion.
    if any(p.is_symlink() for p in (path,*path.parents)) or not stat.S_ISREG(path.lstat().st_mode):raise ValueError('Archive must be regular and unlinked')
    blob=path.read_bytes();digest=hashlib.sha256(blob).hexdigest()
    if digest!=expected_sha:raise ValueError('Selected archive digest mismatch')
    # Immutable one-read DATA reused for CRC, metadata and eventual comparisons.
    try:
        with zipfile.ZipFile(io.BytesIO(blob)) as z:
            names=[i.filename for i in z.infolist()]
            if len(names)!=len(set(n.casefold() for n in names)):raise ValueError('Duplicate archive member')
            for i in z.infolist():
                p=pathlib.PurePosixPath(i.filename)
                if not i.filename or '\\' in i.filename or p.is_absolute() or '..' in p.parts or str(p)!=i.filename.rstrip('/') or stat.S_ISLNK(i.external_attr>>16):raise ValueError('Archive member path invalid')
            if z.testzip() is not None:raise ValueError('Archive CRC failed')
            members={n:z.read(n) for n in names if not n.endswith('/')}
        metadata=json.loads(members['save_metadata.json']);config=json.loads(members['config.json'])
        if metadata.get('saveName')!='s' or type(metadata.get('turnNumber')) is not int or metadata['turnNumber']!=43:raise ValueError('Not exact Save43 metadata')
        if any(config.get(k) is not False for k in ('MusicEnabled','SoundEnabled','GenerateSceneImages','ShowImagesInConsole')) or config.get('ImageProvider')!='off':raise ValueError('Exact muted archive settings absent')
    except (KeyError,json.JSONDecodeError,zipfile.BadZipFile) as e:raise ValueError('Invalid cold archive') from e
    return {'ArchiveSHA256':digest,'ArchiveName':path.name,'Metadata':metadata,'Config':config,'Members':members,'SizeBytes':len(blob)}


def cold_load_completed(raw,cut,menu,view):
    # The real driver's whole transition: exact prior close, sole outcome, fresh screen.
    from fullroute_inputs import routes
    from client_ui import player_ready
    height=len(menu['header'].split('\n'))+len(menu['choices'])
    close=b'\r'+b'\x1b[2K\x1b[1A'*height+b'\x1b[2K\x1b[?25h'
    clear=b'\x1b[2J\x1b[3J\x1b[1;1H'
    tail=raw[cut:];at=tail.find(close)
    if at<0:return False
    if at:
        frames=routes.selected_frames(tail[:at],0,menu)
        if not frames or any(row!=menu['choices'][0] for row in frames):return False
    end=tail.find(clear,at+len(close))
    if end<0:return False
    try:message=tail[at+len(close):end].decode('utf-8')
    except UnicodeError:return False
    return clean_cold_load_message(message) and player_ready(view,raw,cut+end)
