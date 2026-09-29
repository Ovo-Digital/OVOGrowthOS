'use client';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { Card, EmptyState, ErrorState, LoadingState } from '@/components/ui/core';
import { Pagination } from '@/components/list-controls';
import { notify } from '@/components/feedback';
import { turkceTarih } from '@/lib/turkish';

type OrdersPayload = {
  period: string; configured: boolean; lastSyncAt: string | null;
  summary: { orderCount: number; grossTotal: number; cancelledCount: number; cancelledTotal: number; paidTotal: number; refundedTotal: number; currency: string; activeTotal: number };
  panel: { panelExists: boolean; panelGrossSales: number | null; difference: number | null };
  total: number; page: number; pageSize: number;
  items: { orderNumber: number; placedOnUtc: string; currency: string; orderTotal: number; paidAmount: number; refundedAmount: number; orderStatus: number; paymentStatus: number }[];
};

const orderStatus: Record<number, string> = { 10: 'Bekliyor', 20: 'Hazırlanıyor', 30: 'Tamamlandı', 40: 'İptal edildi', 50: 'Onaylandı' };
const paymentStatus: Record<number, string> = { 10: 'Ödeme bekliyor', 20: 'Kart yetkilendirildi', 25: 'Kısmen ödendi', 30: 'Ödendi', 35: 'Kısmen iade edildi', 39: 'İade bekliyor', 40: 'İade edildi', 50: 'Geçersiz kılındı', 60: 'Ödeme iptal edildi' };
const button = 'rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50';

function monthOptions() {
  const now = new Date(); const options: { value: string; label: string }[] = [];
  for (let year = 2026; year <= now.getFullYear(); year++) {
    for (let month = 1; month <= 12; month++) {
      if (year === now.getFullYear() && month > now.getMonth() + 1) break;
      const value = `${year}-${String(month).padStart(2, '0')}`;
      options.push({ value, label: new Intl.DateTimeFormat('tr-TR', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1)) });
    }
  }
  return options;
}

