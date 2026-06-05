export {};

const fsSpecifier = 'node:fs';
const pathSpecifier = 'node:path';
const { readFileSync } = await import(fsSpecifier);
const { basename, join } = await import(pathSpecifier);
const cwd = (globalThis as { process?: { cwd?: () => string } }).process?.cwd?.() ?? '.';
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, 'src', ...relativePath), 'utf-8');
}

function assert(condition: unknown, message: string) {
  if (!condition) {
    throw new Error(message);
  }
}

function assertIncludes(source: string, expected: string, description: string) {
  assert(source.includes(expected), `${description} Missing snippet: ${expected}`);
}

function extractBetween(source: string, start: string, end: string): string {
  const startIndex = source.indexOf(start);
  assert(startIndex >= 0, `Missing start snippet: ${start}`);
  const endIndex = source.indexOf(end, startIndex + start.length);
  assert(endIndex >= 0, `Missing end snippet after ${start}: ${end}`);
  return source.slice(startIndex, endIndex);
}

const commandResultView = readSource('components', 'CommandResultView.tsx');
const promptForm = readSource('components', 'PromptForm.tsx');

assertIncludes(
  commandResultView,
  'result.notifications.map((n, i) =>',
  'CommandResultView must render result.notifications.'
);
assertIncludes(
  commandResultView,
  'className={`block-message block-message--${n.severity.toLowerCase()}`}',
  'CommandResultView must keep severity-colored notification classes.'
);
assertIncludes(
  commandResultView,
  'result.actions.map((action) =>',
  'CommandResultView must render result.actions.'
);
assertIncludes(
  commandResultView,
  'void executeCommand(action.command);',
  'CommandResultView actions must execute through the existing shell command flow.'
);
assertIncludes(
  commandResultView,
  'type LocalPromptResult = { commandResult: ExplorerCommandResult | null; result: ExplorerCommandResult };',
  'CommandResultView must tie local prompt results to the parent command result.'
);
assertIncludes(
  commandResultView,
  'const currentLocalResult = localResult?.commandResult === commandResult ? localResult.result : null;',
  'CommandResultView must ignore stale local prompt results as soon as parent commandResult changes.'
);
assertIncludes(
  commandResultView,
  'const result = currentLocalResult ?? commandResult;',
  'CommandResultView must prefer local prompt results only for the current parent command result.'
);
assertIncludes(
  commandResultView,
  'sanitizeExplorerCommandResultForPlayer(response.data)',
  'CommandResultView must sanitize returned prompt-session results before default rendering.'
);

assert(
  !commandResultView.includes('{result.interactiveSession && result.prompts.length > 0 && ('),
  'CommandResultView must not hide result.prompts just because interactiveSession is null.'
);
assertIncludes(
  commandResultView,
  '{result.prompts.length > 0 && (',
  'CommandResultView must render a prompts section whenever result.prompts is non-empty.'
);
assertIncludes(
  commandResultView,
  'result.interactiveSession ? (',
  'CommandResultView must keep PromptForm for live interactive prompt sessions.'
);
assertIncludes(
  commandResultView,
  '<ReadOnlyPromptList prompts={result.prompts} />',
  'CommandResultView must render read-only prompt cards when there is no interactive session.'
);
assertIncludes(
  commandResultView,
  'function ReadOnlyPromptList({ prompts }: { prompts: UiPrompt[] })',
  'CommandResultView must define player-facing read-only prompt rendering.'
);

assertIncludes(
  promptForm,
  'onCancel?: () => void;',
  'PromptForm must accept an optional cancel callback.'
);
assertIncludes(
  promptForm,
  '{onCancel && (',
  'PromptForm must expose cancellation only when a session can be cancelled.'
);
assertIncludes(
  promptForm,
  'Отменить форму',
  'PromptForm cancellation copy must stay player-facing.'
);

const cancelHandler = extractBetween(
  commandResultView,
  'async function handlePromptCancel()',
  '  return ('
);
assertIncludes(
  cancelHandler,
  'browserApi.cancelPromptSession({',
  'CommandResultView must cancel prompt sessions through the existing browser API client.'
);
assertIncludes(
  cancelHandler,
  'ownerId: result.interactiveSession.ownerId',
  'Prompt session cancellation should pass the session owner id when available.'
);
assertIncludes(
  cancelHandler,
  'setLocalResult({ commandResult, result: advancedEnabled ? response.data : sanitizeExplorerCommandResultForPlayer(response.data) });',
  'Prompt session cancellation must display a sanitized returned command result by default.'
);
assertIncludes(
  cancelHandler,
  'void loadBrowserState();',
  'Prompt session cancellation must refresh shared browser state.'
);

assertIncludes(
  commandResultView,
  'useEffect(() => {',
  'CommandResultView must watch parent commandResult changes.'
);
assertIncludes(
  commandResultView,
  '}, [commandResult]);',
  'CommandResultView must clear local prompt results when the parent command result changes.'
);

const submitHandler = extractBetween(
  commandResultView,
  'async function handlePromptSubmit(event: FormEvent<HTMLFormElement>)',
  '  async function handlePromptCancel()'
);
assertIncludes(
  submitHandler,
  'setLocalResult({ commandResult, result: advancedEnabled ? response.data : sanitizeExplorerCommandResultForPlayer(response.data) });',
  'Prompt session submission must tie sanitized returned command results to the current parent command result by default.'
);

const actionHandler = extractBetween(
  commandResultView,
  'function handleActionClick(action: UiAction)',
  '  function handlePromptAnswerChange'
);
assertIncludes(
  actionHandler,
  'setLocalResult(null);',
  'CommandResultView must clear local prompt results before follow-up actions execute.'
);
assertIncludes(
  actionHandler,
  'void executeCommand(action.command);',
  'Follow-up actions must still use the shell command executor.'
);
