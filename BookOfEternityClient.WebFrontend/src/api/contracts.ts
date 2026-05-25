export type IsoDateTimeString = string;
export type JsonPrimitive = string | number | boolean | null;
export type JsonValue = JsonPrimitive | JsonObject | JsonValue[];
export interface JsonObject {
  [key: string]: JsonValue;
}

export type BrowserApiErrorKind =
  | 'validation-error'
  | 'pending-turn'
  | 'blocked-local-write'
  | 'not-found'
  | 'no-active-session'
  | 'server-diagnostics'
  | 'http-error'
  | 'network-error';

export interface BrowserApiFailure {
  ok: false;
  status: number | null;
  kind: BrowserApiErrorKind;
  message: string;
  playerMessage: string;
  technicalDetails?: string;
  payload?: unknown;
}

export interface BrowserApiSuccess<TData> {
  ok: true;
  status: number;
  data: TData;
}

export type BrowserApiResult<TData> = BrowserApiSuccess<TData> | BrowserApiFailure;

export interface BrowserApiErrorPayload {
  error: string;
  loadedSaveId?: string;
  menu?: BrowserMainMenuDto;
}

export interface BrowserApiEndpointDescriptor {
  id: string;
  method: 'GET' | 'POST';
  path: string;
  playerSurface: 'player-default' | 'advanced-only' | 'shared';
  response: string;
}

export interface BrowserApiContractSummary {
  strategy: 'handwritten-types-with-fixture-guards';
  csharpAuthority: true;
  fixtureCheck: string;
  endpointDocs: BrowserApiEndpointDescriptor[];
}

export interface BrowserMainMenuDto {
  schemaVersion: number;
  session: BrowserMainMenuSessionDto;
  actions: BrowserMainMenuActionDto[];
  saves: BrowserSaveSlotDto[];
  options: BrowserOptionsSummaryDto;
  about: BrowserAboutDto;
  advancedShell: BrowserAdvancedShellDto;
}

export interface BrowserMainMenuSessionDto {
  gameSessionExists: boolean;
  hasReadableSoul: boolean;
  canContinue: boolean;
  continueReason: string;
  soulName: string;
  currentRealm: string;
  realmLabel: string;
  currentIncarnation: number;
  turnNumber: number;
  turnLabel: string;
  terminalSoulDissipated: boolean;
  validationState: string;
  validationLabel: string;
  pendingTurnMessage: string;
  canStartBrowserWrite: boolean;
  localUiLocked: boolean;
  checkedAtUtc: IsoDateTimeString;
}

export interface BrowserMainMenuActionDto {
  id: string;
  label: string;
  description: string;
  enabled: boolean;
  disabledReason: string;
  kind: string;
  command: string;
  targetPanel: string;
}

export interface BrowserSaveSlotDto {
  saveId: string;
  scope: string;
  scopeLabel: string;
  displayName: string;
  description: string;
  characterName: string;
  turnLabel: string;
  timestampUtc: IsoDateTimeString | null;
  fileSizeBytes: number;
}

export interface BrowserOptionsSummaryDto {
  musicEnabled: boolean;
  soundEnabled: boolean;
  consoleFontSize: number;
  guidance: string;
}

export interface BrowserAboutDto {
  title: string;
  body: string;
}

export interface BrowserAdvancedShellDto {
  label: string;
  description: string;
  initiallyExpanded: boolean;
}

export interface BrowserLoadSaveRequest {
  saveId: string | null;
}

export interface BrowserLoadSaveResultDto {
  success: boolean;
  error: string;
  loadedSaveId: string;
  menu: BrowserMainMenuDto;
}

export interface BrowserAudioSettingsDto {
  schemaVersion: number;
  musicEnabled: boolean;
  musicVolume: number;
  soundEnabled: boolean;
  soundVolume: number;
  autoplayGuidance: string;
  missingAssetsMessage: string;
  playlists: BrowserAudioPlaylistDto[];
  cues: BrowserAudioCueDto[];
}

export interface BrowserAudioPlaylistDto {
  id: string;
  label: string;
  usage: string;
  available: boolean;
  tracks: BrowserAudioAssetDto[];
}

export interface BrowserAudioCueDto {
  id: string;
  label: string;
  usage: string;
  available: boolean;
  asset: BrowserAudioAssetDto | null;
}

export interface BrowserAudioAssetDto {
  id: string;
  label: string;
  url: string;
  contentType: string;
}

export interface BrowserAudioSettingsUpdateRequest {
  musicEnabled?: boolean | null;
  musicVolume?: number | null;
  soundEnabled?: boolean | null;
  soundVolume?: number | null;
}

