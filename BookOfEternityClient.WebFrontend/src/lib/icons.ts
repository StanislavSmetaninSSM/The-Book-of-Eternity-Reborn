import {
  BookOpen,
  Swords,
  Heart,
  Globe2,
  ScrollText,
  Backpack,
  Image as ImageIcon,
  Settings,
  HelpCircle,
  Gauge,
  Link2,
  Scale,
  Zap,
  GitBranch,
  Hand,
  Brain,
  Activity,
  Crosshair,
  Eye,
  KeyRound,
  Hourglass,
  type LucideIcon,
} from 'lucide-react';

/**
 * Tab glyph IDs (mirrors src/components/tabBarConfig.ts TabGlyphId).
 * These map to lucide-react icons so the tab bar stays emoji-free (rule #721).
 */
export type TabGlyphId = 'scene' | 'practice' | 'status' | 'help' | 'settings';

const tabIconRegistry: Record<TabGlyphId, LucideIcon> = {
  scene: BookOpen,
  practice: Swords,
  status: Heart,
  help: HelpCircle,
  settings: Settings,
};

export function getTabIcon(id: TabGlyphId): LucideIcon {
  return tabIconRegistry[id] ?? BookOpen;
}

/**
 * Generic route icon registry (for future expansion if the app introduces
 * more route icons). Falls back to BookOpen for unknown IDs.
 */
export type RouteIconId =
  | 'home'
  | 'game'
  | 'soul'
  | 'world'
  | 'journal'
  | 'inventory'
  | 'media'
  | 'settings'
  | 'help'
  | string;

const routeIconRegistry: Record<string, LucideIcon> = {
  home: BookOpen,
  game: Swords,
  soul: Heart,
  world: Globe2,
  journal: ScrollText,
  inventory: Backpack,
  media: ImageIcon,
  settings: Settings,
  help: HelpCircle,
};

export function getRouteIcon(id: RouteIconId): LucideIcon {
  return routeIconRegistry[id] ?? BookOpen;
}

/**
 * QTE mini-game type glyph registry. Maps each practice catalog typeId to a
 * distinctive lucide-react icon so catalog cards are instantly recognizable
 * and stay emoji-free (rule #721). Unknown types fall back to an hourglass.
 */
export type QteTypeGlyphId =
  | 'TimingBar'
  | 'PromptChain'
  | 'BalanceMeter'
  | 'ChargeRelease'
  | 'BranchChoice'
  | 'MashInput'
  | 'PatternMemory'
  | 'RhythmPulse'
  | 'PrecisionChoice'
  | 'StealthNoise'
  | 'LockPinSet'
  | string;

const qteTypeGlyphRegistry: Record<string, LucideIcon> = {
  TimingBar: Gauge,
  PromptChain: Link2,
  BalanceMeter: Scale,
  ChargeRelease: Zap,
  BranchChoice: GitBranch,
  MashInput: Hand,
  PatternMemory: Brain,
  RhythmPulse: Activity,
  PrecisionChoice: Crosshair,
  StealthNoise: Eye,
  LockPinSet: KeyRound,
};

export function getQteTypeGlyph(typeId: QteTypeGlyphId): LucideIcon {
  return qteTypeGlyphRegistry[typeId] ?? Hourglass;
}
