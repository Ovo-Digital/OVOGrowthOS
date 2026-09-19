'use client';
import { useEffect, useState, type FormEvent } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';
const date = (s: string | null) => s ? new Date(s).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' }) : 'Henüz yok';
type Reading = { firstViewedAt: string | null; lastViewedAt: string | null; reviewedAt: string | null };

export function PortalReading({ reportId }: { reportId: string }) {
  const qc = useQueryClient(); const key = ['portal-reading', reportId];
  const root = `/api/portal/reports/${reportId}`;
  const query = useQuery({ queryKey: key, queryFn: () => api<Reading>(root + '/reading'), refetchInterval: 30000 });
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    void api<Reading>(`/api/portal/reports/${reportId}/viewed`, { method: 'POST' }).then(data => {
      if (active) qc.setQueryData(['portal-reading', reportId], data);
    }).catch(() => { if (active) setError('Görüntüleme bilgisi kaydedilemedi. Sayfayı yenileyip tekrar deneyebilirsiniz.'); });
    return () => { active = false; };
  }, [reportId, qc]);
  async function reviewed() {
    if (busy || !confirm('Bu rapor sürümünü incelediğiniz kaydedilsin mi? Bu işlem tutarları kabul etmek, ödeme yapmak veya dönem onaylamak değildir.')) return;
    setBusy(true); setError('');
    try { qc.setQueryData(key, await api<Reading>(root + '/reviewed', { method: 'POST' })); }
    catch (e) { setError(e instanceof Error ? e.message : 'İnceleme kaydedilemedi.'); } finally { setBusy(false); }
  }
  return <Card className="my-3 space-y-2 p-4 text-sm print:hidden"><h3 className="font-semibold">Raporu inceleme bilgisi</h3><p>Bu rapor açıldığında ilk ve son görüntüleme zamanı OVO ekibine gösterilir. “İnceledim” yalnız sizin bu sürümü incelediğinizi belirtir; ticari kabul, dönem onayı veya ödeme değildir. Yeni sürümün kaydı ayrıdır.</p>
    {error && <p role="alert">{error}</p>}{query.isPending ? <p role="status">İnceleme bilgisi yükleniyor…</p> : query.isError ? <p role="alert">İnceleme bilgisi alınamadı. {query.error.message}</p> : <p>İnceledim kaydınız: {date(query.data.reviewedAt)} (Türkiye saati)</p>}
    <button className={button} disabled={busy || query.isPending || query.isError || !!query.data?.reviewedAt} onClick={() => void reviewed()}>{query.data?.reviewedAt ? 'Bu sürümü incelediniz' : 'Bu sürümü inceledim'}</button>
  </Card>;
}

type RequestStatus = 'Requested' | 'Received' | 'Cancelled';
type DataRequest = { id: string; title: string; instructions: string; dueOn: string | null; status: RequestStatus; revision: number; updatedAt: string };
const requestLabels: Record<RequestStatus, string> = { Requested: 'Sizden bekleniyor', Received: 'Ekip teslim aldı', Cancelled: 'Artık istenmiyor' };

export function PortalRequests({ brandId }: { brandId?: string }) {
  const root = brandId ? `/api/portal-management/brands/${brandId}` : '/api/portal'; const qc = useQueryClient();
  const query = useQuery({ queryKey: ['portal-requests', root], queryFn: () => api<DataRequest[]>(root + '/requests'), refetchInterval: 30000 });
  const [creating, setCreating] = useState(false); const [notice, setNotice] = useState('');
  return <Card className="space-y-3 p-5 print:hidden"><h2 className="font-semibold">{brandId ? '5. Müşteriden istenen bilgiler' : 'Sizden beklenen bilgi ve belgeler'}</h2><p className="text-sm">{brandId ? 'Oluşturduğunuz talep bu markanın tüm müşteri hesaplarına gösterilir. Ne gerektiğini ve mevcut güvenli teslim kanalını açıkça yazın. İç not yazmayın.' : 'İstenen bilgiyi açıklamadaki mevcut güvenli kanaldan OVO ekibine iletin. Teslim durumunu ekip kontrol edip günceller.'} Bu sayfadan dosya yüklenmez, e-posta gönderilmez. Talep metni sonradan değiştirilmez; yanlış talebi kapatıp yenisini oluşturun.</p>
    {notice && <p role="status">{notice}</p>}{query.isPending ? <p role="status">Talepler yükleniyor…</p> : query.isError ? <p role="alert">Talepler alınamadı. {query.error.message}</p> : <>{!query.data.length && <p>Henüz bilgi veya belge talebi yok.</p>}{query.data.map(item => <RequestItem key={item.id} item={item} root={root} staff={!!brandId} />)}</>}
    {brandId && (creating ? <RequestCreate root={root} done={() => { setCreating(false); setNotice('Talep markanın portalına eklendi.'); void qc.invalidateQueries({ queryKey: ['portal-requests', root] }); }} cancel={() => setCreating(false)} /> : <button className={button} onClick={() => { setCreating(true); setNotice(''); }}>Yeni bilgi veya belge iste</button>)}
  </Card>;
}