export interface LocalWebUiSessionStatus {
  schemaVersion: number;
  status: string;
  localOnly: boolean;
  basePath: string;
  gameSessionPath: string;
  gameSessionExists: boolean;
  checkedAtUtc: IsoDateTimeString;
  canStartBrowserWrite: boolean;
  pendingTurn: BrowserPendingTurnStatus;
  localUiLock: BrowserLocalUiLockStatus;
}

export interface BrowserPendingTurnStatus {
  hasActiveGmTurn: boolean;
  artifacts: BrowserPendingTurnArtifactStatus[];
  message: string;
}

export interface BrowserPendingTurnArtifactStatus {
  label: string;
  path: string;
  exists: boolean;
  kind: string;
}

export interface BrowserLocalUiLockStatus {
  exists: boolean;
  isReadable: boolean;
  isStale: boolean;
  ownerId: string;
  ownerKind: string;
  ownerLabel: string;
  acquiredAtUtc: IsoDateTimeString | null;
  heartbeatAtUtc: IsoDateTimeString | null;
  leaseSeconds: number;
  lastOperation: string;
}

export interface BrowserGameScreenDto {
  schemaVersion: number;
  theme: BrowserGameScreenThemeDto;
  soul: BrowserGameScreenSoulDto;
  player: BrowserGameScreenPlayerDto;
  world: BrowserGameScreenWorldDto;
  narrative: BrowserGameScreenNarrativeDto;
  afterlife: BrowserGameScreenAfterlifeDto;
  turnState: BrowserGameScreenTurnStateDto;
  actionComposer: BrowserGameScreenActionComposerDto;
  qte: QteWebStateDto;
  actionMenu: BrowserPlayerCommandMenuDto;
  flags: BrowserGameScreenFlagsDto;
}

export interface BrowserGameScreenThemeDto {
  key: string;
  label: string;
  icon: string;
  accent: string;
}

export interface BrowserGameScreenSoulDto {
  name: string;
  realm: string;
  incarnation: number;
  inkFeathers: number;
  enlightenmentTier: string;
  activeGuardianName: string;
}

export interface BrowserGameScreenPlayerDto {
  name: string;
  class: string;
  race: string;
  currentCondition: string;
  healthPercentage: string;
  energyPercentage: string;
  poisePercentage: string;
  activeConditions: string[];
}

export interface BrowserGameScreenWorldDto {
  location: string;
  worldTime: string;
  turnNumber: number;
  sessionId: string;
}

export interface BrowserGameScreenNarrativeDto {
  text: string;
  dialogueOptions: BrowserGameScreenDialogueOptionDto[];
  combatLog: string;
  imagePrompt: string;
}

export interface BrowserGameScreenDialogueOptionDto {
  id: string;
  text: string;
  category: string;
}

export interface BrowserGameScreenAfterlifeDto {
  shiningRadianceExperience: number;
  shiningRadianceTier: number;
  shiningLightSparks: number;
  shiningHallCount: number;
  shiningFactionCount: number;
  hasOpenShiningGatesDraft: boolean;
  isShiningGatesDraftStale: boolean;
}

export interface BrowserGameScreenTurnStateDto {
  state: string;
  title: string;
  message: string;
  canStartBrowserWrite: boolean;
  validationState: string;
  validationLabel: string;
  phase: string;
  phaseLabel: string;
  severity: string;
  playerGuidance: string;
  recommendedActions: BrowserGameScreenTurnActionDto[];
  knownPhases: BrowserGameScreenTurnPhaseDto[];
}

export interface BrowserGameScreenTurnActionDto {
  id: string;
  label: string;
  description: string;
  surface: string;
  enabled: boolean;
  disabledReason: string;
}

export interface BrowserGameScreenTurnPhaseDto {
  id: string;
  label: string;
  description: string;
  surface: string;
}

export interface BrowserGameScreenActionComposerDto {
  canSubmit: boolean;
  mode: string;
  placeholder: string;
  guidance: string;
  disabledReason: string;
}

export interface BrowserPlayerCommandMenuDto {
  schemaVersion: number;
  sections: BrowserPlayerCommandSectionDto[];
}

export interface BrowserPlayerCommandSectionDto {
  id: string;
  label: string;
  description: string;
  playerDefault: boolean;
  actions: BrowserPlayerCommandActionDto[];
}

export interface BrowserPlayerCommandActionDto {
  id: string;
  sectionId: string;
  label: string;
  description: string;
  realmAvailability: string;
  enabled: boolean;
  disabledReason: string;
  playerDefault: boolean;
  mutationMode: string;
  mutationWarning: string;
  formMode: string;
  formLabel: string;
  formPrompt: string;
  advancedCommand: string;
}

