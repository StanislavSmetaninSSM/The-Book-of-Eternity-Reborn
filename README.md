# The Book of Eternity: Reborn

The Book of Eternity: Reborn is an unreleased, non-commercial dark-fantasy RPG
whose evolving world is driven by an external AI Game Master. The repository
contains the .NET 8 game runtime, console client, React Browser Client, game
contracts, examples, and development tooling. Public source availability is
not a release, stability promise, or save-compatibility promise.

> **Current version: 0.5 Pre-Alpha**
>
> ⚠️ Use and play entirely at your own risk. Until release, the game’s
> functionality is not guaranteed: it may work partially or not work at all.
> Save compatibility between pre-release versions is not supported. Any update
> may make existing saves unusable.

> **Текущая версия: 0.5 Pre-Alpha**
>
> ⚠️ Запускайте игру и играйте исключительно на свой страх и риск. До релиза
> работоспособность игры не гарантируется: она может работать частично или не
> работать вообще. Совместимость сохранений между версиями до релиза не
> поддерживается. Любое следующее обновление может сделать старые сохранения
> непригодными.

## О проекте

«The Book of Eternity: Reborn» — ещё не вышедшая некоммерческая ролевая игра
в жанре тёмного фэнтези. Внешний ИИ-ведущий формирует повествование, а клиент
проверяет, материализует и безопасно сохраняет состояние живого мира.

## Текущая концепция

ИИ-ведущий задаёт семантику сцены: повествование, намерения персонажей и
содержательные последствия действий. Клиент не заменяет ведущего, но остаётся
источником технической истины: валидирует входные данные и ответы ведущего,
применяет разрешённые изменения к каноническому состоянию мира и сохраняет их.
Это разделение поддерживает живое повествование вместе с проверяемыми
контрактами и безопасным состоянием игры.

## Клиенты

- Консольный клиент — основной .NET 8 интерфейс для локального запуска и
  работы с внешней командой ИИ-ведущего.
- Browser Client — React-клиент для локальной браузерной оболочки поверх тех
  же игровых контрактов.

Оба клиента работают через уже настроенную совместимую внешнюю команду
ИИ-ведущего. Репозиторий не встраивает и не обещает поддержку какого-либо
конкретного внешнего провайдера.

## Структура репозитория

- `BookOfEternityClient/` — .NET 8 игровой runtime, консольный клиент и
  локальные игровые данные.
- `BookOfEternityClient.WebFrontend/` — React Browser Client.
- `BookOfEternityClient.Tests/` и `BookOfEternityClient.IntegrationTests/` —
  автоматические проверки.
- `BookOfEternityGMBridge/` — граница интеграции с внешним ИИ-ведущим.
- `Rules/`, `TaskGuides/`, `Examples/` и `OtherGuides/` — игровые материалы,
  контракты, примеры и справочные документы.
- `scripts/` и `Tools/` — средства разработки и проверки.

## Предварительные требования

- .NET 8 SDK;
- Node.js и npm для разработки фронтенда;
- PowerShell 7;
- отдельно настроенная совместимая внешняя команда ИИ-ведущего.

## Быстрый старт

```powershell
dotnet run --project BookOfEternityClient
npm ci --prefix BookOfEternityClient.WebFrontend
npm run dev:local --prefix BookOfEternityClient.WebFrontend
```

Перед первым запуском настройте внешнюю команду ИИ-ведущего в соответствии с
локальной конфигурацией проекта. Не передавайте учётные данные в issues,
коммиты или примеры.

## Ограниченная проверка

Используйте ограниченный тестовый runner проекта, а не неограниченный запуск
всего решения:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~RepositoryPublicationDocumentationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

## Вклад в проект

Порядок работы, требования к проверкам и правило обязательной задачи описаны
в [CONTRIBUTING.md](CONTRIBUTING.md). Любое изменение репозитория должно быть
привязано к отслеживаемой задаче (tracked GitHub Issue) до начала реализации.

## Лицензии и исключения

- Программный код и скрипты лицензированы по
  [GNU AGPL-3.0-or-later](LICENSE).
- Оригинальные проектные мир, сюжет, лор, персонажи, правила в прозе,
  диалоги, примеры и иной не-кодовый игровой текст лицензированы по
  [CC BY-NC-SA 4.0](CONTENT_LICENSE.md), если рядом не указано иное.
- Музыка, сторонние работы и исключённые ассеты не получают автоматически ни
  одну из этих лицензий; их границы и происхождение перечислены в
  [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Полные условия и исключения следует читать вместе: [LICENSE](LICENSE),
[CONTENT_LICENSE.md](CONTENT_LICENSE.md) и
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Copyright © 2026 Stanislav Smetanin (Lottarend)
