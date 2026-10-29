import { useEffect, useRef, useState, type FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { ExplorerCommandResult, JsonValue, UiAction, UiPrompt } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import { commandStateLabel } from '../utils/formatters';
import { sanitizeExplorerCommandResultForPlayer, sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';
import { BlockList } from './BlockRenderer';
import { PromptForm, type PromptAnswers } from './PromptForm';
import { WoundCommandView, woundViewMode } from './WoundCommandView';

type LocalPromptResult = { commandResult: ExplorerCommandResult | null; result: ExplorerCommandResult };
type PromptOperation = { commandResult: ExplorerCommandResult | null; kind: 'submit' | 'cancel' | 'action' };

export function CommandResultView() {
  const { advancedEnabled, commandResult, clearCommandResult, executeCommand, loadBrowserState } = useShell();
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [promptOperation, setPromptOperation] = useState<'submit' | 'cancel' | 'action' | null>(null);
  const [localResult, setLocalResult] = useState<LocalPromptResult | null>(null);
  const [promptError, setPromptError] = useState<string | null>(null);
  const activeCommand = useRef(commandResult);
  activeCommand.current = commandResult;
  const activeOperation = useRef<PromptOperation | null>(null);
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; activeOperation.current = null; };
  }, []);

  const currentLocalResult = localResult?.commandResult === commandResult ? localResult.result : null;
  const result = currentLocalResult ?? commandResult;
  const ownsOperation = activeOperation.current?.commandResult === commandResult;
  const isSubmitting = ownsOperation && promptOperation === 'submit';
  const isCancelling = ownsOperation && promptOperation === 'cancel';
  const isBusy = ownsOperation && promptOperation !== null;
  const mode = result ? woundViewMode(result.command) : null;

  useEffect(() => {
    setLocalResult(null);
    setPromptAnswers({});
    setPromptError(null);
  }, [commandResult]);

  if (!result) return null;

  async function handleActionClick(action: UiAction) {
    if (!mounted.current || activeCommand.current !== commandResult || activeOperation.current?.commandResult === commandResult) return;
    if (action.requiresConfirmation && !confirm(`Выполнить: ${action.label}?`)) return;
    const operation: PromptOperation = { commandResult, kind: 'action' };
    activeOperation.current = operation;
    setPromptOperation('action');
    setLocalResult(null);
    setPromptAnswers({});
    setPromptError(null);
    try { await executeCommand(action.command); }
    finally {
      if (activeOperation.current === operation) {
        activeOperation.current = null;
        setPromptOperation(null);
      }
    }
  }

  function handlePromptAnswerChange(promptId: string, value: JsonValue | undefined) {
    setPromptAnswers((prev) => ({ ...prev, [promptId]: value }));
  }

  async function handlePromptSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!mounted.current || !result?.interactiveSession || activeCommand.current !== commandResult ||
      activeOperation.current?.commandResult === commandResult) return;
    const operation: PromptOperation = { commandResult, kind: 'submit' };
    activeOperation.current = operation;
    setPromptError(null);
    setPromptOperation('submit');
    try {
      const response = await browserApi.submitPromptSession({
        sessionId: result.interactiveSession.sessionId,
        ownerId: result.interactiveSession.ownerId,
        answers: promptAnswers
      });
      if (!mounted.current || activeCommand.current !== commandResult || activeOperation.current !== operation) return;
      if (response.ok) {
        setLocalResult({ commandResult, result: advancedEnabled ? response.data : sanitizeExplorerCommandResultForPlayer(response.data) });
        setPromptAnswers({});
      } else setPromptError('Ответ не подтверждён. Форма остаётся открытой; проверьте состояние игры.');
      void loadBrowserState();
    } catch {
      if (mounted.current && activeCommand.current === commandResult && activeOperation.current === operation)
        setPromptError('Не удалось получить ответ. Проверьте состояние игры перед повторным действием.');
    }
    finally {
      if (activeOperation.current === operation) {
        activeOperation.current = null;
        setPromptOperation(null);
      }
    }
  }

  async function handlePromptCancel() {
    if (!mounted.current || !result?.interactiveSession || activeCommand.current !== commandResult ||
      activeOperation.current?.commandResult === commandResult) return;
    const operation: PromptOperation = { commandResult, kind: 'cancel' };
    activeOperation.current = operation;
    setPromptError(null);
    setPromptOperation('cancel');
    try {
      const response = await browserApi.cancelPromptSession({
        sessionId: result.interactiveSession.sessionId,
        ownerId: result.interactiveSession.ownerId
      });
      if (!mounted.current || activeCommand.current !== commandResult || activeOperation.current !== operation) return;
      if (response.ok) {
        setLocalResult({ commandResult, result: advancedEnabled ? response.data : sanitizeExplorerCommandResultForPlayer(response.data) });
        setPromptAnswers({});
      } else setPromptError('Отмена не подтверждена. Форма остаётся открытой; проверьте состояние игры.');
      void loadBrowserState();
    } catch {
      if (mounted.current && activeCommand.current === commandResult && activeOperation.current === operation)
        setPromptError('Не удалось подтвердить отмену. Форма остаётся открытой; проверьте состояние игры.');
    }
    finally {
      if (activeOperation.current === operation) {
        activeOperation.current = null;
        setPromptOperation(null);
      }
    }
  }

  const content = (
    <>
      {promptError && <p className="block-message block-message--error" role="alert">{promptError}</p>}
      {result.notifications.length > 0 && (
        <div className="command-result-view__notifications">
          {result.notifications.map((n, i) => (
            <div key={i} className={`block-message block-message--${n.severity.toLowerCase()}`}>
              <strong>{sanitizePlayerMessage(n.title, 'Уведомление').safe}</strong>
              <p>{sanitizePlayerMessage(n.message, 'Игровое действие изменило состояние.').safe}</p>
            </div>
          ))}
        </div>
      )}

      <div className="command-result-view__content">
        <BlockList
          blocks={result.blocks}
          advancedEnabled={advancedEnabled}
          onAction={handleActionClick}
          availableActions={result.actions}
        />
      </div>

      {result.actions.length > 0 && (
        <div className="command-result-view__actions" aria-label="Доступные действия">
          <span className="command-result-view__actions-label">Доступные действия</span>
          {result.actions.map((action) => (
            <button
              key={action.id}
              type="button"
              className={`btn-action btn-action--${action.style.toLowerCase()}`}
              disabled={isBusy}
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
              showOptionDescriptions={mode !== null}
            />
          ) : (
            <ReadOnlyPromptList prompts={result.prompts} />
          )}
        </div>
      )}
    </>
  );
  if (mode) return (
    <WoundCommandView result={result} mode={mode} busy={isBusy}
      onBack={() => { if (result.interactiveSession) void handlePromptCancel(); else clearCommandResult(); }}>
      {content}
    </WoundCommandView>
  );
  return (
    <div className="command-result-view">
      <div className="command-result-view__header">
        <button type="button" className="btn-back" disabled={isBusy} onClick={clearCommandResult}>← Назад к сцене</button>
        <span className="command-result-view__title">Сведения действия</span>
        <span className="status-pill">{commandStateLabel(result.state)}</span>
      </div>
      {content}
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
