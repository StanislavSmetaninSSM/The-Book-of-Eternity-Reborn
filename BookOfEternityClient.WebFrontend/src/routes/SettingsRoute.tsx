import { useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserClientSettingsDto, BrowserClientSettingsUpdateRequest } from '../api/contracts';
import { EmptyOrFailure } from '../components/ErrorNotice';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';
import { toPlayerFacingText } from '../utils/playerCopy';

export default function SettingsRoute() {
  const { advancedEnabled, loadBrowserState, readyState } = useShell();
  const [settingsResult, setSettingsResult] = useState<BrowserApiResult<BrowserClientSettingsDto> | null>(readyState?.settings ?? null);
  const [notice, setNotice] = useState('');
  const clientSettingsUpdateQueueRef = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    setSettingsResult(readyState?.settings ?? null);
  }, [readyState?.settings]);

  if (!settingsResult) {
    return null;
  }

  if (!isSuccess(settingsResult)) {
    return <EmptyOrFailure result={settingsResult} advancedEnabled={advancedEnabled} errorTitle="Настройки требуют внимания" empty={{
      title: 'Настройки готовятся',
      message: 'Параметры локального клиента появятся, когда общая конфигурация книги будет доступна.',
      action: 'Если вы только открыли клиент, подождите загрузки или вернитесь на главную страницу.'
    }} />;
  }

  const settings = settingsResult.data;

  function updateClientSettings(request: BrowserClientSettingsUpdateRequest) {
    setNotice('Сохраняем настройки книги…');
    clientSettingsUpdateQueueRef.current = clientSettingsUpdateQueueRef.current
      .catch(() => undefined)
      .then(async () => {
        try {
          const updated = await browserApi.updateClientSettings(request);
          setSettingsResult(updated);
          if (isSuccess(updated)) {
            setNotice('Настройки книги сохранены в общей конфигурации клиента.');
            await loadBrowserState();
          } else {
            setNotice(toPlayerFacingText(updated.playerMessage, 'Не удалось сохранить настройки книги.'));
          }
        } catch {
          setNotice('Не удалось сохранить настройки книги. Проверьте локальный клиент и попробуйте ещё раз.');
        }
      });
  }

  return (
    <ShellPanel title="Настройки книги" eyebrow="локальность клиента">
      <p className="muted">Настройки читаются и сохраняются в общей конфигурации игры, чтобы браузерный и консольный клиенты не расходились.</p>

      <div className="settings-route-grid">
        <section className="settings-control-card" aria-labelledby="settings-language-title">
          <h3 id="settings-language-title">Язык клиента</h3>
          <p className="muted">Выберите язык интерфейса там, где локальный клиент уже поддерживает перевод.</p>
          <label>
            <span>Текущий язык</span>
            <select value={settings.language.value} onChange={(event) => void updateClientSettings({ language: event.currentTarget.value })}>
              {settings.language.choices.map((choice) => (
                <option key={choice.value} value={choice.value}>{choice.label}</option>
              ))}
            </select>
          </label>
          <p>{toPlayerFacingText(settings.language.label, 'Русский')}</p>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-difficulty-title">
          <h3 id="settings-difficulty-title">Сложность</h3>
          <p className="muted">Сложность остаётся общей для консольного клиента и для подсказок ГМа.</p>
          <label>
            <span>Режим сложности</span>
            <select value={settings.difficulty.value} onChange={(event) => void updateClientSettings({ difficulty: event.currentTarget.value })}>
              {settings.difficulty.choices.map((choice) => (
                <option key={choice.value} value={choice.value}>{choice.label}</option>
              ))}
            </select>
          </label>
          <p>{settings.difficulty.choices.find((choice) => choice.value === settings.difficulty.value)?.description ?? 'Базовый уровень сложности.'}</p>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-gm-thoughts-title">
          <h3 id="settings-gm-thoughts-title">Показывать мысли ГМа</h3>
          <p className="muted">Это явная настройка игрока: скрытые заметки не появляются в обычной игре без вашего выбора.</p>
          <label className="audio-toggle">
            <input type="checkbox" checked={settings.showGmThoughts} onChange={(event) => void updateClientSettings({ showGmThoughts: event.currentTarget.checked })} />
            <span>{settings.showGmThoughts ? 'Мысли ГМа будут показаны' : 'Мысли ГМа скрыты'}</span>
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-audio-title">
          <h3 id="settings-audio-title">Музыка и звуковые подсказки</h3>
          <p className="muted">Эти значения используют ту же общую настройку, что и постоянная аудиопанель.</p>
          <label className="audio-toggle">
            <input type="checkbox" checked={settings.audio.musicEnabled} onChange={(event) => void updateClientSettings({ musicEnabled: event.currentTarget.checked })} />
            <span>Музыка включена</span>
          </label>
          <label className="audio-slider">
            <span>Громкость музыки: {settings.audio.musicVolume}%</span>
            <input type="range" min="0" max="100" value={settings.audio.musicVolume} onChange={(event) => void updateClientSettings({ musicVolume: Number(event.currentTarget.value) })} />
          </label>
          <label className="audio-toggle">
            <input type="checkbox" checked={settings.audio.soundEnabled} onChange={(event) => void updateClientSettings({ soundEnabled: event.currentTarget.checked })} />
            <span>Звуковые подсказки включены</span>
          </label>
          <label className="audio-slider">
            <span>Громкость подсказок: {settings.audio.soundVolume}%</span>
            <input type="range" min="0" max="100" value={settings.audio.soundVolume} onChange={(event) => void updateClientSettings({ soundVolume: Number(event.currentTarget.value) })} />
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-accessibility-title">
          <h3 id="settings-accessibility-title">Доступность</h3>
          <p className="muted">Эти параметры меняют только представление браузерного клиента и не добавляют отдельной игровой логики.</p>
          <label className="audio-slider">
            <span>Масштаб текста: {settings.accessibility.fontScalePercent}%</span>
            <input type="range" min="80" max="140" step="5" value={settings.accessibility.fontScalePercent} onChange={(event) => void updateClientSettings({ browserFontScalePercent: Number(event.currentTarget.value) })} />
          </label>
          <label className="audio-toggle">
            <input type="checkbox" checked={settings.accessibility.reducedMotion} onChange={(event) => void updateClientSettings({ browserReducedMotion: event.currentTarget.checked })} />
            <span>Снизить движение интерфейса</span>
          </label>
          <label className="audio-toggle">
            <input type="checkbox" checked={settings.accessibility.contrastFriendly} onChange={(event) => void updateClientSettings({ browserContrastFriendly: event.currentTarget.checked })} />
            <span>Контрастный режим</span>
          </label>
        </section>

        <section className="settings-control-card" aria-labelledby="settings-locality-title">
          <h3 id="settings-locality-title">Локальность</h3>
          <p className="status-pill">{settings.locality.localhostOnly ? 'Только localhost/loopback' : 'Нужна проверка локальности'}</p>
          <dl className="kv-list">
            <div><dt>Сессия</dt><dd>{toPlayerFacingText(settings.locality.sessionLabel, 'game_session — локальная папка книги')}</dd></div>
            <div><dt>Папка книги</dt><dd>{settings.locality.gameSessionExists ? 'найдена' : 'ещё не создана'}</dd></div>
            <div><dt>Мост ГМа</dt><dd>{toPlayerFacingText(settings.locality.gmBridgeLabel, settings.locality.gmBridgeEnabled ? 'локальный мост включён' : 'локальный мост выключен')}</dd></div>
          </dl>
          <p className="muted">{toPlayerFacingText(settings.locality.safetySummary, 'Браузерный клиент работает только локально.')}</p>
        </section>
      </div>

      {notice && <p className="composer-notice">{notice}</p>}
      <p className="muted">Опасные технические настройки, ключи, команды запуска и внутренние параметры моста ГМа не показываются обычному игроку без расширенного режима.</p>
    </ShellPanel>
  );
}
