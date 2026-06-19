import { type CSSProperties, useEffect } from 'react';
import { AnimatePresence, motion, MotionConfig } from 'framer-motion';
import './styles.css';
import { ConnectionBanner } from './components/ConnectionBanner';
import { ErrorNotice } from './components/ErrorNotice';
import { LoadingCard } from './components/LoadingCard';
import { TabBar } from './components/TabBar';
import { SceneView } from './components/SceneView';
import { QtePracticeView } from './components/QtePracticeView';
import { DarenShowcaseView } from './components/DarenShowcaseView';
import { StatusView } from './components/StatusView';
import { HelpView } from './components/HelpView';
import { SettingsView } from './components/SettingsView';
import { UnifiedInput } from './components/UnifiedInput';
import { GameLauncher } from './components/GameLauncher';
import { ShellProvider, useShell, type TabId } from './context/ShellContext';
import { VignetteOverlay } from './components/decorative';
import { pageTransition } from './lib/motion';

export default function App() {
  return (
    <ShellProvider>
      <AppShell />
    </ShellProvider>
  );
}

function AppShell() {
  const { activeRoute, advancedEnabled, clientSettings, menu, readyState, realmTheme, shellState, activeTab } = useShell();
  const isLauncherRoute = activeRoute === 'home' && menu !== null;
  const isPracticeRoute = activeRoute === 'practice';
  const isDarenShowcaseRoute = activeRoute === 'daren-showcase';
  const reducedMotion = Boolean(clientSettings?.accessibility.reducedMotion);
  // Mirror the reduced-motion flag onto the document root so that full-viewport
  // decorative layers (VignetteOverlay / film-grain) — which render outside the
  // .browser-shell subtree — also pick up the .is-reduced-motion CSS guards.
  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle('is-reduced-motion', reducedMotion);
    return () => root.classList.remove('is-reduced-motion');
  }, [reducedMotion]);
  // Publish the font/ui scale on the document root. The root font-size rule in
  // layout.css turns --browser-font-scale into the rem unit, so EVERY rem-based
  // size across the app (cards, QTE, Daren prose, …) scales with the setting —
  // not only elements that inherit the .browser-shell font-size.
  const fontScalePercent = clientSettings?.accessibility.fontScalePercent ?? 100;
  const uiScalePercent = clientSettings?.accessibility.uiScalePercent ?? 100;
  useEffect(() => {
    const root = document.documentElement;
    root.style.setProperty('--browser-font-scale', `${fontScalePercent / 100}`);
    root.style.setProperty('--browser-ui-scale', `${uiScalePercent / 100}`);
  }, [fontScalePercent, uiScalePercent]);
  const browserShellClassName = [
    'browser-shell',
    isLauncherRoute ? 'is-launcher-route' : '',
    reducedMotion ? 'is-reduced-motion' : '',
    clientSettings?.accessibility.contrastFriendly ? 'is-contrast-friendly' : ''
  ].filter(Boolean).join(' ');
  const browserShellStyle = {
    '--browser-font-scale': `${(clientSettings?.accessibility.fontScalePercent ?? 100) / 100}`,
    '--browser-ui-scale': `${(clientSettings?.accessibility.uiScalePercent ?? 100) / 100}`
  } as CSSProperties;

  return (
    <MotionConfig reducedMotion={reducedMotion ? 'always' : 'never'}>
      <>
        <VignetteOverlay />
        <main className={browserShellClassName} data-theme-key={realmTheme.key} style={browserShellStyle}>
          <ConnectionBanner />
          {!isLauncherRoute && <TabBar />}
          <section className={`content-area${isLauncherRoute ? ' content-area--launcher' : ''}`} aria-live="polite">
            <AnimatePresence mode="wait">
              <motion.div
                key={`${activeRoute}-${activeTab}`}
                initial="initial"
                animate="enter"
                exit="exit"
                variants={pageTransition}
              >
                {shellState.status === 'loading' && <LoadingCard />}
                {shellState.status === 'error' && <ErrorNotice title="Книга сейчас недоступна" failure={shellState} advancedEnabled={advancedEnabled} />}
                {readyState && (isLauncherRoute ? <GameLauncher menu={menu} /> : isDarenShowcaseRoute ? <DarenShowcaseView /> : <TabContent activeTab={activeTab} />)}
              </motion.div>
            </AnimatePresence>
          </section>
          {!isLauncherRoute && !isPracticeRoute && !isDarenShowcaseRoute && <UnifiedInput />}
        </main>
      </>
    </MotionConfig>
  );
}

function TabContent({ activeTab }: { activeTab: TabId }) {
  switch (activeTab) {
    case 'scene': return <SceneView />;
    case 'practice': return <QtePracticeView />;
    case 'status': return <StatusView />;
    case 'help': return <HelpView />;
    case 'settings': return <SettingsView />;
  }
}
