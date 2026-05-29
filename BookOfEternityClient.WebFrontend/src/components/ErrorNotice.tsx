import type { BrowserApiFailure, BrowserApiResult } from '../api/contracts';
import { toPlayerFacingText } from '../utils/playerCopy';

export interface EmptyStateCopy {
  title: string;
  message: string;
  action: string;
}

export function EmptyState({ title, message, action }: EmptyStateCopy) {
  return (
    <section className="empty-state" aria-label={title}>
      <p className="panel-eyebrow">ожидание главы</p>
      <h2>{title}</h2>
      <p>{message}</p>
      <p className="muted">{action}</p>
    </section>
  );
}

export function EmptyOrFailure<T>({
  result,
  empty,
  errorTitle,
  advancedEnabled
}: {
  result: BrowserApiResult<T>;
  empty: EmptyStateCopy;
  errorTitle: string;
  advancedEnabled: boolean;
}) {
  if (result.ok) {
    return null;
  }

  if (result.kind === 'no-active-session') {
    return <EmptyState {...empty} />;
  }

  return <ApiFailure title={errorTitle} result={result} advancedEnabled={advancedEnabled} />;
}

export function ApiFailure<T>({ title, result, advancedEnabled }: { title: string; result: BrowserApiResult<T>; advancedEnabled: boolean }) {
  if (result.ok) {
    return null;
  }

  return <ErrorNotice title={title} failure={result} advancedEnabled={advancedEnabled} />;
}

export function ErrorNotice({
  title,
  failure,
  advancedEnabled
}: {
  title: string;
  failure: BrowserApiFailure | { playerMessage: string; technicalDetails?: string };
  advancedEnabled: boolean;
}) {
  return (
    <section className="error-notice" role="alert">
      <h2>{title}</h2>
      <p>{toPlayerFacingText(failure.playerMessage, 'Игровое состояние сейчас недоступно.')}</p>
      {failure.technicalDetails && advancedEnabled && (
        <details open>
          <summary>Подробности</summary>
          <pre>{failure.technicalDetails}</pre>
        </details>
      )}
      {failure.technicalDetails && !advancedEnabled && (
        <p className="muted">Технические подробности доступны после явного включения расширенного режима.</p>
      )}
    </section>
  );
}
