import type { FormEvent, ReactNode } from 'react';
import type { BrowserApiResult, ExplorerCommandResult, JsonValue, UiBlock } from '../api/contracts';
import { isSuccess } from '../context/ShellContext';
import { commandStateLabel } from '../utils/formatters';
import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';
import { MapBlock } from './MapBlock';
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
    const { safe } = sanitizePlayerMessage(result.playerMessage, 'Игровое действие сейчас недоступно.');
    return <p className="warning-text">{safe}</p>;
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
    case 'text': {
      const { safe, hasTechnical } = sanitizePlayerMessage(block.text, 'Текст игрового действия недоступен.');
      return hasTechnical
        ? <p className="muted">{safe}</p>
        : <p>{safe}</p>;
    }
    case 'panel':
      return (
        <div className="summary-card">
          <h5>{toPlayerFacingText(block.title, 'Игровая панель')}</h5>
          {block.blocks.map((child, index) => <div key={`${child.kind}-${index}`}>{renderCommandBlock(child)}</div>)}
        </div>
      );
    case 'table':
      return (
        <div className="command-table">
          <p className="muted">{toPlayerFacingText(block.title, 'Таблица')}</p>
          <table>
            <thead><tr>{block.columns.map((col) => <th key={col}>{col}</th>)}</tr></thead>
            <tbody>
              {block.rows.map((row, i) => (
                <tr key={i}>{row.cells.map((cell, j) => <td key={j}>{cell}</td>)}</tr>
              ))}
            </tbody>
          </table>
        </div>
      );
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
      return (
        <figure className="command-result-image">
          {block.url ? (
            <a href={block.url} target="_blank" rel="noreferrer">
              <img src={block.url} alt={block.altText || toPlayerFacingText(block.title, 'Изображение сцены')} loading="lazy" />
            </a>
          ) : (
            <p className="muted">{toPlayerFacingText(block.title, 'Изображение')}: файл недоступен для отображения.</p>
          )}
          {block.title && <figcaption>{toPlayerFacingText(block.title, 'Изображение')}</figcaption>}
        </figure>
      );
    case 'map':
      return <MapBlock block={block} variant="compact" />;
    case 'rawJson':
      return <p className="muted">{toPlayerFacingText(block.title, 'Подробные данные')}: подробные данные доступны в расширенном режиме.</p>;
  }
}
