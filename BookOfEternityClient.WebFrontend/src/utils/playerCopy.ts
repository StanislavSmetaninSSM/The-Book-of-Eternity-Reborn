export const playerCopyReplacements: Array<[RegExp, string]> = [
  [/\bMortal World\b/gi, 'Мир смертных'],
  [/\bChaos Sea\b/gi, 'Море Хаоса'],
  [/\bShining Abode\b/gi, 'Сияющая Обитель'],
  [/\bGM[- ]?turn\b/g, 'ход ГМа'],
  [/\bGM\b/g, 'ГМ'],
  [/QTE action resolved\.?/gi, 'Быстрая сцена завершена.'],
  [/\bQTE\b/g, 'быстрая сцена'],
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
  [/\brollback\b/gi, 'откат'],
  [/blocked by/gi, 'заблокировано из-за'],
  [/\bblocked\b/gi, 'заблокировано'],
  [/\bby\b/gi, 'из-за'],
  [/\bSpectre\.Console\b/g, 'консольный интерфейс'],
  [/state\/contract/gi, 'файлы состояния и контракта'],
  [/snapshot artifact/gi, 'снимок состояния'],
  [/game_session/gi, 'сохранение игры'],
  [/write-flow/gi, 'запись хода'],
  [/manual_saves/gi, 'ручные сохранения'],
  [/autosaves/gi, 'автосохранения'],
  [/--web/g, 'браузерный режим'],
  [/\boffer\b/gi, 'предложение'],
  [/\bsnapshot\b/gi, 'снимок'],
  [/\bartifact\b/gi, 'файл состояния'],
  [/Browser Client/gi, 'браузерный клиент'],
  [/sound-notification/gi, 'звуковая подсказка'],
  [/\brealm\b/gi, 'царство'],
  [/repair\/validation/gi, 'починка и проверка'],
  [/UI-блокировка/gi, 'блокировка интерфейса'],
  [/\bvalidation\b/gi, 'проверка'],
  [/game_state\/meta\/soul_state\.json/gi, 'файл души'],
  [/soul_state\.json/gi, 'файл души'],
  [/game_state/gi, 'папка состояния игры'],
  [/локальный запись хода/gi, 'локальную запись хода'],
  [/тот же локальную/gi, 'ту же локальную'],
  [/\bUI\b/g, 'интерфейс'],
  [/\baction\b/gi, 'действие'],
  [/\bresolved\b/gi, 'завершена'],
  [/\brepair\b/gi, 'починка'],
  [/C\x23\s*/g, ''],
  [/\blifecycle\b/gi, 'состояние хода'],
  [/\bruntime\b/gi, 'игровой слой'],
  [/\bendpoint(s)?\b/gi, 'разделы локального интерфейса'],
  [/\bAPI\b/g, 'локальный интерфейс'],
  [/\bDTO\b/g, 'данные интерфейса'],
  [/\bNPC\b/g, 'персонажи мира']
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
