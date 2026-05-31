import { useEffect, useState, type FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { ExplorerCommandResult, JsonValue, UiAction, UiPrompt } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import { toPlayerFacingText } from '../utils/playerCopy';
import { BlockList } from './BlockRenderer';
import { PromptForm, type PromptAnswers } from './PromptForm';

type LocalPromptResult = { commandResult: ExplorerCommandResult | null; result: ExplorerCommandResult };

export function CommandResultView() {
  const { commandResult, clearCommandResult, executeCommand, loadBrowserState } = useShell();
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [promptOperation, setPromptOperation] = useState<'submit' | 'cancel' | null>(null);
  const [localResult, setLocalResult] = useState<LocalPromptResult | null>(null);

  const currentLocalResult = localResult?.commandResult === commandResult ? localResult.result : null;
  const result = currentLocalResult ?? commandResult;
  const isSubmitting = promptOperation === 'submit';
  const isCancelling = promptOperation === 'cancel';

  useEffect(() => {
    setLocalResult(null);
    setPromptAnswers({});
  }, [commandResult]);

  if (!result) return null;

  function handleActionClick(action: UiAction) {
    if (action.requiresConfirmation && !confirm(`Выполнить: ${action.label}?`)) return;
    setLocalResult(null);
    setPromptAnswers({});
    void executeCommand(action.command);
  }

  function handlePromptAnswerChange(promptId: string, value: JsonValue | undefined) {
    setPromptAnswers((prev) => ({ ...prev, [promptId]: value }));
  }

  async function handlePromptSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!result?.interactiveSession) return;
    setPromptOperation('submit');
    try {
      const response = await browserApi.submitPromptSession({
        sessionId: result.interactiveSession.sessionId,
        ownerId: result.interactiveSession.ownerId,
        answers: promptAnswers
      });
      if (response.ok) {
        setLocalResult({ commandResult, result: response.data });
        setPromptAnswers({});
      }
      void loadBrowserState();
    } catch { /* handled by notice */ }
    setPromptOperation(null);
  }

  async function handlePromptCancel() {
    if (!result?.interactiveSession) return;
    setPromptOperation('cancel');
    try {
      const response = await browserApi.cancelPromptSession({
        sessionId: result.interactiveSession.sessionId,
        ownerId: result.interactiveSession.ownerId
      });
      if (response.ok) {
        setLocalResult({ commandResult, result: response.data });
        setPromptAnswers({});
      }
      void loadBrowserState();
    } catch { /* handled by notice */ }
    setPromptOperation(null);
  }

  return (
    <div className="command-result-view">
      <div className="command-result-view__header">
        <button type="button" className="btn-back" onClick={clearCommandResult}>
          ← Назад к сцене
        </button>
        <span className="command-result-view__command">{result.command}</span>
      </div>

      {result.notifications.length > 0 && (
        <div className="command-result-view__notifications">
          {result.notifications.map((n, i) => (
            <div key={i} className={`block-message block-message--${n.severity.toLowerCase()}`}>
              <strong>{n.title}</strong>
              <p>{n.message}</p>
            </div>
          ))}
        </div>
      )}

      <div className="command-result-view__content">
        <BlockList blocks={result.blocks} />
      </div>

      {result.actions.length > 0 && (
        <div className="command-result-view__actions">
          {result.actions.map((action) => (
            <button
              key={action.id}
              type="button"
              className={`btn-action btn-action--${action.style.toLowerCase()}`}
              onClick={() => handleActionClick(action)}
            >
              {action.label}
            </button>
          ))}
        </div>
      )}

      {result.prompts.length > 0 && (
        <div className="command-result-view__prompts">
          {result.interactiveSession ? (
            <PromptForm
              prompts={result.prompts}
              promptAnswers={promptAnswers}
              onPromptAnswerChange={handlePromptAnswerChange}
              onSubmit={handlePromptSubmit}
              onCancel={handlePromptCancel}
              isSubmitting={isSubmitting}
              isCancelling={isCancelling}
            />
          ) : (
            <ReadOnlyPromptList prompts={result.prompts} />
          )}
        </div>
      )}
    </div>
  );
}

function ReadOnlyPromptList({ prompts }: { prompts: UiPrompt[] }) {
  return (
    <div className="prompt-readonly-list" aria-label="Поля команды">
      <h5>Поля команды</h5>
      {prompts.map((prompt) => (
        <article key={prompt.id} className="prompt-readonly-card">
          <div className="prompt-readonly-card__header">
            <h6>{toPlayerFacingText(prompt.prompt, 'Поле команды')}</h6>
            {prompt.required && <span>обязательно</span>}
          </div>
          {renderReadOnlyPromptDetails(prompt)}
        </article>
      ))}
    </div>
  );
}

function renderReadOnlyPromptDetails(prompt: UiPrompt) {
  switch (prompt.kind) {
    case 'confirmation':
      return (
        <p className="muted">
          Значение по умолчанию: {prompt.defaultValue ? 'подтверждено' : 'не подтверждено'}.
        </p>
      );
    case 'selection':
      return (
        <ul className="prompt-readonly-options">
          {prompt.options.map((option) => (
            <li key={option.value} className={option.disabled ? 'is-disabled' : undefined}>
              <strong>{toPlayerFacingText(option.label, 'вариант')}</strong>
              {option.description && <span>{toPlayerFacingText(option.description, 'Описание варианта недоступно.')}</span>}
            </li>
          ))}
        </ul>
      );
    case 'longTextInput':
      return (
        <textarea
          readOnly
          rows={prompt.minLines ?? 3}
          value={prompt.defaultValue}
          placeholder={toPlayerFacingText(prompt.placeholder, '')}
        />
      );
    case 'textInput':
      return (
        <input
          readOnly
          type="text"
          value={prompt.defaultValue}
          placeholder={toPlayerFacingText(prompt.placeholder, '')}
        />
      );
  }
}
