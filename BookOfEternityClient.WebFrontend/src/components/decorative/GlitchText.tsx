interface GlitchTextProps {
  children: string;
  variant?: 'subtle' | 'intense';
  className?: string;
}

/**
 * Text with chromatic-aberration glitch effect for QTE / error states.
 * Uses pure CSS animation (no framer-motion) so it works even under reduced-motion
 * (where the glitch layers are simply hidden via .is-reduced-motion).
 *
 * The visible text is duplicated as aria-hidden red/cyan layers for the glitch;
 * the accessible text is the last span (no aria-hidden).
 */
export function GlitchText({ children, variant = 'subtle', className = '' }: GlitchTextProps) {
  return (
    <span
      className={`glitch-text glitch-text--${variant} ${className}`.trim()}
      data-text={children}
    >
      <span className="glitch-text__base" aria-hidden="true">{children}</span>
      <span className="glitch-text__red" aria-hidden="true">{children}</span>
      <span className="glitch-text__cyan" aria-hidden="true">{children}</span>
      <span className="glitch-text__visible">{children}</span>
    </span>
  );
}
