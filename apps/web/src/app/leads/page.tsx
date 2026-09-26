'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api, percent } from '@/lib/api';
import { Badge, Card, PageHeader, PrimaryLink } from '@/components/ui/core';
import { notify } from '@/components/feedback';
import { leadSources, leadStages } from '@/components/brand-team';
import { dayText, type Paged, type TeamMember } from '@/components/work-tasks';
import { turkce } from '@/lib/turkish';

type FollowUp = { ownerId: string | null; stage: string; waitingReason: string; nextContactOn: string | null; nextStep: string; revision: number; sourceChannel: string; sourceNote: string; lostOn: string | null; lostReason: string };
type Lead = { id: string; name: string; status: string; contactName: string; contactEmail: string; lastContactOn: string | null; stageEnteredAt: string | null; stageEntryKnown: boolean; followUp: FollowUp | null };
type StageWaitRow = { stage: string; open: number; known: number; unknown: number; averageDays: number | null; longestDays: number };
type Summary = { open: number; lost: number; stages: StageWaitRow[]; sources: { channel: string; label: string; count: number }[]; conversion: { reached: number; converted: number; rate: number | null; beforeMeasurement: number; notMeasured: number }; losses: { reason: string; count: number }[]; renewalPrep: { taskId: string; brandName: string; title: string; dueOn: string; assigneeName: string; completed: boolean }[]; notes: string[] };

const stageDays = (lead: Lead) => {
  if (!lead.stageEnteredAt || !lead.stageEntryKnown) return 'Ölçülmedi';
  const days = Math.floor((Date.now() - new Date(lead.stageEnteredAt).getTime()) / 86400000);
  return `${Math.max(days, 0)} gün`;
};

export default function Page() {
  const [page, setPage] = useState(1);
  const leads = useQuery({ queryKey: ['lead-follow-ups', page], queryFn: () => api<Paged<Lead>>('/api/lead-follow-ups?page=' + page) });
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<{ role: string }>('/api/auth/me') });
  const summary = useQuery({ queryKey: ['pipeline-summary'], queryFn: () => api<Summary>('/api/pipeline/summary') });
  const canManage = me.data?.role === 'Admin' || me.data?.role === 'Partner';
  const refresh = async () => { await leads.refetch(); await summary.refetch(); };
  async function recordLoss(id: string, name: string) {
    const reason = window.prompt(`${name} markası neden kaybedildi? Açıklama yazın.`);
    if (reason === null) return;
    if (!reason.trim()) { notify('Kayıp nedeni yazılmadan kayıt yapılmaz.', 'error'); return; }
    await api(`/api/brands/${id}/pipeline/loss`, { method: 'POST', body: JSON.stringify({ reason }) });
    notify('Kayıp kaydı eklendi.');
    await refresh();
  }
  async function cancelLoss(id: string) {
    await api(`/api/brands/${id}/pipeline/loss/cancel`, { method: 'POST' });
    notify('Kayıp kaydı geri alındı.');
    await refresh();
  }
  return <>
    <PageHeader title="Potansiyel markalar" description="Aday, değerlendirme ve görüşme aşamasındaki markaların son temasını ve sıradaki adımını takip edin." action={<PrimaryLink href="/brands/new">Potansiyel marka ekle</PrimaryLink>} />
    <PipelineSummary summary={summary} />
    <Card className="mt-4 overflow-hidden">
      {leads.isPending ? <p className="p-5" role="status">Markalar yükleniyor…</p> : leads.isError || team.isError ? <p className="p-5" role="alert">{leads.error?.message || team.error?.message} <button className="underline" onClick={() => { leads.refetch(); team.refetch(); }}>Yeniden dene</button></p> : !leads.data?.items.length ? <p className="p-5">Bu aşamalarda marka bulunmuyor.</p> : <><div className="table-scroll"><table className="w-full min-w-[1320px] text-left text-sm"><thead><tr>{['Marka / durumu', 'Sorumlu / iletişim', 'Takip aşaması', 'Aşamada kalma', 'Kaynak kanal', 'Son görüşme', 'Sonraki görüşme / adım', 'Bekleme nedeni', 'Kayıp işlemi'].map(label => <th className="px-4 py-3" key={label}>{label}</th>)}</tr></thead><tbody>{leads.data.items.map(lead => { const owner = team.data?.find(x => x.id === lead.followUp?.ownerId); const lost = lead.followUp?.lostOn ? lead.followUp : null; return <tr className="border-t" key={lead.id}>
        <td className="px-4 py-4"><Link className="font-semibold underline" href={'/brands/' + lead.id + '#team-work'}>{lead.name}</Link><div className="mt-1 flex flex-wrap gap-1"><Badge>{turkce(lead.status)}</Badge>{lost && <Badge tone="red">Kayıp</Badge>}</div></td>
        <td className="px-4">{owner ? owner.name + (owner.isActive ? '' : ' (kapalı hesap)') : team.isPending ? 'Yükleniyor…' : 'Sorumlu atanmadı'}<div className="text-xs">{lead.contactName || lead.contactEmail || 'İletişim bilgisi yok'}</div></td>
        <td className="px-4">{leadStages[lead.followUp?.stage ?? 'New']}</td>
        <td className="px-4">{stageDays(lead)}</td>
        <td className="px-4">{leadSources[lead.followUp?.sourceChannel ?? 'Unspecified']}<div className="break-words text-xs">{lead.followUp?.sourceNote || ''}</div></td>
        <td className="px-4">{dayText(lead.lastContactOn)}</td>
        <td className="max-w-64 px-4">{dayText(lead.followUp?.nextContactOn ?? null)}<div className="break-words text-xs">{lead.followUp?.nextStep || 'Adım belirlenmedi'}</div></td>
        <td className="max-w-64 break-words px-4">{lead.followUp?.waitingReason || '—'}</td>
        <td className="px-4">{!canManage ? <span className="text-xs text-[#6d7175]">Yetki yok</span> : lost ? <button className="underline" onClick={() => cancelLoss(lead.id)}>Kaybı geri al</button> : <button className="underline" onClick={() => recordLoss(lead.id, lead.name)}>Kaybı kaydet</button>}{lost && <div className="break-words text-xs">{lost.lostReason}</div>}</td>
      </tr>; })}</tbody></table></div><div className="flex items-center justify-between p-4 text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki</button><span>{leads.data.total} marka · Sayfa {page}</span><button disabled={page * leads.data.pageSize >= leads.data.total} onClick={() => setPage(page + 1)}>Sonraki</button></div></>}
    </Card>
  </>;
}