export interface BrowserGameScreenFlagsDto {
  isInChaosSea: boolean;
  isInAnyShiningAbodeState: boolean;
  isInShiningAbode: boolean;
  isInShiningAbodePendingBootstrap: boolean;
  isInAfterlifeRealm: boolean;
  canReenterShiningAbode: boolean;
}

export interface BrowserLifecycleDashboardDto {
  schemaVersion: number;
  session: LocalWebUiSessionStatus;
  soul: BrowserSoulSummaryDto;
  pendingTurn: BrowserPendingTurnStatus;
  localUiLock: BrowserLocalUiLockStatus;
  canStartBrowserWrite: boolean;
  validation: BrowserValidationSummaryDto;
  guidance: BrowserLifecycleGuidanceDto[];
  entrypoints: BrowserLifecycleEntrypointDto[];
}

export interface BrowserSoulSummaryDto {
  name: string;
  currentRealm: string;
  realmLabel: string;
  currentIncarnation: number;
  isReadable: boolean;
  readError: string;
}

export interface BrowserValidationSummaryDto {
  state: string;
  statusLabel: string;
  issueCount: number;
  errorCount: number;
  warningCount: number;
  infoCount: number;
  displayedIssueCount: number;
  groups: BrowserValidationGroupDto[];
  issues: BrowserValidationIssueDto[];
}

export interface BrowserValidationGroupDto {
  severity: string;
  category: string;
  section: string;
  count: number;
}

export interface BrowserValidationIssueDto {
  filePath: string;
  severity: string;
  category: string;
  code: string;
  section: string;
  actor: string;
  message: string;
  expected: string;
  actual: string;
  repairHint: string;
}

export interface BrowserLifecycleGuidanceDto {
  severity: string;
  title: string;
  message: string;
  actionLabel: string;
  command: string;
}

export interface BrowserLifecycleEntrypointDto {
  label: string;
  command: string;
  endpoint: string;
  enabled: boolean;
  description: string;
}

export interface ExplorerWebCommandRequest {
  command: string;
  ownerId?: string | null;
  ownerLabel?: string | null;
}

export interface ExplorerPromptSessionSubmitRequest {
  sessionId: string;
  answers: Record<string, JsonValue | undefined>;
  ownerId?: string | null;
}

export interface ExplorerPromptSessionCancelRequest {
  sessionId: string;
  ownerId?: string | null;
}

export type CommandExecutionState = 'Completed' | 'RequiresInput' | 'Pending' | 'Blocked' | 'Failed';
export type UiNotificationSeverity = 'Info' | 'Success' | 'Warning' | 'Error';
export type UiActionStyle = 'Default' | 'Primary' | 'Secondary' | 'Danger';
export type UiTone = 'Default' | 'Muted' | 'Subtle' | 'Accent' | 'Success' | 'Warning' | 'Error';

export interface ExplorerCommandResult {
  command: string;
  state: CommandExecutionState;
  blocks: UiBlock[];
  actions: UiAction[];
  prompts: UiPrompt[];
  notifications: UiNotification[];
  interactiveSession: UiPromptSession | null;
}

export type UiBlock =
  | UiTextBlock
  | UiPanelBlock
  | UiTableBlock
  | UiListBlock
  | UiKeyValueGridBlock
  | UiMessageBlock
  | UiRawJsonBlock
  | UiImageBlock
  | UiMapBlock;

export interface UiTextBlock {
  kind: 'text';
  text: string;
  tone: UiTone;
}

export interface UiPanelBlock {
  kind: 'panel';
  title: string;
  blocks: UiBlock[];
}

export interface UiTableBlock {
  kind: 'table';
  title: string;
  columns: string[];
  rows: UiTableRow[];
}

export interface UiTableRow {
  cells: string[];
}

export interface UiListBlock {
  kind: 'list';
  ordered: boolean;
  items: string[];
}

export interface UiKeyValueGridBlock {
  kind: 'keyValueGrid';
  items: UiKeyValueItem[];
}

export interface UiKeyValueItem {
  key: string;
  value: string;
}

export interface UiMessageBlock {
  kind: 'message';
  severity: UiNotificationSeverity;
  title: string;
  message: string;
}

export interface UiRawJsonBlock {
  kind: 'rawJson';
  title: string;
  json: JsonValue | null;
}

export interface UiImageBlock {
  kind: 'image';
  title: string;
  url: string;
  mediaId: string;
  relativePath: string;
  altText: string;
  contentType: string;
  length: number;
  modifiedAtUtc: IsoDateTimeString;
}

