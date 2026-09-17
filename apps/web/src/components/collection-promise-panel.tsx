'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { periodNumber } from '@/lib/period-input';
import { Card } from '@/components/ui/core';
import { dayText, type Paged, type TeamMember } from '@/components/work-tasks';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';
import type { CollectionBalance } from '@/components/collection-panel';

export type PromiseBalance = { remaining: number; state: string; promisedOn: string };
type Snapshot = { amount: number; promisedOn: string; contactNoteId: string; sourceContactOn: string; sourceText: string; ownerId: string; ownerName: string; ownerActive: boolean; isCancelled: boolean; revision: number; recordedAt: string; taskId: string | null };
type PromiseData = { id: string; brandId: string; year: number; month: number; currency: string; collectionRevision: number; balance: CollectionBalance; canRecord: boolean; promise: Snapshot | null; expectation: PromiseBalance | null;
  task: { id: string; title: string; dueOn: string; completedAt: string | null; assigneeName: string } | null };
type Note = { id: string; contactOn: string; text: string };
type History = { id: string; createdAt: string; userId: string; reason: string; oldValueJson: string; newValueJson: string };
type Mode = 'save' | 'cancel' | 'task';
const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';
export const promiseState = (state: string) => ({ Waiting: 'Ödeme sözü bekleniyor', Overdue: 'Ödeme sözü tarihi geçti', Covered: 'Söz tutarı için bekleyen kalmadı', Cancelled: 'Söz takipten kaldırıldı', NeedsReview: 'İnceleme gerekli' })[state] ?? 'Kontrol edin';

