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
