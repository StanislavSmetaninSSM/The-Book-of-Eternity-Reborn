interface OrnamentBorderProps {
  orientation?: 'horizontal' | 'vertical';
  className?: string;
}

/**
 * SVG ornamental divider — acanthus-leaf flourish with a central gem.
 * Uses currentColor so it inherits the realm accent.
 */
export function OrnamentBorder({ orientation = 'horizontal', className = '' }: OrnamentBorderProps) {
  return (
    <div
      className={`ornament-border ornament-border--${orientation} ${className}`.trim()}
      role="separator"
      aria-orientation={orientation === 'horizontal' ? 'horizontal' : 'vertical'}
    >
      <svg className="ornament-border__svg" viewBox="0 0 240 24" fill="none" aria-hidden="true">
        <path
          d="M0 12 L80 12 M160 12 L240 12"
          stroke="currentColor"
          strokeWidth="1"
          strokeLinecap="round"
          opacity="0.5"
        />
        <path
          d="M88 12 C 96 4, 104 4, 112 12 C 120 20, 128 20, 136 12 C 128 4, 120 4, 112 12 C 104 20, 96 20, 88 12 Z"
          fill="currentColor"
          opacity="0.55"
        />
        <circle cx="120" cy="12" r="2.5" fill="currentColor" opacity="0.9" />
      </svg>
    </div>
  );
}