export function StoreOrdersCard({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const [period, setPeriod] = useState('2026-08');
  const [page, setPage] = useState(1);
  const cache = useQueryClient();
  const query = useQuery({ queryKey: ['store-orders', brandId, period, page], queryFn: () => api<OrdersPayload>(`/api/brands/${brandId}/store-orders?period=${period}&page=${page}`), enabled: internal, refetchOnWindowFocus: false });
  const sync = useMutation({
    mutationFn: () => api<{ message: string }>(`/api/brands/${brandId}/store-orders/sync`, { method: 'POST', body: JSON.stringify({ period }) }),
    onSuccess: result => { notify(result.message); setPage(1); void cache.invalidateQueries({ queryKey: ['store-orders', brandId] }); }
  });
  const backfill = useMutation({
    mutationFn: () => api<{ message: string }>(`/api/brands/${brandId}/store-orders/backfill`, { method: 'POST', body: JSON.stringify({ months: 6 }) }),
    onSuccess: result => { notify(result.message); void cache.invalidateQueries({ queryKey: ['store-orders', brandId] }); }
  });
  if (me.isPending) return null;
  if (me.isError || !internal) return null;
  const isAdmin = me.data.role === 'Admin';
  return <Card className="mt-5 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="font-semibold">Mağaza siparişleri</h2>
      <div className="flex flex-wrap items-center gap-2">
        <label className="text-sm">Dönem
          <select className="input ml-2 mt-0" value={period} onChange={e => { setPeriod(e.target.value); setPage(1); }}>
            {monthOptions().map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
          </select>
        </label>
        {isAdmin && <button className={button} disabled={sync.isPending || backfill.isPending || !query.data?.configured} onClick={() => sync.mutate()}>{sync.isPending ? 'Siparişler alınıyor…' : 'Siparişleri getir'}</button>}
        {isAdmin && <button className={button} disabled={sync.isPending || backfill.isPending || !query.data?.configured} onClick={() => backfill.mutate()}>{backfill.isPending ? 'Geçmiş aylar alınıyor…' : 'Son 6 ayı geriye dönük çek'}</button>}
      </div>
    </div>
    <p className="mt-2 text-sm text-[#6d7175]">Bu bölüm, mağazadan gelen siparişleri yalnız görmek ve kontrol etmek içindir. Siparişler hakedişe, aylık sonuca veya anlaşma kayıtlarına otomatik yazılmaz; dönem onayı yine elle yapılır.</p>
    {sync.isError && <p role="alert" className="mt-3 text-sm text-red-700">{sync.error.message}</p>}
    {backfill.isError && <p role="alert" className="mt-3 text-sm text-red-700">{backfill.error.message}</p>}
    {!isAdmin && <p className="mt-2 text-xs text-[#6d7175]">Siparişleri yenilemek yalnız yönetici yapabilir.</p>}
    {query.isPending ? <LoadingState label="Siparişler yükleniyor…" /> : query.isError ? <div className="mt-3"><ErrorState message={`Siparişler alınamadı. ${query.error.message}`} /><button className={`${button} mt-2`} onClick={() => void query.refetch()}>Yeniden dene</button></div> : <StoreOrdersView payload={query.data} period={period} isAdmin={isAdmin} busy={sync.isPending} onSync={() => sync.mutate()} page={page} onPage={setPage} />}
  </Card>;
}

function StoreOrdersView({ payload: d, period, isAdmin, busy, onSync, page, onPage }: { payload: OrdersPayload; period: string; isAdmin: boolean; busy: boolean; onSync: () => void; page: number; onPage: (page: number) => void }) {
  const s = d.summary; const currency = s.currency || 'TRY';
  return <>
    <p className="mt-3 text-sm" role="status">
      {d.lastSyncAt ? `Son çekim: ${turkceTarih(d.lastSyncAt)}.` : 'Bu dönem için henüz sipariş çekilmedi.'}
      {!d.configured && ' Mağaza API ayarları kayıtlı değil; önce marka API ayarlarını tamamlayın.'}
    </p>
    <div className="mt-3 grid gap-3 rounded-lg border p-4 text-sm sm:grid-cols-2 xl:grid-cols-4">
      <div><div className="label">SİPARİŞ SAYISI</div><div className="font-semibold">{s.orderCount}</div></div>
      <div><div className="label">İPTAL HARİÇ TOPLAM</div><div className="font-semibold">{moneyPrecise(s.activeTotal, currency)}</div></div>
      <div><div className="label">İPTAL EDİLEN</div><div className="font-semibold">{s.cancelledCount} adet · {moneyPrecise(s.cancelledTotal, currency)}</div></div>
      <div><div className="label">İADE EDİLEN</div><div className="font-semibold">{moneyPrecise(s.refundedTotal, currency)}</div></div>
    </div>
    <p className="mt-3 text-sm text-[#6d7175]">
      {d.panel.panelExists
        ? `Paneldeki aylık sonuç ${moneyPrecise(d.panel.panelGrossSales ?? 0, currency)}; mağaza toplamıyla farkı ${moneyPrecise(d.panel.difference ?? 0, currency)}. Farkı görmek için aylık sonuç ekranını kontrol edin; bu bölüm yalnız karşılaştırma yapar.`
        : 'Bu dönem için panelde henüz aylık sonuç kaydı yok; karşılaştırma aylık sonuç girildikten sonra görünür.'}
      {s.cancelledCount > 0 && ` İptal edilen ${s.cancelledCount} sipariş toplamlara dahil edilmedi.`}
    </p>
    {isAdmin && !busy && !d.configured && <p className="mt-2 text-sm text-[#8e1f0b]">Sipariş çekmek için önce Mağaza API ayarları bölümünü kaydedin ve bağlantıyı doğrulayın.</p>}
    {isAdmin && d.configured && !busy && d.total === 0 && <button className={`${button} mt-3`} onClick={onSync}>Bu dönemin siparişlerini getir</button>}
    {isAdmin && busy && <p className="mt-3 text-sm" role="status">Mağazadan siparişler alınıyor…</p>}
    {d.total === 0 ? <div className="mt-4"><EmptyState message={d.lastSyncAt ? 'Bu dönemde mağazada sipariş bulunmuyor.' : 'Henüz sipariş çekilmedi; dönem seçip "Siparişleri getir" düğmesine basın.'} /></div> :
      <div className="mt-4 overflow-x-auto rounded-lg border">
        <table className="w-full min-w-[640px] text-sm">
          <thead className="bg-[#f7f7f8] text-left text-xs text-[#6d7175]"><tr><th className="px-4 py-2">Sipariş no</th><th className="px-4 py-2">Tarih</th><th className="px-4 py-2">Sipariş durumu</th><th className="px-4 py-2">Ödeme durumu</th><th className="px-4 py-2 text-right">Tutar</th></tr></thead>
          <tbody>{d.items.map(item => <tr key={item.orderNumber} className="border-t">
            <td className="px-4 py-2 font-medium">#{item.orderNumber}</td>
            <td className="px-4 py-2">{turkceTarih(item.placedOnUtc)}</td>
            <td className="px-4 py-2">{orderStatus[item.orderStatus] ?? 'Bilinmiyor'}</td>
            <td className="px-4 py-2">{paymentStatus[item.paymentStatus] ?? 'Bilinmiyor'}</td>
            <td className="px-4 py-2 text-right">{moneyPrecise(item.orderTotal, item.currency || currency)}</td>
          </tr>)}</tbody>
        </table>
        <Pagination page={page} pageSize={d.pageSize} total={d.total} onPage={onPage} />
      </div>}
    <p className="mt-3 text-xs text-[#6d7175]">Dönem sınırları Türkiye saatiyle hesaplanır. Tutarlar mağazanın para birimiyle gösterilir; {period} dönemi için saat dilimi ve para birimi eşleştirmesi aktarım sırasında kaydedilir.</p>
  </>;
}
