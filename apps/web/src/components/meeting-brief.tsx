'use client';
import { useQuery } from '@tanstack/react-query';
import { api, money, type SessionUser } from '@/lib/api';
import { Badge, Card, ErrorState, LoadingState } from '@/components/ui/core';
import { dayText } from '@/components/work-tasks';
import { leadStages, leadSources } from '@/components/brand-team';
import { turkce } from '@/lib/turkish';

type Brief = {
  brandId: string;
  brand: { name: string; legalName: string; website: string; country: string; currency: string; industry: string; subIndustry: string; businessModel: string; platform: string; status: string; contactName: string; contactEmail: string; contactPhone: string };
  contact: { ownerName: string | null; stage: string; waitingReason: string; nextContactOn: string | null; nextStep: string; sourceChannel: string; sourceNote: string; stageEnteredAt: string | null; stageEntryKnown: boolean; stageDays: number | null; lostOn: string | null; lostReason: string; lastContactOn: string | null };
  notes: { contactOn: string; text: string; createdBy: string }[];
  evaluation: { id: string; status: string; decision: string; partnershipScore: number; dataConfidenceScore: number; updatedAt: string } | null;
  deals: { id: string; name: string; status: string; dealType: string; startDate: string }[];
  tasks: { id: string; title: string; dueOn: string; priority: string; kind: string; assigneeName: string; overdue: boolean }[];
  latestPeriod: { year: number; month: number; status: string; netRevenue: number; totalAdSpend: number; mer: number } | null;
  generatedAt: string;
  note: string;
};

const stamp = (value: string) => new Date(value).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' });
const merText = (value: number, adSpend: number) => adSpend > 0 ? `${value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}x` : 'Hesaplanamıyor';
const empty = (value: string | null | undefined) => value && value.trim() ? value : 'Bilgi yok';

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  return <div className="rounded-lg border p-3"><h3 className="text-xs font-bold uppercase tracking-wide text-[#6d7175]">{title}</h3><div className="mt-2 space-y-1.5 text-sm">{children}</div></div>;
}
function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return <p><span className="text-[#6d7175]">{label}:</span> <span className="font-medium">{value}</span></p>;
}

