import type { BrowserApiResult, BrowserCommandCoverageDto } from '../api/contracts';
import { browserApiContractSummary } from '../api/client';
import { isSuccess, useShell } from '../context/ShellContext';
import { toPlayerFacingText } from '../utils/playerCopy';
import { ShellPanel } from './ShellPanel';

const browserApiEndpoints = browserApiContractSummary.endpointDocs;

export function AdvancedDiagnosticsPanel() {
  const { advancedEnabled, readyState } = useShell();

  if (!advancedEnabled || !readyState) {
    return null;
  }

  const lifecycle = readyState.lifecycle && isSuccess(readyState.lifecycle) ? readyState.lifecycle.data : null;
  const commandCoverage = readyState.commandCoverage;

  return (
    <section className="advanced-diagnostics" id="advanced-diagnostics" aria-label="Расширенный режим">
      <div>
        <p className="eyebrow">Технический режим</p>
        <h2>Расширенный режим</h2>
        <p>Диагностика команд, проверка состояния и сведения для ремонта. Обычный игрок не обязан видеть эти детали.</p>
      </div>
      <div className="split-grid three">
        <ApiResultCard title={getEndpointLabel('BrowserMainMenuDto')} result={readyState.menu} />
        <ApiResultCard title={getEndpointLabel('LocalWebUiSessionStatus')} result={readyState.session} />
        <ApiResultCard title={getEndpointLabel('BrowserGameScreenDto')} result={readyState.game} />
      </div>
      <ShellPanel title="Контракт локального интерфейса" eyebrow="типизированная схема" nested>
        <ul className="endpoint-list">
          {browserApiEndpoints.map((apiEndpoint) => (
            <li key={apiEndpoint.path}>
              <strong>{apiEndpoint.path}</strong>
              <span>{apiEndpoint.method} · {apiEndpoint.response} · {apiEndpoint.playerSurface}</span>
            </li>
          ))}
        </ul>
      </ShellPanel>
      <CommandCoverageMatrix result={commandCoverage} />
      {lifecycle && (
        <ShellPanel title="Панель состояния" eyebrow="проверка" nested>
          <p>Статус: {lifecycle.validation.statusLabel}</p>
          <p>Ошибки: {lifecycle.validation.errorCount}; предупреждения: {lifecycle.validation.warningCount}</p>
          {lifecycle.validation.groups.length > 0 && (
            <ul className="endpoint-list validation-group-list" aria-label="Группы проверки состояния">
              {lifecycle.validation.groups.map((group) => (
                <li key={`${group.severity}-${group.category}-${group.section}`}>
                  <strong>{group.severity} · {group.category}</strong>
                  <span>{group.section} · {group.count}</span>
                </li>
              ))}
            </ul>
          )}
          {lifecycle.validation.issues.length > 0 && (
            <details>
              <summary>Подробности проверки</summary>
              <ul className="endpoint-list validation-issue-list">
                {lifecycle.validation.issues.map((issue, index) => (
                  <li key={`${issue.filePath}-${issue.code}-${index}`}>
                    <strong>{issue.filePath}</strong>
                    <span>{issue.severity} · {issue.category} · {issue.section}</span>
                    <span>{issue.message}</span>
                    <span>Ожидалось: {issue.expected || '—'} · Сейчас: {issue.actual || '—'}</span>
                    <span>Исправление: {issue.repairHint || '—'}</span>
                  </li>
                ))}
              </ul>
            </details>
          )}
        </ShellPanel>
      )}
    </section>
  );
}

