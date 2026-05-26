import type {
  BrowserApiResult,
  ExplorerCommandResult,
  UiBlock,
  UiNotification,
  UiPrompt
} from './api/contracts';

export interface PlayerDefaultCommandPresentationOptions {
  blockedTextFallback?: string;
  blockTitleFallback?: string;
  notificationTitleFallback?: string;
  notificationMessageFallback?: string;
  promptTextFallback?: string;
  promptPlaceholderFallback?: string;
  optionLabelFallback?: string;
  failureMessageFallback?: string;
  preserveSafeBlocks?: boolean;
}

const defaultPlayerDefaultCommandOptions: Required<PlayerDefaultCommandPresentationOptions> = {
  blockedTextFallback: 'Служебные подробности доступны в расширенном режиме.',
  blockTitleFallback: 'Сведения подготовки',
  notificationTitleFallback: 'Игровое уведомление',
  notificationMessageFallback: 'Действие готово к продолжению.',
  promptTextFallback: 'Заполните поле формы',
  promptPlaceholderFallback: '',
  optionLabelFallback: 'вариант',
  failureMessageFallback: 'Игровое действие сейчас недоступно.',
  preserveSafeBlocks: false
};

const playerDefaultForbiddenPatterns: RegExp[] = [
  /\/[A-Za-z][A-Za-z0-9_-]*/,
  /\bclient-owned\b/i,
  /\bGM[- ]?turn\b/i,
  /\bprotocol\b/i,
  /\bBrowser[- ]?write\b/i,
  /\bprompt[- ]?session\b/i,
  /\b(?:session|prompt|owner)[-_ ]?id\b/i,
  /\braw JSON\b/i,
  /JSON\s*:/i,
  /(?:^|[\\/])game_state(?:[\\/]|$)/i,
  /pending_[A-Za-z0-9_]+\.json/i,
  /\bendpoint(s)?\b/i,
  /\bAPI\b/,
  /\bDTO\b/,
  /\bdebug\b/i,
  /\b[a-z][a-z0-9]*_[a-z0-9_]*\b/i,
  /[A-Za-z]:[\\/]/,
  // Issue #745: Russian-language file/protocol diagnostics
  /Файл\s+\S+\.json\b/i,
  /\.json\b/,
  /Артефакты?\s+протокола/i,
  /не найден или пуст/i,
  /не найден или отсутствует/i,
  /Локальный ход|ход ГМа protocol/i,
  /JSON:\s*\S+/i
];

export function sanitizePlayerDefaultCommandResult(
  result: BrowserApiResult<ExplorerCommandResult>,
  options: PlayerDefaultCommandPresentationOptions = {}
): BrowserApiResult<ExplorerCommandResult> {
  const resolvedOptions = { ...defaultPlayerDefaultCommandOptions, ...options };

  if (!result.ok) {
    return {
      ...result,
      playerMessage: sanitizePlayerDefaultCommandText(result.playerMessage, resolvedOptions.failureMessageFallback)
    };
  }

  return {
    ...result,
    data: {
      ...result.data,
      actions: [],
      blocks: resolvedOptions.preserveSafeBlocks ? sanitizePlayerDefaultBlocks(result.data.blocks, resolvedOptions) : [],
      notifications: result.data.notifications.map((notification) => sanitizePlayerDefaultNotification(notification, resolvedOptions)),
      prompts: result.data.prompts.map((prompt) => sanitizePlayerDefaultPrompt(prompt, resolvedOptions))
    }
  };
}

export function sanitizePlayerDefaultCommandText(value: string | null | undefined, fallback: string): string {
  const source = value?.trim();
  if (!source) {
    return fallback;
  }

  if (playerDefaultForbiddenPatterns.some((pattern) => pattern.test(source))) {
    return fallback;
  }

  return source;
}

function sanitizePlayerDefaultNotification(
  notification: UiNotification,
  options: Required<PlayerDefaultCommandPresentationOptions>
): UiNotification {
  return {
    ...notification,
    title: sanitizePlayerDefaultCommandText(notification.title, options.notificationTitleFallback),
    message: sanitizePlayerDefaultCommandText(notification.message, options.notificationMessageFallback)
  };
}

