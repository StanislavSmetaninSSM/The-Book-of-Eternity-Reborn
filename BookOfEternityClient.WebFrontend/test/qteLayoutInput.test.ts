import { describe, expect, it } from 'vitest';
import {
  formatQteKeyTokenLabel,
  normalizeQteKeyCharacter,
  normalizeQteKeyboardInput,
  qteLayoutKeyLabels
} from '../src/utils/qteKeyInput';

describe('QTE layout-independent keyboard input', () => {
  it('prefers physical KeyboardEvent.code over produced RU characters', () => {
    const cases: Array<[Pick<KeyboardEvent, 'code' | 'key'>, string]> = [
      [{ code: 'KeyQ', key: 'й' }, 'q'],
      [{ code: 'KeyW', key: 'ц' }, 'w'],
      [{ code: 'KeyE', key: 'у' }, 'e'],
      [{ code: 'KeyA', key: 'ф' }, 'a'],
      [{ code: 'KeyS', key: 'ы' }, 's'],
      [{ code: 'KeyD', key: 'в' }, 'd'],
      [{ code: 'Space', key: ' ' }, 'space']
    ];

    for (const [event, expected] of cases) {
      expect(normalizeQteKeyboardInput(event)).toBe(expected);
    }
  });

  it('normalizes fallback RU characters when physical code is unavailable', () => {
    const cases: Array<[Pick<KeyboardEvent, 'code' | 'key'>, string]> = [
      [{ code: '', key: 'й' }, 'q'],
      [{ code: '', key: 'Ц' }, 'w'],
      [{ code: 'Unidentified', key: 'у' }, 'e'],
      [{ code: 'Dead', key: 'Ф' }, 'a'],
      [{ code: '', key: 'ы' }, 's'],
      [{ code: '', key: 'В' }, 'd'],
      [{ code: '', key: ' ' }, 'space']
    ];

    for (const [event, expected] of cases) {
      expect(normalizeQteKeyboardInput(event)).toBe(expected);
    }
  });

  it('formats physical prompt labels with RU fallback labels', () => {
    expect(formatQteKeyTokenLabel('q')).toBe('Q / Й');
    expect(formatQteKeyTokenLabel('w')).toBe('W / Ц');
    expect(formatQteKeyTokenLabel('e')).toBe('E / У');
    expect(formatQteKeyTokenLabel('a')).toBe('A / Ф');
    expect(formatQteKeyTokenLabel('s')).toBe('S / Ы');
    expect(formatQteKeyTokenLabel('d')).toBe('D / В');
    expect(formatQteKeyTokenLabel('space')).toBe('Space');
    expect(qteLayoutKeyLabels).toEqual(['Q / Й', 'W / Ц', 'E / У', 'A / Ф', 'S / Ы', 'D / В', 'Space']);
  });

  it('does not normalize ordinary text chunks or unsupported keys', () => {
    expect(normalizeQteKeyCharacter('привет')).toBeNull();
    expect(normalizeQteKeyCharacter('ж')).toBeNull();
    expect(normalizeQteKeyboardInput({ code: 'KeyZ', key: 'я' })).toBeNull();
  });
});