function CommandCoverageMatrix({ result }: { result: BrowserApiResult<BrowserCommandCoverageDto> | null }) {
  if (!result) {
    return (
      <ShellPanel title="Покрытие команд" eyebrow="паритет браузера" nested>
        <p className="muted">Матрица команд загружается только после включения расширенного режима.</p>
      </ShellPanel>
    );
  }

  if (!isSuccess(result)) {
    return (
      <ShellPanel title="Покрытие команд" eyebrow="паритет браузера" nested>
        <p className="warning-text">{toPlayerFacingText(result.playerMessage, 'Матрица покрытия команд сейчас недоступна.')}</p>
      </ShellPanel>
    );
  }

  const coverage = result.data;
  return (
    <ShellPanel title="Покрытие команд Explorer" eyebrow={`схема ${coverage.schemaVersion}`} nested>
      <p>
        Дескрипторы: {coverage.summary.descriptorCount}; псевдонимы: {coverage.summary.aliasCount};
        подкоманды: {coverage.summary.subcommandCount}; готово для браузера: {coverage.summary.browserExecutableCount}.
      </p>
      <ul className="endpoint-list command-coverage-list" aria-label="Матрица покрытия команд Explorer">
        {coverage.commands.map((command) => (
          <li key={command.id}>
            <strong>{command.primaryActionLabel} · {command.id}</strong>
            <span>{command.surface} · {command.uxDecision} · {command.browserStatus} · {command.formMode}</span>
            <span>{command.group} · {command.mutationMode} · {command.handlerKind}</span>
            <span>Команда: {command.primaryCommand}; псевдонимы: {command.aliases.join(', ')}</span>
            <span>Аудит: {command.auditStatus} · тестовые данные: {command.sampleDataStatus}</span>
            <span>Браузер: {command.browserEvidence}</span>
            <span>Консоль: {command.consoleEvidence}</span>
            <span>Паритет: {command.parityNotes}</span>
            <span>Читаемость: {command.readabilityNotes}</span>
            <span>Разрыв: {command.gapSummary}</span>
            {command.subcommands.length > 0 && (
              <ul className="endpoint-list command-subcoverage-list" aria-label={`Подкоманды ${command.id}`}>
                {command.subcommands.map((subcommand) => (
                  <li key={subcommand.id}>
                    <strong>{subcommand.primaryActionLabel} · {subcommand.id}</strong>
                    <span>{subcommand.surface} · {subcommand.uxDecision} · {subcommand.browserStatus} · {subcommand.formMode}</span>
                    <span>{subcommand.group} · {subcommand.mutationMode} · {subcommand.handlerKind}</span>
                    <span>Команда: {subcommand.canonicalCommand}; псевдонимы: {subcommand.aliases.join(', ')}</span>
                    <span>Аудит: {subcommand.auditStatus} · тестовые данные: {subcommand.sampleDataStatus}</span>
                    <span>Браузер: {subcommand.browserEvidence}</span>
                    <span>Консоль: {subcommand.consoleEvidence}</span>
                    <span>Паритет: {subcommand.parityNotes}</span>
                    <span>Читаемость: {subcommand.readabilityNotes}</span>
                    <span>Разрыв: {subcommand.gapSummary}</span>
                    {(subcommand.followUpIssue || subcommand.reason) && (
                      <span>{subcommand.followUpIssue || 'следующий шаг не указан'} · {subcommand.reason || 'причина не указана'}</span>
                    )}
                  </li>
                ))}
              </ul>
            )}
            {(command.followUpIssue || command.reason) && (
              <span>{command.followUpIssue || 'следующий шаг не указан'} · {command.reason || 'причина не указана'}</span>
            )}
          </li>
        ))}
      </ul>
    </ShellPanel>
  );
}

function ApiResultCard<T>({ title, result }: { title: string; result: BrowserApiResult<T> }) {
  return (
    <div className="summary-card">
      <h3>{title}</h3>
      <p>{isSuccess(result) ? 'Данные получены' : result.playerMessage}</p>
      {!isSuccess(result) && result.technicalDetails && <details><summary>Подробности</summary><pre>{result.technicalDetails}</pre></details>}
    </div>
  );
}

function getEndpointLabel(responseType: string): string {
  return browserApiEndpoints.find((apiEndpoint) => apiEndpoint.response === responseType)?.path ?? responseType;
}
