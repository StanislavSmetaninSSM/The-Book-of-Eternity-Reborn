import type {
  BrowserAudioSettingsDto,
  BrowserGameScreenAfterlifeDto,
  BrowserGameScreenDto,
  BrowserMainMenuDto,
  BrowserPlayerCommandActionDto,
  ExplorerCommandResult,
  LocalWebUiSessionStatus
} from '../api/contracts';
import { toPlayerFacingText } from './playerCopy';

export type QteGrade = 'success' | 'partial' | 'fail';
export type QteAction = NonNullable<NonNullable<BrowserGameScreenDto['qte']['activeScene']>['currentChapter']>['actions'][number];

export function toCommandNotice(result: ExplorerCommandResult): string {
  switch (result.state) {
    case 'RequiresInput':
      return 'Форма открыта. Заполните поля ниже и отправьте её книге.';
    case 'Completed':
      return 'Игровое действие выполнено.';
    case 'Pending':
      return 'Действие ожидает ответа или завершения текущего хода.';
    case 'Blocked':
      return 'Действие сейчас заблокировано состоянием игры.';
    case 'Failed':
      return 'Действие не удалось выполнить; подробности показаны ниже.';
  }
}

export function toLauncherSaveFailureNotice(message: string): string {
  return message.trim()
    ? 'Сохранение не удалось загрузить. Выберите другую запись или попробуйте ещё раз; служебные подробности можно проверить в расширенном режиме.'
    : 'Сохранение не удалось загрузить. Выберите другую запись или попробуйте ещё раз.';
}

export function formatRealmName(realm: string): string {
  switch (realm.trim().toLowerCase()) {
    case 'mortal world':
    case 'mortal-world':
      return 'Мир смертных';
    case 'chaos sea':
    case 'chaos-sea':
      return 'Море Хаоса';
    case 'shining abode':
    case 'shining-abode':
      return 'Сияющая Обитель';
    default:
      return toPlayerFacingText(realm, 'царство уточняется');
  }
}

const canonicalWorldMonthReplacements: Array<[RegExp, string]> = [
  [/\bMonth of Beginnings\b/g, 'Месяц Начал']
];

export function formatWorldTimeForPlayer(value: string | null | undefined, fallback = 'время уточняется'): string {
  const source = value?.trim();
  if (!source) {
    return fallback;
  }

  return canonicalWorldMonthReplacements.reduce(
    (text, [pattern, replacement]) => text.replace(pattern, replacement),
    source
  );
}

export function formatDialogueCategory(category: string): string {
  switch (category.trim().toLowerCase()) {
    case 'exploration':
      return 'исследование';
    case 'dialogue':
    case 'social':
      return 'диалог';
    case 'combat':
      return 'бой';
    case 'lore':
      return 'знание';
    case 'world':
      return 'мир';
    case 'afterlife':
      return 'посмертие';
    default:
      return toPlayerFacingText(category, 'вариант выбора');
  }
}

export function formatTurnStateTitle(turnState: BrowserGameScreenDto['turnState']): string {
  return toPlayerFacingText(turnState.title, formatTurnStateLabel(turnState.phase || turnState.state));
}

export function formatTurnStateMessage(turnState: BrowserGameScreenDto['turnState']): string {
  return toPlayerFacingText(
    turnState.message,
    turnState.canStartBrowserWrite
      ? 'Опишите следующий ход персонажа в художественной форме.'
      : 'Запись хода сейчас недоступна; дождитесь безопасного состояния игры.'
  );
}

export function getComposerPlaceholder(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(actionComposer.placeholder, 'Опишите действие персонажа обычным текстом…');
}

export function getComposerGuidance(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(
    actionComposer.guidance,
    'Пишите действие персонажа обычным текстом; служебные команды доступны только в расширенном режиме.'
  );
}

export function getComposerDisabledReason(actionComposer: BrowserGameScreenDto['actionComposer']): string {
  return toPlayerFacingText(actionComposer.disabledReason, 'Ввод временно недоступен по состоянию хода.');
}

