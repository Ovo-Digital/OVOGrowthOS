'use client';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function NotificationLink() {
  const query = useQuery({ queryKey: ['notification-count'], queryFn: () => api<{ items: { readAt: string | null }[] }>('/api/notifications'), refetchInterval: 60_000 });
  const count = query.data?.items.filter(x => !x.readAt).length ?? 0;
  return <Link href="/notifications" className="rounded-lg px-2 py-2 text-sm underline">Bildirimler{count > 0 ? ` (${count})` : ''}</Link>;
}
