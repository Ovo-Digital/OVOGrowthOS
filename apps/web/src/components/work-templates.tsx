'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, type SessionUser } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { todayText, type TeamMember } from '@/components/work-tasks';
import { useUnsavedChanges } from '@/components/use-unsaved-changes';

type Scope = { kind: string; dealId: string | null; year: number; month: number; startOn: string };
type Step = { key: string; title: string; description: string; dueOn: string; existing: { id: string; revision: number; title: string; assigneeId: string; dueOn: string; completedAt: string | null } | null };
type Preview = { scope: Scope; items: Step[] };
type Assignment = { step: string; assigneeId: string; dueOn: string; existingTaskId: string | null; existingTaskRevision: number | null };
const button = 'rounded-lg border px-3 py-2 text-sm font-semibold disabled:opacity-50';

export function WorkTemplates({ brandId }: { brandId: string }) {
  const [open, setOpen] = useState(false); const [kind, setKind] = useState('BrandStart'); const [preview, setPreview] = useState<Preview | null>(null);
  const [items, setItems] = useState<Assignment[]>([]); const [confirmed, setConfirmed] = useState(false); const [message, setMessage] = useState('');
  const cache = useQueryClient();
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team'), enabled: open && canManage });
  const brand = useQuery({ queryKey: ['work-brand', brandId], queryFn: () => api<{ deals: { id: string; name: string }[] }>(`/api/brands/${brandId}`), enabled: open && canManage });
  useUnsavedChanges(!!preview);
  const prepare = useMutation({ mutationFn: (scope: Scope) => api<Preview>(`/api/brands/${brandId}/work-template/preview`, { method: 'POST', body: JSON.stringify(scope) }), onSuccess: data => {
    setPreview(data); setConfirmed(false); setItems(data.items.map(x => ({ step: x.key, assigneeId: x.existing?.assigneeId ?? '', dueOn: x.existing?.dueOn ?? x.dueOn, existingTaskId: x.existing?.id ?? null, existingTaskRevision: x.existing?.revision ?? null })));
  } });
  const save = useMutation({ mutationFn: () => api<{ created: number; reused: number }>(`/api/brands/${brandId}/work-template/apply`, { method: 'POST', body: JSON.stringify({ scope: preview!.scope, items }) }), onSuccess: result => {
    setPreview(null); setOpen(false); setMessage(`${result.created} görev oluşturuldu.${result.reused ? ` ${result.reused} mevcut kapanış görevi değiştirilmeden kullanıldı.` : ''} Finansal durumlar değişmedi.`);
    for (const key of ['work-tasks', 'tasks', 'work-planning', 'audit']) cache.invalidateQueries({ queryKey: [key] });
  } });
  if (!canManage) return null;
  return <Card className="mt-5 p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">Hazır iş şablonları</h2>{!open && <button className={button} onClick={() => { setOpen(true); setMessage(''); prepare.reset(); save.reset(); }}>Şablondan görev hazırla</button>}</div>
    <p className="mt-2 text-sm">Yeni marka başlangıcı veya aylık kapanış için üç adımı ön izleyin. Aynı kapsam ikinci kez uygulanmaz; mevcut görevler silinmez. <Link href="/guide#gorev-sablonlari-ve-ekip-kapasitesi" className="underline">Kullanım rehberi</Link></p>
    {message && <p role="status" className="mt-3">{message}</p>}
    {open && !preview && <form className="mt-4" onSubmit={e => { e.preventDefault(); const f = new FormData(e.currentTarget); const [year, month] = String(f.get('period') ?? '').split('-').map(Number); prepare.mutate({ kind, dealId: kind === 'MonthlyClose' ? String(f.get('deal')) : null, year: kind === 'MonthlyClose' ? year : 0, month: kind === 'MonthlyClose' ? month : 0, startOn: String(f.get('start')) }); }}>
      <fieldset disabled={prepare.isPending} className="grid gap-4 sm:grid-cols-2"><label>İş şablonu<select className="input mt-1" value={kind} onChange={e => setKind(e.target.value)}><option value="BrandStart">Yeni marka başlangıcı</option><option value="MonthlyClose">Aylık kapanış</option></select></label><label>İlk adımın tarihi<input type="date" name="start" className="input mt-1" min="2020-01-01" max="2100-12-26" required defaultValue={todayText()} /></label>
        {kind === 'MonthlyClose' && <><label>Kapatılacak ay<input type="month" name="period" className="input mt-1" min="2020-01" max="2100-12" required /></label><label>İlgili anlaşma<select name="deal" className="input mt-1" required defaultValue=""><option value="">Anlaşma seçin</option>{brand.data?.deals.map(d => <option value={d.id} key={d.id}>{d.name}</option>)}</select></label></>}
        <div className="flex gap-3"><button className={button} disabled={!team.data || (kind === 'MonthlyClose' && !brand.data)}>{prepare.isPending ? 'Hazırlanıyor…' : 'Görevleri ön izle'}</button><button type="button" className={button} onClick={() => { setOpen(false); prepare.reset(); save.reset(); }}>Vazgeç</button></div></fieldset>
    </form>}
    {preview && <form className="mt-4 space-y-4" onSubmit={e => { e.preventDefault(); if (confirmed) save.mutate(); }}><h3 className="font-semibold">Henüz görev oluşturulmadı — sorumlu ve tarihleri kontrol edin</h3><p className="text-sm">{preview.scope.kind === 'MonthlyClose' ? `${preview.scope.month}/${preview.scope.year} kapanışı` : 'Marka başlangıcı'} · Önerilen tarihler değiştirilebilir; otomatik onay veya paylaşım yapılmaz.</p>
      <fieldset disabled={save.isPending} className="space-y-4">{preview.items.map((step, index) => <article className="rounded-lg border p-4" key={step.key}><h4 className="font-semibold">{step.existing?.title ?? step.title}</h4><p className="my-2 text-sm">{step.description}</p>{step.existing && <p className="mb-3 text-sm">Mevcut görev {step.existing.completedAt ? '(tamamlanmış)' : '(açık)'} kullanılacak; başlığı, sorumlusu, tarihi ve durumu değişmeyecek. Gerekirse görev alanından düzenleyin.</p>}
        <div className="grid gap-3 sm:grid-cols-2"><label>Sorumlu — {step.title}<select required disabled={!!step.existing} className="input mt-1" value={items[index].assigneeId} onChange={e => { setConfirmed(false); setItems(items.map((x, i) => i === index ? { ...x, assigneeId: e.target.value } : x)); }}><option value="">Çalışan seçin</option>{team.data?.map(p => <option value={p.id} key={p.id} disabled={!p.isActive}>{p.name}{!p.isActive && ' (kapalı hesap)'}</option>)}</select></label><label>Son tarih — {step.title}<input type="date" required min="2020-01-01" max="2100-12-31" disabled={!!step.existing} className="input mt-1" value={items[index].dueOn} onChange={e => { setConfirmed(false); setItems(items.map((x, i) => i === index ? { ...x, dueOn: e.target.value } : x)); }} /></label></div></article>)}
        <label className="flex items-start gap-2"><input type="checkbox" className="mt-1" required checked={confirmed} onChange={e => setConfirmed(e.target.checked)} /> Sorumluları ve son tarihleri kontrol ettim; yeni görevler bu plana göre oluşturulsun.</label>
        <div className="flex flex-wrap gap-3"><button className={button} disabled={!confirmed}>{save.isPending ? 'Oluşturuluyor…' : 'Kontrol ettim, görevleri oluştur'}</button><button type="button" className={button} onClick={() => { if (confirm('Ön izlemeyi kapatmak istiyor musunuz? Henüz görevler oluşturulmadı.')) setPreview(null); }}>Ön izlemeyi kapat</button></div>
      </fieldset></form>}
    {(prepare.error || save.error || team.error || brand.error) && <p role="alert" className="mt-3 text-red-700">{prepare.error?.message || save.error?.message || team.error?.message || brand.error?.message}</p>}
  </Card>;
}
