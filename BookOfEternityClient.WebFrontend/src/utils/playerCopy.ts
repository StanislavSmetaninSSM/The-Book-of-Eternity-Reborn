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
  [/\bBrowser[- ]write\b/gi, 'запись из браузера'],
  [/\bbrowser write\b/gi, 'запись из браузера'],
  [/\blocal[- ]write\b/gi, 'локальная запись'],
  [/\bprompt[- ]session\b/gi, 'игровая форма'],
  [/blocked by/gi, 'заблокировано из-за'],
  [/state\/contract/gi, 'файлы состояния и контракта'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/Browser Client/gi, 'браузерный клиент'],
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
  [/\bbrowser_write\b/gi, 'запись из браузера'],
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
  const fallback = 'Браузерный клиент открывает локальную книгу и оставляет игровые решения в основном клиенте.';
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
  /\bprotocol\b/gi,
  /\bартефакты?\s*протокола\b/gi,
  /\bJSON:\s*\w+/gi,
  /\bФайл\s+\S+\s+не найден/gi,
  /\bnpc_core\b/gi,
  /\bsoul_state\b/gi,
  /\bcurrent_location\b/gi,
  /\bnarrative_response\b/gi
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

  let cleaned = source;
  for (const pattern of technicalPatterns) {
    pattern.lastIndex = 0;
    cleaned = cleaned.replace(pattern, '');
  }
  cleaned = cleaned.replace(/\s{2,}/g, ' ').replace(/[—–]\s*$/g, '').trim();

  const safe = cleaned ? toPlayerFacingText(cleaned, fallback) : fallback;
  return { safe, hasTechnical: true };
}