function RequestCreate({ root, done, cancel }: { root: string; done: () => void; cancel: () => void }) {
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); useUnsavedChanges(dirty);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (busy) return; const data = new FormData(e.currentTarget);
    if (!confirm('Bu talep metni markanın tüm müşteri hesaplarına gösterilsin mi?')) return;
    setBusy(true); setError('');
    try { await api(root + '/requests', { method: 'POST', body: JSON.stringify({ title: data.get('title'), instructions: data.get('instructions'), dueOn: data.get('dueOn') || null }) }); done(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Talep oluşturulamadı.'); } finally { setBusy(false); }
  }
  return <form className="space-y-3 border-t pt-3 text-sm" onSubmit={submit} onChange={() => setDirty(true)}>{error && <p role="alert">{error}</p>}<label className="block">İstenen bilgi veya belge<input name="title" maxLength={200} required className="input mt-1" disabled={busy} /></label><label className="block">Müşteriye açıklama ve teslim yolu<textarea name="instructions" rows={3} maxLength={2000} required className="input mt-1" disabled={busy} /></label><label className="block">İstenen son tarih (isteğe bağlı)<input name="dueOn" type="date" min="2020-01-01" max="2100-12-31" className="input mt-1" disabled={busy} /></label><div className="flex flex-wrap gap-2"><button className={button} disabled={busy}>Talebi müşteriye göster</button><button type="button" className={button} disabled={busy} onClick={() => { if (!dirty || confirm('Talep taslağı kaydedilmeyecek. Vazgeçilsin mi?')) cancel(); }}>Vazgeç</button></div></form>;
}

function RequestItem({ item, root, staff }: { item: DataRequest; root: string; staff: boolean }) {
  const [editing, setEditing] = useState(false); const [notice, setNotice] = useState('');
  return <article className="space-y-2 rounded-lg border p-3 text-sm"><h3 className="break-words font-semibold">{item.title} · {requestLabels[item.status]}</h3><p className="whitespace-pre-wrap break-words">{item.instructions}</p><p>İstenen tarih: {item.dueOn ? item.dueOn.split('-').reverse().join('.') : 'Belirtilmedi'} · Son güncelleme: {date(item.updatedAt)} (Türkiye)</p>{notice && <p role="status">{notice}</p>}{staff && (editing ? <RequestStatusEditor item={item} root={root} done={() => { setEditing(false); setNotice('Talebin durumu güncellendi.'); }} cancel={() => setEditing(false)} /> : <button className={button} onClick={() => { setEditing(true); setNotice(''); }}>Teslim durumunu güncelle</button>)}</article>;
}

