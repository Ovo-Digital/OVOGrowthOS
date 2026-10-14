'use client';
import { useEffect, useRef, useState } from 'react';

// Sunucuyla konuşan her işlem lib/api üzerinden geçtiği için tek sayaç bütün
// düğme işlemlerini kapsar. Kısa işlemlerde göz kırpmaması için 250 ms gecikmeli gösterilir.
export function PendingBar() {
    const [visible, setVisible] = useState(false);
    const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
    useEffect(() => {
        const handler = (event: Event) => {
            const busy = (event as CustomEvent<{ busy: boolean }>).detail.busy;
            if (timer.current) { clearTimeout(timer.current); timer.current = null; }
            if (busy) timer.current = setTimeout(() => setVisible(true), 250);
            else setVisible(false);
        };
        window.addEventListener('ovo:busy', handler);
        return () => {
            window.removeEventListener('ovo:busy', handler);
            if (timer.current) clearTimeout(timer.current);
        };
    }, []);
    if (!visible) return null;
    return (
        <div role="status" aria-label="İşlem sürüyor" className="fixed inset-x-0 top-0 z-[200]">
            <div className="h-1 animate-pulse bg-[#008060]" />
            <span className="sr-only">İşlem sürüyor…</span>
        </div>
    );
}
