import { useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type {
  BrowserApiResult,
  BrowserAudioAssetDto,
  BrowserAudioPlaylistDto,
  BrowserAudioSettingsDto,
  BrowserAudioSettingsUpdateRequest
} from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';
import { EmptyOrFailure } from './ErrorNotice';
import { formatSidebarAudioSummary } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';

export function AudioPanel() {
  const { activeRoute, advancedEnabled, readyState } = useShell();
  const [audioResult, setAudioResult] = useState<BrowserApiResult<BrowserAudioSettingsDto> | null>(readyState?.audio ?? null);
  const [notice, setNotice] = useState('');
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioSettingsUpdateQueueRef = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    setAudioResult(readyState?.audio ?? null);
  }, [readyState?.audio]);

  useEffect(() => () => {
    audioElementRef.current?.pause();
    audioElementRef.current = null;
  }, []);

  if (!audioResult) {
    return null;
  }

  if (!isSuccess(audioResult)) {
    return (
      <EmptyOrFailure
        result={audioResult}
        advancedEnabled={advancedEnabled}
        errorTitle="Музыка требует внимания"
        empty={{
          title: 'Музыка ждёт локальные настройки',
          message: 'Панель звука появится, когда книга отдаст общие настройки аудио.',
          action: 'Игра продолжит работать без музыки; технические подробности остаются в расширенном режиме.'
        }}
      />
    );
  }

  const audio = audioResult.data;
  const allAssetsAvailable = audio.playlists.every((p) => p.available) && audio.cues.every((c) => c.available);
  const playlist = selectPreferredPlaylist(audio, activeRoute);
  const hasMusic = Boolean(playlist?.tracks.length);
  const notificationCue = audio.cues.find((cue) => cue.id === 'turn-ready' && cue.asset) ?? audio.cues.find((cue) => cue.asset);

  function updateAudioSettings(request: BrowserAudioSettingsUpdateRequest) {
    audioSettingsUpdateQueueRef.current = audioSettingsUpdateQueueRef.current
      .catch(() => undefined)
      .then(async () => {
        try {
          const updated = await browserApi.updateAudioSettings(request);
          setAudioResult(updated);
          if (isSuccess(updated)) {
            const currentElement = audioElementRef.current;
            if (currentElement) {
              currentElement.volume = volumeToUnit(updated.data.musicVolume);
              if (!updated.data.musicEnabled) {
                currentElement.pause();
              }
            }
            setNotice('Настройки звука сохранены в общей конфигурации книги.');
          } else {
            setNotice(toPlayerFacingText(updated.playerMessage, 'Не удалось сохранить настройки звука.'));
          }
        } catch {
          setNotice('Не удалось сохранить настройки звука. Попробуйте ещё раз или проверьте, что книга запущена.');
        }
      });
    return audioSettingsUpdateQueueRef.current;
  }

  async function unlockBrowserMusic() {
    if (!audio.musicEnabled) {
      setNotice('Музыка выключена в общих настройках книги. Включите её переключателем ниже.');
      return;
    }

    const track = playlist?.tracks[0];
    if (!track) {
      setNotice(toPlayerFacingText(audio.missingAssetsMessage, 'Аудиофайлы для выбранного плейлиста не найдены. Книга продолжит игру без музыки.'));
      return;
    }

    const element = audioElementRef.current ?? new Audio();
    audioElementRef.current = element;
    element.loop = true;
    element.volume = volumeToUnit(audio.musicVolume);
    if (element.src !== new URL(track.url, window.location.href).href) {
      element.src = track.url;
    }

    try {
      await element.play();
      setNotice(`Музыка включена: ${toPlayerFacingText(playlist?.label ?? track.label, 'выбранный плейлист')}. Управление громкостью сохраняется в общих настройках.`);
    } catch {
      setNotice('Вкладка пока не разрешила запустить музыку. Нажмите кнопку ещё раз или проверьте разрешение на звук для этой вкладки.');
    }
  }

  async function previewCue(asset: BrowserAudioAssetDto | null | undefined) {
    if (!asset) {
      setNotice('Файл звуковой подсказки не найден, поэтому предпросмотр недоступен.');
      return;
    }

    if (!audio.soundEnabled) {
      setNotice('Звуковые подсказки выключены в общих настройках книги.');
      return;
    }

    const cueAudio = new Audio();
    cueAudio.src = asset.url;
    cueAudio.volume = volumeToUnit(audio.soundVolume);
    try {
      await cueAudio.play();
      setNotice(`Звуковая подсказка воспроизведена: ${toPlayerFacingText(asset.label, 'подсказка')}.`);
    } catch {
      setNotice('Вкладка пока не разрешила запустить звуковую подсказку. Нажмите кнопку ещё раз или проверьте разрешение на звук для этой вкладки.');
    }
  }

  return (
    <section className="audio-control-panel" aria-labelledby="browser-audio-title">
      <div>
        <p className="panel-eyebrow">музыка и звук</p>
        <h2 id="browser-audio-title">Музыка и звук</h2>
        <p>{toPlayerFacingText(audio.autoplayGuidance, 'Музыка запускается только после вашего нажатия.')}</p>
        <p className="muted">{formatSidebarAudioSummary(audio)}</p>
        {audio.missingAssetsMessage && <p className="warning-text">{toPlayerFacingText(audio.missingAssetsMessage, 'Локальные аудиофайлы не найдены.')}</p>}
      </div>

      <div className="split-grid">
        <div className="summary-card">
          <h3>Музыка</h3>
          <p>{playlist ? `${toPlayerFacingText(playlist.label, 'Плейлист')}: ${toPlayerFacingText(playlist.usage, 'музыка для текущего раздела')}` : 'Плейлисты пока недоступны.'}</p>
          <button type="button" onClick={unlockBrowserMusic} disabled={!audio.musicEnabled || !hasMusic}>
            Включить музыку
          </button>
          {!hasMusic && <p className="muted">Когда в локальной папке появятся треки, книга сможет включить их после вашего нажатия.</p>}
        </div>
        <div className="summary-card">
          <h3>Звуковые подсказки</h3>
          <p>{notificationCue?.usage ? toPlayerFacingText(notificationCue.usage, 'Быстрые сцены и уведомления будут звучать, если локальные файлы найдены.') : 'Быстрые сцены и уведомления будут звучать, если локальные файлы найдены.'}</p>
          <button type="button" onClick={() => void previewCue(notificationCue?.asset)} disabled={!audio.soundEnabled || !notificationCue?.asset}>
            Проверить подсказку
          </button>
        </div>
      </div>

      <div className="audio-settings-grid">
        <label className="audio-toggle">
          <input
            type="checkbox"
            checked={audio.musicEnabled}
            onChange={(event) => void updateAudioSettings({ musicEnabled: event.currentTarget.checked })}
          />
          <span>Музыка включена</span>
        </label>
        <label className="audio-toggle">
          <input
            type="checkbox"
            checked={audio.soundEnabled}
            onChange={(event) => void updateAudioSettings({ soundEnabled: event.currentTarget.checked })}
          />
          <span>Звуковые подсказки включены</span>
        </label>
        <label className="audio-slider">
          <span>Громкость музыки: {audio.musicVolume}%</span>
          <input
            type="range"
            min="0"
            max="100"
            value={audio.musicVolume}
            onChange={(event) => void updateAudioSettings({ musicVolume: Number(event.currentTarget.value) })}
          />
        </label>
        <label className="audio-slider">
          <span>Громкость подсказок: {audio.soundVolume}%</span>
          <input
            type="range"
            min="0"
            max="100"
            value={audio.soundVolume}
            onChange={(event) => void updateAudioSettings({ soundVolume: Number(event.currentTarget.value) })}
          />
        </label>
      </div>

      {advancedEnabled ? (
        <div className="audio-catalog" aria-label="Доступные плейлисты и подсказки">
          {audio.playlists.map((item) => (
            <span key={item.id} className={item.available ? 'status-pill' : 'status-pill is-muted'}>
              {toPlayerFacingText(item.label, 'Плейлист')}: {item.available ? `${item.tracks.length} трек(ов)` : 'файлы не найдены'}
            </span>
          ))}
          {audio.cues.map((cue) => (
            <span key={cue.id} className={cue.available ? 'status-pill' : 'status-pill is-muted'}>
              {toPlayerFacingText(cue.label, 'Звуковая подсказка')}: {cue.available ? 'готово' : 'нет файла'}
            </span>
          ))}
        </div>
      ) : !allAssetsAvailable ? (
        <p className="muted">
          Доступно плейлистов: {audio.playlists.filter((item) => item.available).length}/{audio.playlists.length} · Подсказок: {audio.cues.filter((cue) => cue.available).length}/{audio.cues.length}
        </p>
      ) : null}
      {notice && <p className="composer-notice">{notice}</p>}
    </section>
  );
}

function selectPreferredPlaylist(audio: BrowserAudioSettingsDto, activeRoute: ReturnType<typeof useShell>['activeRoute']): BrowserAudioPlaylistDto | null {
  const preferredId = activeRoute === 'home' ? 'main-menu' : 'in-game';
  return audio.playlists.find((playlist) => playlist.id === preferredId && playlist.available)
    ?? audio.playlists.find((playlist) => playlist.available)
    ?? null;
}

function volumeToUnit(value: number): number {
  return Math.min(1, Math.max(0, value / 100));
}
