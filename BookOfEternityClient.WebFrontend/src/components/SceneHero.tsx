interface SceneHeroProps {
  imageUrl?: string | null;
  eyebrow?: string;
  title: string;
  subtitle?: string;
  loading?: boolean;
}

export function SceneHero({ imageUrl, eyebrow, title, subtitle, loading }: SceneHeroProps) {
  return (
    <header className="scene-hero">
      {imageUrl && (
        <div className="scene-hero__image" aria-hidden="true">
          <img src={imageUrl} alt="" loading="lazy" />
        </div>
      )}
      <div className="scene-hero__beam" aria-hidden="true" />
      <div className="scene-hero__gradient" aria-hidden="true" />
      <div className="scene-hero__content">
        {eyebrow && <span className="scene-hero__eyebrow">{eyebrow}</span>}
        <h1 className="scene-hero__title">{title}</h1>
        {subtitle && <p className="scene-hero__subtitle">{subtitle}</p>}
        {loading && <p className="scene-hero__loading">🎨 Генерация образа…</p>}
      </div>
    </header>
  );
}