function Stat({ label, value, detail }: { label: string; value: string; detail?: string }) {
  return <div className="rounded-lg bg-[#f7f7f8] p-4"><div className="label">{label}</div><div className="mt-2 text-2xl font-bold tracking-tight">{value}</div>{detail && <div className="mt-1 text-xs text-[#6d7175]">{detail}</div>}</div>;
}

function PipelineSummary({ summary }: { summary: ReturnType<typeof useQuery<Summary>> }) {
  if (summary.isPending) return <Card className="mt-4 p-5"><p role="status">Aday hattı özeti yükleniyor…</p></Card>;
  if (summary.isError || !summary.data) return <Card className="mt-4 p-5"><p role="alert">{summary.error?.message} <button className="underline" onClick={() => summary.refetch()}>Yeniden dene</button></p></Card>;
  const data = summary.data;
  const openStages = data.stages.filter(x => x.open > 0);
  return <Card className="p-5">
    <h2 className="font-semibold">Aday hattı özeti</h2>
    <p className="mt-1 text-xs text-[#6d7175]">Bekleme süreleri yalnız ölçüm başlangıcından sonra girilen kayıtlardan hesaplanır. Bilinmeyen günler tahmin edilmez.</p>
    <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <Stat label="AÇIK ADAY" value={String(data.open)} detail={`${data.lost} kayıp kaydı`} />
      <Stat label="KAYIP" value={String(data.lost)} detail={data.losses.length ? `${data.losses.length} farklı neden` : 'Kayıp nedeni kaydı yok'} />
      <Stat label="DÖNÜŞÜM ORANI" value={data.conversion.rate === null ? 'Ölçülemedi' : percent(data.conversion.rate / 100)} detail={`${data.conversion.converted} / ${data.conversion.reached} anlaşma`} />
      <Stat label="ÖLÇÜLMEYEN ADAY" value={String(data.conversion.beforeMeasurement + data.conversion.notMeasured)} detail={`${data.conversion.beforeMeasurement} ölçüm başlangıcı öncesi · ${data.conversion.notMeasured} henüz ölçülmeyen`} />
    </div>
    <div className="mt-4 space-y-2">
      <details><summary className="cursor-pointer text-sm font-semibold">Aşamalarda bekleme süreleri</summary>{openStages.length ? <div className="table-scroll mt-2"><table className="w-full min-w-[520px] text-left text-sm"><thead><tr>{['Aşama', 'Açık', 'Ölçülen gün (ort.)', 'En uzun', 'Bilinmeyen'].map(label => <th className="px-3 py-2" key={label}>{label}</th>)}</tr></thead><tbody>{openStages.map(row => <tr className="border-t" key={row.stage}><td className="px-3 py-2">{leadStages[row.stage]}</td><td className="px-3 py-2">{row.open}</td><td className="px-3 py-2">{row.averageDays === null ? 'Ölçülmedi' : `${row.averageDays} gün`}</td><td className="px-3 py-2">{row.longestDays} gün</td><td className="px-3 py-2">{row.unknown}</td></tr>)}</tbody></table></div> : <p className="mt-2 text-sm">Açık aday bulunmuyor.</p>}</details>
      <details><summary className="cursor-pointer text-sm font-semibold">Kaynak dağılımı</summary><ul className="mt-2 space-y-1 text-sm">{data.sources.map(source => <li key={source.channel}>{source.label}: <strong>{source.count}</strong> marka</li>)}</ul></details>
      <details><summary className="cursor-pointer text-sm font-semibold">Kayıp nedenleri</summary>{data.losses.length ? <ul className="mt-2 space-y-1 text-sm">{data.losses.map(item => <li key={item.reason}>{item.reason || 'Neden yazılmadı'}: <strong>{item.count}</strong> marka</li>)}</ul> : <p className="mt-2 text-sm">Henüz kayıp kaydı yok.</p>}</details>
      <details><summary className="cursor-pointer text-sm font-semibold">Yenileme öncesi görüşmeler</summary>{data.renewalPrep.length ? <ul className="mt-2 space-y-1 text-sm">{data.renewalPrep.map(task => <li key={task.taskId}>{task.brandName}: {task.title} · {dayText(task.dueOn)} · {task.assigneeName}{task.completed ? ' · tamamlandı' : ''}</li>)}</ul> : <p className="mt-2 text-sm">Yenileme hazırlık görevi bulunmuyor.</p>}</details>
      <details><summary className="cursor-pointer text-sm font-semibold">Önemli notlar</summary><ul className="mt-2 list-disc space-y-1 pl-5 text-xs text-[#6d7175]">{data.notes.map(note => <li key={note}>{note}</li>)}</ul></details>
    </div>
  </Card>;
}
