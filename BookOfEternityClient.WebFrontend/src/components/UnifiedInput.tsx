import { useEffect, useMemo, useRef, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { CommandAutocomplete } from './CommandAutocomplete';
import {
  getAutocompleteMatches,
  moveAutocompleteHighlight,
  resolveAutocompleteEnterAction
} from './commandAutocompleteLogic';

type ComposerMode = 'command' | 'post';

export function UnifiedInput() {
  const { composerText, setComposerText, composerNotice, submitComposer, submitComposerText, readyState, gameScreen } = useShell();
  const [composerMode, setComposerMode] = useState<ComposerMode>('command');
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const [activeAutocompleteIndex, setActiveAutocompleteIndex] = useState<number | null>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];
  const canSubmit = gameScreen?.actionComposer.canSubmit ?? false;
  const isPostMode = composerMode === 'post';
  const autocompleteMatches = useMemo(
    () => composerMode === 'command' ? getAutocompleteMatches(commands, composerText) : [],
    [commands, composerMode, composerText]
  );
  const isAutocompleteOpen = composerMode === 'command' && showAutocomplete && autocompleteMatches.length > 0;
  const autocompleteListId = 'command-autocomplete-list';
  const activeAutocompleteOptionId = activeAutocompleteIndex !== null &&
    activeAutocompleteIndex < autocompleteMatches.length &&
    isAutocompleteOpen
    ? `${autocompleteListId}-option-${activeAutocompleteIndex}`
    : undefined;

  useEffect(() => {
    setActiveAutocompleteIndex((current) =>
      current !== null && current >= autocompleteMatches.length ? null : current
    );
  }, [autocompleteMatches.length]);

  function closeAutocomplete() {
    setShowAutocomplete(false);
    setActiveAutocompleteIndex(null);
  }

  function handleChange(value: string) {
    setComposerText(value);
    setShowAutocomplete(composerMode === 'command' && value.startsWith('/') && value.length > 1);
    setActiveAutocompleteIndex(null);
  }

  function handleAutocompleteSelect(command: string) {
    setComposerText(command + ' ');
    closeAutocomplete();
    inputRef.current?.focus();
  }

  function switchComposerMode(nextMode: ComposerMode) {
    setComposerMode(nextMode);
    closeAutocomplete();
    requestAnimationFrame(() => inputRef.current?.focus());
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Escape') {
      if (showAutocomplete) e.preventDefault();
      closeAutocomplete();
      return;
    }
    if (isAutocompleteOpen && (e.key === 'ArrowDown' || e.key === 'ArrowUp')) {
      e.preventDefault();
      setActiveAutocompleteIndex((current) =>
        moveAutocompleteHighlight(
          current,
          e.key === 'ArrowDown' ? 'down' : 'up',
          autocompleteMatches.length
        )
      );
      return;
    }

    if (isPostMode && e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      if (e.nativeEvent.isComposing) return;
      e.preventDefault();
      closeAutocomplete();
      submitComposerText(e.currentTarget.value);
      return;
    }

    // Enter without Shift submits form; Shift+Enter inserts newline
    if (!isPostMode && e.key === 'Enter' && !e.shiftKey) {
      if (e.nativeEvent.isComposing) return;
      e.preventDefault();

      const enterAction = resolveAutocompleteEnterAction({
        isOpen: isAutocompleteOpen,
        activeIndex: activeAutocompleteIndex,
        matches: autocompleteMatches,
        text: e.currentTarget.value,
        canSubmit
      });

      if (enterAction.kind === 'select') {
        handleAutocompleteSelect(enterAction.command);
        return;
      }

      closeAutocomplete();
      if (enterAction.kind === 'submit') {
        submitComposerText(e.currentTarget.value);
      }
    }
  }

  return (
    <div className={`unified-input${isPostMode ? ' is-post-mode' : ''}`}>
      {composerNotice && <p className="unified-input__notice">{composerNotice}</p>}
      <div className="unified-input__mode-toggle" role="group" aria-label="Режим ввода">
        <button
          type="button"
          className={`unified-input__mode-button${composerMode === 'command' ? ' is-active' : ''}`}
          aria-pressed={composerMode === 'command'}
          onClick={() => switchComposerMode('command')}
        >
          Команда
        </button>
        <button
          type="button"
          className={`unified-input__mode-button${composerMode === 'post' ? ' is-active' : ''}`}
          aria-pressed={composerMode === 'post'}
          onClick={() => switchComposerMode('post')}
        >
          Художественный пост
        </button>
      </div>
      <form className="unified-input__form" onSubmit={submitComposer}>
        <div className="unified-input__wrapper">
          <textarea
            ref={inputRef}
            rows={composerMode === 'post' ? 8 : 1}
            value={composerText}
            onChange={(e) => handleChange(e.target.value)}
            onKeyDown={handleKeyDown}
            onFocus={() => { if (composerMode === 'command' && composerText.startsWith('/') && composerText.length > 1) setShowAutocomplete(true); }}
            onBlur={() => setTimeout(closeAutocomplete, 200)}
            placeholder={isPostMode ? 'Опишите действие развернутым художественным постом...' : 'Опишите действие или введите /команду...'}
            disabled={!canSubmit}
            className="unified-input__textarea"
            aria-label={isPostMode ? 'Художественный пост' : 'Команда или действие'}
            aria-controls={isAutocompleteOpen ? autocompleteListId : undefined}
            aria-expanded={isAutocompleteOpen}
            aria-activedescendant={activeAutocompleteOptionId}
          />
          {isAutocompleteOpen && (
            <CommandAutocomplete
              matches={autocompleteMatches}
              activeIndex={activeAutocompleteIndex}
              onSelect={handleAutocompleteSelect}
            />
          )}
        </div>
        <button type="submit" disabled={!composerText.trim() || !canSubmit} className="unified-input__submit">
          Отправить
        </button>
      </form>
    </div>
  );
}
