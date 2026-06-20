import type { ReactNode } from 'react';

interface RuneFrameProps {
  children: ReactNode;
  variant?: 'default' | 'subtle' | 'intense';
  className?: string;
}

/**
 * Decorative bordered frame with animated rune glyphs in the four corners.
 * Pure CSS animation — no framer-motion dependency for this component.
 *
 * Variants:
 *  - default: standard padding + soft glow
 *  - subtle:  tighter padding, no outer glow (use inside other containers)
 *  - intense: extra padding, double border, strong realm glow
 */
export function RuneFrame({ children, variant = 'default', className = '' }: RuneFrameProps) {
  const variantClass = `rune-frame--${variant}`;
  return (
    <div className={`rune-frame ${variantClass} ${className}`.trim()}>
      <span className="rune-frame__corner rune-frame__corner--tl" aria-hidden="true">ᚱ</span>
      <span className="rune-frame__corner rune-frame__corner--tr" aria-hidden="true">ᛟ</span>
      <span className="rune-frame__corner rune-frame__corner--bl" aria-hidden="true">ᛞ</span>
      <span className="rune-frame__corner rune-frame__corner--br" aria-hidden="true">ᚹ</span>
      <div className="rune-frame__inner">
        {children}
      </div>
    </div>
  );
}
