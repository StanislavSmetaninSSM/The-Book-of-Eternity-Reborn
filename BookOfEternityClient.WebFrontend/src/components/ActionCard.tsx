import { useState, type FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { BrowserApiResult, BrowserPlayerCommandActionDto, ExplorerCommandResult } from '../api/contracts';
import { isSuccess, useShell } from '../context/ShellContext';
import { toCommandNotice } from '../utils/formatters';
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';
import { ActionCommandResult } from './CommandResult';
import { buildDefaultPromptAnswers, type PromptAnswers } from './PromptForm';

async function submitGuidedForm(action: BrowserPlayerCommandActionDto) {
  return browserApi.executeExplorerCommand({ command: action.advancedCommand, ownerLabel: 'Игровое меню' });
}

async function submitPromptAnswers(commandResult: BrowserApiResult<ExplorerCommandResult>, promptAnswers: PromptAnswers) {
  if (!isSuccess(commandResult) || !commandResult.data.interactiveSession) {
    return commandResult;
  }

  const session = commandResult.data.interactiveSession;
  return browserApi.submitPromptSession({
    sessionId: session.sessionId,
    ownerId: session.ownerId,
    answers: promptAnswers
  });
}

export function ActionCard({ action }: { action: BrowserPlayerCommandActionDto }) {
  const { advancedEnabled } = useShell();
  const [notice, setNotice] = useState('');
  const [commandResult, setCommandResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isGuidedForm = action.formMode !== 'none';

  async function handleGuidedFormSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSubmitting(true);
    setNotice(isGuidedForm ? 'Открываем игровую форму…' : 'Открываем игровой раздел…');

    const result = await submitGuidedForm(action);
    setCommandResult(result);
    if (isSuccess(result)) {
      setPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toCommandNotice(result.data));
    } else {
      setNotice(result.playerMessage || 'Игровое действие сейчас недоступно.');
    }
    setIsSubmitting(false);
  }

  async function handlePromptAnswersSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!commandResult || !isSuccess(commandResult) || !commandResult.data.interactiveSession) {
      return;
    }

    setIsSubmitting(true);
    setNotice('Отправляем заполненную форму…');
    const result = await submitPromptAnswers(commandResult, promptAnswers);
    setCommandResult(result);
    if (isSuccess(result)) {
      setPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toCommandNotice(result.data));
    } else {
      setNotice(result.playerMessage || 'Игровое действие сейчас недоступно.');
    }
    setIsSubmitting(false);
  }

  const noticeFallback = commandResult && !isSuccess(commandResult)
    ? 'Игровое действие сейчас недоступно.'
    : 'Игровое действие обработано.';

  return (
    <article className={action.enabled ? 'action-card' : 'action-card is-disabled'}>
      <header>
        <h4>{toPlayerFacingText(action.label, 'Игровое действие')}</h4>
        <span className="availability-pill">{toPlayerFacingText(action.realmAvailability, 'Доступность уточняется.')}</span>
      </header>
      <p>{toPlayerFacingText(action.description, 'Описание действия появится здесь.')}</p>
      <p className={action.mutationMode === 'local-turn' ? 'warning-text' : 'muted'}>{toPlayerFacingText(action.mutationWarning, 'Состояние игры не изменится без подтверждения.')}</p>
      {!action.enabled && <p className="warning-text">{toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.')}</p>}
      <form className="guided-form" onSubmit={handleGuidedFormSubmit}>
        <label htmlFor={`action-form-${action.id}`}>{isGuidedForm ? toPlayerFacingText(action.formLabel, 'Открыть форму') : 'Открыть раздел'}</label>
        <p id={`action-form-${action.id}`} className="muted">{toPlayerFacingText(action.formPrompt, 'Откройте игровой раздел без изменения состояния.')}</p>
        <button type="submit" disabled={!action.enabled || isSubmitting}>
          {isSubmitting ? 'Выполняем…' : isGuidedForm ? 'Подготовить форму' : 'Открыть раздел'}
        </button>
      </form>
      {notice && (() => {
        const { safe, hasTechnical } = sanitizePlayerMessage(notice, noticeFallback);
        return (
          <>
            <p className="composer-notice">{safe}</p>
            {hasTechnical && advancedEnabled && <p className="muted">{notice}</p>}
          </>
        );
      })()}
      {commandResult && (
        <ActionCommandResult
          result={commandResult}
          promptAnswers={promptAnswers}
          onPromptAnswerChange={(promptId, value) => setPromptAnswers((current) => ({ ...current, [promptId]: value }))}
          onPromptSubmit={handlePromptAnswersSubmit}
          isSubmitting={isSubmitting}
        />
      )}
    </article>
  );
}
