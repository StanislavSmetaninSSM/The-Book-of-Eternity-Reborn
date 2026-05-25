import type {
  BrowserApiContractSummary,
  BrowserApiEndpointDescriptor,
  BrowserApiErrorKind,
  BrowserApiFailure,
  BrowserApiResult,
  BrowserAudioSettingsDto,
  BrowserAudioSettingsUpdateRequest,
  BrowserCommandCoverageDto,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserLoadSaveRequest,
  BrowserLoadSaveResultDto,
  BrowserMainMenuDto,
  BrowserValidationSummaryDto,
  ExplorerCommandResult,
  ExplorerPromptSessionCancelRequest,
  ExplorerPromptSessionSubmitRequest,
  ExplorerWebCommandRequest,
  LocalWebUiSessionStatus,
  QteWebActionRequest,
  QteWebOfferDecisionRequest,
  QteWebStateDto
} from './contracts';

export interface BrowserApiClientOptions {
  baseUrl?: string;
  fetcher?: typeof fetch;
}

export interface BrowserApiClient {
  getMainMenu(): Promise<BrowserApiResult<BrowserMainMenuDto>>;
  getSessionStatus(): Promise<BrowserApiResult<LocalWebUiSessionStatus>>;
  getGameScreen(): Promise<BrowserApiResult<BrowserGameScreenDto>>;
  getAudioSettings(): Promise<BrowserApiResult<BrowserAudioSettingsDto>>;
  updateAudioSettings(request: BrowserAudioSettingsUpdateRequest): Promise<BrowserApiResult<BrowserAudioSettingsDto>>;
  getLifecycleDashboard(): Promise<BrowserApiResult<BrowserLifecycleDashboardDto>>;
  getCommandCoverage(): Promise<BrowserApiResult<BrowserCommandCoverageDto>>;
  validateLifecycle(): Promise<BrowserApiResult<BrowserValidationSummaryDto>>;
  loadSave(request: BrowserLoadSaveRequest): Promise<BrowserApiResult<BrowserLoadSaveResultDto>>;
  executeExplorerCommand(request: ExplorerWebCommandRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  getPromptSession(sessionId: string): Promise<BrowserApiResult<ExplorerCommandResult>>;
  submitPromptSession(request: ExplorerPromptSessionSubmitRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  cancelPromptSession(request: ExplorerPromptSessionCancelRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  getQteState(): Promise<BrowserApiResult<QteWebStateDto>>;
  resolveQteOffer(request: QteWebOfferDecisionRequest): Promise<BrowserApiResult<QteWebStateDto>>;
  resolveQteAction(request: QteWebActionRequest): Promise<BrowserApiResult<QteWebStateDto>>;
}

export const browserApiEndpointDocs = [
  { id: 'main-menu', method: 'GET', path: '/api/main-menu', playerSurface: 'player-default', response: 'BrowserMainMenuDto' },
  { id: 'session-status', method: 'GET', path: '/api/session', playerSurface: 'shared', response: 'LocalWebUiSessionStatus' },
  { id: 'game-screen', method: 'GET', path: '/api/game-screen', playerSurface: 'player-default', response: 'BrowserGameScreenDto' },
  { id: 'audio-settings', method: 'GET', path: '/api/audio/settings', playerSurface: 'player-default', response: 'BrowserAudioSettingsDto' },
  { id: 'audio-settings-update', method: 'POST', path: '/api/audio/settings', playerSurface: 'player-default', response: 'BrowserAudioSettingsDto' },
  { id: 'audio-asset', method: 'GET', path: '/api/audio/assets/{assetId}', playerSurface: 'player-default', response: 'Audio stream' },
  { id: 'save-load', method: 'POST', path: '/api/saves/load', playerSurface: 'player-default', response: 'BrowserLoadSaveResultDto' },
  { id: 'lifecycle-dashboard', method: 'GET', path: '/api/lifecycle/dashboard', playerSurface: 'advanced-only', response: 'BrowserLifecycleDashboardDto' },
  { id: 'lifecycle-validate', method: 'POST', path: '/api/lifecycle/validate', playerSurface: 'advanced-only', response: 'BrowserValidationSummaryDto' },
  { id: 'command-coverage', method: 'GET', path: '/api/explorer/command-coverage', playerSurface: 'advanced-only', response: 'BrowserCommandCoverageDto' },
  { id: 'explorer-command', method: 'POST', path: '/api/explorer/command', playerSurface: 'advanced-only', response: 'ExplorerCommandResult' },
  { id: 'prompt-session-get', method: 'GET', path: '/api/explorer/prompt-sessions/{sessionId}', playerSurface: 'advanced-only', response: 'ExplorerCommandResult' },
  { id: 'prompt-session-submit', method: 'POST', path: '/api/explorer/prompt-sessions/submit', playerSurface: 'advanced-only', response: 'ExplorerCommandResult' },
  { id: 'prompt-session-cancel', method: 'POST', path: '/api/explorer/prompt-sessions/cancel', playerSurface: 'advanced-only', response: 'ExplorerCommandResult' },
  { id: 'qte-state', method: 'GET', path: '/api/qte/state', playerSurface: 'player-default', response: 'QteWebStateDto' },
  { id: 'qte-offer', method: 'POST', path: '/api/qte/offer', playerSurface: 'player-default', response: 'QteWebStateDto' },
  { id: 'qte-action', method: 'POST', path: '/api/qte/action', playerSurface: 'player-default', response: 'QteWebStateDto' }
] as const satisfies BrowserApiEndpointDescriptor[];

export const browserApiContractSummary = {
  strategy: 'handwritten-types-with-fixture-guards',
  csharpAuthority: true,
  fixtureCheck: 'BrowserApiContractTests + contract-fixture-checks.ts',
  endpointDocs: browserApiEndpointDocs
} as const satisfies BrowserApiContractSummary;

export function createBrowserApiClient(options: BrowserApiClientOptions = {}): BrowserApiClient {
  const baseUrl = options.baseUrl ?? '';
  const fetcher = options.fetcher ?? fetch;

  return {
    getMainMenu: () => requestJson<BrowserMainMenuDto>(fetcher, baseUrl, '/api/main-menu'),
    getSessionStatus: () => requestJson<LocalWebUiSessionStatus>(fetcher, baseUrl, '/api/session'),
    getGameScreen: () => requestJson<BrowserGameScreenDto>(fetcher, baseUrl, '/api/game-screen'),
    getAudioSettings: () => requestJson<BrowserAudioSettingsDto>(fetcher, baseUrl, '/api/audio/settings'),
    updateAudioSettings: (request) => requestJson<BrowserAudioSettingsDto>(fetcher, baseUrl, '/api/audio/settings', jsonInit('POST', request)),
    getLifecycleDashboard: () => requestJson<BrowserLifecycleDashboardDto>(fetcher, baseUrl, '/api/lifecycle/dashboard'),
    getCommandCoverage: () => requestJson<BrowserCommandCoverageDto>(fetcher, baseUrl, '/api/explorer/command-coverage'),
    validateLifecycle: () => requestJson<BrowserValidationSummaryDto>(fetcher, baseUrl, '/api/lifecycle/validate', jsonInit('POST')),
    loadSave: (request) => requestJson<BrowserLoadSaveResultDto>(fetcher, baseUrl, '/api/saves/load', jsonInit('POST', request)),
    executeExplorerCommand: (request) => requestJson<ExplorerCommandResult>(fetcher, baseUrl, '/api/explorer/command', jsonInit('POST', request)),
    getPromptSession: (sessionId) => requestJson<ExplorerCommandResult>(fetcher, baseUrl, `/api/explorer/prompt-sessions/${encodeURIComponent(sessionId)}`),
    submitPromptSession: (request) => requestJson<ExplorerCommandResult>(fetcher, baseUrl, '/api/explorer/prompt-sessions/submit', jsonInit('POST', request)),
    cancelPromptSession: (request) => requestJson<ExplorerCommandResult>(fetcher, baseUrl, '/api/explorer/prompt-sessions/cancel', jsonInit('POST', request)),
    getQteState: () => requestJson<QteWebStateDto>(fetcher, baseUrl, '/api/qte/state'),
    resolveQteOffer: (request) => requestJson<QteWebStateDto>(fetcher, baseUrl, '/api/qte/offer', jsonInit('POST', request)),
    resolveQteAction: (request) => requestJson<QteWebStateDto>(fetcher, baseUrl, '/api/qte/action', jsonInit('POST', request))
  };
}

export const browserApi = createBrowserApiClient();

async function requestJson<TData>(
  fetcher: typeof fetch,
  baseUrl: string,
  path: string,
  init?: RequestInit
): Promise<BrowserApiResult<TData>> {
  try {
    const response = await fetcher(toRequestUrl(baseUrl, path), withJsonAccept(init));
    const payload = await readResponsePayload(response);

    if (!response.ok) {
      return normalizeFailure(response.status, payload);
    }

    return {
      ok: true,
      status: response.status,
      data: payload as TData
    };
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : 'Network request failed.';
    return {
      ok: false,
      status: null,
      kind: 'network-error',
      message,
      playerMessage: 'Локальный игровой клиент сейчас недоступен. Проверьте, что браузерный режим запущен.',
      technicalDetails: message,
      payload: error
    };
  }
}

function jsonInit(method: 'POST', body?: unknown): RequestInit {
  return {
    method,
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json'
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  };
}

function withJsonAccept(init?: RequestInit): RequestInit {
  const headers = new Headers(init?.headers);
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  return {
    ...init,
    headers
  };
}

function toRequestUrl(baseUrl: string, path: string): string {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  if (!baseUrl) {
    return normalizedPath;
  }

  return `${baseUrl.replace(/\/$/, '')}${normalizedPath}`;
}

async function readResponsePayload(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text.trim()) {
    return null;
  }

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

function normalizeFailure(status: number, payload: unknown): BrowserApiFailure {
  const message = readErrorMessage(payload) ?? `HTTP ${status}`;
  const kind = classifyFailure(status, message, payload);
  return {
    ok: false,
    status,
    kind,
    message,
    playerMessage: toPlayerMessage(kind, message),
    technicalDetails: toTechnicalDetails(payload),
    payload
  };
}

function classifyFailure(status: number, message: string, payload: unknown): BrowserApiErrorKind {
  const text = `${message} ${toTechnicalDetails(payload) ?? ''}`.toLowerCase();

  if (status === 404) {
    return text.includes('game_session') ? 'no-active-session' : 'not-found';
  }

  if (text.includes('validation') || text.includes('валидац')) {
    return 'validation-error';
  }

  if (text.includes('gm-turn') || text.includes('гм') || text.includes('pending turn')) {
    return 'pending-turn';
  }

  if (text.includes('lock') || text.includes('блокиров') || text.includes('blocked')) {
    return 'blocked-local-write';
  }

  if (status >= 500) {
    return 'server-diagnostics';
  }

  return 'http-error';
}

function toPlayerMessage(kind: BrowserApiErrorKind, fallback: string): string {
  switch (kind) {
    case 'validation-error':
      return 'Состояние требует проверки или ремонта перед продолжением.';
    case 'pending-turn':
      return 'Нужно дождаться или завершить текущий ход ГМа.';
    case 'blocked-local-write':
      return 'Локальная запись сейчас заблокирована другим процессом или защитным протоколом.';
    case 'not-found':
      return 'Этот раздел локальной книги пока не открылся.';
    case 'no-active-session':
      return 'Активная игровая сессия не найдена. Откройте главное меню или загрузите сохранение.';
    case 'server-diagnostics':
      return 'Локальный игровой клиент вернул техническую ошибку. Подробности доступны в расширенном режиме.';
    case 'network-error':
      return 'Локальный игровой клиент недоступен.';
    case 'http-error':
      return 'Локальный запрос не удался. Подробности доступны в расширенном режиме.';
  }
}

function readErrorMessage(payload: unknown): string | null {
  if (typeof payload === 'string') {
    return payload;
  }

  if (!isRecord(payload)) {
    return null;
  }

  const error = readString(payload, 'error');
  if (error) {
    return error;
  }

  return readString(payload, 'message');
}

function readString(value: Record<string, unknown>, key: string): string | null {
  const field = value[key];
  return typeof field === 'string' && field.trim().length > 0 ? field : null;
}

function toTechnicalDetails(payload: unknown): string | undefined {
  if (payload === null || payload === undefined) {
    return undefined;
  }

  if (typeof payload === 'string') {
    return payload;
  }

  try {
    return JSON.stringify(payload);
  } catch {
    return String(payload);
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
