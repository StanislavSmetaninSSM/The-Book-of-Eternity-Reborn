import type { ExplorerCommandResult, WoundViewMode } from '../api/contracts';

export function validatedWoundView(value: unknown): WoundViewMode | null {
  return value === 'active' || value === 'history' || value === 'treatment' ? value : null;
}

// Recognizes presentation only. Server parsing and every authority check remain authoritative.
function viewFromCommand(command: string): WoundViewMode | null {
  const [name, argument] = command.trim().toLowerCase().split(/\s+/);
  if (['/лечить', '/treat', '/исцелить', '/heal'].includes(name)) return 'treatment';
  if (!['/раны', '/wounds'].includes(name)) return null;
  return argument === 'история' || argument === 'history' ? 'history' : 'active';
}

export function woundPresentation(result: Pick<ExplorerCommandResult, 'command' | 'woundView'>): WoundViewMode | null {
  if (typeof result.command !== 'string') return null;
  // A fresh non-wound response must clear a carried hint. Blank sanitized responses are idempotent.
  return result.command.trim() ? viewFromCommand(result.command) : validatedWoundView(result.woundView);
}
