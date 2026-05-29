const fsModuleName = 'node:fs';
const { readFileSync } = await import(fsModuleName);

const sidebarSource = readSource('../../../BookOfEternityClient.WebFrontend/src/components/PlayerStatusSidebar.tsx');
const appSource = readSource('../../../BookOfEternityClient.WebFrontend/src/App.tsx');
const layoutSource = readSource('../../../BookOfEternityClient.WebFrontend/src/styles/layout.css');
const componentsSource = readSource('../../../BookOfEternityClient.WebFrontend/src/styles/components.css');

assertIncludes(sidebarSource, '<p className="panel-eyebrow sidebar-title">Сводка</p>');
assertExcludes(sidebarSource, '<div className="sidebar-heading">');
assertExcludes(sidebarSource, '<h2>Сводка книги</h2>');
for (const copy of [
  'Мягкая сводка текущей главы без служебных журналов и внутренних проверок.',
  'Это обычное состояние пустой книги, не ошибка клиента.',
  'Глава сохранена; подробности ремонта и проверки остаются в расширенном режиме.',
  'Подробности ремонта, проверки и команд скрыты до явного включения.',
  'Когда появится ожидающий ход или ответ ГМа, книга покажет это здесь игровым языком.',
  'Служебные проверки и сведения для ремонта остаются вторичным режимом.',
  'Герой и душа появятся снова, когда локальная книга отдаст игровую сводку.'
]) {
  assertExcludes(sidebarSource, copy);
}

assertIncludes(appSource, 'useState');
assertIncludes(appSource, 'const [sidebarOpen, setSidebarOpen] = useState(false);');
assertIncludes(appSource, 'className="sidebar-toggle"');
assertIncludes(appSource, "className={`workspace-sidebar${sidebarOpen ? ' is-open' : ''}`}");

assertIncludes(layoutSource, '@media (max-width: 900px)');
assertIncludes(layoutSource, '.workspace-sidebar.is-open');
assertIncludes(layoutSource, 'transform: translateX(100%);');

assertIncludes(componentsSource, '.sidebar-toggle');
assertIncludes(componentsSource, 'display: none;');
assertIncludes(componentsSource, 'width: 44px;');

function readSource(relativePath: string): string {
  return readFileSync(new URL(relativePath, import.meta.url), 'utf8');
}

function assertIncludes(source: string, expected: string) {
  if (!source.includes(expected)) {
    throw new Error(`Expected source to include: ${expected}`);
  }
}

function assertExcludes(source: string, unexpected: string) {
  if (source.includes(unexpected)) {
    throw new Error(`Expected source to exclude: ${unexpected}`);
  }
}
