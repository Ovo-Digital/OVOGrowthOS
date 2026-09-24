'use client';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Preferences = { dailyTasksEmail: boolean; taskDueEmail: boolean; portalMessagesEmail: boolean; portalReportsEmail: boolean; revision: number };
type Item = { id: string; title: string; href: string; createdAt: string; readAt: string | null; emailStatus: string | null };
export type NotificationList = { items: Item[]; preferences: Preferences | null; emailReady: boolean };
const statuses: Record<string, string> = { Pending: 'Gönderim bekliyor', Sending: 'Gönderiliyor', Sent: 'E-posta sunucusu kabul etti', Uncertain: 'Gönderim doğrulanamadı; otomatik tekrarlanmaz', Cancelled: 'Tercih, hesap veya kaynak değiştiği için gönderilmedi' };

export default function Notifications() {
  const qc = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const list = useQuery({ queryKey: ['notifications'], queryFn: async () => {
    await api('/api/notifications/refresh', { method: 'POST' });
    return api<NotificationList>('/api/notifications');
  }, refetchInterval: 60_000, staleTime: 30_000 });
  async function read(id: string) {
    setBusy(true); setMessage('');
    try { await api(`/api/notifications/${id}/read`, { method: 'POST' }); await qc.invalidateQueries({ queryKey: ['notifications'] }); }
    catch (e) { setMessage(e instanceof Error ? e.message : 'Bildirim güncellenemedi.'); }
    finally { setBusy(false); }
  }
  if (list.isPending) return <p role="status">Bildirimler hazırlanıyor…</p>;
  if (list.isError) return <div role="alert">Bildirimler alınamadı. <button className="underline" onClick={() => void list.refetch()}>Yeniden dene</button></div>;
  return <div className="space-y-5"><h1 className="text-2xl font-bold">Bildirimler</h1>
    <p className="text-sm text-[#6d7175]">Son 30 gündeki en yeni 100 kayıt gösterilir. Okundu işareti görevi tamamlamaz, raporu onaylamaz. Artık erişemediğiniz veya geçerliliği kalmayan bildirimler görünmez.</p>
    <button className="btn-secondary" disabled={list.isFetching || busy} onClick={() => void list.refetch()}>Bildirimleri yenile</button>
    {message && <p role="status">{message}</p>}
    {list.data.preferences && <PreferencesForm key={list.data.preferences.revision} value={list.data.preferences} customer={me.data?.role === 'BrandClient'} analyst={me.data?.role === 'Analyst'} ready={list.data.emailReady} onSaved={() => setMessage('Tercihleriniz kaydedildi.')} />}
    <h2 className="text-lg font-semibold">Size gelenler · {list.data.items.filter(x => !x.readAt).length} okunmamış</h2>
    {!list.data.items.length && <p className="card p-5">Şu anda size gösterilecek bildirim yok. Bildirimler takip başladıktan sonraki olaylardan oluşur; eski konuşmaların tamamı burada görünmez.</p>}
    {list.data.items.map(item => <article className="card space-y-2 p-4" key={item.id}>
      <h3 className="font-semibold">{!item.readAt && <span className="mr-2 text-[#008060]">Yeni</span>}{item.title}</h3>
      <p className="text-xs text-[#6d7175]">{new Date(item.createdAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye)</p>
      <p className="text-sm">{item.emailStatus ? statuses[item.emailStatus] : 'Bu kayıt için e-posta istenmedi.'}</p>
      <div className="flex flex-wrap gap-4"><Link href={item.href} className="underline">İlgili sayfayı aç</Link>{!item.readAt && <button disabled={busy} className="underline disabled:opacity-50" onClick={() => void read(item.id)}>Okundu olarak işaretle</button>}</div>
    </article>)}
  </div>;
}

function PreferencesForm({ value, customer, analyst, ready, onSaved }: { value: Preferences; customer: boolean; analyst: boolean; ready: boolean; onSaved: () => void }) {
  const qc = useQueryClient();
  const [form, setForm] = useState(value);
  useUnsavedChanges(JSON.stringify(form) !== JSON.stringify(value));
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const options: [keyof Omit<Preferences, 'revision'>, string][] = customer
    ? [['portalMessagesEmail', 'Sorularıma gelen yanıtlar'], ['portalReportsEmail', 'Markam için paylaşılan yeni raporlar']]
    : [['dailyTasksEmail', 'Günlük açık görev hatırlatması (Türkiye saatiyle 09.00 sonrası)'], ['taskDueEmail', 'Son tarihi yarın, bugün veya geçmiş görevler'], ...(!analyst ? [['portalMessagesEmail', 'Sorumlu olduğum müşteri konuşmaları (atanmamış konular yöneticilere gider)'] as [keyof Omit<Preferences, 'revision'>, string]] : [])];
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return; setBusy(true); setMessage('');
    try { await api('/api/notifications/preferences', { method: 'PUT', body: JSON.stringify(form) }); onSaved(); await qc.invalidateQueries({ queryKey: ['notifications'] }); }
    catch (e) { setMessage(e instanceof Error ? e.message : 'Tercihler kaydedilemedi.'); }
    finally { setBusy(false); }
  }
  return <form className="card space-y-3 p-5" onSubmit={save}><h2 className="font-semibold">E-posta tercihlerim</h2>
    <p className="text-sm">Panel bildirimleri açık kalır. E-postalar başlangıçta kapalıdır. Tercihinizi açmak eski kayıtları yeniden göndermez. İletilerde özel finansal değer veya mesaj metni değil giriş gerektiren bağlantı bulunur.</p>
    {!ready && <p className="text-sm text-[#8a6116]">E-posta hizmeti henüz hazır değil; tercihlerinizi kaydedebilirsiniz, şu anda e-posta gönderilmez.</p>}
    {options.map(([key, label]) => <label className="flex items-start gap-2 text-sm" key={key}><input type="checkbox" checked={form[key]} disabled={busy} onChange={e => setForm({ ...form, [key]: e.target.checked })} />{label}</label>)}
    <button className="btn-primary" disabled={busy}>{busy ? 'Kaydediliyor…' : 'Tercihlerimi kaydet'}</button>{message && <p role="status">{message}</p>}
  </form>;
}
