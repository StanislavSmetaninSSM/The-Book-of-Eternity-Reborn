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

    expect(source).toContain("export function BlockRenderer({ block }: { block: UiBlock }): ReactNode {");
    expect(source).toContain("switch (block.kind) {");
    expect(source).toContain("case 'panel':");
    expect(source).toContain("block.blocks.map((child, i) => <BlockRenderer key={`${child.kind}-${i}`} block={child} />)");
    expect(source).toContain("case 'rawJson':");
  });

  it('exports a BlockList helper and uses player-facing copy fallbacks', () => {
    const source = readBlockRendererSource();

    expect(source).toContain("import { toPlayerFacingText } from '../utils/playerCopy';");
    expect(source).toContain("toPlayerFacingText(block.title, 'Панель')");
    expect(source).toContain("toPlayerFacingText(block.title, 'Сообщение')");
    expect(source).toContain("export function BlockList({ blocks }: { blocks: UiBlock[] }) {");
    expect(source).toContain("blocks.map((block, i) => <BlockRenderer key={`${block.kind}-${i}`} block={block} />)");
  });
});