export function CollectionPromisePanel({ id }: { id: string }) {
  const cache = useQueryClient(); const [action, setAction] = useState<{ mode: Mode; data: PromiseData } | null>(null); const [message, setMessage] = useState('');
  const query = useQuery({ queryKey: ['collection-promise', id], queryFn: () => api<PromiseData>(`/api/performance/${id}/collection/promise`) });
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const manage = me.data?.role === 'Admin' || me.data?.role === 'Partner'; const data = query.data;
  const saved = () => {
    setAction(null); setMessage('İşlem kaydedildi. Ödeme veya finansal onay oluşturulmadı; mevcut takip işi kendiliğinden değiştirilmedi.');
    for (const key of ['collection-promise', 'promise-history', 'collection-planning', 'work-tasks', 'tasks', 'audit']) cache.invalidateQueries({ queryKey: [key] });
  };
  return <Card className="mt-5 p-5"><section id="payment-promise" className="scroll-mt-5">
    <div className="flex flex-wrap justify-between gap-3"><h2 className="text-lg font-semibold">Ödeme sözü ve takip</h2><Link className="text-sm underline" href="/guide#odeme-sozu-ve-takip">Nasıl kullanılır?</Link></div>
    <p className="my-3 text-sm">Markanın bildirdiği tutar ve tarihtir; banka bakiyesi, tahsilat veya ödeme garantisi değildir. Vade değişmez. Müşteri portalında paylaşılmaz.</p>
    {message && <p role="status" className="mb-3">{message}</p>}
    {query.isPending ? <p role="status">Ödeme sözü yükleniyor…</p> : query.isError ? <p role="alert">{query.error.message}</p> : data && <>
      {data.promise ? <div className="space-y-2 rounded-lg border p-4 text-sm"><strong>{promiseState(data.expectation?.state ?? '')}</strong>
        <p>Bildirilen tutar: {moneyPrecise(data.promise.amount, data.currency)} · Tarih: {dayText(data.promise.promisedOn)}</p>
        <p>Sözden beklenen kalan: <strong>{moneyPrecise(data.expectation?.remaining ?? 0, data.currency)}</strong> · Dönemin toplam kalan alacağı: {moneyPrecise(data.balance.outstanding, data.currency)}</p>
        <p>Sorumlu: {data.promise.ownerName}{!data.promise.ownerActive && ' (hesabı kapalı; yeni sorumlu seçin)'} · Sürüm {data.promise.revision}</p>
        <p>Kaynak görüşme: {dayText(data.promise.sourceContactOn)}</p><p className="whitespace-pre-wrap break-words">{data.promise.sourceText}</p>
        <p>Son söz kaydından sonra eklenen geçerli ödemeler bekleyeni azaltır; hatalı ödeme iptali geri getirir. “Bekleyen kalmadı” sözü zamanında yerine getirdiği anlamına gelmez.</p>
      </div> : <p>Kayıtlı ödeme sözü yok. Bu, markanın ödeme yapmayacağı veya alacağın sıfır olduğu anlamına gelmez.</p>}
      {!data.canRecord && <p className="mt-3 text-sm">Yeni söz için önce fatura takibi başlamış, kalan alacağı olan ve inceleme gerektirmeyen bir dönem bulunmalıdır.</p>}
      {data.task && <div className="mt-3 rounded-lg border p-3 text-sm"><p>Bağlı iş: <strong>{data.task.title}</strong> · {data.task.completedAt ? 'Tamamlandı' : 'Açık'} · {dayText(data.task.dueOn)} · {data.task.assigneeName}</p><p className="my-2">Söz değişse de ikinci iş açılmaz; görevin sorumlusu, son tarihi ve tamamlanma durumu kendiliğinden değişmez. Gerekiyorsa mevcut işi düzenleyin veya yeniden açın.</p><Link href={`/brands/${data.brandId}#team-work`} className="underline">Markanın mevcut işlerini aç</Link></div>}
      {!action && <div className="mt-4 flex flex-wrap gap-3">
        {manage && data.canRecord && <button className={button} onClick={() => setAction({ mode: 'save', data })}>{data.promise ? 'Ödeme sözünü gerekçeyle güncelle' : 'Ödeme sözü kaydet'}</button>}
        {manage && data.promise && !data.promise.isCancelled && <button className={button} onClick={() => setAction({ mode: 'cancel', data })}>Sözü takipten kaldır</button>}
        {manage && !data.task && data.promise?.ownerActive && (data.expectation?.remaining ?? 0) > 0 && <button className={button} onClick={() => setAction({ mode: 'task', data })}>Ödeme sözü için takip işi oluştur</button>}
      </div>}
      {action && <PromiseForm mode={action.mode} data={action.data} onClose={() => setAction(null)} onSaved={saved} />}
      {data.promise && <PromiseHistory id={id} currency={data.currency} />}
    </>}
    <button className={`${button} mt-4`} disabled={!!action || query.isFetching} onClick={() => query.refetch()}>Ödeme sözü bilgilerini yenile</button>
    {me.isError && <p role="alert" className="mt-2">İşlem yetkiniz okunamadı: {me.error.message}</p>}
  </section></Card>;
}

