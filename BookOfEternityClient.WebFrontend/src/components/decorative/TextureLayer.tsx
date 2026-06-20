interface TextureLayerProps {
  variant?: 'parchment' | 'stone' | 'metal' | 'leather';
  className?: string;
}

/**
 * Absolutely-positioned texture overlay (inline-SVG noise) for cards
 * and panels that want a material finish. Pointer-events:none, aria-hidden.
 */
export function TextureLayer({ variant = 'parchment', className = '' }: TextureLayerProps) {
  return (
    <div
      className={`texture-layer texture-layer--${variant} ${className}`.trim()}
      aria-hidden="true"
    />
  );
}
