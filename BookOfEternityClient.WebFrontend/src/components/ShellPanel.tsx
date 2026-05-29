import type { ReactNode } from 'react';

export function ShellPanel({
  title,
  eyebrow,
  children,
  nested = false,
  variant
}: {
  title: string;
  eyebrow: string;
  children: ReactNode;
  nested?: boolean;
  variant?: string;
}) {
  const className = ['shell-panel', nested ? 'is-nested' : '', variant ? `panel-${variant}` : '']
    .filter(Boolean)
    .join(' ');

  return (
    <section className={className} data-panel={variant ?? title}>
      <p className="panel-eyebrow">{eyebrow}</p>
      <h2>{title}</h2>
      {children}
    </section>
  );
}
