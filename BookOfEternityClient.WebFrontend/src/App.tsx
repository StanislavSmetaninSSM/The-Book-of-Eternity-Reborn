const roadmapSections = [
  {
    title: 'Главное меню',
    text: 'Игрок начинает с Continue, New Game, Load, Options и About, а не с перечня endpoint-ов.'
  },
  {
    title: 'Игровой экран',
    text: 'Нарратив, состояние души, realm, QTE и ожидание ГМа будут читаться из локального C# API.'
  },
  {
    title: 'Расширенный режим',
    text: 'Командная палитра и диагностика остаются доступными только как явный технический слой.'
  },
  {
    title: 'Подключено к #702',
    text: 'C# host отдаёт production build из `dist/`, а при его отсутствии использует извлечённую player-facing оболочку.'
  }
];

export default function App() {
  return (
    <main className="app-shell" aria-labelledby="browser-client-title">
      <section className="hero-card">
        <p className="eyebrow">Book of Eternity Reborn · Browser Client</p>
        <h1 id="browser-client-title">Локальный игровой клиент</h1>
        <p className="lead">
          Это новая Vite + React + TypeScript основа для браузерного клиента. C# API остаётся источником истины:
          TypeScript отвечает за представление, навигацию и состояние запросов, но не за правила игры.
        </p>
      </section>

      <section className="principles" aria-label="Границы фронтенда">
        <article>
          <h2>Локально и безопасно</h2>
          <p>Клиент предназначен для loopback/localhost-сценария и будет потреблять те же локальные DTO, что обслуживает `--web`.</p>
        </article>
        <article>
          <h2>Presentation-only</h2>
          <p>Игровая логика, сохранения, afterlife/mortal контракты, команды и валидация остаются в C# runtime.</p>
        </article>
        <article>
          <h2>Подключено к #702</h2>
          <p>`--web` теперь отдаёт standalone frontend assets через `LocalWebUiHost`, сохраняя C# API источником истины.</p>
        </article>
      </section>

      <section className="roadmap" aria-label="Первые игровые разделы">
        {roadmapSections.map((section) => (
          <article className="roadmap-card" key={section.title}>
            <h2>{section.title}</h2>
            <p>{section.text}</p>
          </article>
        ))}
      </section>
    </main>
  );
}
