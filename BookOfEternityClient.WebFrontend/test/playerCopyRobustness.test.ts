import { describe, expect, it } from 'vitest';
import { toPlayerFacingText, sanitizePlayerMessage } from '../src/utils/playerCopy';

describe('playerCopy robustness', () => {
  it('does not mangle normal narrative text', () => {
    const narrative = 'You pass by the ancient gate. The hero resolved to act by sunrise.';
    const result = toPlayerFacingText(narrative, 'fallback');
    expect(result).not.toContain('из-за');
    expect(result).not.toContain('действие');
    expect(result).not.toContain('завершена');
    expect(result).toContain('pass');
    expect(result).toContain('gate');
  });

  it('still translates compound technical phrases', () => {
    const technical = 'repair pending turn blocked by validation';
    const result = toPlayerFacingText(technical, 'fallback');
    expect(result).toContain('починка ожидающего хода');
    expect(result).toContain('заблокировано');
    expect(result).toContain('проверка');
  });

  it('translates realm names consistently', () => {
    const text = 'You are in Chaos Sea. The Shining Abode awaits.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('Море Хаоса');
    expect(result).toContain('Сияющая Обитель');
  });

  it('translates GM terminology', () => {
    const text = 'Waiting for GM-turn. The GM will respond.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('ход ГМа');
    expect(result).toContain('ГМ');
  });

  it('handles debug shell replacement without mangling', () => {
    const text = 'Use the debug shell for diagnostics.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('служебная оболочка');
    expect(result).not.toContain('debug shell');
  });

  it('does not replace "by" as standalone word', () => {
    const text = 'Stand by the door. Crafted by the smith.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('by the door');
    expect(result).toContain('by the smith');
  });

  it('does not replace "action" in narrative context', () => {
    const text = 'Take action against the darkness.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('action');
  });

  it('does not replace "realm" in narrative context', () => {
    const text = 'This realm holds ancient secrets.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('realm');
    expect(result).not.toContain('царство');
  });

  it('does not replace "offer" in narrative context', () => {
    const text = 'I offer you my sword and shield.';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('offer');
    expect(result).not.toContain('предложение');
  });

  it('preserves sanitizePlayerMessage behavior for file paths', () => {
    const text = 'Error in game_state/meta/soul_state.json — repair needed';
    const { safe, hasTechnical } = sanitizePlayerMessage(text, 'fallback');
    expect(hasTechnical).toBe(true);
    expect(safe).not.toContain('soul_state.json');
  });

  it('translates identifiers with underscores/hyphens', () => {
    const text = 'Check game_session for write-flow status in manual_saves';
    const result = toPlayerFacingText(text, 'fallback');
    expect(result).toContain('сохранение игры');
    expect(result).toContain('запись хода');
    expect(result).toContain('ручные сохранения');
  });
});
