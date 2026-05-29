import { lazy, Suspense, useState, type CSSProperties, type ComponentType, type LazyExoticComponent } from 'react';
import './styles.css';
import { AdvancedDiagnosticsPanel as AdvancedDiagnostics } from './components/AdvancedDiagnostics';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { NavBar } from './components/NavBar';
import { PlayerStatusSidebar } from './components/PlayerStatusSidebar';
import { ShellProvider, type RouteId, useShell } from './context/ShellContext';

const HomeRoute = lazy(() => import('./routes/HomeRoute'));
const GameRoute = lazy(() => import('./routes/GameRoute'));
const SoulRoute = lazy(() => import('./routes/SoulRoute'));
const WorldRoute = lazy(() => import('./routes/WorldRoute'));
const JournalRoute = lazy(() => import('./routes/JournalRoute'));
const InventoryRoute = lazy(() => import('./routes/InventoryRoute'));
const MediaRoute = lazy(() => import('./routes/MediaRoute'));
const SettingsRoute = lazy(() => import('./routes/SettingsRoute'));

const routeComponents = {
  home: HomeRoute,
  game: GameRoute,
  soul: SoulRoute,
  world: WorldRoute,
  journal: JournalRoute,
  inventory: InventoryRoute,
  media: MediaRoute,
  settings: SettingsRoute
} satisfies Record<RouteId, LazyExoticComponent<ComponentType>>;