function PromiseForm({ mode, data, onClose, onSaved }: { mode: Mode; data: PromiseData; onClose: () => void; onSaved: () => void }) {
  const [dirty, setDirty] = useState(false); const [error, setError] = useState(''); const [page, setPage] = useState(1);
  const [note, setNote] = useState<Note | null>(data.promise ? { id: data.promise.contactNoteId, contactOn: data.promise.sourceContactOn, text: data.promise.sourceText } : null);
  useUnsavedChanges(dirty);
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team'), enabled: mode === 'save' });
  const notes = useQuery({ queryKey: ['contact-notes', data.brandId, page], queryFn: () => api<Paged<Note>>(`/api/brands/${data.brandId}/contact-notes?page=${page}`), enabled: mode === 'save' });
  const save = useMutation({ mutationFn: (body: object) => api(`/api/performance/${data.id}/collection/promise${mode === 'save' ? '' : `/${mode}`}`, { method: mode === 'save' ? 'PUT' : 'POST', body: JSON.stringify(body) }), onSuccess: () => { setDirty(false); onSaved(); } });
  return <form className="mt-4 rounded-lg border bg-[#f7f7f8] p-4" onChange={() => setDirty(true)} onSubmit={e => {
    e.preventDefault(); setError(''); const form = new FormData(e.currentTarget);
    try {
      const revisions = { revision: data.promise?.revision ?? 0, collectionRevision: data.collectionRevision };
      if (mode === 'save' && !note) throw new Error('Ödeme sözünün kaynak görüşme notunu seçin.');
      save.mutate(mode === 'save' ? { ...revisions, amount: periodNumber(String(form.get('amount'))), promisedOn: form.get('date'), contactNoteId: note!.id, ownerId: form.get('owner'), reason: form.get('reason') }
        : mode === 'cancel' ? { ...revisions, reason: form.get('reason') } : { ...revisions, dueOn: form.get('due') });
    } catch (e) { setError(e instanceof Error ? e.message : 'Girişleri kontrol edin.'); }
  }}>
    <h3 className="font-semibold">{mode === 'save' ? 'Bildirilen ödeme sözünü kaydet' : mode === 'cancel' ? 'Ödeme sözünü takipten kaldır' : 'Ödeme sözü takip işi'}</h3>
    <p className="my-3 text-sm">{mode === 'save' ? `Şu andan itibaren beklenen KDV hariç tutarı yazın; eski ödemeleri tekrar dahil etmeyin. Üst sınır: ${moneyPrecise(data.balance.outstanding, data.currency)}. Yeni kayıt önceki sözü değiştirir; geçmiş silinmez. Tutar örneği: 1.234,5678.` : mode === 'cancel' ? 'Yalnız ödeme sözü takvimden çıkar. Alacak, gerçek ödemeler, kaynak görüşme ve bağlı görev silinmez.' : `İş ${data.promise?.ownerName} kişisine atanacak. Son günü siz seçin; ödeme sözü tarihi ${dayText(data.promise?.promisedOn ?? null)}. Mevcut kayıt göreve not edilir; görev ödeme eklemez.`}</p>
    <fieldset disabled={save.isPending} className="grid gap-4 sm:grid-cols-2">
      {mode === 'save' && <>
        <label>Ödeme sözü tutarı ({data.currency})<input name="amount" inputMode="decimal" className="input mt-1" required /></label>
        <label>Ödeme sözü tarihi<input name="date" type="date" className="input mt-1" required min="2020-01-01" max="2100-12-31" defaultValue={data.promise?.promisedOn ?? ''} /></label>
        <label className="sm:col-span-2">Takip sorumlusu<select name="owner" className="input mt-1" required defaultValue={data.promise?.ownerId ?? ''}><option value="">Çalışan seçin</option>{team.data?.map(x => <option value={x.id} key={x.id} disabled={!x.isActive}>{x.name}{!x.isActive && ' (kapalı hesap)'}</option>)}</select></label>
        <div className="sm:col-span-2"><h4 className="font-semibold">Kaynak görüşme notunu seçin</h4><p className="my-2 text-sm">Not yoksa önce <Link className="underline" href={`/brands/${data.brandId}#team-work`}>markanın görüşmelerine</Link> ekleyin, sonra buraya dönün. Kaynağı olmayan ödeme sözü kaydedilmez.</p>
          {notes.isPending ? <p role="status">Görüşmeler yükleniyor…</p> : notes.isError ? <p role="alert">{notes.error.message}</p> : notes.data && <>
            <div className="space-y-2">{notes.data.items.map(x => <label key={x.id} className="flex gap-2 rounded-lg border bg-white p-3 text-sm"><input type="radio" name="note" className="mt-1 shrink-0" checked={note?.id === x.id} onChange={() => setNote(x)} /><span className="min-w-0"><strong>{dayText(x.contactOn)}</strong><span className="block whitespace-pre-wrap break-words">{x.text}</span></span></label>)}</div>
            {notes.data.total === 0 && <p>Kayıtlı görüşme yok.</p>}
            <div className="mt-2 flex justify-between gap-2 text-sm"><button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki notlar</button><span>Sayfa {page}</span><button type="button" disabled={page * notes.data.pageSize >= notes.data.total} onClick={() => setPage(page + 1)}>Sonraki notlar</button></div>
          </>}
          {note && <p className="mt-3 whitespace-pre-wrap break-words text-sm">Seçilen kaynak · {dayText(note.contactOn)}: {note.text}</p>}
        </div>
      </>}
      {mode !== 'task' ? <label className="sm:col-span-2">{mode === 'cancel' ? 'Kaldırma nedeni' : 'Kayıt veya değişiklik nedeni'}<textarea name="reason" className="input mt-1" required minLength={5} maxLength={1000} rows={3} /></label>
        : <label>Takip işinin son günü<input name="due" type="date" className="input mt-1" required min="2020-01-01" max="2100-12-31" /></label>}
      <label className="flex items-start gap-2 sm:col-span-2"><input type="checkbox" required className="mt-1" /><span className="text-sm">Bilgileri kontrol ettim. Bu işlem tahsilat veya “ödendi” durumu oluşturmaz.</span></label>
      <div className="flex flex-wrap gap-3 sm:col-span-2"><button className={button} disabled={mode === 'save' && (!note || !team.data || team.isError)}>{save.isPending ? 'Kaydediliyor…' : mode === 'save' ? 'Sözü kaydet' : mode === 'cancel' ? 'Gerekçeyle takipten kaldır' : 'Takip işini kaydet'}</button><button type="button" className={button} onClick={() => { if (!dirty || confirm('Yazdıklarınızı kaydetmeden kapatmak istiyor musunuz?')) onClose(); }}>Vazgeç</button></div>
    </fieldset>
    {(error || save.error || (mode === 'save' && team.error)) && <p role="alert" className="mt-3 text-red-700">{error || save.error?.message || team.error?.message} Kayıt değişmişse yazdıklarınızı kontrol edip formu kapatın ve bilgileri yenileyin.</p>}
  </form>;
}