export interface UiMapBlock {
  kind: 'map';
  title: string;
  map: MapViewDto;
}

export interface MapViewDto {
  schemaVersion: number;
  realm: string;
  title: string;
  currentNodeId: string;
  layers: MapLayerDto[];
  zLevels: MapZLevelDto[];
  nodes: MapNodeDto[];
  links: MapLinkDto[];
  regions: MapRegionDto[];
}

export interface MapLayerDto {
  id: string;
  label: string;
  isDefault: boolean;
}

export interface MapZLevelDto {
  z: number;
  label: string;
}

export interface MapNodeDto {
  id: string;
  label: string;
  type: string;
  x: number;
  y: number;
  z: number;
  layer: string;
  isCurrent: boolean;
  ownerFactionId: string;
  ownerFactionName: string;
  influence: Record<string, number>;
  details: MapDetailItemDto[];
}

export interface MapLinkDto {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
  label: string;
  state: string;
  layer: string;
  z: number | null;
}

export interface MapRegionDto {
  id: string;
  label: string;
  ownerFactionId: string;
  ownerFactionName: string;
  layer: string;
  z: number | null;
  nodeIds: string[];
}

export interface MapDetailItemDto {
  key: string;
  value: string;
}

export interface UiAction {
  id: string;
  label: string;
  command: string;
  style: UiActionStyle;
  requiresConfirmation: boolean;
  payload: JsonValue | null;
}

export type UiPrompt = UiConfirmationPrompt | UiSelectionPrompt | UiTextInputPrompt | UiLongTextInputPrompt;

export interface UiPromptBase {
  id: string;
  prompt: string;
  required: boolean;
}

export interface UiConfirmationPrompt extends UiPromptBase {
  kind: 'confirmation';
  defaultValue: boolean;
}

export interface UiSelectionPrompt extends UiPromptBase {
  kind: 'selection';
  options: UiSelectionOption[];
  allowCustom: boolean;
}

export interface UiSelectionOption {
  value: string;
  label: string;
  description: string;
  disabled: boolean;
}

export interface UiTextInputPrompt extends UiPromptBase {
  kind: 'textInput';
  defaultValue: string;
  placeholder: string;
}

export interface UiLongTextInputPrompt extends UiPromptBase {
  kind: 'longTextInput';
  defaultValue: string;
  placeholder: string;
  minLines: number | null;
  maxLines: number | null;
}

export interface UiNotification {
  severity: UiNotificationSeverity;
  title: string;
  message: string;
}

export interface UiPromptSession {
  sessionId: string;
  submitEndpoint: string;
  cancelEndpoint: string;
  requiresLocalUiLock: boolean;
  ownerId: string;
  expiresAtUtc: IsoDateTimeString;
}

export interface QteWebOfferDecisionRequest {
  decision: 'accept' | 'decline' | null;
}

export interface QteWebActionRequest {
  actionId: string | null;
  grade: 'success' | 'partial' | 'fail' | null;
}

export interface QteWebStateDto {
  state: string;
  offer: QteWebOfferDto | null;
  activeScene: QteWebActiveSceneDto | null;
  resolution: QteWebResolutionDto | null;
  completion: QteWebCompletionDto | null;
  lastResolvedReminder: string | null;
  lastDeclinedQteId: string | null;
  availableOperations: string[];
  notification: string | null;
  error: string | null;
}

export interface QteWebOfferDto {
  qteId: string;
  title: string;
  offerText: string | null;
  introNarrative: string | null;
  declineHint: string | null;
  cinematicJustification: string | null;
  sceneImagePrompt: string | null;
  startChapterId: string;
}

export interface QteWebActiveSceneDto {
  qteId: string;
  title: string;
  acceptedAtTurn: number;
  currentChapter: QteWebChapterDto | null;
}

export interface QteWebChapterDto {
  chapterId: string;
  title: string | null;
  narrative: string | null;
  chapterImagePrompt: string | null;
  actions: QteWebActionDto[];
}

export interface QteWebActionDto {
  actionId: string;
  label: string;
  checkType: string;
  baseDifficulty: number;
  primaryCharacteristic: string;
  requiresSubmittedGrade: boolean;
  gradeOptions: string[];
}

export interface QteWebResolutionDto {
  state: string;
  qteId: string;
  chapterId: string;
  actionId: string;
  grade: string;
  resultText: string;
  nextChapterId: string | null;
}

export interface QteWebCompletionDto {
  qteId: string;
  outcomeId: string;
  summary: string;
}
