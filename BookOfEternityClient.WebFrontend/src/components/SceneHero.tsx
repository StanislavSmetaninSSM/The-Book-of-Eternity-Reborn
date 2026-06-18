import { CinematicSceneHero } from './decorative';

interface SceneHeroProps {
  imageUrl?: string | null;
  fallbackImageUrl?: string | null;
  eyebrow?: string;
  title: string;
  subtitle?: string;
  loading?: boolean;
}

/**
 * SceneHero delegates to the new CinematicSceneHero (BG3-grade parallax hero).
 * Same props — drop-in replacement.
 */
export function SceneHero(props: SceneHeroProps) {
  return <CinematicSceneHero {...props} />;
}
