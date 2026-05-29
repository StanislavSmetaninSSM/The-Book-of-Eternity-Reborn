import type { BrowserApiResult, ExplorerCommandResult, UiBlock } from '../src/api/contracts.js';
import { sanitizePlayerDefaultCommandResult } from '../src/playerFacingCommandResult.js';

const forbiddenVisiblePatterns = [
  /\/world_[a-z_]+/i,
  /\/api\//i,
  /client-owned/i,
  /JSON\s*:/i,
  /raw JSON/i,
  /game_state/i,
  /pending_[a-z0-9_]+\.json/i,
  /debug/i,
  /endpoint/i,
  /GM-turn/i,
  /protocol/i,
  /Browser-write/i,
  /prompt-session/i,
  /\b[a-z][a-z0-9]*_[a-z0-9_]*\b/i,
  /C:\\/i
];

const unsafeWorldSetupResult: BrowserApiResult<ExplorerCommandResult> = {
  ok: true,
  status: 200,
  data: {
    command: '/world_setup',
    state: 'RequiresInput',
    blocks: [
      {
        kind: 'text',
        tone: 'Default',
        text: 'Use /world_rules to edit the client-owned game_state/control/pending_incarnation_world_setup.json file. JSON: {"mode":"draft"}'
      },
      {
        kind: 'panel',
        title: 'Локальный ход / GM-turn protocol',
        blocks: [
          {
            kind: 'text',
            tone: 'Muted',
            text: 'Browser-write prompt-session world_setup_mode status is diagnostic-only.'
          }
        ]
      },
      {
        kind: 'panel',
        title: 'client-owned /world_setup diagnostics',
        blocks: [
          { kind: 'rawJson', title: 'JSON:', json: { path: 'game_state/control/pending_incarnation_world_setup.json' } },
          {
            kind: 'list',
            ordered: false,
            items: [
              'Название мира',
              'Open /world_rules for raw JSON diagnostics',
              'Директивы мира'
            ]
          },
          {
            kind: 'keyValueGrid',
            items: [
              { key: 'Режим подготовки мира', value: 'Новая глава' },
              { key: 'client-owned path', value: 'game_state/control/pending_incarnation_world_setup.json' }
            ]
          }
        ]
      }
    ],
    actions: [
      {
        id: 'open-world-rules',
        label: 'Open /world_rules',
        command: '/world_rules',
        style: 'Default',
        requiresConfirmation: false,
        payload: null
      }
    ],
    prompts: [
      {
        kind: 'selection',
        id: 'world-setup-mode',
        prompt: 'Режим подготовки мира',
        required: true,
        options: [
          { value: 'draft', label: 'Создать или редактировать', description: 'Подготовить новую главу.', disabled: false }
        ],
        allowCustom: false
      },
      {
        kind: 'textInput',
        id: 'world-name',
        prompt: 'Название мира',
        required: true,
        defaultValue: '',
        placeholder: 'Название мира'
      },
      {
        kind: 'longTextInput',
        id: 'world-directives',
        prompt: 'Директивы мира',
        required: true,
        defaultValue: '',
        placeholder: 'Опишите тон, границы и стартовую ситуацию.',
        minLines: 3,
        maxLines: 8
      }
    ],
    notifications: [
      {
        severity: 'Info',
        title: 'World setup form',
        message: 'Opened /world_setup prompt-session endpoint; submitEndpoint is /api/explorer/prompt-sessions/submit.'
      }
    ],
    interactiveSession: {
      sessionId: 'world-setup-session',
      submitEndpoint: '/api/explorer/prompt-sessions/submit',
      cancelEndpoint: '/api/explorer/prompt-sessions/cancel',
      requiresLocalUiLock: true,
      ownerId: 'world-setup-owner',
      expiresAtUtc: '2026-05-26T00:00:00Z'
    }
  }
};

