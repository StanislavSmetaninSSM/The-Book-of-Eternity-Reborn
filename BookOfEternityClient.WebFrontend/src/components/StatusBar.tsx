export function StatusBar({ label, value }: { label: string; value: string }) {
  const numericValue = Number.parseFloat(value);
  const percent = Number.isFinite(numericValue) ? Math.max(0, Math.min(100, numericValue)) : 0;

  return (
    <div className="status-bar">
      <span>{label}</span>
      <div aria-hidden="true"><i style={{ width: `${percent}%` }} /></div>
      <strong>{value || '0%'}</strong>
    </div>
  );
}
