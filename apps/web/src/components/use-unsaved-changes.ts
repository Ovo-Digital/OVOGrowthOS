'use client';
import { useEffect } from 'react';
import { useDialog } from '@/components/ui/modal';

export function useUnsavedChanges(dirty: boolean) {
  const { confirm } = useDialog();
  useEffect(() => {
    if (!dirty) return;
    const unload = (e: BeforeUnloadEvent) => { e.preventDefault(); e.returnValue = ''; };
    const leaving = async (e: MouseEvent) => {
      const link = e.target instanceof Element ? e.target.closest('a') : null;
      if (!link || e.defaultPrevented || e.metaKey || e.ctrlKey || link.target === '_blank') return;
      const url = new URL(link.href, window.location.href);
      if (url.pathname === window.location.pathname && url.search === window.location.search) return;
      e.preventDefault(); e.stopPropagation();
      if (await confirm({ title: 'Sayfadan ayrılıyor musunuz?', message: 'Kaydedilmemiş değişiklikler var. Kaydetmeden ayrılmak istiyor musunuz?' })) {
        window.location.assign(url.pathname + url.search + url.hash);
      }
    };
    window.addEventListener('beforeunload', unload); document.addEventListener('click', leaving, true);
    return () => { window.removeEventListener('beforeunload', unload); document.removeEventListener('click', leaving, true); };
  }, [dirty, confirm]);
}
