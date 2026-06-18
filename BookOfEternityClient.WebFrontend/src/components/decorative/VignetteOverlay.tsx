/**
 * Fixed-position vignette + film-grain layers for the whole shell.
 * Mount once at the App root, above everything else.
 * Both layers are aria-hidden and pointer-events:none.
 */
export function VignetteOverlay() {
  return (
    <>
      <div className="vignette-overlay" aria-hidden="true" />
      <div className="film-grain" aria-hidden="true" />
    </>
  );
}
