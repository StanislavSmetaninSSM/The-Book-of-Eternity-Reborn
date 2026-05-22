namespace BookOfEternityClient.Services;

public static class LocalMapViewerAssets
{
    public const string StyleSheet = """
    .map-block {
      --atlas-ink: #2b2116;
      --atlas-muted: #6f5935;
      --atlas-gold: #cda85a;
      --atlas-blood: #7b241b;
      --atlas-moss: #285238;
      --atlas-parchment: #d8bd82;
      --atlas-parchment-deep: #a9824d;
      display: grid;
      gap: .8rem;
      border-color: rgba(205, 168, 90, .42);
      background:
        radial-gradient(circle at 18% 8%, rgba(205, 168, 90, .16), transparent 18rem),
        linear-gradient(145deg, rgba(32, 39, 30, .96), rgba(19, 21, 17, .94));
      box-shadow: 0 1.2rem 3rem rgba(0, 0, 0, .22), inset 0 0 0 1px rgba(255, 233, 172, .06);
    }
    .map-block h2 {
      letter-spacing: .03em;
    }
    .map-subtitle {
      color: var(--muted, #a9b2a4);
      margin: -.35rem 0 .15rem;
    }
    .map-toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: .55rem;
      align-items: center;
    }
    .map-toolbar label {
      color: var(--muted, #a9b2a4);
      display: flex;
      gap: .35rem;
      align-items: center;
    }
    .map-canvas {
      width: 100%;
      min-height: 28rem;
      border: 2px solid rgba(86, 55, 22, .68);
      border-radius: .95rem;
      background:
        radial-gradient(circle at 18% 22%, rgba(255, 246, 190, .25), transparent 15rem),
        radial-gradient(circle at 86% 72%, rgba(88, 47, 26, .18), transparent 18rem),
        repeating-linear-gradient(35deg, rgba(96, 65, 30, .045) 0 2px, transparent 2px 8px),
        linear-gradient(135deg, var(--atlas-parchment), var(--atlas-parchment-deep));
      box-shadow: inset 0 0 3.5rem rgba(42, 24, 8, .28), 0 .8rem 2rem rgba(22, 13, 5, .28);
      touch-action: none;
    }
    .map-atlas-frame { position: relative; }
    .atlas-texture { opacity: .36; mix-blend-mode: multiply; pointer-events: none; }
    .map-link { stroke: rgba(81, 57, 31, .82); stroke-linecap: round; stroke-dasharray: .25 .36; }
    .map-link--dangerous { stroke: var(--atlas-blood); stroke-dasharray: .08 .28; }
    .map-node { cursor: pointer; outline: none; }
    .map-node circle {
      filter: drop-shadow(0 .18px .18px rgba(29, 19, 8, .6));
      transition: r .15s ease, stroke-width .15s ease;
    }
    .map-node:hover circle, .map-node:focus circle, .map-node--selected circle {
      r: .82;
      stroke-width: .2;
    }
    .map-node text {
      fill: var(--atlas-ink);
      font: 1.05px Georgia, "Times New Roman", serif;
      paint-order: stroke;
      stroke: rgba(245, 229, 180, .72);
      stroke-width: .18px;
      pointer-events: none;
    }
    .map-empty {
      position: absolute;
      inset: 42% 1rem auto;
      margin: 0 auto;
      width: fit-content;
      max-width: min(90%, 34rem);
      border: 1px solid rgba(86, 55, 22, .35);
      border-radius: .9rem;
      background: rgba(247, 229, 177, .78);
      color: var(--atlas-ink);
      padding: .75rem 1rem;
      box-shadow: 0 .7rem 1.4rem rgba(58, 35, 13, .18);
    }
    .map-empty[hidden] { display: none; }
    .map-legend {
      display: flex;
      flex-wrap: wrap;
      gap: .55rem .9rem;
      align-items: center;
      color: var(--muted, #a9b2a4);
    }
    .map-legend strong { color: var(--accent, #e1b85e); }
    .map-legend span { display: inline-flex; gap: .35rem; align-items: center; }
    .legend-swatch {
      width: .75rem;
      height: .75rem;
      border: 1px solid rgba(247, 217, 145, .75);
      border-radius: 50%;
      background: var(--atlas-blood);
    }
    .legend-swatch.current { background: var(--atlas-moss); }
    .legend-swatch.faction { background: #80501f; }
    .legend-swatch.contested { background: linear-gradient(135deg, #80501f 0 48%, var(--atlas-blood) 52%); }
    .map-region {
      fill: rgba(128, 80, 31, .16);
      stroke: rgba(105, 60, 22, .42);
      stroke-width: .18;
      stroke-dasharray: .6 .32;
      pointer-events: none;
    }
    .map-region-label {
      fill: rgba(43, 33, 22, .68);
      font: .8px Georgia, "Times New Roman", serif;
      paint-order: stroke;
      stroke: rgba(247, 229, 177, .6);
      stroke-width: .16px;
      pointer-events: none;
    }
    .map-political-halo {
      fill: rgba(128, 80, 31, .24);
      stroke: rgba(128, 80, 31, .38);
      stroke-width: .09;
      pointer-events: none;
    }
    .map-political-halo--contested {
      fill: rgba(123, 36, 27, .22);
      stroke: rgba(123, 36, 27, .58);
      stroke-dasharray: .16 .12;
    }
    .map-node--contested circle {
      stroke: var(--atlas-blood);
      stroke-dasharray: .16 .1;
    }
    .map-block[data-layer-state="hidden"] .map-card {
      opacity: .78;
    }
    .map-card {
      border: 1px solid rgba(222, 183, 99, .2);
      border-radius: .85rem;
      background: rgba(0, 0, 0, .16);
      padding: .8rem;
      color: inherit;
    }
    .map-card dl {
      display: grid;
      grid-template-columns: minmax(7rem, 12rem) 1fr;
      gap: .35rem .75rem;
      margin: 0;
    }
    .map-card dt { color: var(--muted, #a9b2a4); }
    .map-card dd { margin: 0; }
    """;

