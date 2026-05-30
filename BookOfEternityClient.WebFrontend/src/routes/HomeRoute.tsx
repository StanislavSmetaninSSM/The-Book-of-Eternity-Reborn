import { EmptyOrFailure } from '../components/ErrorNotice';
import { GameLauncher } from '../components/GameLauncher';
import { SceneHero } from '../components/SceneHero';
import { ShellPanel } from '../components/ShellPanel';
import { isSuccess, useShell } from '../context/ShellContext';

export default function HomeRoute() {
  const { advancedEnabled, readyState } = useShell();

  if (!readyState) {
    return null;
  }

  if (!isSuccess(readyState.menu)) {
    return (
      <EmptyOrFailure
        result={readyState.menu}
        advancedEnabled={advancedEnabled}
        errorTitle="Главная книга требует внимания"
        empty={{
          title: 'Главное меню книги пока не готово',
          message: 'Браузер ждёт ответ локальной книги с безопасными стартовыми действиями.',
          action: 'Проверьте, что локальный клиент запущен; если проблема повторится, откройте «Расширенный режим».'
        }}
      />
    );
  }

  return (
    <ShellPanel title="Главная книга" eyebrow="старт и продолжение партии">
      <SceneHero
        eyebrow="Книга Вечности"
        title="Перерождение"
        subtitle="Бесконечное странствие души через жизни, смерти и перерождения"
      />
      <GameLauncher menu={readyState.menu.data} />
    </ShellPanel>
  );
}
