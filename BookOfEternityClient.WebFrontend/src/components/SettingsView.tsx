import { useCallback, useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserClientSettingsDto, BrowserClientSettingsUpdateRequest, BrowserMainMenuDto } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';
import { toLauncherSaveFailureNotice } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { AudioPanel } from './AudioPanel';

export function SettingsView() {
  const { readyState, menu, advancedEnabled, setAdvancedEnabled, setActiveRoute, loadBrowserState } = useShell();
  const [settings, setSettings] = useState<BrowserClientSettingsDto | null>(null);
  const [saveNotice, setSaveNotice] = useState('');
  const [loadingSaveId, setLoadingSaveId] = useState<string | null>(null);
  const updateQueue = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  useEffect(() => {
    if (readyState && isSuccess(readyState.settings)) {
      setSettings(readyState.settings.data);
    }
  }, [readyState]);

  const debouncedUpdate = useCallback((patch: BrowserClientSettingsUpdateRequest) => {
    if (updateQueue.current) clearTimeout(updateQueue.current);
    updateQueue.current = setTimeout(() => {
      void browserApi.updateClientSettings(patch).then(() => void loadBrowserState());
    }, 500);
  }, [loadBrowserState]);

  async function loadSaveSlot(slot: BrowserMainMenuDto['saves'][number]) {
    setLoadingSaveId(slot.saveId);
    setSaveNotice('Загружаем выбранное сохранение…');
    try {
      const result = await browserApi.loadSave({ saveId: slot.saveId });
      if (!isMountedRef.current) {
        return;
      }
      if (isSuccess(result) && result.data.success) {
        setSaveNotice(`Сохранение «${toPlayerFacingText(slot.displayName, 'выбранная запись')}» загружено. Открываем главу…`);
        setActiveRoute('game');
        await loadBrowserState();
        return;
      }
      if (isSuccess(result)) {
        setSaveNotice(toLauncherSaveFailureNotice(result.data.error));
        return;
      }
      setSaveNotice(toLauncherSaveFailureNotice(result.playerMessage));
    } catch {
      if (!isMountedRef.current) {
        return;
      }
      setSaveNotice('Сохранение не удалось загрузить. Проверьте, что книга запущена, и попробуйте ещё раз.');
    } finally {
      if (isMountedRef.current) {
        setLoadingSaveId(null);
      }
    }
  }

  if (!settings) {
    return <div className="settings-view"><p className="block-text--muted">Загрузка настроек…</p></div>;
  }

  return (
    <div className="settings-view">
      <section className="settings-card">
        <h3>⚙️ Основные</h3>
        <div className="settings-row">
          <label>Язык книги</label>
          <select
            value={settings.language.value}
            onChange={(e) => {
              setSettings({ ...settings, language: { ...settings.language, value: e.target.value } });
              debouncedUpdate({ language: e.target.value });
            }}
          >
            {settings.language.choices.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </select>
        </div>
        <div className="settings-row">
          <label>Сложность</label>
          <select
            value={settings.difficulty.value}
            onChange={(e) => {
              setSettings({ ...settings, difficulty: { ...settings.difficulty, value: e.target.value } });
              debouncedUpdate({ difficulty: e.target.value });
            }}
          >
            {settings.difficulty.choices.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </select>
        </div>
        <div className="settings-row">
          <label>Показывать мысли ГМа</label>
          <input
            type="checkbox"
            checked={settings.showGmThoughts}
            onChange={(e) => { setSettings({ ...settings, showGmThoughts: e.target.checked }); debouncedUpdate({ showGmThoughts: e.target.checked }); }}
          />
        </div>
      </section>

      <AudioPanel />

      <section className="settings-card" aria-labelledby="browser-saves-title">
        <h3 id="browser-saves-title">Сохранения</h3>
        <p className="block-text--muted">
          Здесь можно выбрать локальную запись и вернуться к ней без выхода в главное меню.
        </p>
        <div className="launcher-save-list settings-save-list">
          {menu?.saves && menu.saves.length > 0 ? menu.saves.map((slot) => (
            <article key={slot.saveId} className="launcher-save-card">
              <div>
                <h4>{toPlayerFacingText(slot.displayName, 'Сохранение')}</h4>
                <p>{toPlayerFacingText(slot.description, 'Локальная запись готова к загрузке.')}</p>
              </div>
              <dl className="kv-list">
                <div><dt>Тип</dt><dd>{toPlayerFacingText(slot.scopeLabel, 'сохранение')}</dd></div>
                <div><dt>Герой</dt><dd>{slot.characterName || 'не указан'}</dd></div>
                <div><dt>Ход</dt><dd>{toPlayerFacingText(slot.turnLabel, 'ход уточняется')}</dd></div>
              </dl>
              <button type="button" className="launcher-secondary-action" disabled={loadingSaveId !== null} onClick={() => void loadSaveSlot(slot)}>
                {loadingSaveId === slot.saveId ? 'Загружаем…' : 'Загрузить сохранение'}
              </button>
            </article>
          )) : <p className="muted">Сохранений пока нет. Когда локальная книга найдёт ручные или автоматические записи, они появятся здесь.</p>}
        </div>
        {saveNotice && <p className="composer-notice">{saveNotice}</p>}
      </section>

      <section className="settings-card">
        <h3>♿ Доступность</h3>
        <div className="settings-row">
          <label>Размер шрифта</label>
          <input
            type="range"
            min="80"
            max="200"
            step="1"
            value={settings.accessibility.fontScalePercent}
            onChange={(e) => {
              const v = Number(e.target.value);
              setSettings({ ...settings, accessibility: { ...settings.accessibility, fontScalePercent: v } });
              debouncedUpdate({ browserFontScalePercent: v });
            }}
          />
          <span>{settings.accessibility.fontScalePercent}%</span>
        </div>
        <div className="settings-row">
          <label>Масштаб интерфейса</label>
          <input
            type="range"
            min="80"
            max="200"
            step="1"
            value={settings.accessibility.uiScalePercent}
            onChange={(e) => {
              const v = Number(e.target.value);
              setSettings({ ...settings, accessibility: { ...settings.accessibility, uiScalePercent: v } });
              debouncedUpdate({ browserUiScalePercent: v });
            }}
          />
          <span>{settings.accessibility.uiScalePercent}%</span>
        </div>
        <div className="settings-row">
          <label>Уменьшить анимации</label>
          <input
            type="checkbox"
            checked={settings.accessibility.reducedMotion}
            onChange={(e) => { setSettings({ ...settings, accessibility: { ...settings.accessibility, reducedMotion: e.target.checked } }); debouncedUpdate({ browserReducedMotion: e.target.checked }); }}
          />
        </div>
      </section>

      <section className="settings-card">
        <h3>🔧 Расширенный режим</h3>
        <div className="settings-row">
          <label>Показывать технические данные</label>
          <input
            type="checkbox"
            checked={advancedEnabled}
            onChange={() => setAdvancedEnabled((v) => !v)}
          />
        </div>
        <p className="block-text--muted">Включает доступ к полному каталогу команд, JSON-блокам и диагностике.</p>
      </section>

      <section className="settings-card">
        <h3>ℹ️ Информация</h3>
        <div className="settings-row">
          <label>Сессия</label>
          <span className="settings-value">{settings.locality.sessionLabel}</span>
        </div>
        <div className="settings-row">
          <label>ГМ-мост</label>
          <span className="settings-value">{settings.locality.gmBridgeLabel}</span>
        </div>
        <p className="block-text--muted">{settings.locality.safetySummary}</p>
      </section>
    </div>
  );
}