function PromiseHistory({ id, currency }: { id: string; currency: string }) {
  const [page, setPage] = useState(1);
  const query = useQuery({ queryKey: ['promise-history', id, page], queryFn: () => api<Paged<History>>(`/api/performance/${id}/collection/promise/history?page=${page}`) });
  const snapshot = (json: string) => {
    if (!json) return 'Önceki ödeme sözü yok.';
    try { const s = JSON.parse(json) as Snapshot; return `${moneyPrecise(s.amount, currency)} · ${dayText(s.promisedOn)} · ${s.ownerName} · Sürüm ${s.revision} · ${s.isCancelled ? 'Takipten kaldırıldı' : 'Kayıtlı söz'}${s.taskId ? ' · Takip işi bağlı' : ''}\nKaynak (${dayText(s.sourceContactOn)}): ${s.sourceText}`; }
    catch { return 'Bu kaydın ayrıntıları görüntülenemiyor.'; }
  };
  return <details className="mt-5"><summary className="cursor-pointer font-semibold">Ödeme sözü değişiklik geçmişi</summary>
    {query.isPending ? <p role="status">Geçmiş yükleniyor…</p> : query.isError ? <p role="alert">{query.error.message}</p> : <><div className="mt-3 space-y-3">{query.data.items.map(h => <article className="rounded-lg border p-3 text-sm" key={h.id}><p>{new Date(h.createdAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} · {h.userId}</p><p className="mt-2 whitespace-pre-wrap break-words">Gerekçe: {h.reason}</p><p className="mt-2 whitespace-pre-wrap break-words">Önce: {snapshot(h.oldValueJson)}</p><p className="mt-2 whitespace-pre-wrap break-words">Sonra: {snapshot(h.newValueJson)}</p></article>)}</div><div className="mt-3 flex justify-between gap-2 text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki değişiklikler</button><span>{query.data.total} değişiklik · Sayfa {page}</span><button disabled={page * query.data.pageSize >= query.data.total} onClick={() => setPage(page + 1)}>Sonraki değişiklikler</button></div></>}
  </details>;
}
