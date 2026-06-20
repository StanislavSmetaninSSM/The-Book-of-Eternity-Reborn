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
  createdSaveId?: string;
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

export interface BrowserCreateSaveRequest {
  saveName: string | null;
}

export interface BrowserCreateSaveResultDto {
  success: boolean;
  error: string;
  createdSaveId: string;
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

export interface BrowserClientSettingsDto {
  schemaVersion: number;
  language: BrowserSettingsChoiceGroupDto;
  difficulty: BrowserSettingsChoiceGroupDto;
  showGmThoughts: boolean;
  audio: BrowserClientAudioSettingsDto;
  accessibility: BrowserClientAccessibilitySettingsDto;
  locality: BrowserClientLocalityDto;
}

export interface BrowserSettingsChoiceGroupDto {
  value: string;
  label: string;
  choices: BrowserSettingsChoiceDto[];
}

export interface BrowserSettingsChoiceDto {
  value: string;
  label: string;
  description: string;
}

export interface BrowserClientAudioSettingsDto {
  musicEnabled: boolean;
  musicVolume: number;
  soundEnabled: boolean;
  soundVolume: number;
}

export interface BrowserClientAccessibilitySettingsDto {
  fontScalePercent: number;
  uiScalePercent: number;
  reducedMotion: boolean;
  contrastFriendly: boolean;
}

export interface BrowserClientLocalityDto {
  localhostOnly: boolean;
  sessionLabel: string;
  gameSessionExists: boolean;
  gmBridgeEnabled: boolean;
  gmBridgeLabel: string;
  safetySummary: string;
}

export interface BrowserClientSettingsUpdateRequest {
  language?: string | null;
  difficulty?: string | null;
  showGmThoughts?: boolean | null;
  musicEnabled?: boolean | null;
  musicVolume?: number | null;
  soundEnabled?: boolean | null;
  soundVolume?: number | null;
  browserFontScalePercent?: number | null;
  browserUiScalePercent?: number | null;
  browserReducedMotion?: boolean | null;
  browserContrastFriendly?: boolean | null;
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
  media: BrowserGameScreenMediaDto;
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

export interface BrowserGameScreenMediaDto {
  schemaVersion: number;
  sceneImagePrompt: string;
  gallery: BrowserGameScreenMediaItemDto[];
  map: MapViewDto;
}

export interface BrowserGameScreenMediaItemDto {
  mediaId: string;
  url: string;
  fileName: string;
  contentType: string;
  length: number;
  modifiedAtUtc: IsoDateTimeString;
}

export interface BrowserMediaGenerateRequest {
  prompt: string;
  entityType: string;
  entityKey: string;
}

export interface BrowserMediaGenerateResult {
  success: boolean;
  mediaId: string | null;
  url: string | null;
  errorMessage: string | null;
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

export interface BrowserCommandCoverageDto {
  schemaVersion: number;
  summary: BrowserCommandCoverageSummaryDto;
  commands: BrowserCommandCoverageEntryDto[];
}

export interface BrowserCommandCoverageSummaryDto {
  descriptorCount: number;
  aliasCount: number;
  subcommandCount: number;
  browserExecutableCount: number;
  playerDefaultActionCount: number;
  advancedOnlyActionCount: number;
  mutatingCommandCount: number;
  commandsNeedingFollowUpCount: number;
}

export interface BrowserCommandCoverageEntryDto {
  id: string;
  aliases: string[];
  group: string;
  mutationMode: string;
  browserStatus: string;
  handlerKind: string;
  uxDecision: string;
  surface: string;
  formMode: string;
  primaryActionLabel: string;
  primaryCommand: string;
  subcommands: BrowserCommandSubcommandCoverageDto[];
  followUpIssue: string;
  reason: string;
  auditStatus: string;
  sampleDataStatus: string;
  browserEvidence: string;
  consoleEvidence: string;
  parityNotes: string;
  readabilityNotes: string;
  gapSummary: string;
}

export interface BrowserCommandSubcommandCoverageDto {
  id: string;
  aliases: string[];
  canonicalCommand: string;
  group: string;
  mutationMode: string;
  browserStatus: string;
  handlerKind: string;
  uxDecision: string;
  surface: string;
  formMode: string;
  primaryActionLabel: string;
  primaryCommand: string;
  followUpIssue: string;
  reason: string;
  auditStatus: string;
  sampleDataStatus: string;
  browserEvidence: string;
  consoleEvidence: string;
  parityNotes: string;
  readabilityNotes: string;
  gapSummary: string;
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
  advancedEnabled?: boolean | null;
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

export interface BrowserPlayerActionRequest {
  text: string;
  ownerId?: string | null;
  ownerLabel?: string | null;
}

export interface BrowserPlayerActionResult {
  success: boolean;
  playerMessage: string;
  technicalDetail?: string | null;
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

export type QteWebCheckConfigDto =
  | QteTimingBarCheckConfigDto
  | QtePromptChainCheckConfigDto
  | QteBalanceMeterCheckConfigDto
  | QteChargeReleaseCheckConfigDto
  | QteBranchChoiceCheckConfigDto
  | QteMashInputCheckConfigDto
  | QtePatternMemoryCheckConfigDto
  | QteRhythmPulseCheckConfigDto
  | QtePrecisionChoiceCheckConfigDto
  | QteStealthNoiseCheckConfigDto
  | QteLockPinSetCheckConfigDto
  | QteUnsupportedCheckConfigDto;

export interface QteCheckConfigBase {
  kind: string;
  supported: boolean;
}

export interface QteTimingBarCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  width: number;
  successStart: number;
  successWidth: number;
  partialStart: number;
  partialWidth: number;
  tickMs: number;
}

export interface QtePromptChainCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  sequence: string[];
  allowedMistakes: number;
  timeoutMs: number;
}

export interface QteBalanceMeterCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  safeHalfWidth: number;
  tickMs: number;
  ticks: number;
}

export interface QteChargeReleaseCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  targetStart: number;
  targetWidth: number;
  tickMs: number;
  partialPadding: number;
}

export interface QteBranchChoiceCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  choiceGrade: string;
}

