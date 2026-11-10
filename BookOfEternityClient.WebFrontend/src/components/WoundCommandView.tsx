import { useEffect, useRef, type ReactNode } from 'react';
import type { ExplorerCommandResult, WoundViewMode } from '../api/contracts';
import '../styles/wounds.css';

export function WoundCommandView({ result, mode, busy, onBack, children }: {
  result: ExplorerCommandResult;
  mode: WoundViewMode;
  busy: boolean;
  onBack: () => void;
  children: ReactNode;
}) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => { heading.current?.focus(); }, [result]);
  const live = result.interactiveSession !== null;
  const title = mode === 'history' ? 'История заживших ран' : mode === 'active' ? 'Активные раны' : 'Лечение ран';
  const step = result.prompts.some(p => p.id === 'wound_target') ? 0 : live ? 1 : 2;
  const status = result.state === 'RequiresInput' ? 'Ожидается ваш выбор'
    : result.state === 'Pending' ? 'Ожидается ответ'
      : result.state === 'Failed' ? 'Действие не выполнено'
        : result.state === 'Blocked' ? 'Действие недоступно' : 'Ответ клиента';

  return (
    <section className="command-result-view wound-command-view" aria-labelledby="wound-view-heading">
      <header className="wound-command-view__header">
        <button type="button" className="btn-back" disabled={busy} onClick={onBack}>
          {live ? 'Отменить выбор' : '← Назад к сцене'}
        </button>
        <p className="wound-command-view__eyebrow">Раны и заживление</p>
        <h1 id="wound-view-heading" ref={heading} tabIndex={-1}>{title}</h1>
        <p className="wound-command-view__intro">
          {mode === 'history' ? 'Здесь только известные записи о заживших ранах.'
            : mode === 'active' ? 'Текущие раны и известные сведения о восстановлении. История открывается отдельно.'
              : 'Выбор показывает доступные сведения. Лечение начинается только после отдельного подтверждения; условия и результат указаны ниже.'}
        </p>
        <p className="status-pill" role="status">{status}</p>
      </header>
      {mode === 'treatment' && (
        <ol className="wound-command-view__steps" aria-label="Этапы лечения">
          {['Цель', 'Рана и подтверждение', 'Ответ клиента'].map((label, index) => (
            <li key={label} aria-current={index === step ? 'step' : undefined}>
              <span aria-hidden="true">{index + 1}</span>{label}
            </li>
          ))}
        </ol>
      )}
      {children}
    </section>
  );
}
