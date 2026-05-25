import { browserApiContractSummary } from './api/client';

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
    title: 'Подключено к #703',
    text: 'React-компоненты получают BrowserApiClient и DTO-типы из единого TypeScript contract layer.'
  }
];

const contractEndpointCards = browserApiContractSummary.endpointDocs.map((endpoint) => ({
  title: endpoint.id,
  text: `${endpoint.method} · ${endpoint.response} · ${endpoint.playerSurface}`
}));

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
          <h2>Типизированный контракт</h2>
          <p>Issue #703 добавляет единый BrowserApiClient, DTO-типы и fixture guards между C# и TypeScript.</p>
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

      <section className="roadmap" aria-label="Типизированные API-контракты">
        {contractEndpointCards.map((section) => (
          <article className="roadmap-card" key={section.title}>
            <h2>{section.title}</h2>
            <p>{section.text}</p>
          </article>
        ))}
      </section>
    </main>
  );
}
