'use client';
import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { periodNumber } from '@/lib/period-input';
import { Card, EmptyState } from '@/components/ui/core';
import { notify } from '@/components/feedback';
import { dayText } from '@/components/work-tasks';

type PlanRow = { performanceId: string; year: number; month: number; dueOn: string | null; outstanding: number; suggested: number; remainingAfter: number };
type Plan = { brandId: string; currency: string; received: number; totalOutstanding: number; allocated: number; leftover: number; skippedReview: number; otherCurrencies: string[]; rows: PlanRow[] };
type CollectionView = { revision: number };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';
const todayIso = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`; };

export function PaymentAllocator({ currency, weeks, brands }: { currency: string; weeks: number; brands: { id: string; name: string }[] }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const cache = useQueryClient();
  const [brandId, setBrandId] = useState('');
  const [amountText, setAmountText] = useState('');
  const [paidOn, setPaidOn] = useState(todayIso());
  const [reference, setReference] = useState('');
  const [note, setNote] = useState('');
  const [plan, setPlan] = useState<Plan | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [saved, setSaved] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState<Record<string, boolean>>({});
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  if (me.isPending || !allowed) return null;
  const brandName = brands.find(b => b.id === brandId)?.name ?? '';
  async function calculate() {
    if (busy) return;
    setError(''); setPlan(null); setSaved({}); setSaving({});
    let amount = 0;
    try {
      if (!brandId) throw new Error('Önce markayı seçin.');
      amount = periodNumber(amountText, false);
      if (amount <= 0) throw new Error('Dağıtılacak tutar sıfırdan büyük olmalı.');
      if (!reference.trim()) throw new Error('Banka dekontundaki referansı yazın; her döneme sıra eklenerek kaydedilir.');
    } catch (e) { setError(e instanceof Error ? e.message : 'Dağıtım hazırlanamadı.'); return; }
    setBusy(true);
    try {
      setPlan(await api<Plan>(`/api/brands/${brandId}/payment-plan?amount=${amount}&currency=${encodeURIComponent(currency)}`));
    } catch (e) { setError(e instanceof Error ? e.message : 'Dağıtım hesaplanamadı.'); }
    finally { setBusy(false); }
  }
  async function saveRow(row: PlanRow, index: number, total: number) {
    if (busy || saving[row.performanceId] || saved[row.performanceId]) return;
    setError('');
    setSaving(s => ({ ...s, [row.performanceId]: true }));
    try {
      const view = await api<CollectionView>(`/api/performance/${row.performanceId}/collection`);
      const ref = `${reference.trim()}-${index + 1}/${total}`;
      await api(`/api/performance/${row.performanceId}/collection/payments`, {
        method: 'POST',
        body: JSON.stringify({ id: crypto.randomUUID(), amount: row.suggested, paidOn, reference: ref, note: note.trim(), revision: view.revision }),
      });
      setSaved(s => ({ ...s, [row.performanceId]: ref }));
      notify(`${row.month}/${row.year} dönemine ${moneyPrecise(row.suggested, currency)} kaydedildi (${ref}).`);
      await cache.invalidateQueries({ queryKey: ['collection-planning', weeks, currency] });
    } catch (e) { setError(e instanceof Error ? e.message : 'Ödeme kaydedilemedi.'); }
    finally { setSaving(s => ({ ...s, [row.performanceId]: false })); }
  }
  const activeRows = (plan?.rows ?? []).filter(r => r.suggested > 0);
  return <Card className="mb-4 p-5"><h2 className="font-semibold">Toplu ödemeyi dönemlere dağıt</h2>
    <p className="my-3 text-sm">Markanın tek seferde gönderdiği tutar, vadesi en eski açık alacaktan başlayarak önerilir. Öneri yalnız bilgidir; her dönem kaydı siz basınca ve ayrı ayrı oluşur. Dağıtılmayan tutar kaydedilmez.</p>
    {brands.length === 0 ? <EmptyState message="Bu para biriminde alacağı kalan marka yok." /> : <>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <label className="text-sm">Marka<select className="input mt-1" value={brandId} disabled={busy} onChange={e => { setBrandId(e.target.value); setPlan(null); setSaved({}); }}><option value="">Marka seçin</option>{brands.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
        <label className="text-sm">Gelen toplam tutar ({currency})<input className="input mt-1" value={amountText} disabled={busy} inputMode="decimal" placeholder="120.000,50" onChange={e => setAmountText(e.target.value)} /></label>
        <label className="text-sm">Gerçekleşme tarihi<input className="input mt-1" type="date" value={paidOn} disabled={busy} min="2020-01-01" max="2100-12-31" onChange={e => setPaidOn(e.target.value)} /></label>
        <label className="text-sm">Banka referansı<input className="input mt-1" value={reference} disabled={busy} placeholder="BANKA-2026-118" onChange={e => setReference(e.target.value)} /></label>
        <label className="text-sm md:col-span-2">Not (isteğe bağlı)<input className="input mt-1" value={note} disabled={busy} placeholder="Havale açıklaması" onChange={e => setNote(e.target.value)} /></label>
      </div>
      <button className={`${button} mt-3`} disabled={busy} onClick={() => void calculate()}>{busy ? 'Hesaplanıyor…' : 'Dağıtımı hesapla'}</button>
      {error && <p role="alert" className="mt-3 text-sm text-red-700">{error}</p>}
      {plan && <div className="mt-4">
        <p className="text-sm" role="status">{brandName} · Gelen: <strong>{moneyPrecise(plan.received, currency)}</strong> · Açık alacak: <strong>{moneyPrecise(plan.totalOutstanding, currency)}</strong> · Dağıtılan: <strong>{moneyPrecise(plan.allocated, currency)}</strong>{plan.leftover > 0 && <> · Dağıtılmayan: <strong>{moneyPrecise(plan.leftover, currency)}</strong> (kaydedilmez; tutarı ve açık alacakları kontrol edin)</>}</p>
        {plan.skippedReview > 0 && <p className="mt-2 text-sm text-amber-800">İnceleme gereken {plan.skippedReview} dönem dağıtıma alınmadı; önce yöneticinizle kayıtları kontrol edin.</p>}
        {plan.otherCurrencies.length > 0 && <p className="mt-2 text-sm">Bu markanın başka para birimlerinde de açık alacağı var: {plan.otherCurrencies.join(', ')}. Para birimleri birbirine eklenmez.</p>}
        {activeRows.length === 0 ? <div className="mt-3"><EmptyState message="Dağıtılacak açık alacak bulunamadı. Tutarı, para birimini ve markanın açık alacaklarını kontrol edin." /></div> :
          <div className="mt-3 overflow-x-auto rounded-lg border"><table className="w-full min-w-[640px] text-sm">
            <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Dönem</th><th className="px-4 py-2">Vade</th><th className="px-4 py-2 text-right">Kalan alacak</th><th className="px-4 py-2 text-right">Önerilen</th><th className="px-4 py-2 text-right">Kaydedilince kalan</th><th className="px-4 py-2">Kayıt</th></tr></thead>
            <tbody>{plan.rows.filter(r => r.suggested > 0).map((r, i) => <tr key={r.performanceId} className="border-t">
              <td className="px-4 py-2 font-medium">{r.month}/{r.year}</td>
              <td className="px-4 py-2">{dayText(r.dueOn)}</td>
              <td className="px-4 py-2 text-right">{moneyPrecise(r.outstanding, currency)}</td>
              <td className="px-4 py-2 text-right font-semibold">{moneyPrecise(r.suggested, currency)}</td>
              <td className="px-4 py-2 text-right">{moneyPrecise(r.remainingAfter, currency)}</td>
              <td className="px-4 py-2">{saved[r.performanceId] ? <span className="text-sm text-green-800">Kaydedildi ({saved[r.performanceId]})</span> : <button className={button} disabled={busy || !!saving[r.performanceId]} onClick={() => void saveRow(r, i, activeRows.length)}>{saving[r.performanceId] ? 'Kaydediliyor…' : 'Bu döneme kaydet'}</button>}</td>
            </tr>)}</tbody>
          </table></div>}
        <p className="mt-3 text-xs text-[#6d7175]">Her kayıt ayrı banka referansıyla oluşur ({reference.trim() || 'referans'}-1/{activeRows.length} gibi); aynı dekont ikinci kez kaydedilmez. Kayıtlar hakediş ekranında ve işlem geçmişinde görünür.</p>
      </div>}
    </>}
  </Card>;
}
