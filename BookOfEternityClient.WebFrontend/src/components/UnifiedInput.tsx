import { useRef, useState } from 'react';
import { isSuccess, useShell } from '../context/ShellContext';
import { CommandAutocomplete } from './CommandAutocomplete';

export function UnifiedInput() {
  const { composerText, setComposerText, composerNotice, submitComposer, submitComposerText, readyState, gameScreen } = useShell();
  const [showAutocomplete, setShowAutocomplete] = useState(false);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  const coverage = readyState?.commandCoverage;
  const commands = coverage && isSuccess(coverage) ? coverage.data.commands : [];
  const canSubmit = gameScreen?.actionComposer.canSubmit ?? false;

  function handleChange(value: string) {
    setComposerText(value);
    setShowAutocomplete(value.startsWith('/') && value.length > 1);
  }

  function handleAutocompleteSelect(command: string) {
    setComposerText(command + ' ');
    setShowAutocomplete(false);
    inputRef.current?.focus();
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Escape') {
      setShowAutocomplete(false);
      return;
    }
    // Enter without Shift submits form; Shift+Enter inserts newline
    if (e.key === 'Enter' && !e.shiftKey) {
      if (e.nativeEvent.isComposing) return;
      e.preventDefault();
      if (showAutocomplete) {
        setShowAutocomplete(false);
      }
      if (e.currentTarget.value.trim() && canSubmit) {
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
            onFocus={() => { if (composerText.startsWith('/')) setShowAutocomplete(true); }}
            onBlur={() => setTimeout(() => setShowAutocomplete(false), 200)}
            placeholder="Опишите действие или введите /команду..."
            disabled={!canSubmit}
            className="unified-input__textarea"
          />
          {showAutocomplete && (
            <CommandAutocomplete
              commands={commands}
              query={composerText}
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
