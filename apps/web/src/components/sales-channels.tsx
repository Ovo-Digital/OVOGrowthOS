'use client';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { notify } from '@/components/feedback';
import { Card } from '@/components/ui/core';
import { useDialog } from '@/components/ui/modal';
import { FieldHint } from '@/components/field-hint';

export type SalesChannel = { id: string; brandId: string; name: string; isActive: boolean; createdAt: string; updatedAt: string };

export function SalesChannelsCard({ brandId }: { brandId: string }) {
  const qc = useQueryClient(); const { confirm, prompt } = useDialog();
  const [name, setName] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const channels = useQuery({ queryKey: ['sales-channels', brandId], queryFn: () => api<SalesChannel[]>(`/api/brands/${brandId}/sales-channels`) });
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  async function refresh() { await qc.invalidateQueries({ queryKey: ['sales-channels', brandId] }); }
  async function add() {
    const value = name.trim();
    if (value.length < 2) { setError('Kanal adı en az 2 karakter olmalıdır.'); return; }
    setBusy(true); setError('');
    try {
      await api(`/api/brands/${brandId}/sales-channels`, { method: 'POST', body: JSON.stringify({ name: value }) });
      setName(''); notify('Satış kanalı eklendi.'); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : 'Kanal eklenemedi.'); }
    finally { setBusy(false); }
  }
  async function rename(channel: SalesChannel) {
    const next = await prompt({ title: 'Kanalı yeniden adlandır', message: `"${channel.name}" için yeni adı yazın. Kayıtlı dönemlerdeki kanal adı da bu listeden okunur.`, label: 'Kanal adı', confirmLabel: 'Kaydet' });
    if (!next?.trim() || next.trim() === channel.name) return;
    setBusy(true); setError('');
    try {
      await api(`/api/sales-channels/${channel.id}`, { method: 'PUT', body: JSON.stringify({ name: next.trim(), isActive: channel.isActive }) });
      notify('Kanal adı güncellendi.'); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : 'Kanal güncellenemedi.'); }
    finally { setBusy(false); }
  }
  async function toggle(channel: SalesChannel) {
    const to = !channel.isActive;
    if (!to && !(await confirm({ title: 'Kanalı pasif yap', message: `"${channel.name}" pasif olur; yeni dönemlere ciro girilemez ama geçmiş kayıtlar korunur.`, confirmLabel: 'Pasif yap' }))) return;
    setBusy(true); setError('');
    try {
      await api(`/api/sales-channels/${channel.id}`, { method: 'PUT', body: JSON.stringify({ name: channel.name, isActive: to }) });
      notify(to ? 'Kanal etkinleştirildi.' : 'Kanal pasif yapıldı.'); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : 'Kanal güncellenemedi.'); }
    finally { setBusy(false); }
  }
  async function remove(channel: SalesChannel) {
    if (!(await confirm({ title: 'Kanalı sil', message: `"${channel.name}" kalıcı olarak silinecek. Anlaşma veya dönem kaydında kullanıldıysa silinemez; o durumda pasif yapın.`, tone: 'danger', confirmLabel: 'Sil' }))) return;
    setBusy(true); setError('');
    try {
      await api(`/api/sales-channels/${channel.id}`, { method: 'DELETE' });
      notify('Kanal silindi.'); await refresh();
    } catch (e) { setError(e instanceof Error ? e.message : 'Kanal silinemedi.'); }
    finally { setBusy(false); }
  }
  return <Card className="p-5"><h2 className="font-semibold">Satış kanalları</h2>
    <p className="mt-2 text-sm">Önce markanın satış kanallarını tanımlayın (ör. Web sitesi, Trendyol, Hepsiburada). Aylık ciro bu kanallara ayrı ayrı girilir; anlaşmada her kanala farklı gelir payı verilebilir.</p>
    {channels.isPending && <p role="status" className="mt-3 text-sm">Kanallar yükleniyor…</p>}
    {channels.isError && <p role="alert" className="mt-3 text-sm text-red-700">{channels.error.message}</p>}
    {channels.data && channels.data.length === 0 && <p className="mt-3 text-sm">Henüz kanal tanımlanmadı. İlk kanalı aşağıdan ekleyin.</p>}
    {channels.data && channels.data.length > 0 && <ul className="mt-3 space-y-2">{channels.data.map(c => <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
      <span><strong>{c.name}</strong> <span className="text-[#6d7175]">· {c.isActive ? 'Etkin' : 'Pasif'}</span></span>
      {allowed && <span className="flex flex-wrap gap-2">
        <button type="button" className="underline" disabled={busy} onClick={() => void rename(c)}>Yeniden adlandır</button>
        <button type="button" className="underline" disabled={busy} onClick={() => void toggle(c)}>{c.isActive ? 'Pasif yap' : 'Etkinleştir'}</button>
        <button type="button" className="underline" disabled={busy} onClick={() => void remove(c)}>Sil</button>
      </span>}
    </li>)}</ul>}
    {allowed && <div className="mt-4 flex flex-wrap gap-2"><label className="grow text-sm">Yeni kanal adı<FieldHint text="Markanın satış yaptığı kanalın adı (ör. Web sitesi, Trendyol). Sonradan yeniden adlandırılabilir." /><input className="input mt-1" value={name} disabled={busy} maxLength={80} onChange={e => setName(e.target.value)} placeholder="Örn. Trendyol" /></label>
      <button type="button" className="self-end rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50" disabled={busy} onClick={() => void add()}>{busy ? 'Ekleniyor…' : 'Kanal ekle'}</button></div>}
    {!allowed && me.data && <p className="mt-3 text-sm">Kanalları yalnız yönetici ve iş ortağı düzenleyebilir.</p>}
    {error && <p role="alert" className="mt-3 text-sm text-red-700">{error}</p>}
  </Card>;
}
