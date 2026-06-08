export type QteKeyToken = 'q' | 'w' | 'e' | 'a' | 's' | 'd' | 'space';

export interface QteKeyboardInputLike {
  code?: string | null;
  key?: string | null;
}

const codeTokens: Record<string, QteKeyToken> = {
  KeyQ: 'q',
  KeyW: 'w',
  KeyE: 'e',
  KeyA: 'a',
  KeyS: 's',
  KeyD: 'd',
  Space: 'space'
};

const characterTokens: Record<string, QteKeyToken> = {
  q: 'q',
  й: 'q',
  w: 'w',
  ц: 'w',
  e: 'e',
  у: 'e',
  a: 'a',
  ф: 'a',
  s: 's',
  ы: 's',
  d: 'd',
  в: 'd',
  ' ': 'space'
};

const promptLabels: Record<QteKeyToken, string> = {
  q: 'Q / Й',
  w: 'W / Ц',
  e: 'E / У',
  a: 'A / Ф',
  s: 'S / Ы',
  d: 'D / В',
  space: 'Space'
};

const qteLayoutKeyTokens: QteKeyToken[] = ['q', 'w', 'e', 'a', 's', 'd', 'space'];

export const qteLayoutKeyLabels: string[] = qteLayoutKeyTokens.map(formatQteKeyTokenLabel);

export const qteLayoutSupportNote =
  `Клавиши быстрых сцен читаются как физические: ${qteLayoutKeyLabels.slice(0, -1).join(', ')} и ${qteLayoutKeyLabels[qteLayoutKeyLabels.length - 1]} работают без смены раскладки.`;

export function normalizeQteKeyboardInput(input: QteKeyboardInputLike): QteKeyToken | null {
  const physicalCode = input.code?.trim();
  if (physicalCode && codeTokens[physicalCode]) {
    return codeTokens[physicalCode];
  }

  return normalizeQteKeyCharacter(input.key);
}

export function normalizeQteKeyCharacter(value: string | null | undefined): QteKeyToken | null {
  if (!value) {
    return null;
  }

  if (value === 'Space') {
    return 'space';
  }

  if (value.length !== 1) {
    return null;
  }

  return characterTokens[value.toLowerCase()] ?? null;
}

export function formatQteKeyTokenLabel(token: QteKeyToken): string {
  return promptLabels[token];
}