    public const string Script = """
    (function (global) {
      function el(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = String(text);
        return node;
      }

      function legendItem(className, text) {
        const node = el('span', '', '');
        const swatch = el('i', `legend-swatch ${className}`.trim(), '');
        swatch.setAttribute('aria-hidden', 'true');
        node.append(swatch, document.createTextNode(text));
        return node;
      }

      function labelWithControl(label, control) {
        const node = document.createElement('label');
        node.append(document.createTextNode(label));
        node.append(control);
        return node;
      }

      function renderMapBlock(block, options) {
        const settings = options || {};
        const map = block?.map ?? block ?? {};
        const blockClass = settings.blockClass === undefined ? 'block' : settings.blockClass;
        const node = el('section', `${blockClass} map-block`.trim());
        node.dataset.mapJson = JSON.stringify(map);
        node.append(el('h2', '', block?.title || map.title || 'Карта'));
        node.append(el('p', 'map-subtitle', 'Локальный атлас: уровни, слои, влияние фракций и точки интереса.'));

        const toolbar = el('div', 'map-toolbar');
        const zSelect = document.createElement('select');
        zSelect.className = 'map-z-filter';
        for (const level of map.zLevels ?? [{ z: 0, label: 'земля' }]) {
          zSelect.append(new Option(level.label ?? String(level.z), String(level.z ?? 0)));
        }
        const layerSelect = document.createElement('select');
        layerSelect.className = 'map-layer-filter';
        for (const layer of map.layers ?? [{ id: 'world', label: 'Мир' }]) {
          layerSelect.append(new Option(layer.label ?? layer.id, layer.id ?? 'world'));
        }
        toolbar.append(labelWithControl('Уровень', zSelect), labelWithControl('Слой', layerSelect));
        const politicalToggle = document.createElement('input');
        politicalToggle.type = 'checkbox';
        politicalToggle.className = 'map-political-toggle';
        politicalToggle.checked = true;
        toolbar.append(labelWithControl('Политическое влияние', politicalToggle));
        const zoomIn = el('button', 'secondary', 'Приблизить');
        const zoomOut = el('button', 'secondary', 'Отдалить');
        const reset = el('button', 'secondary', 'Сброс');
        for (const button of [zoomIn, zoomOut, reset]) button.type = 'button';
        toolbar.append(zoomIn, zoomOut, reset);
        node.append(toolbar);

        const legend = el('div', 'map-legend');
        legend.setAttribute('aria-label', 'Легенда карты');
        legend.append(
          el('strong', '', 'Легенда карты'),
          legendItem('current', 'Текущая точка'),
          legendItem('', 'Обычная точка'),
          legendItem('faction', 'Влияние фракций'),
          legendItem('contested', 'Спорная зона'));
        node.append(legend);

        const frame = el('div', 'map-atlas-frame');
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.classList.add('map-canvas');
        svg.setAttribute('role', 'img');
        svg.setAttribute('aria-label', map.title || 'Карта');
        svg.setAttribute('viewBox', '-20 -20 40 40');
        const empty = el('p', 'map-empty', 'Нет точек на выбранном уровне или слое.');
        empty.hidden = true;
        frame.append(svg, empty);
        node.append(frame);

        const card = el('aside', 'map-card', 'Выберите точку на карте.');
        node.append(card);

        let currentViewBox = [-20, -20, 40, 40];
        let dragStart = null;
        let selectedNodeId = map.currentNodeId ?? '';

        function draw() {
          const selectedZ = Number(zSelect.value || 0);
          const selectedLayer = layerSelect.value || 'world';
          const nodes = (map.nodes ?? []).filter(item => Number(item.z ?? 0) === selectedZ && ((item.layer ?? 'world') === selectedLayer));
          const nodeById = new globalThis.Map(nodes.map(item => [item.id, item]));
          svg.replaceChildren();
          node.dataset.layerState = nodes.length ? 'visible' : 'hidden';
          empty.hidden = nodes.length !== 0;

          const defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
          defs.innerHTML = '<filter id="atlas-texture-shared"><feTurbulence type="fractalNoise" baseFrequency="0.9" numOctaves="3" seed="12"/><feColorMatrix type="saturate" values="0"/></filter>';
          const texture = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
          texture.setAttribute('class', 'atlas-texture');
          texture.setAttribute('x', '-2000');
          texture.setAttribute('y', '-2000');
          texture.setAttribute('width', '4000');
          texture.setAttribute('height', '4000');
          texture.setAttribute('filter', 'url(#atlas-texture-shared)');
          svg.append(defs, texture);
          if (politicalToggle.checked) drawPoliticalOverlay(nodes);

          for (const link of map.links ?? []) {
            const source = nodeById.get(link.sourceNodeId);
            const target = nodeById.get(link.targetNodeId);
            if (!source || !target) continue;
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            line.setAttribute('x1', source.x ?? 0);
            line.setAttribute('y1', -(source.y ?? 0));
            line.setAttribute('x2', target.x ?? 0);
            line.setAttribute('y2', -(target.y ?? 0));
            line.setAttribute('class', `map-link ${link.state === 'dangerous' ? 'map-link--dangerous' : ''}`);
            line.setAttribute('stroke-width', '.16');
            svg.append(line);
          }

          for (const mapNode of nodes) {
            const group = document.createElementNS('http://www.w3.org/2000/svg', 'g');
            group.classList.add('map-node');
            if (mapNode.id === selectedNodeId) group.classList.add('map-node--selected');
            if (isContested(mapNode)) group.classList.add('map-node--contested');
            group.setAttribute('tabindex', '0');
            const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            circle.setAttribute('cx', mapNode.x ?? 0);
            circle.setAttribute('cy', -(mapNode.y ?? 0));
            circle.setAttribute('r', mapNode.isCurrent ? '.72' : '.5');
            circle.setAttribute('fill', mapNode.isCurrent ? '#285238' : mapNode.ownerFactionId ? '#80501f' : '#6a2d22');
            circle.setAttribute('stroke', '#f1d58b');
            circle.setAttribute('stroke-width', '.12');
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('x', Number(mapNode.x ?? 0) + .72);
            label.setAttribute('y', -Number(mapNode.y ?? 0) - .42);
            label.textContent = mapNode.label ?? mapNode.id ?? '?';
            group.append(circle, label);
            group.addEventListener('click', () => showMapNode(mapNode));
            group.addEventListener('keydown', event => {
              if (event.key === 'Enter' || event.key === ' ') showMapNode(mapNode);
            });
            svg.append(group);
          }

          fitMap(nodes);
        }

        function drawPoliticalOverlay(nodes) {
          const byId = new globalThis.Map(nodes.map(item => [item.id, item]));
          for (const region of map.regions ?? []) {
            const regionNodes = (region.nodeIds ?? []).map(id => byId.get(id)).filter(Boolean);
            if (!regionNodes.length) continue;
            const xs = regionNodes.map(item => Number(item.x ?? 0));
            const ys = regionNodes.map(item => -Number(item.y ?? 0));
            const cx = xs.reduce((a, b) => a + b, 0) / xs.length;
            const cy = ys.reduce((a, b) => a + b, 0) / ys.length;
            const radius = Math.max(2.2, ...regionNodes.map(item => Math.hypot(Number(item.x ?? 0) - cx, -Number(item.y ?? 0) - cy) + 1.6));
            const halo = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            halo.setAttribute('class', 'map-region');
            halo.setAttribute('cx', cx);
            halo.setAttribute('cy', cy);
            halo.setAttribute('r', radius);
            svg.append(halo);
            const label = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            label.setAttribute('class', 'map-region-label');
            label.setAttribute('x', cx - radius * .45);
            label.setAttribute('y', cy - radius * .72);
            label.textContent = region.ownerFactionName || region.label || region.ownerFactionId || '';
            svg.append(label);
          }
          for (const mapNode of nodes) {
            if (!mapNode.ownerFactionId && !mapNode.ownerFactionName && !Object.keys(mapNode.influence ?? {}).length) continue;
            const halo = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
            halo.setAttribute('class', `map-political-halo ${isContested(mapNode) ? 'map-political-halo--contested' : ''}`);
            halo.setAttribute('cx', mapNode.x ?? 0);
            halo.setAttribute('cy', -(mapNode.y ?? 0));
            halo.setAttribute('r', isContested(mapNode) ? '1.22' : '1.02');
            svg.append(halo);
          }
        }

        function isContested(mapNode) {
          const values = Object.values(mapNode.influence ?? {}).map(Number).filter(value => value >= 25).sort((a, b) => b - a);
          return values.length >= 2 && values[0] - values[1] <= 10;
        }

        function fitMap(nodes) {
          if (!nodes.length) {
            currentViewBox = [-20, -20, 40, 40];
            svg.setAttribute('viewBox', currentViewBox.join(' '));
            return;
          }
          const xs = nodes.map(item => Number(item.x ?? 0));
          const ys = nodes.map(item => -Number(item.y ?? 0));
          const minX = Math.min(...xs) - 3;
          const maxX = Math.max(...xs) + 9;
          const minY = Math.min(...ys) - 3;
          const maxY = Math.max(...ys) + 3;
          currentViewBox = [minX, minY, Math.max(10, maxX - minX), Math.max(10, maxY - minY)];
          svg.setAttribute('viewBox', currentViewBox.join(' '));
        }

        function zoom(factor) {
          const [x, y, width, height] = currentViewBox;
          const nextWidth = width * factor;
          const nextHeight = height * factor;
          currentViewBox = [x + (width - nextWidth) / 2, y + (height - nextHeight) / 2, nextWidth, nextHeight];
          svg.setAttribute('viewBox', currentViewBox.join(' '));
        }

        function showMapNode(mapNode) {
          selectedNodeId = mapNode.id ?? '';
          for (const item of svg.querySelectorAll('.map-node')) item.classList.remove('map-node--selected');
          const selected = [...svg.querySelectorAll('.map-node')].find(item => item.textContent === (mapNode.label ?? mapNode.id ?? '?'));
          if (selected) selected.classList.add('map-node--selected');
          card.replaceChildren();
          card.append(el('h3', '', mapNode.label ?? mapNode.id ?? 'Локация'));
          const dl = document.createElement('dl');
          const details = [...(mapNode.details ?? [])];
          if (mapNode.ownerFactionName || mapNode.ownerFactionId) {
            details.push({ key: 'Фракция', value: mapNode.ownerFactionName || mapNode.ownerFactionId });
          }
          details.push({ key: 'Уровень', value: String(mapNode.z ?? 0) });
          for (const item of details) {
            dl.append(el('dt', '', item.key ?? ''));
            dl.append(el('dd', '', item.value ?? ''));
          }
          card.append(dl);
        }

        zSelect.addEventListener('change', draw);
        layerSelect.addEventListener('change', draw);
        politicalToggle.addEventListener('change', draw);
        zoomIn.addEventListener('click', () => zoom(.8));
        zoomOut.addEventListener('click', () => zoom(1.25));
        reset.addEventListener('click', draw);
        svg.addEventListener('wheel', event => {
          event.preventDefault();
          zoom(event.deltaY < 0 ? .9 : 1.1);
        }, { passive: false });
        svg.addEventListener('pointerdown', event => {
          svg.setPointerCapture(event.pointerId);
          dragStart = { x: event.clientX, y: event.clientY, viewBox: [...currentViewBox] };
        });
        svg.addEventListener('pointermove', event => {
          if (!dragStart) return;
          const [, , width, height] = dragStart.viewBox;
          const dx = (event.clientX - dragStart.x) * width / Math.max(1, svg.clientWidth);
          const dy = (event.clientY - dragStart.y) * height / Math.max(1, svg.clientHeight);
          currentViewBox = [dragStart.viewBox[0] - dx, dragStart.viewBox[1] - dy, width, height];
          svg.setAttribute('viewBox', currentViewBox.join(' '));
        });
        svg.addEventListener('pointerup', () => { dragStart = null; });
        svg.addEventListener('pointercancel', () => { dragStart = null; });

        draw();
        const current = (map.nodes ?? []).find(item => item.id === map.currentNodeId);
        if (current) showMapNode(current);
        return node;
      }

      function mountStandalone(root) {
        const target = root || document.querySelector('[data-map-json]');
        if (!target) return null;
        const map = JSON.parse(target.dataset.mapJson || '{}');
        const rendered = renderMapBlock({ title: map.title, map }, { blockClass: '' });
        target.replaceChildren(rendered);
        return rendered;
      }

      global.BookOfEternityMapViewer = {
        renderMapBlock,
        mountStandalone
      };
    })(window);
    """;
}
