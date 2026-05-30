import { useState, type ReactNode } from 'react';
import type { JsonValue } from '../api/contracts';

interface JsonTreeViewerProps {
  data: JsonValue;
  title?: string;
  defaultExpanded?: boolean;
  maxInitialDepth?: number;
}

export function JsonTreeViewer({ data, title, defaultExpanded = true, maxInitialDepth = 2 }: JsonTreeViewerProps) {
  return (
    <div className="json-tree">
      {title && <h4 className="json-tree__title">{title}</h4>}
      <div className="json-tree__body">
        <JsonNode value={data} depth={0} maxInitialDepth={maxInitialDepth} defaultExpanded={defaultExpanded} keyName={undefined} />
      </div>
    </div>
  );
}

function JsonNode({ value, depth, maxInitialDepth, defaultExpanded, keyName }: {
  value: JsonValue;
  depth: number;
  maxInitialDepth: number;
  defaultExpanded: boolean;
  keyName: string | undefined;
}): ReactNode {
  if (value === null) return <span className="json-tree__line"><KeyLabel k={keyName} /><span className="json-val--null">null</span></span>;
  if (typeof value === 'string') return <span className="json-tree__line"><KeyLabel k={keyName} /><span className="json-val--string">&quot;{value}&quot;</span></span>;
  if (typeof value === 'number') return <span className="json-tree__line"><KeyLabel k={keyName} /><span className="json-val--number">{value}</span></span>;
  if (typeof value === 'boolean') return <span className="json-tree__line"><KeyLabel k={keyName} /><span className="json-val--bool">{value ? 'true' : 'false'}</span></span>;

  const isArray = Array.isArray(value);
  const entries = isArray
    ? value.map((entry, index) => [String(index), entry] as const)
    : Object.entries(value);
  const count = entries.length;

  return (
    <CollapsibleNode
      keyName={keyName}
      label={isArray ? `[${count} items]` : `{${count} keys}`}
      depth={depth}
      maxInitialDepth={maxInitialDepth}
      defaultExpanded={defaultExpanded}
      isEmpty={count === 0}
    >
      {entries.map(([k, v]) => (
        <JsonNode key={k} value={v} depth={depth + 1} maxInitialDepth={maxInitialDepth} defaultExpanded={defaultExpanded} keyName={k} />
      ))}
    </CollapsibleNode>
  );
}

function CollapsibleNode({ keyName, label, depth, maxInitialDepth, defaultExpanded, isEmpty, children }: {
  keyName: string | undefined;
  label: string;
  depth: number;
  maxInitialDepth: number;
  defaultExpanded: boolean;
  isEmpty: boolean;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(defaultExpanded && depth < maxInitialDepth);

  if (isEmpty) {
    return <span className="json-tree__line"><KeyLabel k={keyName} /><span className="json-val--empty">{label}</span></span>;
  }

  return (
    <div className="json-tree__node">
      <button
        type="button"
        className="json-tree__toggle"
        onClick={() => setExpanded((state) => !state)}
        aria-expanded={expanded}
      >
        <span className={`json-tree__arrow ${expanded ? 'is-open' : ''}`}>▶</span>
        <KeyLabel k={keyName} />
        <span className="json-val--bracket">{label}</span>
      </button>
      {expanded && <div className="json-tree__children">{children}</div>}
    </div>
  );
}

function KeyLabel({ k }: { k: string | undefined }) {
  if (k === undefined) return null;
  return <span className="json-tree__key">{k}: </span>;
}
