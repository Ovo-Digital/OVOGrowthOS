'use client';
import { useEffect } from 'react';

export function useUnsavedChanges(dirty: boolean) {
  useEffect(() => {
    if (!dirty) return;
    const unload = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ''; };
    const leaving = (e: MouseEvent) => {
      const link = e.target instanceof Element ? e.target.closest('a') : null;
      if (!link || e.defaultPrevented || e.metaKey || e.ctrlKey || link.target === '_blank') return;
      const url = new URL(link.href, window.location.href);
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;
      if (!window.confirm('Kaydedilmemiş değişiklikler var. Kaydetmeden ayrılmak istiyor musunuz?')) { e.preventDefault(); e.stopPropagation(); }
    };
    window.addEventListener('beforeunload', unload); document.addEventListener('click', leaving, true);
    return () => { window.removeEventListener('beforeunload', unload); document.removeEventListener('click', leaving, true); };
  }, [dirty]);
}
