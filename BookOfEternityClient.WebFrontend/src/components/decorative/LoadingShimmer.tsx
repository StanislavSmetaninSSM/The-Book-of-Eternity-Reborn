interface LoadingShimmerProps {
  lines?: number;
  className?: string;
  hasImage?: boolean;
}

/**
 * Skeleton loader with shimmer sweep + realm-aware tint.
 * Replaces the bare LoadingCard body for a more cinematic loading state.
 */
export function LoadingShimmer({ lines = 3, className = '', hasImage = false }: LoadingShimmerProps) {
  return (
    <div
      className={`loading-shimmer ${className}`.trim()}
      role="status"
      aria-label="Загрузка…"
      aria-live="polite"
    >
      {hasImage && <div className="loading-shimmer__image" />}
      <div className="loading-shimmer__lines">
        {Array.from({ length: lines }).map((_, i) => (
          <div
            key={i}
            className="loading-shimmer__line"
            style={{ width: `${80 - i * 12}%` }}
          />
        ))}
      </div>
      <span className="sr-only">Загружаем книгу…</span>
    </div>
  );
}
