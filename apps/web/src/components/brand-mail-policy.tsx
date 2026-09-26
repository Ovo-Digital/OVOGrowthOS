'use client';
import { FormEvent, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Card } from '@/components/ui/core';

type Schedule = { enabled: boolean; day: number; hour: number; nextOccurrenceAt: string; targetYear: number; targetMonth: number; targetReportId: string | null; targetReportPublished: boolean; sendingNow: boolean };
type Policy = { reportEmailEnabled: boolean; subjectTemplate: string; bodyTemplate: string; revision: number; emailReady: boolean; scheduledReportEnabled: boolean; scheduledSendDay: number; scheduledSendHour: number; schedule: Schedule };
type Report = { id: string; year: number; month: number; version: number; revokedAt: string | null };
type Preview = { subject: string; body: string; note: string; deferredUntil: string | null; recipients: { id: string; name: string; email: string; eligible: boolean; deferred: boolean; scheduledFor: string | null; reasons: string[] }[] };
const button = 'rounded-lg border px-3 py-2 text-sm disabled:opacity-50';
const trDate = (iso: string) => new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', dateStyle: 'long', timeStyle: 'short' }).format(new Date(iso));

export function BrandMailPolicyPanel({ brandId, reports }: { brandId: string; reports: Report[] }) {
  const root = `/api/portal-management/brands/${brandId}/email-policy`;
  const policy = useQuery({ queryKey: ['brand-mail-policy', brandId], queryFn: () => api<Policy>(root) });
  const [notice, setNotice] = useState('');
  return <Card className="space-y-3 p-5"><h2 className="font-semibold">Rapor e-postası izni, zamanlama ve alıcı kontrolü</h2>
    <p className="text-sm">Bu markanın rapor bildirimlerini ayrıca yönetebilirsiniz. İlk açılışta kapalıdır. Marka iznini açmak kişinin kendi tercihini değiştirmez; görev, konuşma ve hesap davetleri bu anahtardan etkilenmez.</p>
    {notice && <p role="status">{notice}</p>}
    {policy.isPending && <p>Markanın e-posta kuralı yükleniyor…</p>}
    {policy.isError && <p role="alert">{policy.error.message} <button className={button} onClick={() => void policy.refetch()}>Yeniden yükle</button></p>}
    {policy.data && <PolicyForm key={policy.data.revision} policy={policy.data} brandId={brandId} root={root} reports={reports} onNotice={setNotice} />}
  </Card>;
}

