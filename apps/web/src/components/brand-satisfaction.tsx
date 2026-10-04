'use client';
import { useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Badge, Card, ErrorState, LoadingState } from '@/components/ui/core';
import { notify } from '@/components/feedback';

type Rating = { id: string; year: number; month: number; score: number; scoreLabel: string; comment: string; createdBy: string; createdAt: string; updatedBy: string; updatedAt: string };
type Payload = { average: number; count: number; items: Rating[] };

const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
const months = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];
const scoreLabels = ['', 'Çok kötü', 'Kötü', 'Orta', 'İyi', 'Çok iyi'];
const oneDecimal = (v: number) => new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(v);
const stamp = (v: string) => new Date(v).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' });
const currentYear = new Date().getFullYear();

export function BrandSatisfaction({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canWrite = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const cache = useQueryClient();
  const query = useQuery({ queryKey: ['satisfaction', brandId], queryFn: () => api<Payload>(`/api/brands/${brandId}/satisfaction`), refetchOnWindowFocus: false });
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (busy) return;
    const form = e.currentTarget; const data = new FormData(form);
    setBusy(true); setError('');
    try {
      await api(`/api/brands/${brandId}/satisfaction`, { method: 'POST', body: JSON.stringify({ year: Number(data.get('year')), month: Number(data.get('month')), score: Number(data.get('score')), comment: String(data.get('comment') ?? '') }) });
      notify('Memnuniyet puanı kaydedildi.'); form.reset();
      await cache.invalidateQueries({ queryKey: ['satisfaction', brandId] });
    } catch (e) { setError(e instanceof Error ? e.message : 'Memnuniyet puanı kaydedilemedi.'); } finally { setBusy(false); }
  }
  if (me.isPending) return null;
  if (query.isPending) return <Card className="mt-4 p-5"><LoadingState label="Memnuniyet puanları yükleniyor…" /></Card>;
  if (query.isError) return <Card className="mt-4 p-5"><ErrorState message={`Memnuniyet puanları alınamadı. ${query.error.message}`} /></Card>;
  const data = query.data;
  const tone = data.count === 0 ? 'neutral' : data.average >= 4 ? 'green' : data.average >= 3 ? 'yellow' : 'red';
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 className="font-semibold">Müşteri memnuniyet puanı</h2>
        <p className="mt-1 text-xs text-[#6d7175]">{data.count === 0 ? 'Henüz dönem puanı girilmedi.' : `${data.count} dönem kayıtlı · 5 üzerinden ortalama ${oneDecimal(data.average)}`}</p>
      </div>
      <Badge tone={tone}>{data.count === 0 ? 'Puan yok' : `${data.count} dönem`}</Badge>
    </div>
    <p className="mt-3 text-sm text-[#6d7175]">Puan 1–5 aralığında müşteriyle konuşulan memnuniyeti ve varsa kısa yorumu saklar. Bu bilgi yalnız ilişki takibi içindir; sağlık skoruna, hakedişe, karar ekranlarına veya ödeme tutarlarına işlenmez.</p>
    {query.data.items.length > 0 && <div className="mt-4 overflow-x-auto rounded-lg border">
      <table className="w-full min-w-[640px] text-sm">
        <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Dönem</th><th className="px-4 py-2">Puan</th><th className="px-4 py-2">Yorum</th><th className="px-4 py-2">Kaydeden</th></tr></thead>
        <tbody>{data.items.map(row => <tr key={row.id} className="border-t align-top">
          <td className="px-4 py-2">{months[row.month - 1]} {row.year}</td>
          <td className="px-4 py-2"><span className="font-semibold">{row.score} / 5</span> · {row.scoreLabel}</td>
          <td className="whitespace-pre-wrap break-words px-4 py-2">{row.comment || <span className="text-[#6d7175]">Yorum yazılmadı</span>}</td>
          <td className="px-4 py-2 text-xs text-[#6d7175]">{row.updatedBy || row.createdBy}<br />{stamp(row.updatedAt || row.createdAt)}</td>
        </tr>)}</tbody>
      </table>
    </div>}
    {data.count === 0 && <p className="mt-3 text-sm">Kayıtlı puan yok. Aşağıdaki formdan ilk dönemi girebilirsiniz.</p>}
    {canWrite ? <form className="mt-4 space-y-3 rounded-lg border p-3" onSubmit={submit}>
      <h3 className="text-sm font-semibold">Dönem puanı kaydet</h3>
      <p className="text-xs text-[#6d7175]">Aynı dönem ikinci kez girilirse önceki puan ve yorum güncellenir; ayrı kayıt oluşmaz.</p>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <label className="text-sm">Yıl<input className="input mt-1" type="number" name="year" min={2020} max={2100} required defaultValue={currentYear} disabled={busy} /></label>
        <label className="text-sm">Ay<select className="input mt-1" name="month" defaultValue={new Date().getMonth() + 1} disabled={busy}>{months.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}</select></label>
        <label className="text-sm">Puan<select className="input mt-1" name="score" required disabled={busy}>{[1, 2, 3, 4, 5].map(s => <option key={s} value={s}>{s} · {scoreLabels[s]}</option>)}</select></label>
        <label className="text-sm">Yorum (en fazla 1000 karakter)<input className="input mt-1" name="comment" maxLength={1000} disabled={busy} placeholder="İsteğe bağlı" /></label>
      </div>
      <div className="flex flex-wrap gap-3">
        <button className={button} disabled={busy}>{busy ? 'Kaydediliyor…' : 'Puanı kaydet'}</button>
        <span className="self-center text-xs text-[#6d7175]">Girilen puan yalnız bu sayfada gösterilir.</span>
      </div>
      {error && <p role="alert" className="text-sm text-[#d72c0d]">{error}</p>}
    </form> : <p className="mt-4 text-sm text-[#6d7175]">Memnuniyet puanını yalnız yönetici veya ortak kaydedebilir.</p>}
  </Card>;
}
