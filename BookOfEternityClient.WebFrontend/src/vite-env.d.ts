/// <reference types="vite/client" />

// Side-effect CSS imports from @fontsource packages don't have TypeScript
// type declarations — declare them as modules so tsc doesn't error.
declare module '@fontsource-variable/inter';
declare module '@fontsource/cinzel/*';
declare module '@fontsource/cinzel-decorative/*';
declare module '@fontsource/cormorant-garamond/*';
declare module '@fontsource/jetbrains-mono/*';