function PolicyForm({ policy, brandId, root, reports, onNotice }: { policy: Policy; brandId: string; root: string; reports: Report[]; onNotice: (message: string) => void }) {
  const qc = useQueryClient();
  const [enabled, setEnabled] = useState(policy.reportEmailEnabled);
  const [scheduled, setScheduled] = useState(policy.scheduledReportEnabled);
  const [day, setDay] = useState(policy.scheduledSendDay);
  const [hour, setHour] = useState(policy.scheduledSendHour);
  const [subject, setSubject] = useState(policy.subjectTemplate);
  const [body, setBody] = useState(policy.bodyTemplate);
  const [reason, setReason] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [reportId, setReportId] = useState('');
  const dirty = enabled !== policy.reportEmailEnabled || subject !== policy.subjectTemplate || body !== policy.bodyTemplate
    || scheduled !== policy.scheduledReportEnabled || day !== policy.scheduledSendDay || hour !== policy.scheduledSendHour;
  const preview = useQuery({ queryKey: ['brand-mail-preview', brandId, reportId, policy.revision], queryFn: () => api<Preview>(`${root}/preview/${reportId}`), enabled: !!reportId && !dirty });
  async function save(e: FormEvent) {
    e.preventDefault(); if (busy) return;
    setBusy(true); setError(''); onNotice('');
    try {
      const result = await api<{ message: string }>(root, { method: 'PUT', body: JSON.stringify({ reportEmailEnabled: enabled, subjectTemplate: subject, bodyTemplate: body, scheduledReportEnabled: scheduled, scheduledSendDay: day, scheduledSendHour: hour, reason, revision: policy.revision }) });
      onNotice(result.message);
      await Promise.all([qc.invalidateQueries({ queryKey: ['brand-mail-policy', brandId] }), qc.invalidateQueries({ queryKey: ['brand-mail-preview', brandId] })]);
    } catch (e) { setError(e instanceof Error ? e.message : 'Kural kaydedilemedi.'); } finally { setBusy(false); }
  }
  const s = policy.schedule;
  return <>
    <p className="text-sm">Genel e-posta hizmeti: {policy.emailReady ? 'Hazır' : 'Kapalı veya eksik bilgi var'}. Bu ekran SMTP şifresini göstermez.</p>
    <form className="space-y-3" onSubmit={save}>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={enabled} disabled={busy} onChange={e => setEnabled(e.target.checked)} />Bu markanın rapor e-postalarına izin ver</label>
      <fieldset disabled={busy} className="grid gap-3 rounded-lg border p-3 sm:grid-cols-2">
        <legend className="px-1 text-sm font-semibold">Aylık zamanlanmış gönderim</legend>
        <label className="flex items-center gap-2 text-sm sm:col-span-2"><input type="checkbox" checked={scheduled} disabled={!enabled} onChange={e => setScheduled(e.target.checked)} />Her ayın belirli gününde bir önceki ayın paylaşılmış raporunu gönder</label>
        <label className="block text-sm">Ayın günü<select className="input mt-1" value={day} disabled={!enabled || !scheduled} onChange={e => setDay(Number(e.target.value))}>{Array.from({ length: 31 }, (_, i) => i + 1).map(d => <option key={d} value={d}>{d}</option>)}</select></label>
        <label className="block text-sm">Saat (Türkiye)<select className="input mt-1" value={hour} disabled={!enabled || !scheduled} onChange={e => setHour(Number(e.target.value))}>{Array.from({ length: 24 }, (_, i) => i).map(h => <option key={h} value={h}>{String(h).padStart(2, '0')}:00</option>)}</select></label>
        {!enabled && <p className="text-sm sm:col-span-2">Zamanlanmış gönderim için önce markanın rapor e-postası iznini açın.</p>}
        {enabled && scheduled && <p className="text-sm sm:col-span-2">Yalnız bir önceki ayın, müşteri portalında paylaşılmış kapanmış raporu gönderilir. E-posta dosya eklemez; giriş gerektiren portal adresini içerir. Daha eski aylar toplu olarak gönderilmez.</p>}
        {enabled && !scheduled && <p className="text-sm sm:col-span-2">Zamanlanmış gönderim kapalı. Yalnız rapor paylaşıldığı anda bildirim e-postası gider.</p>}
        {s && <p className="text-sm sm:col-span-2" role="status">
          {s.enabled
            ? s.sendingNow
              ? `Gönderim penceresi açık: ${s.targetMonth}/${s.targetYear} dönemi ${trDate(s.nextOccurrenceAt)} için işleniyor.`
              : `Sıradaki gönderim: ${trDate(s.nextOccurrenceAt)} → ${s.targetMonth}/${s.targetYear} dönemi.`
            : 'Zamanlanmış gönderim kapalı.'}
          {s.enabled && !s.targetReportPublished && ` ${s.targetMonth}/${s.targetYear} raporu henüz paylaşılmadı; bu dönem için e-posta gönderilmez ve daha eski bir ay gönderilmez.`}
          {s.enabled && s.targetReportPublished && !s.sendingNow && ' İlgili rapor paylaşılmış durumda; gönderim günü güncel izinler yeniden kontrol edilerek yapılır.'}
        </p>}
      </fieldset>
      <label className="block text-sm">E-posta konusu<input className="input mt-1" required maxLength={180} value={subject} onChange={e => setSubject(e.target.value)} /></label>
      <label className="block text-sm">E-posta mesajı<textarea className="input mt-1 min-h-32" required maxLength={2000} value={body} onChange={e => setBody(e.target.value)} /></label>
      <p className="text-sm">{'{marka} marka adını, {donem} raporun ayını, {baglanti} giriş gerektiren portal adresini ekler. Mesajda {baglanti} bulunmalı. HTML, dış bağlantı veya finansal rakam eklemeyin; iç bilgiler e-postaya yazılmamalıdır.'}</p>
      <label className="block text-sm">Değişiklik gerekçesi<input className="input mt-1" required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} placeholder="Örneğin: Marka iletişim planı netleştirildi" /></label>
      <p className="text-sm">Kaydetmek yeni bir e-posta oluşturmaz. Kapatmak sonraki gönderimleri durdurur; başlamış veya gönderilmiş ileti geri alınamaz. Aynı rapor aynı kişiye yalnız bir kez e-posta ile gider.</p>
      <button className={button} disabled={busy}>{busy ? 'Kaydediliyor…' : 'Markanın e-posta kuralını kaydet'}</button>
      {error && <p role="alert">{error}</p>}
    </form>
    <h3 className="font-semibold">Seçilen rapor kimlere gidebilir?</h3>
    <label className="block text-sm">Alıcı kontrolü yapılacak rapor<select className="input mt-1" value={reportId} onChange={e => setReportId(e.target.value)}><option value="">Rapor seçin</option>{reports.map(r => <option key={r.id} value={r.id}>{r.month}/{r.year} · Sürüm {r.version}{r.revokedAt ? ' · Geri çekildi' : ''}</option>)}</select></label>
    {!reports.length && <p className="text-sm">Henüz yayımlanmış rapor yok. Rapor yayımlandıktan sonra alıcı kontrolü yapılabilir; bu ekran kendiliğinden rapor yayımlamaz.</p>}
    {dirty && <p role="status" className="text-sm">Ön izleme için önce değişiklikleri kaydedin.</p>}
    {reportId && !dirty && <>
      <button className={button} disabled={preview.isFetching} onClick={() => void preview.refetch()}>Alıcı durumunu yenile</button>
      {preview.isPending && <p>Alıcılar kontrol ediliyor…</p>}
      {preview.isError && <p role="alert">{preview.error.message}</p>}
      {preview.data && <div className="space-y-3 text-sm">
        <p>{preview.data.note}</p>
        <p>{preview.data.recipients.filter(r => r.eligible && !r.deferred).length} kişinin koşulları uygun, {preview.data.recipients.filter(r => r.deferred).length} kişi zamanlanmış gönderimi bekliyor. Bu sayı gönderildiği veya teslim edildiği anlamına gelmez.</p>
        <div className="rounded-lg border p-3"><p className="break-words font-semibold">Konu: {preview.data.subject}</p><p className="mt-2 whitespace-pre-wrap break-words">{preview.data.body}</p><p className="mt-2">Gönderimde kişisel bildirim tercih hatırlatması da eklenir.</p></div>
        {!preview.data.recipients.length && <p>Bu markaya bağlı müşteri hesabı yok.</p>}
        {preview.data.recipients.map(r => <div key={r.id} className="rounded-lg border p-3"><p className="break-words font-semibold">{r.name} · {r.email}</p>
          <p>{r.deferred ? `Koşullar uygun; e-posta ${trDate(r.scheduledFor ?? preview.data.deferredUntil!)} tarihinde zamanlanmış gönderimle gidecek.` : r.eligible ? 'Güncel koşullar uygun; gönderim öncesi tekrar kontrol edilir.' : 'Gönderime uygun değil:'}</p>
          {r.reasons.length > 0 && <ul className="ml-5 list-disc">{r.reasons.map(reason => <li key={reason}>{reason}</li>)}</ul>}</div>)}
      </div>}
    </>}
  </>;
}
