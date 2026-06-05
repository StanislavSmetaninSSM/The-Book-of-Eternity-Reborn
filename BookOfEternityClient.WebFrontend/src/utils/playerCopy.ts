import type {
  ExplorerCommandResult,
  UiAction,
  UiBlock,
  UiPrompt,
  UiSelectionOption
} from '../api/contracts';

export const compoundReplacements: Array<[RegExp, string]> = [
  [/\bMortal World\b/gi, 'Мир смертных'],
  [/\bChaos Sea\b/gi, 'Море Хаоса'],
  [/\bShining Abode\b/gi, 'Сияющая Обитель'],
  [/\bGM[- ]?turn\b/g, 'ход ГМа'],
  [/QTE action resolved\.?/gi, 'Быстрая сцена завершена.'],
  [/debug shell/gi, 'служебная оболочка'],
  [/Slash-команды/gi, 'служебные команды'],
  [/\bslash commands?\b/gi, 'служебные команды'],
  [/Нужен repair pending turn/gi, 'Нужна починка ожидающего хода'],
  [/repair pending turn/gi, 'починка ожидающего хода'],
  [/нужен repair/gi, 'нужна починка'],
  [/\bpending[- ]turn\b/gi, 'ожидающий ход'],
  [/\bturn[- ]writer\b/gi, 'запись хода'],
  [/\bBrowser[- ]write\b/gi, 'локальная запись'],
  [/\bbrowser write\b/gi, 'локальная запись'],
  [/\blocal[- ]write\b/gi, 'локальная запись'],
  [/\bprompt[- ]session\b/gi, 'игровая форма'],
  [/blocked by/gi, 'заблокировано из-за'],
  [/state\/contract/gi, 'файлы состояния и контракта'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/Browser Client/gi, 'локальная книга'],
  [/repair\/validation/gi, 'починка и проверка'],
  [/UI-блокировка/gi, 'блокировка интерфейса'],
  [/game_state\/meta\/soul_state\.json/gi, 'файл души'],
  [/soul_state\.json/gi, 'файл души'],
  [/локальный запись хода/gi, 'локальную запись хода'],
  [/тот же локальную/gi, 'ту же локальную']
];

export const technicalTermReplacements: Array<[RegExp, string]> = [
  [/\bGM\b/g, 'ГМ'],
  [/\bQTE\b/g, 'быстрая сцена'],
  [/\brollback\b/gi, 'откат'],
  [/\bblocked\b/gi, 'заблокировано'],
  [/\bSpectre\.Console\b/g, 'консольный интерфейс'],
  [/game_session/gi, 'сохранение игры'],
  [/write-flow/gi, 'запись хода'],
  [/manual_saves/gi, 'ручные сохранения'],
  [/autosaves/gi, 'автосохранения'],
  [/--web/g, 'браузерный режим'],
  [/\bsnapshot\b/gi, 'снимок'],
  [/sound-notification/gi, 'звуковая подсказка'],
  [/\bpending_shining_abode\b/gi, 'ожидание Сияющей Обители'],
  [/\bpending_chaos_sea\b/gi, 'ожидание Моря Хаоса'],
  [/\bturn_writer\b/gi, 'запись хода'],
  [/\bbrowser_write\b/gi, 'запись действия'],
  [/\bincarnation\b/gi, 'инкарнация'],
  [/\bvalidation\b/gi, 'проверка'],
  [/game_state/gi, 'папка состояния игры'],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoint(s)?\b/gi, 'разделы локального интерфейса'],
  [/\bAPI\b/g, 'локальный интерфейс'],
  [/\bDTO\b/g, 'данные интерфейса'],
  [/\bNPC\b/g, 'персонажи мира']
];

export const playerCopyReplacements: Array<[RegExp, string]> = [
  ...compoundReplacements,
  ...technicalTermReplacements,
];

export const launcherAboutCopyReplacements: Array<[RegExp, string]> = [
  [/\bdebug\b/gi, 'служебная'],
  [/\bdiagnostics?\b/gi, 'проверочные сведения'],
  [/\btechnical details?\b/gi, 'служебные сведения'],
  [/\btechnical\b/gi, 'служебный'],
  [/\bdeveloper\b/gi, 'служебный'],
  [/\braw JSON\b/gi, 'подробные данные']
];

