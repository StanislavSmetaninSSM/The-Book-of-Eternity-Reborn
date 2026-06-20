import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const frontendDir = process.cwd();

function readSource(...relativePath: string[]): string {
  return readFileSync(join(frontendDir, ...relativePath), 'utf-8').replace(/\r\n/g, '\n');
}

describe('browser settings saves and audio surfaces #1186 #1187', () => {
  it('exposes save management from the browser settings route', () => {
    const settingsView = readSource('src', 'components', 'SettingsView.tsx');

    expect(settingsView).toContain('Сохранения');
    expect(settingsView).toContain('menu?.saves');
    expect(settingsView).toContain('browserApi.createSave');
    expect(settingsView).toContain('browserApi.loadSave');
    expect(settingsView).toContain('Сохранить игру');
    expect(settingsView).toContain('Загрузить сохранение');
    expect(settingsView).toContain('Сохранений пока нет');
  });

  it('mounts the real audio panel from settings so music is playable', () => {
    const settingsView = readSource('src', 'components', 'SettingsView.tsx');
    const audioPanel = readSource('src', 'components', 'AudioPanel.tsx');

    expect(settingsView).toContain("import { AudioPanel } from './AudioPanel';");
    expect(settingsView).toContain('<AudioPanel />');
    expect(audioPanel).toContain('async function unlockBrowserMusic()');
    expect(audioPanel).toContain('await element.play();');
    expect(audioPanel).toContain('Вкладка пока не разрешила запустить музыку');
  });
});