export function formatSessionStatus(status: string): string {
  switch (status.trim().toLowerCase()) {
    case 'ok':
    case 'ready':
      return 'Клиент готов';
    case 'missing':
    case 'not_found':
    case 'notfound':
      return 'Сохранение не найдено';
    case 'blocked':
      return 'Запись временно заблокирована';
    case 'error':
      return 'Нужна проверка состояния';
    default:
      return status.trim() ? 'Состояние требует внимания' : 'Состояние уточняется';
  }
}

export function formatTurnStateLabel(state: string): string {
  switch (state.trim().toLowerCase()) {
    case 'idle':
    case 'ready':
      return 'Готово к ходу';
    case 'composing-action':
    case 'composing_action':
      return 'Игрок готовит действие';
    case 'turn-submitted':
    case 'turn_submitted':
      return 'Ход отправляется';
    case 'waitinggm':
    case 'waiting_gm':
    case 'waiting-gm':
    case 'pending':
    case 'pending-gm-turn':
      return 'Ожидаем ответ ГМа';
    case 'accepted':
      return 'Ответ ГМа принят';
    case 'validationfailed':
    case 'validation_failed':
    case 'validation-failed':
    case 'validation-errors':
      return 'Проверка не прошла';
    case 'repairrequired':
    case 'repair_required':
    case 'repair-required':
    case 'pending-turn-repair':
      return 'Нужна починка';
    case 'error-restored':
    case 'gm-turn-error':
      return 'Ошибка восстановлена';
    case 'cancelled':
      return 'Ход отменён';
    case 'blocked':
      return 'Ход заблокирован';
    case 'error':
      return 'Ошибка хода';
    default:
      return state.trim() ? 'Состояние хода' : 'Ход уточняется';
  }
}

export function formatQteStateLabel(qte: BrowserGameScreenDto['qte']): string {
  if (qte.notification) {
    return toPlayerFacingText(qte.notification, 'Быстрая сцена изменила состояние.');
  }

  if (qte.error) {
    return 'Быстрая сцена требует внимания.';
  }

  switch (qte.state.trim().toLowerCase()) {
    case 'noscene':
    case 'none':
    case 'idle':
      return 'Быстрая сцена не активна.';
    case 'offer':
      return 'Доступна быстрая сцена.';
    case 'active':
      return 'Быстрая сцена активна.';
    case 'resolution':
    case 'resolved':
      return 'Быстрая сцена завершилась.';
    case 'completed':
      return 'Итог быстрой сцены записан.';
    default:
      return 'Состояние быстрой сцены уточняется.';
  }
}

export function commandStateLabel(state: ExplorerCommandResult['state']): string {
  switch (state) {
    case 'RequiresInput':
      return 'Требуется ввод';
    case 'Completed':
      return 'Выполнено';
    case 'Pending':
      return 'Ожидает';
    case 'Blocked':
      return 'Заблокировано';
    case 'Failed':
      return 'Ошибка';
  }
}

export const qteGradeOrder: QteGrade[] = ['success', 'partial', 'fail'];

export function qteGradeOptionsForAction(action: QteAction): QteGrade[] {
  const normalized = action.gradeOptions.map(normalizeQteGrade);
  const unique = qteGradeOrder.filter((grade) => normalized.includes(grade));
  return unique.length > 0 ? unique : qteGradeOrder;
}

export function normalizeQteGrade(value: string | null | undefined): QteGrade {
  switch ((value ?? '').trim().toLowerCase()) {
    case 'partial':
    case 'part':
    case 'mixed':
      return 'partial';
    case 'fail':
    case 'failure':
    case 'failed':
      return 'fail';
    case 'success':
    default:
      return 'success';
  }
}

export function formatQteGradeLabel(grade: QteGrade): string {
  switch (grade) {
    case 'success':
      return 'Успех';
    case 'partial':
      return 'Частичный успех';
    case 'fail':
      return 'Провал';
  }
}

export function formatQteActionCheck(action: QteAction): string {
  const difficulty = action.baseDifficulty > 0 ? `сложность ${action.baseDifficulty}` : 'сложность уточняется';
  const characteristic = toPlayerFacingText(action.primaryCharacteristic, 'проверка');
  return `${characteristic} · ${difficulty}`;
}

