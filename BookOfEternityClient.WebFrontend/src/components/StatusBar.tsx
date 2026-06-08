type StatusSeverity = 'good' | 'warning' | 'danger';

export function StatusBar({ label, value }: { label: string; value?: string | null }) {
  const percent = parseStatusPercent(value);
  const severity = resolveStatusSeverity(percent);
  const displayValue = `${formatStatusPercent(percent)}%`;

  return (
    <div className={`status-bar status-bar--${severity}`}>
      <span>{label}</span>
      <div className="status-bar__track" aria-hidden="true"><i className="status-bar__fill" style={{ width: `${percent}%` }} /></div>
      <strong>{displayValue}</strong>
    </div>
  );
}

function parseStatusPercent(value: string | null | undefined): number {
  const match = value?.trim().replace(',', '.').match(/^(-?\d+(?:\.\d+)?)\s*%?$/);
  if (!match) {
    return 0;
  }

  const numericValue = Number.parseFloat(match[1]);
  return Number.isFinite(numericValue) ? Math.max(0, Math.min(100, numericValue)) : 0;
}

function resolveStatusSeverity(percent: number): StatusSeverity {
  if (percent > 66) {
    return 'good';
  }

  if (percent > 33) {
    return 'warning';
  }

  return 'danger';
}

function formatStatusPercent(percent: number): string {
  return Number.isInteger(percent) ? String(percent) : percent.toFixed(1).replace(/\.0$/, '');
}
