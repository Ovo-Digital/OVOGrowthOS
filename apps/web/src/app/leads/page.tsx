'use client';
import Link from 'next/link';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { Badge, Card, PageHeader, PrimaryLink } from '@/components/ui/core';
import { leadStages } from '@/components/brand-team';
import { dayText, type Paged, type TeamMember } from '@/components/work-tasks';
import { turkce } from '@/lib/turkish';
type Lead = { id: string; name: string; status: string; contactName: string; contactEmail: string; lastContactOn: string | null; followUp: { ownerId: string | null; stage: string; waitingReason: string; nextContactOn: string | null; nextStep: string } | null };
export default function Page() {
  const [page, setPage] = useState(1);
  const leads = useQuery({ queryKey: ['lead-follow-ups', page], queryFn: () => api<Paged<Lead>>('/api/lead-follow-ups?page=' + page) });
  const team = useQuery({ queryKey: ['team'], queryFn: () => api<TeamMember[]>('/api/team') });
  return <><PageHeader title="Potansiyel markalar" description="Aday, değerlendirme ve görüşme aşamasındaki markaların son temasını ve sıradaki adımını takip edin." action={<PrimaryLink href="/brands/new">Potansiyel marka ekle</PrimaryLink>} /><Card className="overflow-hidden">
    {leads.isPending ? <p className="p-5" role="status">Markalar yükleniyor…</p> : leads.isError || team.isError ? <p className="p-5" role="alert">{leads.error?.message || team.error?.message} <button className="underline" onClick={() => { leads.refetch(); team.refetch(); }}>Yeniden dene</button></p> : !leads.data?.items.length ? <p className="p-5">Bu aşamalarda marka bulunmuyor.</p> : <><div className="table-scroll"><table className="w-full min-w-[960px] text-left text-sm"><thead><tr>{['Marka / durumu', 'Sorumlu / iletişim', 'Takip aşaması', 'Son görüşme', 'Sonraki görüşme / adım', 'Bekleme nedeni'].map(label => <th className="px-4 py-3" key={label}>{label}</th>)}</tr></thead><tbody>{leads.data.items.map(lead => { const owner = team.data?.find(x => x.id === lead.followUp?.ownerId); return <tr className="border-t" key={lead.id}><td className="px-4 py-4"><Link className="font-semibold underline" href={'/brands/' + lead.id + '#team-work'}>{lead.name}</Link><div className="mt-1"><Badge>{turkce(lead.status)}</Badge></div></td><td className="px-4">{owner ? owner.name + (owner.isActive ? '' : ' (kapalı hesap)') : team.isPending ? 'Yükleniyor…' : 'Sorumlu atanmadı'}<div className="text-xs">{lead.contactName || lead.contactEmail || 'İletişim bilgisi yok'}</div></td><td className="px-4">{leadStages[lead.followUp?.stage ?? 'New']}</td><td className="px-4">{dayText(lead.lastContactOn)}</td><td className="max-w-64 px-4">{dayText(lead.followUp?.nextContactOn ?? null)}<div className="break-words text-xs">{lead.followUp?.nextStep || 'Adım belirlenmedi'}</div></td><td className="max-w-64 break-words px-4">{lead.followUp?.waitingReason || '—'}</td></tr>; })}</tbody></table></div><div className="flex items-center justify-between p-4 text-sm"><button disabled={page <= 1} onClick={() => setPage(page - 1)}>Önceki</button><span>{leads.data.total} marka · Sayfa {page}</span><button disabled={page * leads.data.pageSize >= leads.data.total} onClick={() => setPage(page + 1)}>Sonraki</button></div></>}
  </Card></>;
}
