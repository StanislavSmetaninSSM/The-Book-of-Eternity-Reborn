import { motion, useScroll, useTransform } from 'framer-motion';
import { useRef } from 'react';

interface CinematicSceneHeroProps {
  imageUrl?: string | null;
  fallbackImageUrl?: string | null;
  eyebrow?: string;
  title: string;
  subtitle?: string;
  loading?: boolean;
}

/**
 * Full-bleed cinematic scene hero with:
 *  - parallax image (drifts + scales on scroll)
 *  - depth-of-field blur layer
 *  - candle-flicker light beam
 *  - god-rays ambient
 *  - vignette + gradient overlays
 *  - staggered content entrance via framer-motion
 *
 * Replaces the original 240px .scene-hero with a 380px+ cinematic banner.
 * Same props as the existing SceneHero component — drop-in replacement.
 */
export function CinematicSceneHero({
  imageUrl,
  fallbackImageUrl,
  eyebrow,
  title,
  subtitle,
  loading,
}: CinematicSceneHeroProps) {
  const resolvedImageUrl = imageUrl ?? fallbackImageUrl ?? null;
  const isFallbackImage = !imageUrl && Boolean(fallbackImageUrl);
  const ref = useRef<HTMLElement>(null);
  const { scrollYProgress } = useScroll({
    target: ref,
    offset: ['start start', 'end start'],
  });
  const imageY = useTransform(scrollYProgress, [0, 1], ['0%', '20%']);
  const imageScale = useTransform(scrollYProgress, [0, 1], [1.05, 1.15]);
  const contentY = useTransform(scrollYProgress, [0, 1], ['0%', '-30%']);
  const overlayOpacity = useTransform(scrollYProgress, [0, 1], [1, 0.6]);

  return (
    <header className="cinematic-hero" ref={ref}>
      {resolvedImageUrl && (
        <motion.div
          className={`cinematic-hero__image${isFallbackImage ? ' cinematic-hero__image--fallback' : ''}`}
          style={{ y: imageY, scale: imageScale }}
          aria-hidden="true"
        >
          <img
            src={resolvedImageUrl}
            alt=""
            loading="lazy"
            onError={(event) => { event.currentTarget.hidden = true; }}
          />
          <div className="cinematic-hero__image-blur" aria-hidden="true" />
        </motion.div>
      )}
      <div className="cinematic-hero__beam" aria-hidden="true" />
      <div className="god-rays" aria-hidden="true" />
      <motion.div
        className="cinematic-hero__gradient"
        style={{ opacity: overlayOpacity }}
        aria-hidden="true"
      />
      <motion.div
        className="cinematic-hero__content"
        style={{ y: contentY }}
      >
        {eyebrow && (
          <motion.span
            className="cinematic-hero__eyebrow"
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.1 }}
          >
            {eyebrow}
          </motion.span>
        )}
        <motion.h1
          className="cinematic-hero__title"
          initial={{ opacity: 0, y: 14 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.7, delay: 0.2, ease: [0.2, 0.8, 0.2, 1] }}
        >
          {title}
        </motion.h1>
        {subtitle && (
          <motion.p
            className="cinematic-hero__subtitle"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.35 }}
          >
            {subtitle}
          </motion.p>
        )}
        {loading && <p className="cinematic-hero__loading">Образ сцены проявляется…</p>}
      </motion.div>
      <div className="cinematic-hero__vignette" aria-hidden="true" />
    </header>
  );
}
