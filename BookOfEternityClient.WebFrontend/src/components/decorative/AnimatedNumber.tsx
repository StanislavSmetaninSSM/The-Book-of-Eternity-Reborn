import { useEffect, useRef, useState } from 'react';
import { animate } from 'framer-motion';

interface AnimatedNumberProps {
  value: number;
  duration?: number;
  className?: string;
  format?: (n: number) => string;
}

/**
 * Number that animates from its previous value to the new value.
 * Uses framer-motion's `animate` (no DOM element motion needed — just numeric tween).
 * Falls back to instant value if reduced-motion is set on the shell.
 */
export function AnimatedNumber({ value, duration = 0.8, className = '', format }: AnimatedNumberProps) {
  const [display, setDisplay] = useState(value);
  const previousValue = useRef(value);
  const prefersReducedMotion = usePrefersReducedMotion();

  useEffect(() => {
    const from = previousValue.current;
    const to = value;
    if (from === to || prefersReducedMotion) {
      setDisplay(to);
      previousValue.current = to;
      return;
    }
    const controls = animate(from, to, {
      duration,
      ease: [0.2, 0.8, 0.2, 1],
      onUpdate: (v) => setDisplay(v),
    });
    previousValue.current = to;
    return () => controls.stop();
  }, [value, duration, prefersReducedMotion]);

  const text = format ? format(display) : Math.round(display).toString();
  return <span className={`animated-number ${className}`.trim()}>{text}</span>;
}

function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    const mql = window.matchMedia('(prefers-reduced-motion: reduce)');
    setReduced(mql.matches);
    const handler = (e: MediaQueryListEvent) => setReduced(e.matches);
    mql.addEventListener('change', handler);
    return () => mql.removeEventListener('change', handler);
  }, []);
  return reduced;
}
