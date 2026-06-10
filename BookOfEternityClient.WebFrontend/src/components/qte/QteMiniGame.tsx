import { useEffect, useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import type {
  QteBalanceMeterCheckConfigDto,
  QteChargeReleaseCheckConfigDto,
  QteLockPinSetCheckConfigDto,
  QteLockPinWindowDto,
  QteMashInputCheckConfigDto,
  QtePatternMemoryCheckConfigDto,
  QtePrecisionChoiceCheckConfigDto,
  QtePromptChainCheckConfigDto,
  QteRhythmPulseCheckConfigDto,
  QteStealthNoiseCheckConfigDto,
  QteTimingBarCheckConfigDto,
  QteWebActionDto
} from '../../api/contracts';
import {
  resolveBalanceMeterGrade,
  resolveChargeReleaseGrade,
  resolveLockPinSetGrade,
  resolveMashInputGrade,
  resolvePatternMemoryGrade,
  resolvePrecisionChoiceGrade,
  resolvePromptChainGrade,
  resolveRhythmPulseGrade,
  resolveStealthNoiseGrade,
  resolveTimingBarGrade,
  type QteLockPinAttempt,
  type QteMiniGameGrade
} from '../../qte/qteGradeHelpers';
import { formatQteKeyTokenLabel, normalizeQteKeyboardInput, type QteKeyToken } from '../../utils/qteKeyInput';
import { toPlayerFacingText } from '../../utils/playerCopy';

interface QteMiniGameProps {
  action: QteWebActionDto;
  disabled: boolean;
  onSubmit: (grade: QteMiniGameGrade | null) => void;
}

export function QteMiniGame({ action, disabled, onSubmit }: QteMiniGameProps) {
  const config = action.checkConfig;

  switch (config.kind) {
    case 'TimingBar':
      return <TimingBarGame config={config as QteTimingBarCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'PromptChain':
      return <PromptChainGame config={config as QtePromptChainCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'BalanceMeter':
      return <BalanceMeterGame config={config as QteBalanceMeterCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'ChargeRelease':
      return <ChargeReleaseGame config={config as QteChargeReleaseCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'BranchChoice':
      return <BranchChoiceGame disabled={disabled} onSubmit={onSubmit} />;
    case 'MashInput':
      return <MashInputGame config={config as QteMashInputCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'PatternMemory':
      return <PatternMemoryGame config={config as QtePatternMemoryCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'RhythmPulse':
      return <RhythmPulseGame config={config as QteRhythmPulseCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'PrecisionChoice':
      return <PrecisionChoiceGame config={config as QtePrecisionChoiceCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'StealthNoise':
      return <StealthNoiseGame config={config as QteStealthNoiseCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'LockPinSet':
      return <LockPinSetGame config={config as QteLockPinSetCheckConfigDto} disabled={disabled} onSubmit={onSubmit} />;
    case 'Unsupported':
    default:
      return <UnsupportedQteGame />;
  }
}

function useQteDeadline(durationMs: number, active: boolean, onExpire: () => void, resetKey: string | number = 0): number {
  const [remainingMs, setRemainingMs] = useState(durationMs);
  const onExpireRef = useRef(onExpire);

  useEffect(() => {
    onExpireRef.current = onExpire;
  }, [onExpire]);

  useEffect(() => {
    if (!active) {
      setRemainingMs(durationMs);
      return undefined;
    }

    const startedAt = Date.now();
    let expired = false;
    const tick = () => {
      const nextRemaining = Math.max(0, durationMs - (Date.now() - startedAt));
      setRemainingMs(nextRemaining);
      if (!expired && nextRemaining <= 0) {
        expired = true;
        onExpireRef.current();
      }
    };

    const interval = window.setInterval(tick, 100);
    const timeout = window.setTimeout(tick, durationMs + 20);
    tick();
    return () => {
      window.clearInterval(interval);
      window.clearTimeout(timeout);
    };
  }, [active, durationMs, resetKey]);

  return remainingMs;
}

function TimingBarGame({ config, disabled, onSubmit }: SharedGameProps<QteTimingBarCheckConfigDto>) {
  const [position, setPosition] = useState(0);
  const [direction, setDirection] = useState(1);

  useEffect(() => {
    if (disabled) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      setPosition((current) => {
        const next = current + direction;
        if (next >= config.width - 1 || next <= 0) {
          setDirection((currentDirection) => currentDirection * -1);
        }

        return Math.max(0, Math.min(config.width - 1, next));
      });
    }, Math.max(40, config.tickMs));

    return () => window.clearInterval(timer);
  }, [config.tickMs, config.width, direction, disabled]);

  const submit = () => onSubmit(resolveTimingBarGrade(config, position));

  return (
    <MiniGameFrame
      kind="TimingBar"
      title="Полоса реакции"
      instructions={`Нажмите ${formatToken('space')}, когда огонёк войдёт в светлую зону.`}
      disabled={disabled}
      onToken={(token) => {
        if (token === 'space') {
          submit();
        }
      }}
    >
      <MeterTrack width={config.width} marker={position} successStart={config.successStart} successWidth={config.successWidth} partialStart={config.partialStart} partialWidth={config.partialWidth} />
      <button type="button" onClick={submit} disabled={disabled}>Поймать момент</button>
    </MiniGameFrame>
  );
}

function PromptChainGame({ config, disabled, onSubmit }: SharedGameProps<QtePromptChainCheckConfigDto>) {
  const [entered, setEntered] = useState<string[]>([]);
  const enteredRef = useRef<string[]>([]);
  const remainingMs = useQteDeadline(
    Math.max(config.timeoutMs, config.timeoutMs * config.sequence.length),
    !disabled,
    () => onSubmit(resolvePromptChainGrade(config.sequence, enteredRef.current, config.allowedMistakes)),
    `${config.timeoutMs}:${config.sequence.length}`
  );
  const submitToken = (token: string) => {
    const next = [...enteredRef.current, token];
    enteredRef.current = next;
    setEntered(next);
    if (next.length >= config.sequence.length) {
      onSubmit(resolvePromptChainGrade(config.sequence, next, config.allowedMistakes));
    }
  };

  return (
    <MiniGameFrame
      kind="PromptChain"
      title="Цепь знаков"
      instructions={`Повторите цепь: ${formatTokenList(config.sequence)}. Осталось ${formatRemainingMs(remainingMs)}.`}
      disabled={disabled}
      onToken={submitToken}
    >
      <TokenProgress expected={config.sequence} entered={entered} />
      <TokenButtons tokens={uniqueTokens(config.sequence)} disabled={disabled} onToken={submitToken} />
    </MiniGameFrame>
  );
}

function BalanceMeterGame({ config, disabled, onSubmit }: SharedGameProps<QteBalanceMeterCheckConfigDto>) {
  const [value, setValue] = useState(50);
  const [safeTicks, setSafeTicks] = useState(0);
  const [ticks, setTicks] = useState(0);
  const safeTicksRef = useRef(0);
  const ticksRef = useRef(0);
  const driftRef = useRef(1);
  const completedRef = useRef(false);
  const inSafeZone = Math.abs(value - 50) <= config.safeHalfWidth;

  const sample = (nextValue = value) => {
    if (completedRef.current) {
      return;
    }

    const nextTicks = Math.min(config.ticks, ticksRef.current + 1);
    ticksRef.current = nextTicks;
    setTicks(nextTicks);
    if (Math.abs(nextValue - 50) <= config.safeHalfWidth) {
      safeTicksRef.current += 1;
      setSafeTicks(safeTicksRef.current);
    }

    if (nextTicks >= config.ticks) {
      completedRef.current = true;
      onSubmit(resolveBalanceMeterGrade(safeTicksRef.current, nextTicks));
    }
  };
  const adjust = (delta: number) => {
    const nextValue = Math.max(0, Math.min(100, value + delta));
    setValue(nextValue);
    sample(nextValue);
  };

  useEffect(() => {
    if (disabled) {
      return undefined;
    }

    const interval = window.setInterval(() => {
      setValue((current) => {
        const nextValue = Math.max(0, Math.min(100, current + driftRef.current * 4));
        if (nextValue >= 68 || nextValue <= 32) {
          driftRef.current *= -1;
        }

        sample(nextValue);
        return nextValue;
      });
    }, Math.max(70, config.tickMs));
    return () => window.clearInterval(interval);
  }, [config.tickMs, disabled]);

  return (
    <MiniGameFrame
      kind="BalanceMeter"
      title="Равновесие"
      instructions={`${formatToken('a')} тянет влево, ${formatToken('d')} тянет вправо. Держите метку у центра.`}
      disabled={disabled}
      onToken={(token) => {
        if (token === 'a') {
          adjust(-10);
        } else if (token === 'd') {
          adjust(10);
        }
      }}
    >
      <div className="qte-meter" aria-label={`Баланс ${value}`}>
        <span className="qte-meter__safe" style={{ left: `${50 - config.safeHalfWidth}%`, width: `${config.safeHalfWidth * 2}%` }} />
        <span className="qte-meter__marker" style={{ left: `${value}%` }} />
      </div>
      <p className="muted">{inSafeZone ? 'Метка держится в безопасной зоне.' : 'Метка уходит от центра.'} Шаг {ticks}/{config.ticks}</p>
      <div className="phase-chip-grid">
        <button type="button" onClick={() => adjust(-10)} disabled={disabled}>Влево</button>
        <button type="button" onClick={() => adjust(10)} disabled={disabled}>Вправо</button>
      </div>
    </MiniGameFrame>
  );
}

function ChargeReleaseGame({ config, disabled, onSubmit }: SharedGameProps<QteChargeReleaseCheckConfigDto>) {
  const [charging, setCharging] = useState(false);
  const [charge, setCharge] = useState(0);

  useEffect(() => {
    if (!charging || disabled) {
      return undefined;
    }

    const timer = window.setInterval(() => {
      setCharge((current) => {
        const next = Math.min(100, current + 5);
        if (next >= 100) {
          onSubmit('fail');
        }

        return next;
      });
    }, Math.max(40, config.tickMs));

    return () => window.clearInterval(timer);
  }, [charging, config.tickMs, disabled, onSubmit]);

  const trigger = () => {
    if (!charging) {
      setCharging(true);
      return;
    }

    onSubmit(resolveChargeReleaseGrade(config, charge));
  };

  return (
    <MiniGameFrame
      kind="ChargeRelease"
      title="Накопление силы"
      instructions={`Нажмите ${formatToken('space')}, чтобы начать заряд, затем отпустите его в светлом окне.`}
      disabled={disabled}
      onToken={(token) => {
        if (token === 'space') {
          trigger();
        }
      }}
    >
      <ChargeTrack charge={charge} targetStart={config.targetStart} targetWidth={config.targetWidth} />
      <button type="button" onClick={trigger} disabled={disabled}>{charging ? 'Отпустить заряд' : 'Начать заряд'}</button>
    </MiniGameFrame>
  );
}

function BranchChoiceGame({ disabled, onSubmit }: Pick<QteMiniGameProps, 'disabled' | 'onSubmit'>) {
  return (
    <MiniGameFrame
      kind="BranchChoice"
      title="Выбор ветви"
      instructions="Здесь важен сам выбор действия; книга уже знает его вес."
      disabled={disabled}
    >
      <button type="button" onClick={() => onSubmit(null)} disabled={disabled}>Выбрать действие</button>
    </MiniGameFrame>
  );
}

function MashInputGame({ config, disabled, onSubmit }: SharedGameProps<QteMashInputCheckConfigDto>) {
  const accepted = useMemo(() => new Set(config.keys), [config.keys]);
  const [presses, setPresses] = useState(0);
  const pressesRef = useRef(0);
  const remainingMs = useQteDeadline(
    config.durationMs,
    !disabled,
    () => onSubmit(resolveMashInputGrade(config, pressesRef.current)),
    `${config.durationMs}:${config.successTarget}`
  );
  const submitPress = (token: string) => {
    if (!accepted.has(token)) {
      return;
    }

    const next = pressesRef.current + 1;
    pressesRef.current = next;
    setPresses(next);
    if (next >= config.successTarget) {
      onSubmit('success');
    }
  };

  return (
    <MiniGameFrame
      kind="MashInput"
      title="Рывок усилия"
      instructions={`Быстро нажимайте ${formatTokenList(config.keys)} до заполнения шкалы.`}
      disabled={disabled}
      onToken={submitPress}
    >
      <ProgressBar value={presses} max={config.successTarget} />
      <p className="muted">Нажатия: {presses}/{config.successTarget}; частичный успех начинается с {config.partialTarget}. Осталось {formatRemainingMs(remainingMs)}.</p>
      <div className="phase-chip-grid">
        {config.keys.map((token) => (
          <button key={token} type="button" onClick={() => submitPress(token)} disabled={disabled}>{formatToken(token)}</button>
        ))}
        <button type="button" onClick={() => onSubmit(resolveMashInputGrade(config, presses))} disabled={disabled}>Завершить рывок</button>
      </div>
    </MiniGameFrame>
  );
}

function PatternMemoryGame({ config, disabled, onSubmit }: SharedGameProps<QtePatternMemoryCheckConfigDto>) {
  const [phase, setPhase] = useState<'reveal' | 'input'>('reveal');
  const [entered, setEntered] = useState<string[]>([]);
  const enteredRef = useRef<string[]>([]);
  const inputActive = phase === 'input' && !disabled;
  const remainingMs = useQteDeadline(
    config.inputTimeoutMs,
    inputActive,
    () => onSubmit(resolvePatternMemoryGrade(config.sequence, enteredRef.current, config.allowedMistakes, true)),
    phase
  );

  useEffect(() => {
    if (disabled || phase !== 'reveal') {
      return undefined;
    }

    const timeout = window.setTimeout(() => setPhase('input'), Math.max(300, config.revealMs));
    return () => window.clearTimeout(timeout);
  }, [config.revealMs, disabled, phase]);

  const submitToken = (token: string) => {
    if (phase !== 'input') {
      return;
    }

    const next = [...enteredRef.current, token];
    enteredRef.current = next;
    setEntered(next);
    if (next.length >= config.sequence.length) {
      onSubmit(resolvePatternMemoryGrade(config.sequence, next, config.allowedMistakes));
    }
  };

  return (
    <MiniGameFrame
      kind="PatternMemory"
      title="Память рун"
      instructions={phase === 'reveal'
        ? `Запомните порядок знаков. Ввод начнётся через ${formatRemainingMs(config.revealMs)}.`
        : `Повторите порядок по памяти. Осталось ${formatRemainingMs(remainingMs)}.`}
      disabled={disabled}
      onToken={submitToken}
    >
      {phase === 'reveal'
        ? <TokenReveal sequence={config.sequence} />
        : <TokenProgress expected={config.sequence} entered={entered} hideExpected />}
      <TokenButtons tokens={uniqueTokens((config.alphabet?.length ?? 0) > 0 ? config.alphabet : config.sequence)} disabled={disabled || phase !== 'input'} onToken={submitToken} />
    </MiniGameFrame>
  );
}

function RhythmPulseGame({ config, disabled, onSubmit }: SharedGameProps<QteRhythmPulseCheckConfigDto>) {
  const [startedAt] = useState(() => Date.now());
  const [hits, setHits] = useState<number[]>([]);
  const hitsRef = useRef<number[]>([]);
  const totalDurationMs = Math.max(1, (config.pulseOffsetsMs[config.pulseOffsetsMs.length - 1] ?? 0) + config.hitWindowMs);
  const remainingMs = useQteDeadline(
    totalDurationMs,
    !disabled,
    () => onSubmit(resolveRhythmPulseGrade(config, hitsRef.current)),
    totalDurationMs
  );
  const hit = () => {
    const next = [...hitsRef.current, Date.now() - startedAt];
    hitsRef.current = next;
    setHits(next);
  };

  return (
    <MiniGameFrame
      kind="RhythmPulse"
      title="Пульс ритма"
      instructions={`Нажимайте ${formatToken('space')} около зарубок ритма. Звук не требуется.`}
      disabled={disabled}
      onToken={(token) => {
        if (token === 'space') {
          hit();
        }
      }}
    >
      <RhythmTrack offsets={config.pulseOffsetsMs} hitWindowMs={config.hitWindowMs} />
      <p className="muted">Попытки: {hits.length}/{config.pulseOffsetsMs.length}; допустимые промахи: {config.allowedMisses}. Осталось {formatRemainingMs(remainingMs)}.</p>
      <div className="phase-chip-grid">
        <button type="button" onClick={hit} disabled={disabled}>Ударить в пульс</button>
      </div>
    </MiniGameFrame>
  );
}

function PrecisionChoiceGame({ config, disabled, onSubmit }: SharedGameProps<QtePrecisionChoiceCheckConfigDto>) {
  const remainingMs = useQteDeadline(
    config.timeoutMs,
    !disabled,
    () => onSubmit(resolvePrecisionChoiceGrade(config.choices, null, true, config.timeoutGrade)),
    `${config.timeoutMs}:${config.choices.length}`
  );
  const choose = (choiceId: string) => onSubmit(resolvePrecisionChoiceGrade(config.choices, choiceId, false, config.timeoutGrade));

  return (
    <MiniGameFrame
      kind="PrecisionChoice"
      title="Точный выбор"
      instructions={`Выберите вариант до того, как момент уйдёт. Осталось ${formatRemainingMs(remainingMs)}.`}
      disabled={disabled}
      onKeyDown={(event) => {
        const key = event.key.trim();
        if (/^[1-8]$/.test(key)) {
          const choice = config.choices[Number(key) - 1];
          if (choice) {
            event.preventDefault();
            choose(choice.id);
          }
        }
      }}
    >
      <div className="qte-choice-grid">
        {config.choices.map((choice, index) => (
          <button
            key={choice.id}
            type="button"
            onClick={() => choose(choice.id)}
            disabled={disabled}
          >
            <span>{index + 1}. {toPlayerFacingText(choice.label, 'Вариант')}</span>
            {choice.description && <small>{toPlayerFacingText(choice.description, 'Описание скрыто в сцене.')}</small>}
          </button>
        ))}
      </div>
    </MiniGameFrame>
  );
}

function StealthNoiseGame({ config, disabled, onSubmit }: SharedGameProps<QteStealthNoiseCheckConfigDto>) {
  const [startedAt] = useState(() => Date.now());
  const [recoveries, setRecoveries] = useState<number[]>([]);
  const recoveriesRef = useRef<number[]>([]);
  const remainingMs = useQteDeadline(
    config.durationMs,
    !disabled,
    () => onSubmit(resolveStealthNoiseGrade(config, recoveriesRef.current)),
    `${config.durationMs}:${config.recoveryPerInput}`
  );
  const recover = () => {
    const next = [...recoveriesRef.current, Date.now() - startedAt];
    recoveriesRef.current = next;
    setRecoveries(next);
  };
  const label = toPlayerFacingText(config.recoveryLabel ?? '', 'приглушить шум');

  return (
    <MiniGameFrame
      kind="StealthNoise"
      title="Тихий проход"
      instructions={`Нажимайте ${formatToken(config.recoveryKey)}, чтобы ${label}, пока шкала шума ниже опасной метки.`}
      disabled={disabled}
      onToken={(token) => {
        if (token === config.recoveryKey) {
          recover();
        }
      }}
    >
      <NoiseTrack config={config} recoveries={recoveries.length} />
      <p className="muted">Сбросы шума: {recoveries.length}. Опасный порог: {config.dangerThreshold}. Осталось {formatRemainingMs(remainingMs)}.</p>
      <div className="phase-chip-grid">
        <button type="button" onClick={recover} disabled={disabled}>{label}</button>
      </div>
    </MiniGameFrame>
  );
}

function LockPinSetGame({ config, disabled, onSubmit }: SharedGameProps<QteLockPinSetCheckConfigDto>) {
  const [startedAt] = useState(() => Date.now());
  const [currentPin, setCurrentPin] = useState(0);
  const [position, setPosition] = useState(50);
  const [attempts, setAttempts] = useState<QteLockPinAttempt[]>([]);
  const attemptsRef = useRef<QteLockPinAttempt[]>([]);
  const [opened, setOpened] = useState<boolean[]>(() => Array.from({ length: config.pinCount }, () => false));
  const pinName = toPlayerFacingText(config.pinLabel ?? '', 'штифт');
  const remainingMs = useQteDeadline(
    config.timerMs,
    !disabled,
    () => onSubmit(resolveLockPinSetGrade(config, attemptsRef.current)),
    `${config.timerMs}:${config.pinCount}`
  );

  useEffect(() => {
    if (disabled) {
      return undefined;
    }

    const interval = window.setInterval(() => {
      setPosition((current) => Math.max(0, Math.min(100, current + config.pinDriftPerSecond / 4)));
    }, 250);
    return () => window.clearInterval(interval);
  }, [config.pinDriftPerSecond, disabled]);

  const adjust = (delta: number) => setPosition((current) => Math.max(0, Math.min(100, current + delta)));
  const setPin = () => {
    const attempt = {
      offsetMs: Date.now() - startedAt,
      pinIndex: currentPin,
      position
    };
    const nextAttempts = [...attemptsRef.current, attempt];
    attemptsRef.current = nextAttempts;
    setAttempts(nextAttempts);

    if (isInsideWindow(config.pinWindows[currentPin], position)) {
      const nextOpened = opened.map((value, index) => index === currentPin ? true : value);
      setOpened(nextOpened);
      if (nextOpened.every(Boolean)) {
        onSubmit(resolveLockPinSetGrade(config, nextAttempts));
        return;
      }

      const nextPin = nextOpened.findIndex((value) => !value);
      setCurrentPin(nextPin >= 0 ? nextPin : currentPin);
      setPosition(50);
    }
  };

  return (
    <MiniGameFrame
      kind="LockPinSet"
      title="Штифты замка"
      instructions={`${formatToken(config.adjustKey)} двигает ${pinName}, Shift+${formatToken(config.adjustKey)} опускает, ${formatToken(config.setKey)} фиксирует.`}
      disabled={disabled}
      onKeyDown={(event) => {
        const token = normalizeQteKeyboardInput(event);
        if (token === config.adjustKey && event.shiftKey) {
          event.preventDefault();
          adjust(-5);
        } else if (token === config.adjustKey) {
          event.preventDefault();
          adjust(5);
        } else if (token === config.setKey) {
          event.preventDefault();
          setPin();
        }
      }}
    >
      <LockTrack config={config} currentPin={currentPin} position={position} opened={opened} />
      <p className="muted">Осталось {formatRemainingMs(remainingMs)}. Попытки: {attempts.length}. Допустимые ошибки: {config.maxMistakes}.</p>
      <div className="phase-chip-grid">
        <button type="button" onClick={() => adjust(-5)} disabled={disabled}>Опустить</button>
        <button type="button" onClick={() => adjust(5)} disabled={disabled}>Поднять</button>
        <button type="button" onClick={setPin} disabled={disabled}>Зафиксировать {pinName}</button>
      </div>
    </MiniGameFrame>
  );
}

function UnsupportedQteGame() {
  return (
    <MiniGameFrame
      kind="Unsupported"
      title="Сцена ждёт обновления"
      instructions="Эта быстрая сцена ждёт обновления книги. Выберите другое действие или вернитесь после обновления клиента."
    >
      <p className="muted">Обычная панель не открывает обходной выбор результата для этого испытания.</p>
    </MiniGameFrame>
  );
}

function MiniGameFrame({
  kind,
  title,
  instructions,
  children,
  disabled = false,
  onToken,
  onKeyDown
}: {
  kind: string;
  title: string;
  instructions: string;
  children: ReactNode;
  disabled?: boolean;
  onToken?: (token: string) => void;
  onKeyDown?: (event: KeyboardEvent<HTMLDivElement>) => void;
}) {
  return (
    <div
      className="qte-mini-game"
      data-qte-mini-game={kind}
      tabIndex={0}
      onKeyDown={(event) => {
        if (disabled) {
          return;
        }

        if (!shouldHandleQteFrameShortcut(event)) {
          return;
        }

        if (onKeyDown) {
          onKeyDown(event);
          return;
        }

        const token = normalizeQteKeyboardInput(event);
        if (token && onToken) {
          event.preventDefault();
          onToken(token);
        }
      }}
    >
      <div className="qte-mini-game__header">
        <strong>{title}</strong>
        <span>мини-игра</span>
      </div>
      <p>{instructions}</p>
      {children}
    </div>
  );
}

function shouldHandleQteFrameShortcut(event: KeyboardEvent<HTMLDivElement>): boolean {
  const target = event.target;
  if (!(target instanceof Element)) {
    return true;
  }

  if (target === event.currentTarget) {
    return true;
  }

  return target.closest('button, a, input, select, textarea, [role="button"], [contenteditable="true"]') === null;
}

function MeterTrack({
  width,
  marker,
  successStart,
  successWidth,
  partialStart,
  partialWidth
}: {
  width: number;
  marker: number;
  successStart: number;
  successWidth: number;
  partialStart: number;
  partialWidth: number;
}) {
  return (
    <div className="qte-bar" aria-label="Полоса реакции">
      {Array.from({ length: width }).map((_, index) => {
        const isSuccess = index >= successStart && index < successStart + successWidth;
        const isPartial = index >= partialStart && index < partialStart + partialWidth;
        return (
          <span
            key={index}
            className={[
              'qte-bar__cell',
              isSuccess ? 'is-success' : '',
              !isSuccess && isPartial ? 'is-partial' : '',
              index === marker ? 'is-marker' : ''
            ].filter(Boolean).join(' ')}
          />
        );
      })}
    </div>
  );
}

function ChargeTrack({ charge, targetStart, targetWidth }: { charge: number; targetStart: number; targetWidth: number }) {
  return (
    <div className="qte-meter" aria-label={`Заряд ${charge}`}>
      <span className="qte-meter__safe" style={{ left: `${targetStart}%`, width: `${targetWidth}%` }} />
      <span className="qte-meter__marker" style={{ left: `${charge}%` }} />
    </div>
  );
}

function ProgressBar({ value, max }: { value: number; max: number }) {
  const width = max > 0 ? Math.min(100, Math.max(0, value / max * 100)) : 0;
  return (
    <div className="qte-progress" aria-label={`Прогресс ${value} из ${max}`}>
      <span style={{ width: `${width}%` }} />
    </div>
  );
}

function TokenReveal({ sequence }: { sequence: readonly string[] }) {
  return (
    <div className="qte-token-row" aria-label="Показ последовательности">
      {sequence.map((token, index) => (
        <span key={`${token}-${index}`}>{formatToken(token)}</span>
      ))}
    </div>
  );
}

function TokenProgress({ expected, entered, hideExpected = false }: { expected: readonly string[]; entered: readonly string[]; hideExpected?: boolean }) {
  return (
    <div className="qte-token-row" aria-label="Ход последовательности">
      {expected.map((token, index) => (
        <span key={`${token}-${index}`} className={entered[index] ? (entered[index] === token ? 'is-success' : 'is-fail') : ''}>
          {entered[index] ? formatToken(entered[index]) : hideExpected ? '...' : formatToken(token)}
        </span>
      ))}
    </div>
  );
}

function TokenButtons({ tokens, disabled, onToken }: { tokens: readonly string[]; disabled: boolean; onToken: (token: string) => void }) {
  return (
    <div className="phase-chip-grid">
      {tokens.map((token) => (
        <button key={token} type="button" onClick={() => onToken(token)} disabled={disabled}>{formatToken(token)}</button>
      ))}
    </div>
  );
}

function RhythmTrack({ offsets, hitWindowMs }: { offsets: readonly number[]; hitWindowMs: number }) {
  const total = Math.max(1, offsets[offsets.length - 1] ?? 1);
  return (
    <div className="qte-rhythm-track" aria-label="Дорожка ритма">
      {offsets.map((offset, index) => (
        <span key={`${offset}-${index}`} style={{ left: `${Math.min(98, Math.max(2, offset / total * 100))}%` }}>
          {Math.round(hitWindowMs)} мс
        </span>
      ))}
    </div>
  );
}

function NoiseTrack({ config, recoveries }: { config: Pick<QteStealthNoiseCheckConfigDto, 'startingNoise' | 'dangerThreshold'>; recoveries: number }) {
  const noise = Math.max(0, Math.min(100, config.startingNoise + 25 - recoveries * 8));
  return (
    <div className="qte-meter" aria-label={`Шум ${noise}`}>
      <span className="qte-meter__danger" style={{ left: `${config.dangerThreshold}%` }} />
      <span className="qte-meter__fill" style={{ width: `${noise}%` }} />
    </div>
  );
}

function LockTrack({ config, currentPin, position, opened }: { config: QteLockPinSetCheckConfigDto; currentPin: number; position: number; opened: readonly boolean[] }) {
  return (
    <div className="qte-lock-grid" aria-label="Штифты замка">
      {config.pinWindows.map((window, index) => (
        <div key={window.pin} className={opened[index] ? 'is-open' : index === currentPin ? 'is-current' : ''}>
          <span>{toPlayerFacingText(window.label ?? '', `Штифт ${window.pin}`)}</span>
          <div className="qte-meter">
            <span className="qte-meter__safe" style={{ left: `${window.min}%`, width: `${window.max - window.min}%` }} />
            <span className="qte-meter__marker" style={{ left: `${index === currentPin ? position : (window.min + window.max) / 2}%` }} />
          </div>
        </div>
      ))}
    </div>
  );
}

function uniqueTokens(tokens: readonly string[]): string[] {
  return Array.from(new Set(tokens));
}

function formatTokenList(tokens: readonly string[]): string {
  return tokens.map(formatToken).join(' - ');
}

function formatRemainingMs(remainingMs: number): string {
  return `${Math.max(0, Math.ceil(remainingMs / 1000))} с`;
}

function formatToken(token: string): string {
  const normalized = token.trim().toLowerCase();
  return isQteKeyToken(normalized) ? formatQteKeyTokenLabel(normalized) : normalized.toUpperCase();
}

function isQteKeyToken(token: string): token is QteKeyToken {
  return token === 'q' || token === 'w' || token === 'e' || token === 'a' || token === 's' || token === 'd' || token === 'space';
}

function isInsideWindow(window: QteLockPinWindowDto | undefined, position: number): boolean {
  return Boolean(window && position >= window.min && position <= window.max);
}

type SharedGameProps<TConfig> = {
  config: TConfig;
  disabled: boolean;
  onSubmit: (grade: QteMiniGameGrade) => void;
};
