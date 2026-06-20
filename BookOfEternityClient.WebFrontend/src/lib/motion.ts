import type { Variants } from 'framer-motion';

export const easeOutCinematic: [number, number, number, number] = [0.2, 0.8, 0.2, 1];
export const easePageTransition: [number, number, number, number] = [0.65, 0, 0.35, 1];

export const staggerContainer: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.08,
      delayChildren: 0.05,
    },
  },
};

export const fadeUp: Variants = {
  hidden: { opacity: 0, y: 12 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.5, ease: easeOutCinematic },
  },
};

export const scaleIn: Variants = {
  hidden: { opacity: 0, scale: 0.96 },
  visible: {
    opacity: 1,
    scale: 1,
    transition: { duration: 0.42, ease: easeOutCinematic },
  },
};

export const glowPulse: Variants = {
  idle: {
    boxShadow: '0 0 0px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 0%, transparent)',
  },
  active: {
    boxShadow: [
      '0 0 12px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 22%, transparent)',
      '0 0 26px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 38%, transparent)',
      '0 0 12px color-mix(in srgb, var(--realm-accent, var(--color-gold)) 22%, transparent)',
    ],
    transition: { duration: 2.2, repeat: Infinity, ease: 'easeInOut' },
  },
};

export const pageTransition: Variants = {
  initial: { opacity: 0, filter: 'blur(8px)' },
  enter: {
    opacity: 1,
    filter: 'blur(0px)',
    transition: { duration: 0.48, ease: easePageTransition },
  },
  exit: {
    opacity: 0,
    filter: 'blur(12px)',
    transition: { duration: 0.32, ease: easePageTransition },
  },
};

export const runeFlicker: Variants = {
  idle: { opacity: 0.4 },
  active: {
    opacity: [0.4, 0.95, 0.5, 0.9, 0.4],
    transition: { duration: 3.2, repeat: Infinity, ease: 'easeInOut' },
  },
};
