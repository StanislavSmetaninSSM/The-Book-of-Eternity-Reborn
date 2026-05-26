import { useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';

export interface DetailSurfaceSection {
  title: string;
  eyebrow?: string;
  icon?: string;
  content: ReactNode;
}

export interface DetailSurfaceCardProps {
  detailSurfaceId: string;
  eyebrow: string;
  title: string;
  icon?: string;
  summary: ReactNode;
  status?: string;
  detailsTitle: string;
  detailsIntro?: ReactNode;
  sections: DetailSurfaceSection[];
  emptyMessage?: string;
  errorMessage?: string;
  loading?: boolean;
}

export function DetailSurfaceCard({
  detailSurfaceId,
  eyebrow,
  title,
  icon,
  summary,
  status,
  detailsTitle,
  detailsIntro,
  sections,
  emptyMessage = 'Подробности появятся, когда книга отдаст данные этого раздела.',
  errorMessage,
  loading = false
}: DetailSurfaceCardProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const closeButtonRef = useRef<HTMLButtonElement | null>(null);
  const modalRef = useRef<HTMLElement | null>(null);
  const titleId = `${detailSurfaceId}-title`;

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    closeButtonRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        closeDetailSurface();
        return;
      }

      if (event.key === 'Tab') {
        trapFocusInsideDetailSurface(event);
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen]);

  function openDetailSurface() {
    setIsOpen(true);
  }

  function closeDetailSurface() {
    setIsOpen(false);
    setIsFullscreen(false);
    window.setTimeout(() => triggerRef.current?.focus(), 0);
  }

  function trapFocusInsideDetailSurface(event: KeyboardEvent) {
    const modal = modalRef.current;
    if (!modal) {
      return;
    }

    const focusableControls = getFocusableDetailControls(modal);
    if (focusableControls.length === 0) {
      event.preventDefault();
      modal.focus();
      return;
    }

    const firstControl = focusableControls[0];
    const lastControl = focusableControls[focusableControls.length - 1];
    const activeElement = document.activeElement;

    if (event.shiftKey && (activeElement === firstControl || !modal.contains(activeElement))) {
      event.preventDefault();
      lastControl.focus();
      return;
    }

    if (!event.shiftKey && activeElement === lastControl) {
      event.preventDefault();
      firstControl.focus();
    }
  }

  function getFocusableDetailControls(container: HTMLElement) {
    return Array.from(
      container.querySelectorAll<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
      )
    ).filter((element) => !element.hasAttribute('disabled') && element.getAttribute('aria-hidden') !== 'true');
  }

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        className="detail-surface-card"
        aria-haspopup="dialog"
        aria-expanded={isOpen}
        aria-controls={isOpen ? `${detailSurfaceId}-dialog` : undefined}
        onClick={openDetailSurface}
      >
        <span className="detail-surface-card-eyebrow">{eyebrow}</span>
        <span className="detail-surface-card-title">
          {icon && <span aria-hidden="true">{icon}</span>}
          <strong>{title}</strong>
        </span>
        <span className="detail-surface-card-summary">{summary}</span>
        {status && <span className="status-pill">{status}</span>}
        <span className="detail-surface-card-action">Открыть детали</span>
      </button>

      {isOpen && (
        <div className="detail-surface-overlay" onMouseDown={closeDetailSurface}>
          <section
            ref={modalRef}
            id={`${detailSurfaceId}-dialog`}
            role="dialog"
            aria-modal="true"
            aria-labelledby={titleId}
            className={`detail-surface-modal${isFullscreen ? ' is-fullscreen' : ''}`}
            onMouseDown={(event) => event.stopPropagation()}
          >
            <header className="detail-surface-header">
              <div>
                <p className="panel-eyebrow">{eyebrow}</p>
                <h2 id={titleId}>{detailsTitle}</h2>
              </div>
              <div className="detail-surface-controls" role="group" aria-label="Управление подробностями">
                <button type="button" aria-label="Вернуться к карточке" onClick={closeDetailSurface}>Назад</button>
                {isFullscreen ? (
                  <button
                    type="button"
                    aria-label="Свернуть панель подробностей"
                    onClick={() => setIsFullscreen(false)}
                  >
                    Свернуть
                  </button>
                ) : (
                  <button
                    type="button"
                    aria-label="Развернуть панель подробностей"
                    onClick={() => setIsFullscreen(true)}
                  >
                    Развернуть
                  </button>
                )}
                <button ref={closeButtonRef} type="button" aria-label="Закрыть подробности" onClick={closeDetailSurface}>Закрыть</button>
              </div>
            </header>

            {detailsIntro && <div className="detail-surface-intro">{detailsIntro}</div>}
            {loading && <p className="detail-surface-loading">Книга собирает подробности этого раздела…</p>}
            {errorMessage && <p className="detail-surface-error">{errorMessage}</p>}
            {!loading && !errorMessage && sections.length === 0 && <p className="detail-surface-empty">{emptyMessage}</p>}
            {!loading && !errorMessage && sections.length > 0 && (
              <div className="detail-surface-sections">
                {sections.map((section) => (
                  <section className="detail-surface-section" key={`${detailSurfaceId}-${section.title}`}>
                    <div className="detail-surface-section-heading">
                      {section.icon && <span aria-hidden="true">{section.icon}</span>}
                      <div>
                        {section.eyebrow && <p className="panel-eyebrow">{section.eyebrow}</p>}
                        <h3>{section.title}</h3>
                      </div>
                    </div>
                    <div className="detail-surface-section-body">{section.content}</div>
                  </section>
                ))}
              </div>
            )}
          </section>
        </div>
      )}
    </>
  );
}
