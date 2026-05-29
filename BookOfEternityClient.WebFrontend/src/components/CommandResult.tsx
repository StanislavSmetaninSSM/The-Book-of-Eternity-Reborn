import type { FormEvent, ReactNode } from 'react';
import type { BrowserApiResult, ExplorerCommandResult, JsonValue, UiBlock } from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import { commandStateLabel } from '../utils/formatters';
import { toPlayerFacingText } from '../utils/playerCopy';
import { PromptForm, type PromptAnswers } from './PromptForm';

interface ActionCommandResultProps {
  result: BrowserApiResult<ExplorerCommandResult>;
  promptAnswers: PromptAnswers;
  onPromptAnswerChange: (promptId: string, value: JsonValue | undefined) => void;
  onPromptSubmit: (event: FormEvent<HTMLFormElement>) => void;
  isSubmitting: boolean;
}

export function ActionCommandResult({
  result,
  promptAnswers,
  onPromptAnswerChange,
  onPromptSubmit,
  isSubmitting
}: ActionCommandResultProps) {
  if (!isSuccess(result)) {
    return <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Игровое действие сейчас недоступно.')}</p>;
  }

  const command = result.data;
  return (
    <section className="command-result" aria-label="Результат игрового действия">
      <p className="status-pill">{commandStateLabel(command.state)}</p>
      {command.notifications.map((notification, index) => (
        <p key={`${notification.title}-${index}`} className="composer-notice">
          <strong>{toPlayerFacingText(notification.title, 'Уведомление')}</strong> — {toPlayerFacingText(notification.message, 'Игровое действие изменило состояние.')}
        </p>
      ))}
      {command.blocks.map((block, index) => (
        <div key={`${block.kind}-${index}`}>{renderCommandBlock(block)}</div>
      ))}
      {command.interactiveSession && command.prompts.length > 0 && (
        <PromptForm
          prompts={command.prompts}
          promptAnswers={promptAnswers}
          onPromptAnswerChange={onPromptAnswerChange}
          onSubmit={onPromptSubmit}
          isSubmitting={isSubmitting}
        />
      )}
    </section>
  );
}

export function renderCommandBlock(block: UiBlock): ReactNode {
  switch (block.kind) {
    case 'text':
      return <p>{toPlayerFacingText(block.text, 'Текст игрового действия недоступен.')}</p>;
    case 'panel':
      return (
        <div className="summary-card">
          <h5>{toPlayerFacingText(block.title, 'Игровая панель')}</h5>
          {block.blocks.map((child, index) => <div key={`${child.kind}-${index}`}>{renderCommandBlock(child)}</div>)}
        </div>
      );
    case 'table':
      return <p>{toPlayerFacingText(block.title, 'Таблица')}: {block.rows.length} строк.</p>;
    case 'list':
      return <ul>{block.items.map((item) => <li key={item}>{toPlayerFacingText(item, 'пункт списка')}</li>)}</ul>;
    case 'keyValueGrid':
      return (
        <dl className="kv-list">
          {block.items.map((item) => (
            <div key={item.key}>
              <dt>{toPlayerFacingText(item.key, 'параметр')}</dt>
              <dd>{toPlayerFacingText(item.value, 'значение')}</dd>
            </div>
          ))}
        </dl>
      );
    case 'message':
      return (
        <p className="composer-notice">
          <strong>{toPlayerFacingText(block.title, 'Сообщение')}</strong> — {toPlayerFacingText(block.message, 'Игровое действие изменило состояние.')}
        </p>
      );
    case 'image':
      return <p>{toPlayerFacingText(block.title, 'Изображение')}: изображение готово к просмотру.</p>;
    case 'map':
      return <p>{toPlayerFacingText(block.title, 'Карта')}: карта содержит {block.map.nodes.length} точек.</p>;
    case 'rawJson':
      return <p className="muted">{toPlayerFacingText(block.title, 'Подробные данные')}: подробные данные доступны в расширенном режиме.</p>;
  }
}
