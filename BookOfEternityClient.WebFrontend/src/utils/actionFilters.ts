import type {
  BrowserPlayerCommandActionDto,
  BrowserPlayerCommandMenuDto,
  BrowserPlayerCommandSectionDto
} from '../api/contracts';

export const journalSectionMatchers = ['quest', 'квест', 'journal', 'журнал', 'archive', 'архив', 'chronicle', 'хроника', 'story', 'история', 'faction', 'фракц', 'guardian', 'хранител'];
export const inventorySectionMatchers = ['inventory', 'инвентар', 'item', 'предмет', 'craft', 'ремес', 'equip', 'экип', 'storage', 'хранилищ'];
export const rebornSectionMatchers = ['afterlife', 'посмер', 'soul', 'душ', 'shining', 'сияющ', 'abode', 'обител', 'chaos', 'хаос', 'guardian', 'хранител', 'gate', 'врат'];
export const shiningAbodeActionMatchers = ['shining', 'сияющ', 'abode', 'обител', 'radiance', 'сияни', 'spark', 'искра', 'hall', 'зал', 'gate', 'врат'];
export const chaosSeaActionMatchers = ['chaos', 'хаос', 'sea', 'море', 'guardian', 'хранител', 'abode', 'обител'];

export function filterActionsForPanel(
  sections: BrowserPlayerCommandSectionDto[],
  matchers: string[]
): BrowserPlayerCommandActionDto[] {
  const normalizedMatchers = matchers.map((matcher) => matcher.toLocaleLowerCase('ru-RU'));
  return sections
    .flatMap((section) => section.actions)
    .filter((action) => {
      const haystack = [
        action.id,
        action.label,
        action.description,
        action.formLabel,
        action.formPrompt
      ].join(' ').toLocaleLowerCase('ru-RU');
      return normalizedMatchers.some((matcher) => haystack.includes(matcher));
    })
    .slice(0, 4);
}

export function filterActionSections(menu: BrowserPlayerCommandMenuDto, matchers: string[]): BrowserPlayerCommandSectionDto[] {
  return menu.sections.flatMap((section) => {
    if (!section.playerDefault || section.actions.length === 0) {
      return [];
    }

    const matchingActions = section.actions.filter((action) => matchesActionSectionOrAction(section, action, matchers));
    if (matchingActions.length === 0) {
      return [];
    }

    return [{ ...section, actions: matchingActions }];
  });
}

export function matchesActionSectionOrAction(
  section: BrowserPlayerCommandSectionDto,
  action: BrowserPlayerCommandActionDto,
  matchers: string[]
): boolean {
  const haystack = [
    section.id,
    section.label,
    section.description,
    action.id,
    action.label,
    action.description,
    action.formLabel,
    action.formPrompt,
    action.advancedCommand
  ].join(' ').toLocaleLowerCase('ru-RU');
  const normalizedMatchers = matchers.map((matcher) => matcher.toLocaleLowerCase('ru-RU'));

  return normalizedMatchers.some((matcher) => haystack.includes(matcher));
}
