'use client';
import Link from 'next/link';
import { useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api, moneyPrecise, type SessionUser } from '@/lib/api';
import { Card, PageHeader } from '@/components/ui/core';

type Field = { key: string; label: string };
type Upload = { fileName: string; contentBase64: string };
type Inspection = { headers: string[]; sample: { line: number; values: string[] }[]; rowCount: number; mapping: Record<string, number> };
type Total = { currency: string; netRevenue: number; ovoFee: number; adSpend: number; brandContribution: number };
type Preview = { rows: { line: number; brand: string; deal: string; period: string; currency: string; netRevenue: number | null; ovoFee: number | null; adSpend: number | null; brandContribution: number | null; errors: string[] }[]; totals: Total[]; canCommit: boolean; previewToken: string | null; expiresAt: string };
type Saved = { batchId: string; rowCount: number; totals: Total[]; records: { id: string; year: number; month: number }[] };
type Deal = { id: string; brandId: string; name: string; status: string; currency: string; brand: { name: string } };
type History = { id: string; userId: string; createdAt: string; details: string };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';

export default function Page() {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const allowed = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const fields = useQuery({ queryKey: ['import-fields'], queryFn: () => api<Field[]>('/api/performance-imports/fields'), enabled: allowed });
  const deals = useQuery({ queryKey: ['deals'], queryFn: () => api<Deal[]>('/api/deals'), enabled: allowed });
  const history = useQuery({ queryKey: ['import-history'], queryFn: () => api<History[]>('/api/performance-imports/history'), enabled: allowed });
  const queryClient = useQueryClient();
  const [upload, setUpload] = useState<Upload | null>(null); const [inspection, setInspection] = useState<Inspection | null>(null);
  const [mapping, setMapping] = useState<Record<string, number>>({}); const [preview, setPreview] = useState<Preview | null>(null);
  const [saved, setSaved] = useState<Saved | null>(null); const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const operation = useRef(false);
  async function read(file?: File) {
    if (operation.current) return; operation.current = true; setBusy(true); setError(''); setSaved(null); setPreview(null); setConfirmed(false); setUpload(null); setInspection(null);
    try {
      if (!file) return;
      if (file.size > 1_000_000 || !/\.(csv|xlsx)$/i.test(file.name)) throw new Error('En fazla 1 MB büyüklüğünde .xlsx veya UTF-8 .csv dosyası seçin.');
      const contentBase64 = await new Promise<string>((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(String(reader.result).split(',')[1]); reader.onerror = () => reject(new Error('Dosya okunamadı.')); reader.readAsDataURL(file); });
      const next = { fileName: file.name, contentBase64 };
      const result = await api<Inspection>('/api/performance-imports/inspect', { method: 'POST', body: JSON.stringify(next) });
      setUpload(next); setInspection(result); setMapping(result.mapping);
    } catch (e) { setError(e instanceof Error ? e.message : 'Dosya okunamadı.'); }
    finally { setBusy(false); operation.current = false; }
  }
  async function calculate() {
    if (!upload || operation.current) return; operation.current = true; setBusy(true); setError(''); setPreview(null); setConfirmed(false);
    try { setPreview(await api<Preview>('/api/performance-imports/preview', { method: 'POST', body: JSON.stringify({ ...upload, mapping }) })); }
    catch (e) { setError(e instanceof Error ? e.message : 'Ön izleme hazırlanamadı.'); }
    finally { setBusy(false); operation.current = false; }
  }
  async function commit() {
    if (!upload || !preview?.canCommit || !confirmed || operation.current) return; operation.current = true; setBusy(true); setError('');
    try {
      const result = await api<Saved>('/api/performance-imports/commit', { method: 'POST', body: JSON.stringify({ ...upload, mapping, previewToken: preview.previewToken }) });
      setSaved(result); setPreview(null); setInspection(null); setUpload(null); setConfirmed(false);
      await Promise.all([queryClient.invalidateQueries({ queryKey: ['performance'] }), queryClient.invalidateQueries({ queryKey: ['import-history'] }), queryClient.invalidateQueries({ queryKey: ['dashboard'] })]);
    } catch (e) { setError(e instanceof Error ? e.message : 'Aktarım tamamlanamadı.'); setPreview(null); setConfirmed(false); }
    finally { setBusy(false); operation.current = false; }
  }
  if (me.isPending) return <p>Hesap yetkisi kontrol ediliyor…</p>;
  if (me.isError) return <p role="alert">{me.error.message}</p>;
  if (!allowed) return <Card className="p-5">Dosyadan aktarımı yönetici veya ortak yapabilir. <Link className="underline" href="/performance">Aylık sonuçlara dön</Link></Card>;
  const completeMapping = fields.data?.every(f => mapping[f.key] >= 0) && new Set(Object.values(mapping)).size === fields.data?.length;
  return <>
    <PageHeader title="Dosyadan aylık sonuç aktar" description="Önce sütunları eşleştirin, sonra bütün satırları kontrol edin. Onayınızdan önce kayıt oluşmaz." />
    <Card className="mb-4 space-y-3 p-5"><h2 className="font-semibold">1. Şablonu doldurun</h2>
      <p className="text-sm">Her satır bir markanın bir ayıdır. Yalnız yeni taslaklar oluşturulur; mevcut taslak veya kilitli dönemler değişmez. En fazla 200 satır ve 1 MB dosya kullanın.</p>
      <div className="flex flex-wrap gap-3"><a className={button} href="/templates/ovo-aylik-veri.xlsx" download>Excel şablonu indir</a><a className={button} href="/templates/ovo-aylik-veri.csv" download>CSV şablonu indir</a><Link className={button} href="/guide">Kullanım rehberi</Link></div>
      <p className="text-sm">Tüm sayı alanlarını doldurun; harcama yoksa 0 yazın. CSV için Türkçe sayı biçimi kullanın: 1.234,56. Dönemi 2026-08 veya 08.2026 yazın. Excel’de yalnız değer kullanın; formüller ve birleşik hücreler kabul edilmez. Birden fazla sayfa varsa yalnız <strong>AylikVeri</strong> okunur.</p>
      <p className="text-sm">Brüt satış, KDV ve diğer kesintileri aylık sonuç ekranındaki tanımlarla girin. Hesaplanan net ciro veya hakedişi bu alanlara yazmayın. Para birimleri ayrı hesaplanır.</p>
      <details><summary className="cursor-pointer font-semibold">Marka ve anlaşma kodlarını göster</summary>{deals.isPending ? <p>Liste yükleniyor…</p> : deals.isError ? <p role="alert">{deals.error.message}</p> : <div className="mt-3 space-y-3">{deals.data?.filter(d => d.status === 'Active').map(d => <div className="rounded-lg border p-3 text-sm" key={d.id}><p className="font-semibold">{d.brand.name} · {d.name} · {d.currency}</p><p className="mt-2 break-all">Marka kodu: <code className="select-all">{d.brandId}</code></p><p className="break-all">Anlaşma kodu: <code className="select-all">{d.id}</code></p></div>)}{!deals.data?.some(d => d.status === 'Active') && <p>Etkin anlaşma yok. Önce markanın anlaşmasını etkinleştirin.</p>}</div>}</details>
    </Card>
    <Card className="mb-4 space-y-3 p-5"><h2 className="font-semibold">2. Dosyayı seçin ve sütunları eşleştirin</h2><label className="block text-sm">Aylık veri dosyası<input className="mt-2 block max-w-full" type="file" accept=".xlsx,.csv" disabled={busy} onChange={e => { const file = e.target.files?.[0]; e.target.value = ''; void read(file); }} /></label>
      {busy && <p role="status">İşlem sürüyor, lütfen bekleyin…</p>}{error && <p role="alert" className="text-sm text-red-700">{error}</p>}{fields.isError && <p role="alert">{fields.error.message}</p>}
      {inspection && <><p className="text-sm">{upload?.fileName} · {inspection.rowCount} veri satırı. Her alanı ayrı bir sütuna bağlayın. Eşleştirmediğiniz ek sütunlar aktarılmaz.</p><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{fields.data?.map(f => <label className="text-sm" key={f.key}>{f.label}<select className="input mt-1" disabled={busy} value={mapping[f.key] ?? -1} onChange={e => { setMapping({ ...mapping, [f.key]: Number(e.target.value) }); setPreview(null); setConfirmed(false); }}><option value={-1}>Sütun seçin</option>{inspection.headers.map((h, i) => <option key={i} value={i}>{h}</option>)}</select><span className="mt-1 block break-all text-xs text-gray-600">İlk satır: {inspection.sample[0]?.values[mapping[f.key]] || 'Boş'}</span></label>)}</div><button className={button} disabled={busy || !completeMapping} onClick={() => void calculate()}>Satırları kontrol et ve ön izle</button></>}
    </Card>
    {preview && <Card className="mb-4 p-5"><h2 className="font-semibold">3. Sonuçları kontrol edin</h2><p className="my-3 text-sm">{preview.canCommit ? 'Bütün satırlar uygun. Aşağıdaki tutarları kontrol edip onaylayın.' : 'Hatalı satırlar var. Hiçbir kayıt oluşturulmadı. Dosyayı düzeltip yeniden seçin; hatalı dosya için kısmi toplam göstermiyoruz.'}</p><div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr>{['Satır', 'Marka / anlaşma', 'Dönem', 'Net ciro', 'OVO hakedişi', 'Reklam', 'Markanın katkısı', 'Kontrol'].map(x => <th className="border-b p-2" key={x}>{x}</th>)}</tr></thead><tbody>{preview.rows.map((r, i) => <tr key={i}><td className="border-b p-2">{r.line}</td><td className="border-b p-2">{r.brand}<small className="block">{r.deal}</small></td><td className="border-b p-2">{r.period} {r.currency}</td>{[r.netRevenue, r.ovoFee, r.adSpend, r.brandContribution].map((v, j) => <td className="whitespace-nowrap border-b p-2" key={j}>{v === null ? 'Hesaplanmadı' : moneyPrecise(v, r.currency)}</td>)}<td className="min-w-60 border-b p-2">{r.errors.length ? <ul className="list-disc pl-4 text-red-700">{r.errors.map((e, j) => <li key={j}>{e}</li>)}</ul> : 'Yeni taslak olarak aktarılabilir'}</td></tr>)}</tbody></table></div>
      <Totals totals={preview.totals} />{preview.canCommit && <div className="mt-4 space-y-3"><p className="text-xs">Ön izleme onayı 15 dakika geçerlidir. Kaydetme sırasında dosya ve anlaşmalar yeniden kontrol edilir. Sonuçlar otomatik onaylanmaz veya kilitlenmez.</p><label className="flex items-start gap-2 text-sm"><input type="checkbox" checked={confirmed} disabled={busy} onChange={e => setConfirmed(e.target.checked)} />Markaları, dönemleri, para birimlerini ve tutarları kontrol ettim. {preview.rows.length} yeni taslak oluşturulmasını onaylıyorum.</label><button className="rounded-lg bg-[#303030] px-4 py-2 text-sm text-white disabled:opacity-50" disabled={busy || !confirmed} onClick={() => void commit()}>Onayla ve taslakları kaydet</button></div>}
    </Card>}
    {saved && <Card className="mb-4 p-5"><h2 className="font-semibold" role="status">{saved.rowCount} aylık sonuç taslak olarak kaydedildi</h2><Totals totals={saved.totals} /><p className="my-3 text-sm">Kontrole gönderme ve başka bir yetkilinin onay süreci devam eder. Dosyayı tekrar yüklemek ikinci kayıt oluşturmaz.</p><div className="flex flex-wrap gap-3">{saved.records.map((r, i) => <Link className="underline" key={r.id} href={`/performance/${r.id}`}>{i + 1}. kayıt · {r.month}/{r.year}</Link>)}</div></Card>}
    <Card className="p-5"><h2 className="font-semibold">Son 30 başarılı aktarım</h2><p className="my-2 text-sm">Dosyanın içeriği saklanmaz; dosya adı, aktaran kişi, zaman, toplamlar ve oluşturulan kayıtlar işlem geçmişine yazılır.</p>{history.isPending ? <p>Geçmiş yükleniyor…</p> : history.isError ? <p role="alert">{history.error.message}</p> : history.data?.length ? <div className="space-y-3">{history.data.map(h => { const detail = JSON.parse(h.details) as { fileName: string; rowCount: number }; return <div key={h.id} className="rounded-lg border p-3 text-sm"><p className="break-all font-semibold">{detail.fileName} · {detail.rowCount} kayıt</p><p>{h.userId} · {new Date(h.createdAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' })} (Türkiye saati)</p></div>; })}</div> : <p className="text-sm">Henüz başarılı aktarım yok.</p>}</Card>
  </>;
}
function Totals({ totals }: { totals: Total[] }) { return <div className="mt-4 space-y-3">{totals.map(t => <div key={t.currency} className="rounded-lg border p-3 text-sm"><h3 className="font-semibold">Dosya toplamı · {t.currency}</h3><p>Net ciro: {moneyPrecise(t.netRevenue, t.currency)}</p><p>OVO hakedişi: {moneyPrecise(t.ovoFee, t.currency)}</p><p>Reklam: {moneyPrecise(t.adSpend, t.currency)}</p><p>Markaya kalan katkı: {moneyPrecise(t.brandContribution, t.currency)}</p></div>)}</div>; }