const unsafeFailure: BrowserApiResult<ExplorerCommandResult> = {
  ok: false,
  status: 500,
  kind: 'server-diagnostics',
  message: 'POST /api/explorer/command failed',
  playerMessage: 'Поле world_setup_mode обязательно. Prompt-session world-setup-session отсутствует for /world_setup. JSON: {"path":"game_state/control/pending_incarnation_world_setup.json"}',
  technicalDetails: 'C:\\Users\\Ёж\\debug.log'
};

const sanitized = sanitizePlayerDefaultCommandResult(unsafeWorldSetupResult, {
  blockedTextFallback: 'Служебные подробности скрыты в обычном режиме.',
  notificationTitleFallback: 'Форма новой главы',
  notificationMessageFallback: 'Форма новой главы готова к заполнению.',
  failureMessageFallback: 'Форма новой главы сейчас недоступна.'
});

if (!sanitized.ok) {
  throw new Error('Expected successful command result to stay successful.');
}

const visibleText = collectVisibleCommandText(sanitized.data).join('\n');
for (const pattern of forbiddenVisiblePatterns) {
  if (pattern.test(visibleText)) {
    throw new Error(`Player-default command text leaked forbidden pattern ${pattern}:\n${visibleText}`);
  }
}

for (const expected of ['Название мира', 'Директивы мира', 'Режим подготовки мира']) {
  if (!visibleText.includes(expected)) {
    throw new Error(`Expected player-safe form text to remain visible: ${expected}\n${visibleText}`);
  }
}

if (sanitized.data.actions.length !== 0) {
  throw new Error('Player-default launcher presentation must not retain raw command actions.');
}

if (sanitized.data.blocks.length !== 0) {
  throw new Error(`Player-default launcher presentation must not render C# diagnostic/status blocks: ${JSON.stringify(sanitized.data.blocks)}`);
}

const safeFailure = sanitizePlayerDefaultCommandResult(unsafeFailure, {
  failureMessageFallback: 'Форма новой главы сейчас недоступна.'
});

if (safeFailure.ok) {
  throw new Error('Expected failed command result to stay failed.');
}

for (const pattern of forbiddenVisiblePatterns) {
  if (pattern.test(safeFailure.playerMessage)) {
    throw new Error(`Player-default failure message leaked forbidden pattern ${pattern}: ${safeFailure.playerMessage}`);
  }
}

if (safeFailure.playerMessage !== 'Форма новой главы сейчас недоступна.') {
  throw new Error(`Expected unsafe failure text to fall back to player copy, got: ${safeFailure.playerMessage}`);
}

function collectVisibleCommandText(result: ExplorerCommandResult): string[] {
  return [
    ...result.notifications.flatMap((notification) => [notification.title, notification.message]),
    ...result.blocks.flatMap(collectVisibleBlockText),
    ...result.prompts.flatMap((prompt) => {
      switch (prompt.kind) {
        case 'confirmation':
          return [prompt.prompt];
        case 'selection':
          return [prompt.prompt, ...prompt.options.flatMap((option) => [option.label, option.description])];
        case 'textInput':
        case 'longTextInput':
          return [prompt.prompt, prompt.placeholder, prompt.defaultValue];
      }
    })
  ];
}

function collectVisibleBlockText(block: UiBlock): string[] {
  switch (block.kind) {
    case 'text':
      return [block.text];
    case 'panel':
      return [block.title, ...block.blocks.flatMap(collectVisibleBlockText)];
    case 'table':
      return [block.title, ...block.columns, ...block.rows.flatMap((row) => row.cells)];
    case 'list':
      return block.items;
    case 'keyValueGrid':
      return block.items.flatMap((item) => [item.key, item.value]);
    case 'message':
      return [block.title, block.message];
    case 'rawJson':
      return [block.title, JSON.stringify(block.json)];
    case 'image':
      return [block.title, block.altText, block.url, block.relativePath, block.mediaId];
    case 'map':
      return [block.title, block.map.title, block.map.currentNodeId];
  }
}

import './shellContextComponents.test.js';
import './navBarShortcuts.test.js';
import './playerStatusSidebar.test.js';
import './uiStructure.test.js';
import './useShellStateSettled.test.js';
import './gracefulDegradation.test.js';
import './promptFormSanitization.test.js';
