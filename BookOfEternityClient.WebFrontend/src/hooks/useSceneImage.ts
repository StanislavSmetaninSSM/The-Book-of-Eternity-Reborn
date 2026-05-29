import { useEffect, useRef, useState } from 'react';
import { browserApi } from '../api/client';
import type { BrowserGameScreenMediaItemDto } from '../api/contracts';

interface SceneImageState {
  url: string | null;
  loading: boolean;
  error: string | null;
}

function sanitizeEntityKey(value: string): string {
  const sanitized = value
    .replace(/[<>:"/\\|?*\u0000-\u001F]/g, '_')
    .slice(0, 80)
    .trim()
    .replace(/[.\s]+$/g, '');

  return sanitized || 'entity';
}

function matchesImageKind(item: BrowserGameScreenMediaItemDto, entityKey: string): boolean {
  const stem = item.fileName.replace(/\.[^.]+$/, '');
  return stem === entityKey || stem.startsWith(`${entityKey}__img_`);
}

export function useSceneImage(
  sceneImagePrompt: string | null | undefined,
  gallery: BrowserGameScreenMediaItemDto[],
  imageKind: 'scene' | 'location' = 'scene',
  entityIdentity?: string | null
): SceneImageState {
  const [state, setState] = useState<SceneImageState>({ url: null, loading: false, error: null });
  const generatingRef = useRef<string | null>(null);
  const lastPromptRef = useRef<string | null>(null);

  useEffect(() => {
    const entityKey = sanitizeEntityKey(entityIdentity ?? sceneImagePrompt ?? imageKind);
    const sceneImage = gallery.find(item => matchesImageKind(item, entityKey));

    if (sceneImage) {
      if (generatingRef.current === entityKey) {
        generatingRef.current = null;
      }
      setState({ url: sceneImage.url, loading: false, error: null });
      return;
    }

    if (!sceneImagePrompt || generatingRef.current === entityKey || lastPromptRef.current === entityKey) {
      return;
    }

    lastPromptRef.current = entityKey;
    generatingRef.current = entityKey;
    setState({ url: null, loading: true, error: null });

    browserApi.generateMedia({
      prompt: sceneImagePrompt,
      entityType: imageKind,
      entityKey
    }).then(result => {
      if (generatingRef.current !== entityKey) {
        return;
      }

      generatingRef.current = null;
      if (result.ok && result.data.success && result.data.url) {
        setState({ url: result.data.url, loading: false, error: null });
      } else {
        const msg = result.ok ? result.data.errorMessage : null;
        setState({ url: null, loading: false, error: msg || null });
      }
    }).catch(() => {
      if (generatingRef.current !== entityKey) {
        return;
      }

      generatingRef.current = null;
      setState({ url: null, loading: false, error: null });
    });
  }, [sceneImagePrompt, gallery, imageKind, entityIdentity]);

  return state;
}