export interface QteMashInputCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  keys: string[];
  durationMs: number;
  targetPresses: number;
  partialThreshold: number;
  successTarget: number;
  partialTarget: number;
}

export interface QtePatternMemoryCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  alphabet: string[];
  sequence: string[];
  sequenceLength: number;
  revealMs: number;
  inputTimeoutMs: number;
  allowedMistakes: number;
}

export interface QteRhythmPulseCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  pulseCount: number;
  beatIntervalMs: number;
  hitWindowMs: number;
  allowedMisses: number;
  patternVariation: string;
  pulseOffsetsMs: number[];
}

export interface QtePrecisionChoiceCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  choices: QtePrecisionChoiceOptionDto[];
  correctChoiceId: string;
  timeoutMs: number;
  timeoutGrade: string;
  revealedDecoyHintCount: number;
  decoyHints: QtePrecisionChoiceDecoyHintDto[];
}

export interface QtePrecisionChoiceOptionDto {
  id: string;
  label: string;
  grade: string;
  description?: string;
  hint?: string;
}

export interface QtePrecisionChoiceDecoyHintDto {
  choiceId: string;
  hint: string;
}

export interface QteStealthNoiseCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  durationMs: number;
  startingNoise: number;
  dangerThreshold: number;
  noiseDriftPerSecond: number;
  recoveryPerInput: number;
  allowedOverThresholdMs: number;
  recoveryKey: string;
  recoveryLabel?: string;
  warningLabel?: string;
  gradeThresholds: QteStealthNoiseGradeThresholdsDto;
}