function sanitizePlayerDefaultPrompt(
  prompt: UiPrompt,
  options: Required<PlayerDefaultCommandPresentationOptions>
): UiPrompt {
  switch (prompt.kind) {
    case 'confirmation':
      return {
        ...prompt,
        prompt: sanitizePlayerDefaultCommandText(prompt.prompt, options.promptTextFallback)
      };
    case 'selection':
      return {
        ...prompt,
        prompt: sanitizePlayerDefaultCommandText(prompt.prompt, options.promptTextFallback),
        options: prompt.options.map((option) => ({
          ...option,
          label: sanitizePlayerDefaultCommandText(option.label, options.optionLabelFallback),
          description: sanitizePlayerDefaultCommandText(option.description, '')
        }))
      };
    case 'textInput':
      return {
        ...prompt,
        prompt: sanitizePlayerDefaultCommandText(prompt.prompt, options.promptTextFallback),
        defaultValue: sanitizePlayerDefaultCommandText(prompt.defaultValue, ''),
        placeholder: sanitizePlayerDefaultCommandText(prompt.placeholder, options.promptPlaceholderFallback)
      };
    case 'longTextInput':
      return {
        ...prompt,
        prompt: sanitizePlayerDefaultCommandText(prompt.prompt, options.promptTextFallback),
        defaultValue: sanitizePlayerDefaultCommandText(prompt.defaultValue, ''),
        placeholder: sanitizePlayerDefaultCommandText(prompt.placeholder, options.promptPlaceholderFallback)
      };
  }
}

function sanitizePlayerDefaultBlocks(
  blocks: UiBlock[],
  options: Required<PlayerDefaultCommandPresentationOptions>
): UiBlock[] {
  return blocks.flatMap((block) => {
    const sanitized = sanitizePlayerDefaultBlock(block, options);
    return sanitized ? [sanitized] : [];
  });
}

function sanitizePlayerDefaultBlock(
  block: UiBlock,
  options: Required<PlayerDefaultCommandPresentationOptions>
): UiBlock | null {
  switch (block.kind) {
    case 'text': {
      const text = sanitizePlayerDefaultCommandText(block.text, '');
      return text ? { ...block, text } : null;
    }
    case 'panel': {
      const childBlocks = sanitizePlayerDefaultBlocks(block.blocks, options);
      if (childBlocks.length === 0) {
        return null;
      }

      return {
        ...block,
        title: sanitizePlayerDefaultCommandText(block.title, options.blockTitleFallback),
        blocks: childBlocks
      };
    }
    case 'table': {
      const title = sanitizePlayerDefaultCommandText(block.title, '');
      if (!title || block.columns.some(isUnsafePlayerDefaultCommandText) || block.rows.some((row) => row.cells.some(isUnsafePlayerDefaultCommandText))) {
        return null;
      }

      return block;
    }
    case 'list': {
      const items = block.items.filter((item) => !isUnsafePlayerDefaultCommandText(item));
      return items.length > 0 ? { ...block, items } : null;
    }
    case 'keyValueGrid': {
      const items = block.items.filter(
        (item) => !isUnsafePlayerDefaultCommandText(item.key) && !isUnsafePlayerDefaultCommandText(item.value)
      );
      return items.length > 0 ? { ...block, items } : null;
    }
    case 'message': {
      return {
        ...block,
        title: sanitizePlayerDefaultCommandText(block.title, options.notificationTitleFallback),
        message: sanitizePlayerDefaultCommandText(block.message, options.notificationMessageFallback)
      };
    }
    case 'rawJson':
      return null;
    case 'image': {
      if ([block.url, block.relativePath, block.mediaId, block.altText, block.title].some(isUnsafePlayerDefaultCommandText)) {
        return null;
      }

      return block;
    }
    case 'map': {
      if ([block.title, block.map.title, block.map.currentNodeId].some(isUnsafePlayerDefaultCommandText)) {
        return null;
      }

      return block;
    }
  }
}

function isUnsafePlayerDefaultCommandText(value: string | null | undefined): boolean {
  const source = value?.trim();
  return Boolean(source && playerDefaultForbiddenPatterns.some((pattern) => pattern.test(source)));
}