export function toPlayerFacingText(value: string | null | undefined, fallback: string): string {
  const source = value?.trim();
  if (!source) {
    return fallback;
  }

  const normalized = playerCopyReplacements.reduce(
    (text, [pattern, replacement]) => text.replace(pattern, replacement),
    source
  );

  return normalized.trim() || fallback;
}

export function playerLauncherAboutText(text: string): string {
  const fallback = 'Локальная книга открывает текущую главу и оставляет игровые решения внутри игры.';
  const playerText = toPlayerFacingText(text, fallback);
  const sanitized = launcherAboutCopyReplacements.reduce(
    (copy, [pattern, replacement]) => copy.replace(pattern, replacement),
    playerText
  );

  return sanitized.trim() || fallback;
}

const technicalPatterns: RegExp[] = [
  /\b[\w/\\]+\.json\b/gi,
  /\b[\w/\\]+\.txt\b/gi,
  /\b[\w/\\]+\.md\b/gi,
  /\bgame_state[\\/][\w/\\]+/gi,
  /\boutput[\\/][\w/\\]+/gi,
  /\bBrowser[- ]write\b/gi,
  /\bbrowser[-_ ]write\b/gi,
  /\bprompt[- ]session\b/gi,
  /\bGM[- ]?turn\b/gi,
  /\brollback\b/gi,
  /\bprotocol\b/gi,
  /\bпротокол[а-я]*\b/gi,
  /\bдруг(?:ой|ому|ого)\s+UI\b/gi,
  /\bбраузер[а-я]*\b/gi,
  /\bbrowser\b/gi,
  /\bclient[- ]owned\b/gi,
  /\bclient\b/gi,
  /\bSpectre\.Console\b/g,
  /\bартефакты?\s*протокола\b/gi,
  /\bJSON:\s*\w+/gi,
  /\braw\s+JSON\b/gi,
  /\bendpoint(s)?\b/gi,
  /\/api\/[\w/.-]+/gi,
  /\bФайл\s+\S+\s+не найден/gi,
  /\bnpc_core\b/gi,
  /\bsoul_state\b/gi,
  /\bcurrent_location\b/gi,
  /\bnarrative_response\b/gi
];

const defaultBoundaryTechnicalPatterns: RegExp[] = [
  /\bBrowser[- ]write\b/gi,
  /\bbrowser[-_ ]write\b/gi,
  /\bprompt[- ]session\b/gi,
  /\bGM[- ]?turn\b/gi,
  /\brollback\b/gi,
  /\bдруг(?:ой|ому|ого)\s+UI\b/gi,
  /\bбраузер[а-я]*\b/gi,
  /\bbrowser\b/gi,
  /\bclient\b/gi,
  /\bSpectre\.Console\b/g
];

export function containsTechnicalDetails(text: string | null | undefined): boolean {
  if (!text) return false;
  return technicalPatterns.some(pattern => {
    pattern.lastIndex = 0;
    return pattern.test(text);
  });
}

export function sanitizePlayerMessage(text: string | null | undefined, fallback: string): { safe: string; hasTechnical: boolean } {
  const source = text?.trim();
  if (!source) {
    return { safe: fallback, hasTechnical: false };
  }

  const hasTechnical = containsTechnicalDetails(source);
  if (!hasTechnical) {
    return { safe: toPlayerFacingText(source, fallback), hasTechnical: false };
  }

  if (matchesAny(defaultBoundaryTechnicalPatterns, source)) {
    return { safe: fallback, hasTechnical: true };
  }

  let cleaned = source;
  for (const pattern of technicalPatterns) {
    pattern.lastIndex = 0;
    cleaned = cleaned.replace(pattern, '');
  }
  cleaned = cleaned.replace(/\s{2,}/g, ' ').replace(/[—–]\s*$/g, '').trim();

  const safe = cleaned ? toPlayerFacingText(cleaned, fallback) : fallback;
  return { safe, hasTechnical: true };
}

