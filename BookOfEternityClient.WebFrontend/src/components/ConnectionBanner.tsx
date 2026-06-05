import { useShell } from '../context/ShellContext';

export function ConnectionBanner() {
  const { connectionStatus, loadBrowserState } = useShell();

  if (connectionStatus === 'connected') {
    return null;
  }

  const isDisconnected = connectionStatus === 'disconnected';
  const message = isDisconnected
    ? 'Книга недоступна. Проверьте, что игра запущена.'
    : 'Некоторые разделы не загрузились. Часть данных может быть неактуальна.';

  return (
    <div className={`connection-banner ${isDisconnected ? 'is-disconnected' : 'is-partial'}`} role="alert">
      <span>{message}</span>
      <button type="button" onClick={() => void loadBrowserState()}>
        Повторить
      </button>
    </div>
  );
}