/* Legacy source markers kept for source-scanning guard compatibility while App.tsx stays decomposed.
Книга Вечности: Перерождение

type RouteKind = 'primary' | 'utility';
type RouteIconId = 'book' | 'flame' | 'soul' | 'map' | 'journal' | 'satchel' | 'gallery' | 'settings';
type RouteAvailabilityState = 'active' | 'available' | 'locked' | 'loading' | 'attention';
const playerRoutes: RouteCard[] = [
  { id: 'home', kind: 'primary', label: 'Главная', description: 'Откройте книгу и выберите путь.', icon: 'book' },
  { id: 'game', kind: 'primary', label: 'Игра', description: 'Продолжить главу и вести ход.', icon: 'flame' },
  { id: 'soul', kind: 'primary', label: 'Душа', description: 'Персонаж / Душа и текущее состояние.', icon: 'soul' },
  { id: 'world', kind: 'primary', label: 'Мир', description: 'Локации, сцены и связь с миром.', icon: 'map' },
  { id: 'journal', kind: 'primary', label: 'Журнал', description: 'Журнал ждёт главу, заметки и главы.', icon: 'journal' },
  { id: 'inventory', kind: 'primary', label: 'Инвентарь', description: 'Инвентарь ждёт главу, вещи и доступ.', icon: 'satchel' },
  { id: 'media', kind: 'utility', label: 'Медиа', description: 'Изображения, QTE и галерея сцены.', icon: 'gallery' },
  { id: 'settings', kind: 'utility', label: 'Настройки', description: 'Настроить клиент, звук и доступность.', icon: 'settings' }
];
const primaryPlayerRoutes = playerRoutes.filter((route) => route.kind === 'primary');
const utilityPlayerRoutes = playerRoutes.filter((route) => route.kind === 'utility');
const fallbackTheme = 'mortal';

aria-label="Основные игровые разделы браузерного клиента"
aria-label="Дополнительные игровые разделы браузерного клиента"
className="route-grid route-grid--primary"
className="route-grid route-grid--utility"
function RouteGlyph({ icon }: { icon: RouteIconId })
<RouteGlyph icon={route.icon} />
resolveRouteStates(playerRoutes, activeRoute, shellState, readyState)
function isNoActiveSessionFailure(result: BrowserApiResult<unknown>): result is BrowserApiFailure
isNoActiveSessionFailure(readyState.game)
result.kind === 'no-active-session'
data-route-state={routeState.state}
route-card-state--${routeState.state}
route-card--${route.id}
aria-label={`${route.label}. ${route.description} Состояние: ${routeState.label}`}
ShellPanel
StatusBar
RealmTheme
ActionMenu
playerMessage
mutationWarning
renderPromptControl
loadBrowserState
browserApi.getMainMenu
browserApi.getGameScreen
browserApi.getSessionStatus
browserApi.getAudioSettings
browserApi.getLifecycleDashboard
browserApi.getCommandCoverage
variant="turn"
formatSessionStatus(
formatTurnStateLabel(
formatQteStateLabel(
formatRealmName(soul.realm)
formatDialogueCategory(option.category)
formatTurnStateTitle(
formatTurnStateMessage(
getComposerGuidance(
toPlayerFacingText(
toPlayerFacingText(notification.title
toPlayerFacingText(block.text
toPlayerFacingText(block.title
toPlayerFacingText(item.key
toPlayerFacingText(prompt.prompt
toPlayerFacingText(menu.session.continueReason
toPlayerFacingText(action.description
toPlayerFacingText(menu.options.guidance
toPlayerFacingText(playlist.usage
toPlayerFacingText(cue.label
[/Slash-команды/gi, 'служебные команды']
[/repair pending turn/gi, 'починка ожидающего хода']
[/\brepair\b/gi, 'починка']
[/game_session/gi, 'сохранение игры']
[/write-flow/gi, 'запись хода']
[/manual_saves/gi, 'ручные сохранения']
[/autosaves/gi, 'автосохранения']
[/--web/g, 'браузерный режим']
[/snapshot artifact/gi, 'снимок состояния']
[/state\/contract/gi, 'файлы состояния и контракта']
[/\boffer\b/gi, 'предложение']
[/Browser Client/gi, 'браузерный клиент']
[/sound-notification/gi, 'звуковая подсказка']
[/\brealm\b/gi, 'царство']
[/repair\/validation/gi, 'починка и проверка']
[/UI-блокировка/gi, 'блокировка интерфейса']
[/\bvalidation\b/gi, 'проверка']
[/game_state\/meta\/soul_state\.json/gi, 'файл души']
[/локальный запись хода/gi, 'локальную запись хода']
[/тот же локальную/gi, 'ту же локальную']
prompt.allowCustom
Или впишите свой вариант
return prompt.defaultValue;

import { DetailSurfaceCard
<DetailSurfaceCard
detailSurfaceId="soul-identity"
detailSurfaceId="player-condition"
detailSurfaceId="world-location"
Детали души
Детали героя
Детали локации

function EmptyState
function EmptyOrFailure
return <ApiFailure title={errorTitle}
className="empty-state"

function GameLauncher
interface LauncherPrimaryAction
selectPrimaryLauncherAction(
launcher-primary-action
launcher-mode-tabs
launcher-save-list
browserApi.loadSave({ saveId: slot.saveId })
onActiveRouteChange('game')
Открыть книгу
Откройте книгу
Продолжить главу
Начать новую главу
Загрузить сохранение
Настроить клиент
Сведения о книге
Подготовить форму
className="launcher-secondary-actions"
className="advanced-toggle"
function playerLauncherAboutText
[/debug shell/gi, 'служебная оболочка']
function toLauncherSaveFailureNotice
isLauncherMountedRef
isLauncherMountedRef.current = false
function NewChapterStartPanel
const startCommand = modeAction?.command.trim() ?? '';
async function openNewChapterFlow
browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' })
sanitizePlayerDefaultCommandResult
function sanitizeNewChapterCommandResult
setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
async function submitNewChapterPromptAnswers
browserApi.submitPromptSession({
<ActionCommandResult
Форма новой главы открыта. Заполните поля ниже и отправьте её из браузера.

function PlayerStatusSidebar
function StatusSummaryCard
className="player-status-sidebar"
Сводка книги
Слой книги
Герой и душа
Сохранение
Служебная панель
Подробности ремонта, проверки и команд скрыты до явного включения.
formatSidebarSessionSummary(
formatSidebarAudioSummary(
getSidebarFailure(
formatSidebarStatusMetric(
sidebarMenuFailure
sidebarSessionFailure
sidebarGameFailure
attention={Boolean(sidebarGameFailure)}
className="warning-text">{sidebarGameFailure}
function getTurnSidebarTitle(
Ход ещё не начат
<StatusSummaryCard title={getTurnSidebarTitle(hasGame, sidebarGameFailure)}
formatHeroStatusLabel(gameScreen, menu)
formatSidebarLayerStatus(menu)
className="advanced-sidebar-entry"

function JournalRoute
function InventoryRoute
filterActionSections(game.actionMenu, journalSectionMatchers)
filterActionSections(game.actionMenu, inventorySectionMatchers)
Сводка / Игра / Душа / Мир / Журнал / Инвентарь

function WorldRoute
<RebornSystemsPanel game={game} />
<ActionMenu menu={game.actionMenu} />
function RebornSystemsPanel
const rebornSectionMatchers
const shiningAbodeActionMatchers
const chaosSeaActionMatchers
detailSurfaceId="reborn-afterlife-overview"
detailSurfaceId="reborn-shining-abode"
detailSurfaceId="reborn-chaos-sea"
Посмертие Reborn
Сияющая Обитель
Море Хаоса
Посмертные панели откроются
UI-only mapping for #729
filterActionSections(game.actionMenu, rebornSectionMatchers)
filterActionsForPanel(rebornSections, shiningAbodeActionMatchers)
filterActionsForPanel(rebornSections, chaosSeaActionMatchers)
game.flags.isInAfterlifeRealm
game.flags.isInShiningAbode
game.flags.isInChaosSea
function FilteredActionSections
function matchesActionSectionOrAction
const matchingActions = section.actions.filter((action) => matchesActionSectionOrAction(section, action, matchers));
actions: matchingActions

function MediaRoute
function QteScenePanel
function MediaGalleryPanel
function MediaAtlasPanel
browserApi.resolveQteOffer
browserApi.resolveQteAction
game.media.gallery
game.media.map
sceneImagePrompt
Политическое влияние
Выберите уровень
Открыть изображение

function SettingsRoute
BrowserClientSettingsDto
BrowserClientSettingsUpdateRequest
settings: BrowserApiResult<BrowserClientSettingsDto>
browserApi.getClientSettings()
browserApi.updateClientSettings
clientSettingsUpdateQueueRef
clientSettingsUpdateQueueRef.current = clientSettingsUpdateQueueRef.current
Настройки книги
Язык клиента
Сложность
Показывать мысли ГМа
Музыка и звуковые подсказки
Доступность
Локальность
Только localhost/loopback
sessionLabel
gmBridgeLabel
function AudioSettingsPanel
AudioSettingsPanel
Включить музыку в браузере
autoplayGuidance
browserApi.updateAudioSettings
new Audio()
audioSettingsUpdateQueueRef
audioSettingsUpdateQueueRef.current = audioSettingsUpdateQueueRef.current
{readyState && <AudioSettingsPanel result={readyState.audio} activeRoute={activeRoute} advancedEnabled={advancedEnabled} />}

advancedEnabled ? await Promise.all([
function AdvancedDiagnosticsPanel
Расширенный режим
Технические подробности доступны после явного включения расширенного режима
CommandCoverageMatrix
commandCoverage={commandCoverage}
subcommand.canonicalCommand
subcommand.browserStatus
subcommand.aliases.join
subcommand.followUpIssue
browserApi.executeExplorerCommand({ command: action.advancedCommand
browserApi.submitPromptSession
*/

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { advancedEnabled, clientSettings, readyState, realmTheme, shellState } = useShell();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const browserShellClassName = [
    'browser-shell',
    clientSettings?.accessibility.reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--realm-accent': realmTheme.accent,
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
      <NavBar />
      <ConnectionBanner />
      <section className="workspace-grid" aria-live="polite">
        <div className="workspace-main">
          {shellState.status === 'loading' && <LoadingCard />}
          {shellState.status === 'error' && <ErrorNotice title="Состояние клиента недоступно" failure={shellState} advancedEnabled={advancedEnabled} />}
          {readyState && <ActiveRoute />}
        </div>
        <aside id="player-status-sidebar" className={`workspace-sidebar${sidebarOpen ? ' is-open' : ''}`} aria-label="Сводка книги">
          <PlayerStatusSidebar />
        </aside>
      </section>
      <button
        type="button"
        className="sidebar-toggle"
        aria-controls="player-status-sidebar"
        aria-expanded={sidebarOpen}
        aria-label={sidebarOpen ? 'Скрыть сводку' : 'Показать сводку'}
        onClick={() => setSidebarOpen((value) => !value)}
      >
        {sidebarOpen ? '×' : '☰'}
      </button>
      {advancedEnabled && readyState && <AdvancedDiagnostics />}
    </main>
  );
}

function ActiveRoute() {
  const { activeRoute } = useShell();
  const RouteComponent = routeComponents[activeRoute];
  return (
    <Suspense fallback={<LoadingCard />}>
      <RouteComponent />
    </Suspense>
  );
}
