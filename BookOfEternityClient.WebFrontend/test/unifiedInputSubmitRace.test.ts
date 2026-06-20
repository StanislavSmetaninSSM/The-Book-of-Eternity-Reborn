export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { join } = await import(pathSpecifier);
const frontendDir = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assertIncludes(source: string, expected: string, description: string) {
  if (!source.includes(expected)) {
    throw new Error(`${description} Missing snippet: ${expected}`);
  }
}

const unifiedInput = readSource('components', 'UnifiedInput.tsx');
const shellContext = readSource('context', 'ShellContext.tsx');

assertIncludes(
  shellContext,
  'submitComposerText: (text: string) => void;',
  'ShellContext should expose a text-based composer submission path shared by form and keyboard submits.'
);
assertIncludes(
  shellContext,
  'submitComposerText(composerText);',
  'The form submit wrapper should delegate to the shared text-based composer path.'
);
assertIncludes(
  unifiedInput,
  'submitComposerText',
  'UnifiedInput should use the shared text-based composer path for keyboard submits.'
);
assertIncludes(
  unifiedInput,
  'e.nativeEvent.isComposing',
  'Enter handling should ignore IME composition keystrokes.'
);
assertIncludes(
  unifiedInput,
  'if (!isPostMode && e.key === \'Enter\' && !e.shiftKey)',
  'Enter without Shift should remain the command-mode submit shortcut while post mode keeps newlines available.'
);
assertIncludes(
  unifiedInput,
  'if (isPostMode && e.key === \'Enter\' && (e.ctrlKey || e.metaKey))',
  'Post mode should still provide an explicit keyboard submit path without stealing ordinary newlines.'
);
assertIncludes(
  unifiedInput,
  'submitComposerText(e.currentTarget.value);',
  'Enter submit should pass the textarea current value instead of relying on possibly stale context state.'
);

if (unifiedInput.includes('requestSubmit(')) {
  throw new Error('Enter submit should not redispatch a form submit from keydown; it should call the shared text-based path directly.');
}
