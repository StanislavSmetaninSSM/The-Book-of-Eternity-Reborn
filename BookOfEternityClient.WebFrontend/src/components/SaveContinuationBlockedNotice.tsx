import type { SavePersistenceNotice } from '../utils/savePersistenceNotice';

/** Replaces interactive shell surfaces while an unresolved save follow-up forbids continuation. */
export function SaveContinuationBlockedNotice({ notice }: { notice: SavePersistenceNotice }) {
  return <main className="browser-shell">
    <section className="content-area" role="alert" aria-live="assertive">
      <h2>Продолжение остановлено</h2>
      <p>{notice.message}</p>
      {notice.committed && notice.createdSaveId && <p>Созданное сохранение: <strong>{notice.createdSaveId}</strong></p>}
      <p>Проверьте локальное хранилище и завершите его восстановление. Затем перезагрузите это окно, чтобы заново проверить состояние книги.</p>
    </section>
  </main>;
}
