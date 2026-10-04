import type { LoadPersistenceNotice } from '../utils/loadPersistenceNotice';

/** Retains confirmed load identity while replacing every interactive shell surface. */
export function LoadContinuationBlockedNotice({ notice }: { notice: LoadPersistenceNotice }) {
  return <main className="browser-shell"><section className="content-area" role="alert" aria-live="assertive">
    <h2>Продолжение остановлено</h2><p>{notice.message}</p>
    {notice.disposition === 'Committed' && notice.loadedSaveId && <p>Загруженное сохранение: <strong>{notice.loadedSaveId}</strong></p>}
    <p>Проверьте локальное хранилище и завершите восстановление книги. Затем перезагрузите это окно. Не повторяйте загрузку до проверки её результата.</p>
  </section></main>;
}

/** Keeps a nonblocking load follow-up visible after its original component navigates away. */
export function LoadFollowUpNotice({ notice }: { notice: LoadPersistenceNotice }) {
  return <section className="composer-notice" role="status" aria-live="polite">
    <p>Служебное завершение попытки загрузки требует проверки.</p>
    {notice.disposition === 'Committed' && notice.loadedSaveId
      ? <p>Подтверждённая запись той загрузки: <strong>{notice.loadedSaveId}</strong></p>
      : <p>{notice.message}</p>}
  </section>;
}
