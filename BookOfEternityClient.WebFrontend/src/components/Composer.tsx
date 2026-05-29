import { useState } from 'react';
import type { FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { BrowserGameScreenDto } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import {
  getComposerDisabledReason,
  getComposerGuidance,
  getComposerPlaceholder
} from '../utils/formatters';
import { ActionPalette } from './ActionPalette';

export type ComposerMode = 'prose' | 'actions';

export function Composer({ actionComposer }: { actionComposer: BrowserGameScreenDto['actionComposer'] }) {
  const { loadBrowserState } = useShell();
  const [text, setText] = useState('');
  const [notice, setNotice] = useState('');
  const [mode, setMode] = useState<ComposerMode>('prose');

  function submitProse(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = text.trim();

    if (!normalized) {
      return;
    }

    if (normalized.startsWith('/')) {
      setNotice('Служебные команды не выполняются из основного поля. Используйте режим «Действия» или расширенный режим.');
      return;
    }

    setNotice('Отправляем действие…');
    void browserApi.submitPlayerAction({ text: normalized }).then((result) => {
      if (result.ok && result.data.success) {
        setNotice(result.data.playerMessage);
        setText('');
        void loadBrowserState();
      } else if (result.ok && !result.data.success) {
        setNotice(result.data.playerMessage);
      } else {
        setNotice('Не удалось отправить действие. Попробуйте ещё раз.');
      }
    }).catch(() => {
      setNotice('Ошибка соединения. Убедитесь, что клиент запущен.');
    });
  }

  return (
    <section className="composer-container" aria-label="Ввод игрока">
      <div className="composer-mode-toggle" role="tablist" aria-label="Режим ввода">
        <button
          type="button"
          role="tab"
          className={mode === 'prose' ? 'is-active' : ''}
          aria-selected={mode === 'prose'}
          onClick={() => setMode('prose')}
        >
          Художественный ввод
        </button>
        <button
          type="button"
          role="tab"
          className={mode === 'actions' ? 'is-active' : ''}
          aria-selected={mode === 'actions'}
          onClick={() => setMode('actions')}
        >
          Действия
        </button>
      </div>

      {mode === 'prose' && (
        <form className="composer" onSubmit={submitProse}>
          <textarea
            id="player-action"
            name="player-action"
            rows={3}
            value={text}
            onChange={(event) => setText(event.currentTarget.value)}
            placeholder={getComposerPlaceholder(actionComposer)}
            disabled={!actionComposer.canSubmit}
          />
          {!actionComposer.canSubmit && (
            <p className="warning-text">{getComposerDisabledReason(actionComposer)}</p>
          )}
          <div className="composer-footer">
            <p className="muted">{getComposerGuidance(actionComposer)}</p>
            <button type="submit" disabled={!text.trim() || !actionComposer.canSubmit}>
              Отправить
            </button>
          </div>
          {notice && <p className="composer-notice">{notice}</p>}
        </form>
      )}

      {mode === 'actions' && <ActionPalette />}
    </section>
  );
}
