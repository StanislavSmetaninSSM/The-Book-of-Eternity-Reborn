import { useState } from 'react';
import type { FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { BrowserGameScreenDto, ExplorerCommandResult, BrowserApiResult, JsonValue } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import {
  getComposerDisabledReason,
  getComposerGuidance,
  getComposerPlaceholder
} from '../utils/formatters';
import { ActionPalette } from './ActionPalette';
import { ActionCommandResult } from './CommandResult';

export type ComposerMode = 'prose' | 'actions';

export function Composer({ actionComposer }: { actionComposer: BrowserGameScreenDto['actionComposer'] }) {
  const { loadBrowserState } = useShell();
  const [text, setText] = useState('');
  const [notice, setNotice] = useState('');
  const [mode, setMode] = useState<ComposerMode>('prose');
  const [commandResult, setCommandResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [promptAnswers, setPromptAnswers] = useState<Record<string, JsonValue | undefined>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  function handlePromptAnswerChange(promptId: string, value: JsonValue | undefined) {
    setPromptAnswers(prev => ({ ...prev, [promptId]: value }));
  }

  function handlePromptSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!commandResult || !commandResult.ok || !commandResult.data?.interactiveSession) return;
    setIsSubmitting(true);
    void browserApi.submitPromptSession({
      sessionId: commandResult.data.interactiveSession.sessionId,
      answers: promptAnswers
    }).then((result) => {
      setCommandResult(result);
      setPromptAnswers({});
      setIsSubmitting(false);
      void loadBrowserState();
    }).catch(() => {
      setIsSubmitting(false);
      setNotice('Ошибка при отправке ответа.');
    });
  }

  function submitProse(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalized = text.trim();

    if (!normalized) {
      return;
    }

    if (normalized.startsWith('/')) {
      setNotice('Выполняю команду…');
      setCommandResult(null);
      void browserApi.executeExplorerCommand({ command: normalized }).then((result) => {
        setCommandResult(result);
        setNotice('');
        setText('');
        void loadBrowserState();
      }).catch(() => {
        setNotice('Ошибка соединения при выполнении команды.');
      });
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
            placeholder={getComposerPlaceholder(actionComposer) + ' • Команды: /инвентарь, /статы, /нпс...'}
            disabled={!actionComposer.canSubmit}
          />
          {!actionComposer.canSubmit && (
            <p className="warning-text">{getComposerDisabledReason(actionComposer)}</p>
          )}
          <div className="composer-footer">
            <p className="muted">{getComposerGuidance(actionComposer)} Для команд используйте /команда (например /инвентарь, /статы, /навыки).</p>
            <button type="submit" disabled={!text.trim() || !actionComposer.canSubmit}>
              Отправить
            </button>
          </div>
          {notice && <p className="composer-notice">{notice}</p>}
        </form>
      )}

      {mode === 'actions' && <ActionPalette />}

      {commandResult && (
        <div className="command-result-container">
          <div className="command-result-header">
            <h4>📋 Результат команды</h4>
            <button type="button" className="command-result-close" onClick={() => setCommandResult(null)}>✕</button>
          </div>
          <ActionCommandResult
            result={commandResult}
            promptAnswers={promptAnswers}
            onPromptAnswerChange={handlePromptAnswerChange}
            onPromptSubmit={handlePromptSubmit}
            isSubmitting={isSubmitting}
          />
        </div>
      )}
    </section>
  );
}
