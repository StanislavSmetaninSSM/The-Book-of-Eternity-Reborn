import type { LoadPersistenceNotice } from '../utils/loadPersistenceNotice';

/** Retains confirmed load identity while replacing every interactive shell surface. */
export function LoadContinuationBlockedNotice({ notice }: { notice: LoadPersistenceNotice }) {
  return <main className="browser-shell"><section className="content-area" role="alert" aria-live="assertive">
    <h2>Продолжение остановлено</h2><p>{notice.message}</p>
    {notice.disposition === 'Committed' && notice.loadedSaveId && <p>Загруженное сохранение: <strong>{notice.loadedSaveId}</strong></p>}
    <p>Проверьте локальное хранилище и завершите восстановление книги. Затем перезагрузите это окно. Не повторяйте загрузку до проверки её результата.</p>
  </section></main>;
}
