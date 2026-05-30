import { useCallback, useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserClientSettingsDto, BrowserClientSettingsUpdateRequest } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';

export function SettingsView() {
  const { readyState, advancedEnabled, setAdvancedEnabled, loadBrowserState } = useShell();
  const [settings, setSettings] = useState<BrowserClientSettingsDto | null>(null);
  const updateQueue = useRef<ReturnType<typeof setTimeout> | null>(null);

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

  if (!settings) {
    return <div className="settings-view"><p className="block-text--muted">Загрузка настроек…</p></div>;
  }

  return (
    <div className="settings-view">
      <section className="settings-card">
        <h3>⚙️ Основные</h3>
        <div className="settings-row">
          <label>Язык клиента</label>
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

      <section className="settings-card">
        <h3>🔊 Звук</h3>
        <div className="settings-row">
          <label>Музыка</label>
          <input
            type="checkbox"
            checked={settings.audio.musicEnabled}
            onChange={(e) => { setSettings({ ...settings, audio: { ...settings.audio, musicEnabled: e.target.checked } }); debouncedUpdate({ musicEnabled: e.target.checked }); }}
          />
        </div>
        <div className="settings-row">
          <label>Громкость звуков</label>
          <input
            type="range"
            min="0"
            max="100"
            value={settings.audio.soundVolume}
            onChange={(e) => {
              const v = Number(e.target.value);
              setSettings({ ...settings, audio: { ...settings.audio, soundVolume: v } });
              debouncedUpdate({ soundVolume: v });
            }}
          />
          <span>{settings.audio.soundVolume}%</span>
        </div>
      </section>

      <section className="settings-card">
        <h3>♿ Доступность</h3>
        <div className="settings-row">
          <label>Размер шрифта</label>
          <input
            type="range"
            min="80"
            max="150"
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
            max="140"
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
