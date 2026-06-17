interface SceneHeroProps {
  imageUrl?: string | null;
  fallbackImageUrl?: string | null;
  eyebrow?: string;
  title: string;
  subtitle?: string;
  loading?: boolean;
}

export function SceneHero({ imageUrl, fallbackImageUrl, eyebrow, title, subtitle, loading }: SceneHeroProps) {
  const resolvedImageUrl = imageUrl ?? fallbackImageUrl ?? null;
  const isFallbackImage = !imageUrl && Boolean(fallbackImageUrl);

  return (
    <header className="scene-hero">
      {resolvedImageUrl && (
        <div className={`scene-hero__image${isFallbackImage ? ' scene-hero__image--fallback' : ''}`} aria-hidden="true">
          <img
            src={resolvedImageUrl}
            alt=""
            loading="lazy"
            onError={(event) => { event.currentTarget.hidden = true; }}
          />
        </div>
      )}
      <div className="scene-hero__beam" aria-hidden="true" />
      <div className="scene-hero__gradient" aria-hidden="true" />
      <div className="scene-hero__content">
        {eyebrow && <span className="scene-hero__eyebrow">{eyebrow}</span>}
        <h1 className="scene-hero__title">{title}</h1>
        {subtitle && <p className="scene-hero__subtitle">{subtitle}</p>}
        {loading && <p className="scene-hero__loading">Образ сцены проявляется…</p>}
      </div>
    </header>
  );
}
