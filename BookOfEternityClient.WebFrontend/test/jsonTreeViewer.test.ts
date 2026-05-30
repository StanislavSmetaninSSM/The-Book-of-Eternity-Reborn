import { existsSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const cwd = process.cwd();
const frontendDir = basename(cwd) === 'BookOfEternityClient.WebFrontend'
  ? cwd
  : join(cwd, 'BookOfEternityClient.WebFrontend');

function readJsonTreeViewerSource(): string {
  const filePath = join(frontendDir, 'src', 'components', 'JsonTreeViewer.tsx');
  expect(existsSync(filePath)).toBe(true);
  return readFileSync(filePath, 'utf-8');
}

describe('JsonTreeViewer source', () => {
  it('renders collapsible JSON nodes with typed contract values', () => {
    const source = readJsonTreeViewerSource();

    expect(source).toContain("import { useState, type ReactNode } from 'react';");
    expect(source).toContain("import type { JsonValue } from '../api/contracts';");
    expect(source).toContain("export function JsonTreeViewer({ data, title, defaultExpanded = true, maxInitialDepth = 2 }: JsonTreeViewerProps)");
    expect(source).toContain("<JsonNode value={data} depth={0} maxInitialDepth={maxInitialDepth} defaultExpanded={defaultExpanded} keyName={undefined} />");
    expect(source).toContain('aria-expanded={expanded}');
    expect(source).toContain("className={`json-tree__arrow ${expanded ? 'is-open' : ''}`}");
  });
});
