import { existsSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readBlockRendererSource(): string {
  const filePath = join(frontendDir, 'src', 'components', 'BlockRenderer.tsx');
  expect(existsSync(filePath)).toBe(true);
  return readFileSync(filePath, 'utf-8');
}

describe('BlockRenderer source', () => {
  it('defines recursive UiBlock rendering', () => {
    const source = readBlockRendererSource();

    expect(source).toContain("export function BlockRenderer({ block, advancedEnabled = false }: { block: UiBlock; advancedEnabled?: boolean }): ReactNode {");
    expect(source).toContain("switch (block.kind) {");
    expect(source).toContain("case 'panel':");
    expect(source).toContain("block.blocks.map((child, i) => <BlockRenderer key={`${child.kind}-${i}`} block={child} advancedEnabled={advancedEnabled} />)");
    expect(source).toContain("case 'rawJson':");
  });

  it('exports a BlockList helper and uses player-facing copy fallbacks', () => {
    const source = readBlockRendererSource();

    expect(source).toContain("import { sanitizePlayerMessage, toPlayerFacingText } from '../utils/playerCopy';");
    expect(source).toContain("toPlayerFacingText(block.title, 'Панель')");
    expect(source).toContain("const title = sanitizePlayerMessage(block.title, 'Сообщение').safe;");
    expect(source).toContain("export function BlockList({ blocks, advancedEnabled = false }: { blocks: UiBlock[]; advancedEnabled?: boolean }) {");
    expect(source).toContain("blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} advancedEnabled={advancedEnabled} />)");
  });

  it('hides rawJson blocks by default and restores JsonTreeViewer after advanced opt-in', () => {
    const source = readBlockRendererSource();

    expect(source).toContain("import { JsonTreeViewer } from './JsonTreeViewer';");
    expect(source).toContain("import type { UiBlock, UiTone } from '../api/contracts';");
    expect(source).toContain('if (advancedEnabled) {');
    expect(source).toContain("<JsonTreeViewer data={block.json}");
    expect(source).toContain("Подробные сведения доступны в расширенном режиме.");
    expect(source).not.toContain('<pre>{JSON.stringify(block.json, null, 2)}</pre>');
  });
});