function matchesAny(patterns: RegExp[], text: string): boolean {
  return patterns.some(pattern => {
    pattern.lastIndex = 0;
    return pattern.test(text);
  });
}

export function sanitizeExplorerCommandResultForPlayer(result: ExplorerCommandResult): ExplorerCommandResult {
  return {
    ...result,
    command: '',
    blocks: result.blocks.map(sanitizeUiBlockForPlayer),
    actions: result.actions.map(sanitizeUiActionForPlayer),
    prompts: result.prompts.map(sanitizeUiPromptForPlayer),
    notifications: result.notifications.map((notification) => ({
      ...notification,
      title: safePlayerText(notification.title, 'Уведомление'),
      message: safePlayerText(notification.message, 'Игровое действие изменило состояние.')
    })),
    interactiveSession: result.interactiveSession
      ? {
          ...result.interactiveSession,
          submitEndpoint: '',
          cancelEndpoint: ''
        }
      : null
  };
}

function sanitizeUiBlockForPlayer(block: UiBlock): UiBlock {
  switch (block.kind) {
    case 'text':
      return { ...block, text: safePlayerText(block.text, 'Текст действия недоступен.') };
    case 'panel':
      return {
        ...block,
        title: safePlayerText(block.title, 'Панель'),
        blocks: block.blocks.map(sanitizeUiBlockForPlayer)
      };
    case 'table':
      return {
        ...block,
        title: safePlayerText(block.title, 'Таблица'),
        columns: block.columns.map((column) => safePlayerText(column, 'Столбец')),
        rows: block.rows.map((row) => ({
          ...row,
          cells: row.cells.map((cell) => safePlayerText(cell, '—'))
        }))
      };
    case 'list':
      return {
        ...block,
        items: block.items.map((item) => safePlayerText(item, 'пункт списка'))
      };
    case 'keyValueGrid':
      return {
        ...block,
        items: block.items.map((item) => ({
          key: safePlayerText(item.key, 'параметр'),
          value: safePlayerText(item.value, 'значение')
        }))
      };
    case 'message':
      return {
        ...block,
        title: safePlayerText(block.title, 'Сообщение'),
        message: safePlayerText(block.message, 'Игровое действие изменило состояние.')
      };
    case 'rawJson':
      return {
        kind: 'text',
        text: 'Подробные сведения доступны в расширенном режиме.',
        tone: 'Muted'
      };
    case 'image':
      return {
        ...block,
        title: safePlayerText(block.title, 'Изображение'),
        altText: safePlayerText(block.altText, block.title || 'Изображение')
      };
    case 'map':
      return {
        ...block,
        title: safePlayerText(block.title, 'Карта')
      };
  }
}

function sanitizeUiActionForPlayer(action: UiAction): UiAction {
  return {
    ...action,
    label: safePlayerText(action.label, 'Действие'),
    payload: null
  };
}

function sanitizeUiPromptForPlayer(prompt: UiPrompt): UiPrompt {
  switch (prompt.kind) {
    case 'confirmation':
      return {
        ...prompt,
        prompt: safePlayerText(prompt.prompt, 'Поле формы')
      };
    case 'selection':
      return {
        ...prompt,
        prompt: safePlayerText(prompt.prompt, 'Поле формы'),
        options: prompt.options.map(sanitizeUiSelectionOptionForPlayer)
      };
    case 'textInput':
      return {
        ...prompt,
        prompt: safePlayerText(prompt.prompt, 'Поле формы'),
        placeholder: safePlayerText(prompt.placeholder, '')
      };
    case 'longTextInput':
      return {
        ...prompt,
        prompt: safePlayerText(prompt.prompt, 'Поле формы'),
        placeholder: safePlayerText(prompt.placeholder, '')
      };
  }
}

function sanitizeUiSelectionOptionForPlayer(option: UiSelectionOption): UiSelectionOption {
  return {
    ...option,
    label: safePlayerText(option.label, 'вариант'),
    description: safePlayerText(option.description, '')
  };
}

function safePlayerText(text: string | null | undefined, fallback: string): string {
  return sanitizePlayerMessage(text, fallback).safe;
}