export function MeetingBrief({ brandId }: { brandId: string }) {
  const me = useQuery({ queryKey: ['session-user'], queryFn: () => api<SessionUser>('/api/auth/me') });
  const internal = ['Admin', 'Partner', 'Analyst'].includes(me.data?.role ?? '');
  const query = useQuery({ queryKey: ['meeting-brief', brandId], queryFn: () => api<Brief>(`/api/brands/${brandId}/meeting-brief`), enabled: internal, refetchOnWindowFocus: false });
  if (me.isPending || !internal) return null;
  if (query.isPending) return <Card className="mt-4 p-5"><LoadingState label="Görüşme brief'i hazırlanıyor…" /></Card>;
  if (query.isError) return <Card className="mt-4 p-5"><ErrorState message={`Brief alınamadı. ${query.error.message}`} /></Card>;
  const b = query.data;
  const c = b.contact;
  return <Card className="mt-4 p-5">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 className="font-semibold">Görüşme brief’i</h2>
        <p className="mt-1 text-xs text-[#6d7175]">Görüşme öncesi tek ekranda marka, takip ve açık işlerin özeti · {stamp(b.generatedAt)}</p>
      </div>
      {c.lostOn && <Badge tone="red">Kayıp kaydı var</Badge>}
    </div>
    <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
      <Block title="Marka künyesi">
        <Row label="Sektör" value={empty(b.brand.industry)} />
        <Row label="Alt sektör" value={empty(b.brand.subIndustry)} />
        <Row label="Platform" value={turkce(b.brand.platform)} />
        <Row label="İş modeli" value={turkce(b.brand.businessModel)} />
        <Row label="Ülke" value={b.brand.country} />
        <Row label="İnternet sitesi" value={b.brand.website ? <a className="underline" href={b.brand.website.startsWith('http') ? b.brand.website : `https://${b.brand.website}`} target="_blank" rel="noreferrer">{b.brand.website}</a> : 'Bilgi yok'} />
        <Row label="Durum" value={<Badge>{turkce(b.brand.status)}</Badge>} />
      </Block>
      <Block title="İletişim">
        <Row label="Yetkili" value={empty(b.brand.contactName)} />
        <Row label="E-posta" value={b.brand.contactEmail ? <a className="underline" href={`mailto:${b.brand.contactEmail}`}>{b.brand.contactEmail}</a> : 'Bilgi yok'} />
        <Row label="Telefon" value={empty(b.brand.contactPhone)} />
        <Row label="Kaynak" value={leadSources[c.sourceChannel] ?? c.sourceChannel} />
        <Row label="Kaynak notu" value={empty(c.sourceNote)} />
      </Block>
      <Block title="Görüşme takibi">
        <Row label="Sorumlu" value={empty(c.ownerName)} />
        <Row label="Takip aşaması" value={leadStages[c.stage] ?? c.stage} />
        <Row label="Aşamada kalma" value={c.stageEntryKnown && c.stageDays !== null ? `${c.stageDays} gün` : 'Ölçülmedi'} />
        <Row label="Son görüşme" value={dayText(c.lastContactOn)} />
        <Row label="Sonraki görüşme" value={dayText(c.nextContactOn)} />
        <Row label="Sıradaki adım" value={empty(c.nextStep)} />
        <Row label="Bekleme nedeni" value={empty(c.waitingReason)} />
        {c.lostOn && <Row label="Kayıp nedeni" value={<span className="text-[#d72c0d]">{c.lostReason || 'Neden yazılmadı'}</span>} />}
      </Block>
      <Block title="Son görüşme notları">
        {b.notes.length === 0 ? <p className="text-[#6d7175]">Kayıtlı görüşme notu yok.</p>
          : b.notes.map(note => <div key={`${note.contactOn}-${note.createdBy}`} className="text-xs">
              <p className="font-semibold">{dayText(note.contactOn)} · {note.createdBy || 'Bilinmiyor'}</p>
              <p className="whitespace-pre-wrap break-words text-[#4a4d50]">{note.text}</p>
            </div>)}
      </Block>
      <Block title="Değerlendirme">
        {b.evaluation ? <>
          <Row label="Durum" value={turkce(b.evaluation.status)} />
          <Row label="Karar" value={turkce(b.evaluation.decision)} />
          <Row label="İş ortaklığı puanı" value={`${b.evaluation.partnershipScore} / 100`} />
          <Row label="Veri güven puanı" value={`${b.evaluation.dataConfidenceScore} / 100`} />
          <Row label="Güncelleme" value={stamp(b.evaluation.updatedAt)} />
        </> : <p className="text-[#6d7175]">Henüz değerlendirme yapılmadı.</p>}
      </Block>
      <Block title="Açık anlaşmalar">
        {b.deals.length === 0 ? <p className="text-[#6d7175]">Açık anlaşma yok.</p>
          : b.deals.map(deal => <p key={deal.id}><span className="font-medium">{deal.name}</span> <Badge tone={deal.status === 'Active' ? 'green' : 'blue'}>{turkce(deal.status)}</Badge> <span className="text-xs text-[#6d7175]">{turkce(deal.dealType)}</span></p>)}
      </Block>
      <Block title="Açık görevler">
        {b.tasks.length === 0 ? <p className="text-[#6d7175]">Açık görev yok.</p>
          : b.tasks.map(task => <p key={task.id} className="flex flex-wrap items-center gap-1.5 text-xs">
              <span className="font-medium">{task.title}</span>
              {task.overdue && <Badge tone="red">Gecikti</Badge>}
              <span className="text-[#6d7175]">{dayText(task.dueOn)} · {task.assigneeName || 'Atanmadı'}</span>
            </p>)}
      </Block>
      <Block title="Son aylık kayıt">
        {b.latestPeriod ? <>
          <Row label="Dönem" value={`${String(b.latestPeriod.month).padStart(2, '0')}/${b.latestPeriod.year}`} />
          <Row label="Durum" value={turkce(b.latestPeriod.status)} />
          <Row label="Net ciro" value={money(b.latestPeriod.netRevenue, b.brand.currency)} />
          <Row label="Reklam harcaması" value={money(b.latestPeriod.totalAdSpend, b.brand.currency)} />
          <Row label="Reklam verimliliği" value={merText(b.latestPeriod.mer, b.latestPeriod.totalAdSpend)} />
        </> : <p className="text-[#6d7175]">Bu marka için aylık kayıt yok.</p>}
      </Block>
    </div>
    {b.notes.length === 0 && <p className="mt-3 text-xs text-[#6d7175]">Görüşme notu eklemek için sayfanın altındaki “Sorumlu ve görüşme takibi” bölümünü kullanın.</p>}
    <p className="mt-4 text-xs text-[#6d7175]">{b.note}</p>
  </Card>;
}
