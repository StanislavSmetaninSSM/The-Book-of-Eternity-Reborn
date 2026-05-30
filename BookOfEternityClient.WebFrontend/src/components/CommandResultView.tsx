import { useState, type FormEvent } from 'react';
import { browserApi } from '../api/client';
import type { ExplorerCommandResult, JsonValue, UiAction } from '../api/contracts';
import { useShell } from '../context/ShellContext';
import { BlockList } from './BlockRenderer';
import { PromptForm, type PromptAnswers } from './PromptForm';

export function CommandResultView() {
  const { commandResult, clearCommandResult, executeCommand, loadBrowserState } = useShell();
  const [promptAnswers, setPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [localResult, setLocalResult] = useState<ExplorerCommandResult | null>(null);

  const result = localResult ?? commandResult;
  if (!result) return null;

  function handleActionClick(action: UiAction) {
    if (action.requiresConfirmation && !confirm(`Выполнить: ${action.label}?`)) return;
    void executeCommand(action.command);
  }

  function handlePromptAnswerChange(promptId: string, value: JsonValue | undefined) {
    setPromptAnswers((prev) => ({ ...prev, [promptId]: value }));
  }

  async function handlePromptSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!result?.interactiveSession) return;
    setIsSubmitting(true);
    try {
      const response = await browserApi.submitPromptSession({
        sessionId: result.interactiveSession.sessionId,
        answers: promptAnswers
      });
      if (response.ok) {
        setLocalResult(response.data);
        setPromptAnswers({});
      }
      void loadBrowserState();
    } catch { /* handled by notice */ }
    setIsSubmitting(false);
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

      {result.interactiveSession && result.prompts.length > 0 && (
        <div className="command-result-view__prompts">
          <PromptForm
            prompts={result.prompts}
            promptAnswers={promptAnswers}
            onPromptAnswerChange={handlePromptAnswerChange}
            onSubmit={handlePromptSubmit}
            isSubmitting={isSubmitting}
          />
        </div>
      )}
    </div>
  );
}
