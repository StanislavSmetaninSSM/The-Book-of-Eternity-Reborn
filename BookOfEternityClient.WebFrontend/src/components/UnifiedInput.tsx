import { useEffect, useMemo, useRef, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { CommandAutocomplete } from './CommandAutocomplete';
import {
  getAutocompleteMatches,
  moveAutocompleteHighlight,
  resolveAutocompleteEnterAction
} from './commandAutocompleteLogic';

export function UnifiedInput() {
  const { composerText, setComposerText, composerNotice, submitComposer, submitComposerText, readyState, gameScreen } = useShell();
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const [activeAutocompleteIndex, setActiveAutocompleteIndex] = useState<number | null>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];
  const canSubmit = gameScreen?.actionComposer.canSubmit ?? false;
  const autocompleteMatches = useMemo(
    () => getAutocompleteMatches(commands, composerText),
    [commands, composerText]
  );
  const isAutocompleteOpen = showAutocomplete && autocompleteMatches.length > 0;
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
    setShowAutocomplete(value.startsWith('/') && value.length > 1);
    setActiveAutocompleteIndex(null);
  }

  function handleAutocompleteSelect(command: string) {
    setComposerText(command + ' ');
    closeAutocomplete();
    inputRef.current?.focus();
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
    // Enter without Shift submits form; Shift+Enter inserts newline
    if (e.key === 'Enter' && !e.shiftKey) {
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
    <div className="unified-input">
      {composerNotice && <p className="unified-input__notice">{composerNotice}</p>}
      <form className="unified-input__form" onSubmit={submitComposer}>
        <div className="unified-input__wrapper">
          <textarea
            ref={inputRef}
            rows={1}
            value={composerText}
            onChange={(e) => handleChange(e.target.value)}
            onKeyDown={handleKeyDown}
            onFocus={() => { if (composerText.startsWith('/') && composerText.length > 1) setShowAutocomplete(true); }}
            onBlur={() => setTimeout(closeAutocomplete, 200)}
            placeholder="Опишите действие или введите /команду..."
            disabled={!canSubmit}
            className="unified-input__textarea"
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
