import { ShellPanel } from './ShellPanel';
import { LoadingShimmer } from './decorative';

export function LoadingCard() {
  return (
    <ShellPanel title="Открываем книгу" eyebrow="главная книга">
      <p>Готовим главное меню, сохранения, сцену и состояние главы…</p>
      <LoadingShimmer hasImage lines={4} />
    </ShellPanel>
  );
}