export function formatMediaSize(length: number): string {
  if (!Number.isFinite(length) || length <= 0) {
    return 'размер уточняется';
  }

  const units = ['Б', 'КБ', 'МБ', 'ГБ'];
  let value = length;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(value >= 10 || unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
}

export function formatMediaDate(value: string): string {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) {
    return 'дата уточняется';
  }

  return date.toLocaleString('ru-RU', { dateStyle: 'medium', timeStyle: 'short' });
}

export function formatHeroStatusLabel(gameScreen: BrowserGameScreenDto | null, menu: BrowserMainMenuDto | null): string {
  if (gameScreen) {
    return formatTurnStateTitle(gameScreen.turnState);
  }

  if (menu && !menu.session.canContinue) {
    return 'Глава ещё не открыта';
  }

  return 'Книга ждёт открытия';
}

export function formatSidebarLayerStatus(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Книга ждёт открытия.';
  }

  if (!menu.session.canContinue) {
    return 'Откройте новую главу или загрузите сохранение, чтобы увидеть состояние мира.';
  }

  const validationLabel = menu.session.validationLabel;
  return toPlayerFacingText(validationLabel, 'Книга ждёт открытия');
}

export function formatSidebarSessionSummary(session: LocalWebUiSessionStatus | null, menu: BrowserMainMenuDto | null): string {
  if (menu && !menu.session.canContinue && !menu.session.hasReadableSoul) {
    return 'Активной главы пока нет — начните новую или загрузите сохранение.';
  }

  if (session?.gameSessionExists) {
    return session.canStartBrowserWrite
      ? 'Локальная партия найдена, запись следующего хода доступна.'
      : 'Локальная партия найдена, но ход сейчас ждёт безопасного момента.';
  }

  if (menu?.session.gameSessionExists || menu?.session.canContinue) {
    return 'Есть глава, которую можно продолжить с главной страницы.';
  }

  return 'Активной главы пока нет — начните новую или загрузите сохранение.';
}

export function formatSidebarSaveSummary(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Список сохранений появится после ответа локальной книги.';
  }

  if (menu.saves.length > 0) {
    return `Доступно сохранений: ${menu.saves.length}. Последние записи доступны на главной странице.`;
  }

  return 'Сохранений пока не найдено; можно начать новую главу.';
}

export function formatSidebarStatusMetric(value: string): string {
  const normalized = value.trim();
  if (!normalized) {
    return '—';
  }

  return normalized.endsWith('%') ? normalized : `${normalized}%`;
}

export function formatSidebarAudioSummary(audio: BrowserAudioSettingsDto): string {
  const availablePlaylists = audio.playlists.filter((playlist) => playlist.available).length;
  const availableCues = audio.cues.filter((cue) => cue.available).length;
  return `Музыка ${audio.musicEnabled ? 'включена' : 'выключена'}; плейлистов найдено: ${availablePlaylists}; подсказок: ${availableCues}.`;
}

export function formatRebornLockStatus(game: BrowserGameScreenDto): string {
  if (game.flags.isInAfterlifeRealm) {
    return `${formatRealmName(game.soul.realm)} · перья ${game.soul.inkFeathers}`;
  }

  return 'Посмертные панели откроются, когда душа перейдёт в посмертие.';
}

export function formatShiningGateStatus(afterlife: BrowserGameScreenAfterlifeDto): string {
  if (afterlife.isShiningGatesDraftStale) {
    return 'Черновик врат требует обновления';
  }

  if (afterlife.hasOpenShiningGatesDraft) {
    return 'Черновик врат открыт';
  }

  return 'Врата ждут подходящего момента';
}

export function formatActionPreview(action: BrowserPlayerCommandActionDto): string {
  if (action.enabled) {
    return toPlayerFacingText(action.description, 'Действие доступно для текущей главы.');
  }

  return toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.');
}

export function formatTurnLifecycleActionDescription(action: BrowserGameScreenDto['turnState']['recommendedActions'][number]): string {
  if (action.enabled) {
    return toPlayerFacingText(action.description, 'Действие доступно для текущего состояния хода.');
  }

  return toPlayerFacingText(action.disabledReason, 'Действие сейчас недоступно.');
}