function RequestStatusEditor({ item, root, done, cancel }: { item: DataRequest; root: string; done: () => void; cancel: () => void }) {
  const qc = useQueryClient(); const [revision, setRevision] = useState(item.revision); const [status, setStatus] = useState(item.status); const [reason, setReason] = useState(''); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  useUnsavedChanges(!!reason || status !== item.status);
  async function submit(e: FormEvent) { e.preventDefault(); if (busy) return; setBusy(true); setError('');
    try { await api(root + '/requests/' + item.id, { method: 'PUT', body: JSON.stringify({ status, reason, revision }) }); await qc.invalidateQueries({ queryKey: ['portal-requests', root] }); done(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Durum kaydedilemedi.'); await qc.invalidateQueries({ queryKey: ['portal-requests', root] }); } finally { setBusy(false); }
  }
  return <form onSubmit={submit} className="space-y-2">{error && <p role="alert">{error}</p>}{revision !== item.revision && <p role="alert">Talep başka bir kişi tarafından değiştirildi. Üstteki yeni durumu kontrol edin. <button type="button" className="underline" disabled={busy} onClick={() => { setRevision(item.revision); setStatus(item.status); }}>Gerekçemi koruyup güncel durumu al</button></p>}<label className="block">Teslim durumu<select className="input mt-1" value={status} disabled={busy} onChange={e => setStatus(e.target.value as RequestStatus)}>{Object.entries(requestLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label><label className="block">İç işlem gerekçesi<textarea className="input mt-1" rows={2} required maxLength={1000} value={reason} disabled={busy} onChange={e => setReason(e.target.value)} /></label><p>Durum müşteriye görünür; gerekçe yalnız iç işlem geçmişinde tutulur.</p><div className="flex flex-wrap gap-2"><button className={button} disabled={busy || revision !== item.revision}>Durumu kaydet</button><button type="button" className={button} disabled={busy} onClick={() => { if (!reason || confirm('Kaydedilmemiş gerekçe silinsin mi?')) cancel(); }}>Vazgeç</button></div></form>;
}

export function PortalReportFollowup({ brandId }: { brandId: string }) {
  const root = `/api/portal-management/brands/${brandId}`;
  const updates = useQuery({ queryKey: ['portal-admin', brandId, 'report-updates'], queryFn: () => api<{ id: string; year: number; month: number; version: number; needsUpdate: boolean; canPublish: boolean }[]>(root + '/report-updates'), refetchInterval: 30000 });
  const readings = useQuery({ queryKey: ['portal-admin', brandId, 'readings'], queryFn: () => api<(Reading & { reportId: string; name: string; year: number; month: number; version: number; revokedAt: string | null })[]>(root + '/readings'), refetchInterval: 30000 });
  return <Card className="space-y-3 p-5"><h2 className="font-semibold">Rapor güncelliği ve inceleme takibi</h2><p className="text-sm">Her ayın paylaşımı açık son sürümü güncel marka rakamlarıyla karşılaştırılır. Yeni tahsilat veya ödeme iptali gibi bir fark varsa yeni sürüm gerekebilir. Sistem kendiliğinden rapor yayımlamaz; ön izlemeyi kontrol edip siz paylaşın. Eski sürümün inceleme kaydı yeni sürüme taşınmaz.</p>
    {updates.isPending ? <p role="status">Rapor güncelliği kontrol ediliyor…</p> : updates.isError ? <p role="alert">Güncellik kontrol edilemedi. {updates.error.message}</p> : <>{!updates.data.length ? <p>Paylaşımı açık rapor yok.</p> : !updates.data.some(r => r.needsUpdate) && <p>Paylaşımı açık son sürümlerin marka rakamlarında fark görülmedi.</p>}{updates.data.filter(r => r.needsUpdate).map(r => <p key={r.id} className="rounded-lg border p-3 text-sm">{r.month}/{r.year} · Sürüm {r.version}: Yeni sürüm gerekebilir. {r.canPublish ? <a className="underline" href="#portal-publish">Yayımlama bölümünde ön izlemeyi kontrol et</a> : 'Dönem şu an yayımlanabilir durumda değil; önce dönem durumunu kontrol edin.'}</p>)}</>}
    <h3 className="font-semibold">Müşterinin görüntüleme ve “İnceledim” kayıtları</h3><p className="text-sm">Görüntüleme yalnız raporun ekranda açıldığı bilgisidir; okunduğunu kanıtlamaz. “İnceledim” ticari kabul, ödeme veya dönem onayı değildir. Zamanlar Türkiye saatidir.</p>
    {readings.isPending ? <p role="status">İnceleme kayıtları yükleniyor…</p> : readings.isError ? <p role="alert">İnceleme kayıtları alınamadı. {readings.error.message}</p> : <>{!readings.data.length && <p>Henüz kaydedilmiş görüntüleme yok. Özellik eklenmeden önceki görüntülemeler bilinmiyor.</p>}{readings.data.map((r, i) => <div key={`${r.reportId}-${i}`} className="rounded-lg border p-3 text-sm"><p className="font-semibold">{r.name} · {r.month}/{r.year} · Sürüm {r.version}{r.revokedAt && ' · Paylaşım geri çekildi'}</p><p>İlk görüntüleme: {date(r.firstViewedAt)} · Son görüntüleme: {date(r.lastViewedAt)}</p><p>İnceledim: {date(r.reviewedAt)}</p></div>)}</>}
  </Card>;
}
