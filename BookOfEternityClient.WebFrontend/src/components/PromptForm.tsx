import type { FormEvent } from 'react';
import type { JsonValue, UiPrompt } from '../api/contracts';
import { toPlayerFacingText } from '../utils/playerCopy';

export type PromptAnswers = Record<string, JsonValue | undefined>;

interface PromptFormProps {
  prompts: UiPrompt[];
  promptAnswers: PromptAnswers;
  onPromptAnswerChange: (promptId: string, value: JsonValue | undefined) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
  onCancel?: () => void;
  isSubmitting: boolean;
  isCancelling?: boolean;
}

export function PromptForm({
  prompts,
  promptAnswers,
  onPromptAnswerChange,
  onSubmit,
  onCancel,
  isSubmitting,
  isCancelling = false
}: PromptFormProps) {
  const isBusy = isSubmitting || isCancelling;

  return (
    <form className="prompt-form" onSubmit={onSubmit}>
      <h5>Заполните игровую форму</h5>
      {prompts.map((prompt) => renderPromptControl(prompt, promptAnswers[prompt.id], onPromptAnswerChange))}
      <div className="prompt-form__actions">
        <button type="submit" disabled={isBusy}>
          {isSubmitting ? 'Отправляем…' : 'Отправить форму'}
        </button>
        {onCancel && (
          <button type="button" className="prompt-form__cancel" disabled={isBusy} onClick={onCancel}>
            {isCancelling ? 'Отменяем…' : 'Отменить форму'}
          </button>
        )}
      </div>
    </form>
  );
}

export function renderPromptControl(
  prompt: UiPrompt,
  value: JsonValue | undefined,
  onPromptAnswerChange: (promptId: string, value: JsonValue | undefined) => void
) {
  const controlId = `prompt-${prompt.id}`;

  switch (prompt.kind) {
    case 'confirmation':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control checkbox-control">
          <input
            id={controlId}
            type="checkbox"
            checked={typeof value === 'boolean' ? value : prompt.defaultValue}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.checked)}
          />
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
        </label>
      );
    case 'selection': {
      const selectedValue = typeof value === 'string' ? value : '';
      const selectedKnownOption = prompt.options.some((option) => option.value === selectedValue);
      const customValue = selectedValue && !selectedKnownOption ? selectedValue : '';

      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <select
            id={controlId}
            value={selectedKnownOption ? selectedValue : ''}
            required={prompt.required && !customValue}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          >
            <option value="">Выберите вариант…</option>
            {prompt.options.map((option) => (
              <option key={option.value} value={option.value} disabled={option.disabled}>
                {toPlayerFacingText(option.label, 'вариант')}
              </option>
            ))}
          </select>
          {prompt.allowCustom && (
            <input
              type="text"
              value={customValue}
              placeholder="Или впишите свой вариант…"
              onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
            />
          )}
        </label>
      );
    }
    case 'longTextInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <textarea
            id={controlId}
            rows={prompt.minLines ?? 3}
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={toPlayerFacingText(prompt.placeholder, '')}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          />
        </label>
      );
    case 'textInput':
      return (
        <label key={prompt.id} htmlFor={controlId} className="prompt-control">
          <span>{toPlayerFacingText(prompt.prompt, 'Подтвердите действие')}</span>
          <input
            id={controlId}
            type="text"
            value={typeof value === 'string' ? value : prompt.defaultValue}
            placeholder={toPlayerFacingText(prompt.placeholder, '')}
            required={prompt.required}
            onChange={(event) => onPromptAnswerChange(prompt.id, event.currentTarget.value)}
          />
        </label>
      );
  }
}

export function buildDefaultPromptAnswers(prompts: UiPrompt[]): PromptAnswers {
  return Object.fromEntries(prompts.map((prompt) => [prompt.id, defaultPromptValue(prompt)]));
}

export function defaultPromptValue(prompt: UiPrompt): JsonValue | undefined {
  switch (prompt.kind) {
    case 'confirmation':
      return prompt.defaultValue;
    case 'selection':
      return undefined;
    case 'longTextInput':
    case 'textInput':
      return prompt.defaultValue;
  }
}