export interface QteStealthNoiseGradeThresholdsDto {
  successMaxNoise: number;
  successMaxOverThresholdMs: number;
  partialMaxNoise: number;
  partialMaxOverThresholdMs: number;
}

export interface QteLockPinSetCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  pinCount: number;
  pinWindows: QteLockPinWindowDto[];
  timerMs: number;
  pickDurability: number;
  maxMistakes: number;
  pinDriftPerSecond: number;
  adjustKey: string;
  setKey: string;
  pinLabel?: string;
  durabilityLabel?: string;
  warningLabel?: string;
  gradeThresholds: QteLockPinSetGradeThresholdsDto;
}

export interface QteLockPinWindowDto {
  pin: number;
  min: number;
  max: number;
  label?: string;
}

export interface QteLockPinSetGradeThresholdsDto {
  successMaxTimeMs: number;
  successMaxMistakes: number;
  partialMaxTimeMs: number;
  partialMaxMistakes: number;
}

export interface QteUnsupportedCheckConfigDto extends QteCheckConfigBase {
  kind: string;
  supported: boolean;
  checkType: string;
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

export interface QtePracticeWebStateDto {
  state: string;
  catalog: QtePracticeCatalogEntryDto[];
  selectedTypeId: string | null;
  selectedDifficultyId: string | null;
  activeScene: QteWebActiveSceneDto | null;
  resolution: QteWebResolutionDto | null;
  completion: QteWebCompletionDto | null;
  feedbackTitle: string;
  feedback: string;
  localScoreNotice: string;
  availableOperations: string[];
  notification: string | null;
  error: string | null;
}

export interface QtePracticeCatalogEntryDto {
  typeId: string;
  title: string;
  description: string;
  instructions: string;
  available: boolean;
  unavailableReason: string | null;
  supportedSurfaces: string[];
  difficulties: QtePracticeDifficultyDto[];
}

export interface QtePracticeDifficultyDto {
  difficultyId: string;
  label: string;
  description: string;
}

export interface QtePracticeStartRequest {
  typeId: string | null;
  difficultyId: string | null;
}

export interface QtePracticeActionRequest {
  actionId: string | null;
  grade: string | null;
}

export interface DarenShowcaseWebStateDto {
  state: string;
  introTitle: string;
  introText: string;
  boundaryNotice: string;
  rewardNotice: string;
  bestReward: DarenRewardProfileDto | null;
  activeScene: QteWebActiveSceneDto | null;
  resolution: QteWebResolutionDto | null;
  completion: QteWebCompletionDto | null;
  ending: DarenShowcaseEndingDto | null;
  availableOperations: string[];
  notification: string | null;
  error: string | null;
}

export interface DarenRewardProfileDto {
  tierId: string;
  tierName: string;
  inkFeatherBonus: number;
  bestScore: number;
  completedAtUtc: string;
  summary: string;
}

export interface DarenShowcaseEndingDto {
  tierId: string | null;
  displayName: string;
  normalizedScore: number;
  inkFeatherBonus: number;
  grantsReward: boolean;
  epilogue: string;
  rewardExplanation: string;
  rewardMessage: string;
  rewardProfileSummary: string;
}

export interface DarenShowcaseActionRequest {
  actionId: string | null;
  grade: string | null;
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
  scoreState: QteWebScoreStateDto | null;
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
  checkConfig: QteWebCheckConfigDto;
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
  scoreSummary: QteWebScoreSummaryDto | null;
}

export interface QteWebScoreStateDto {
  metrics: QteWebScoreMetricDto[];
}

export interface QteWebScoreSummaryDto {
  rank: QteWebScoreRankDto | null;
  metrics: QteWebScoreMetricDto[];
}

export interface QteWebScoreRankDto {
  id: string;
  label: string;
  summary: string | null;
}

export interface QteWebScoreMetricDto {
  id: string;
  label: string;
  value: number;
  min: number;
  max: number;
  visibility: 'always' | 'final' | 'hidden' | string;
}
