'use client';
import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';
import { EmptyState, ErrorState, LoadingState } from '@/components/ui/core';
import { Pagination, useDebouncedValue } from '@/components/list-controls';
import { turkce } from '@/lib/turkish';

type Preferences = { dailyTasksEmail: boolean; taskDueEmail: boolean; portalMessagesEmail: boolean; portalReportsEmail: boolean; promiseRemindersEmail: boolean; weeklyDigestEmail: boolean; revision: number };
type Item = { id: string; kind: string; title: string; href: string; createdAt: string; readAt: string | null; emailStatus: string | null };
export type NotificationList = { items: Item[]; total: number; page: number; pageSize: number; unreadCount: number; preferences: Preferences | null; emailReady: boolean };
const statuses: Record<string, string> = { Pending: 'Gönderim bekliyor', Sending: 'Gönderiliyor', Sent: 'E-posta sunucusu kabul etti', Uncertain: 'Gönderim doğrulanamadı; otomatik tekrarlanmaz', Cancelled: 'Tercih, hesap veya kaynak değiştiği için gönderilmedi' };
const kinds = ['DailyTasks', 'TaskDue', 'PortalQuestion', 'PortalReply', 'PortalReport', 'PromiseReminder', 'WeeklyDigest', 'StoreSync', 'RenewalDue', 'AdSpendSync'];

export default function Notifications() {
  const qc = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [search, setSearch] = useState('');
  const [kind, setKind] = useState('');
  const [read, setRead] = useState('');
  const [sort, setSort] = useState('recent');
  const [page, setPage] = useState(1);
  const term = useDebouncedValue(search);
  const filtered = search !== '' || kind !== '' || read !== '';
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const list = useQuery({ queryKey: ['notifications', term, kind, read, sort, page], queryFn: async () => {
    await api('/api/notifications/refresh', { method: 'POST' });
    const params = new URLSearchParams({ page: String(page), pageSize: '20', sort });
    if (term) params.set('search', term);
    if (kind) params.set('kind', kind);
    if (read) params.set('read', read);
    return api<NotificationList>(`/api/notifications?${params}`);
  }, refetchInterval: 60_000, staleTime: 30_000 });
  function resetPage() { setPage(1); }
  async function readItem(id: string) {
    setBusy(true); setMessage('');
    try { await api(`/api/notifications/${id}/read`, { method: 'POST' }); await qc.invalidateQueries({ queryKey: ['notifications'] }); }
    catch (e) { setMessage(e instanceof Error ? e.message : 'Bildirim güncellenemedi.'); }
    finally { setBusy(false); }
  }
  if (list.isPending) return <LoadingState label="Bildirimler hazırlanıyor…" />;
  if (list.isError) return <div><ErrorState message="Bildirimler alınamadı." /><div className="px-5 pb-5"><button className="underline" onClick={() => void list.refetch()}>Yeniden dene</button></div></div>;
  return <div className="space-y-5"><h1 className="text-2xl font-bold">Bildirimler</h1>
    <p className="text-sm text-[#6d7175]">Son 30 gündeki kayıtlar gösterilir. Okundu işareti görevi tamamlamaz, raporu onaylamaz. Artık erişemediğiniz veya geçerliliği kalmayan bildirimler görünmez.</p>
    <button className="btn-secondary" disabled={list.isFetching || busy} onClick={() => void list.refetch()}>Bildirimleri yenile</button>
    {message && <p role="status">{message}</p>}
    {list.data.preferences && <PreferencesForm key={list.data.preferences.revision} value={list.data.preferences} customer={me.data?.role === 'BrandClient'} analyst={me.data?.role === 'Analyst'} ready={list.data.emailReady} onSaved={() => setMessage('Tercihleriniz kaydedildi.')} />}
    <h2 className="text-lg font-semibold">Size gelenler · {list.data.unreadCount} okunmamış</h2>
    <div className="card space-y-3 p-4">
      <label className="block text-sm">Başlıkta ara<input aria-label="Bildirim başlığında ara" className="input mt-1" placeholder="Örn. görev, rapor, yanıt…" value={search} onChange={e => { setSearch(e.target.value); resetPage(); }} /></label>
      <div className="flex flex-wrap gap-3">
        <label className="block text-sm">Türe göre<select aria-label="Türe göre filtrele" className="input mt-1 max-w-[240px]" value={kind} onChange={e => { setKind(e.target.value); resetPage(); }}><option value="">Tüm türler</option>{kinds.map(k => <option key={k} value={k}>{turkce(k)}</option>)}</select></label>
        <label className="block text-sm">Okunma durumuna göre<select aria-label="Okunma durumuna göre filtrele" className="input mt-1 max-w-[220px]" value={read} onChange={e => { setRead(e.target.value); resetPage(); }}><option value="">Tümü</option><option value="unread">Okunmamış</option><option value="read">Okunmuş</option></select></label>
        <label className="block text-sm">Sıralama<select aria-label="Sıralama" className="input mt-1 max-w-[190px]" value={sort} onChange={e => { setSort(e.target.value); resetPage(); }}><option value="recent">En yeni</option><option value="oldest">En eski</option></select></label>
      </div>
      {filtered && <p className="text-sm text-[#6d7175]">Filtreye uyan {list.data.total} kayıt var. <button className="underline" onClick={() => { setSearch(''); setKind(''); setRead(''); resetPage(); }}>Filtreleri temizle</button></p>}
    </div>
    {!list.data.items.length && <div className="card"><EmptyState message={filtered ? 'Arama ve filtrelere uygun bildirim bulunamadı.' : 'Şu anda size gösterilecek bildirim yok. Bildirimler takip başladıktan sonraki olaylardan oluşur; eski konuşmaların tamamı burada görünmez.'} /></div>}
    {list.data.items.map(item => <article className="card space-y-2 p-4" key={item.id}>
      <h3 className="font-semibold">{!item.readAt && <span className="mr-2 text-[#008060]">Yeni</span>}{item.title}</h3>
      <p><span className="rounded-full bg-[#f1f2f3] px-2 py-0.5 text-xs text-[#4a4d50]">{turkce(item.kind)}</span></p>
      <p className="text-xs text-[#6d7175]">{new Date(item.createdAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye)</p>
      <p className="text-sm">{item.emailStatus ? statuses[item.emailStatus] : 'Bu kayıt için e-posta istenmedi.'}</p>
      <div className="flex flex-wrap gap-4"><Link href={item.href} className="underline">İlgili sayfayı aç</Link>{!item.readAt && <button disabled={busy} className="underline disabled:opacity-50" onClick={() => void readItem(item.id)}>Okundu olarak işaretle</button>}</div>
    </article>)}
    <Pagination page={list.data.page} pageSize={list.data.pageSize} total={list.data.total} onPage={setPage} />
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
    : [['dailyTasksEmail', 'Günlük açık görev hatırlatması (Türkiye saatiyle 09.00 sonrası)'], ['taskDueEmail', 'Son tarihi yarın, bugün veya geçmiş görevler'], ['promiseRemindersEmail', 'Yarın vadesi gelen veya geciken ödeme sözleri'], ['weeklyDigestEmail', 'Her pazartesi 09.00’da haftalık yönetim özeti (toplantı öncesi)'], ...(!analyst ? [['portalMessagesEmail', 'Sorumlu olduğum müşteri konuşmaları (atanmamış konular yöneticilere gider)'] as [keyof Omit<Preferences, 'revision'>, string]] : [])];
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
