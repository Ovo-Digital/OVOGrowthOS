'use client';
import {Suspense,useMemo,useState} from 'react';
import Link from 'next/link';
import {useSearchParams} from 'next/navigation';
import {useQuery,useQueryClient} from '@tanstack/react-query';
import {api,money,type SessionUser} from '@/lib/api';
import {Badge,Card,PageHeader} from '@/components/ui/core';

type Source={key:string;label:string;state:'entered'|'zero'|'missing'|'notApplicable';amount:number;note:string};
type Alert={code:string;severity:'warning'|'info';finding:string;whyItMatters:string;nextStep:string};
type Approval={preparedBy:string|null;reviewedBy:string|null;complete:boolean};
type Brand={brandId:string;brandName:string;dealId:string|null;expectation:string;performanceId:string|null;status:string|null;origin:string;originDetail:string;responsible:string;responsibleId:string|null;approval:Approval|null;sources:Source[];alerts:Alert[];readiness:'ready'|'attention'|'missing'|'notApplicable';taskId:string|null;taskCompleted:boolean};
type Report={period:{year:number;month:number};label:string;summary:{total:number;ready:number;attention:number;missing:number;notApplicable:number;withOpenTask:number};brands:Brand[]};

const readinessLabels:Record<Brand['readiness'],string>={ready:'Hazır',attention:'İncelenecek',missing:'Kayıt yok',notApplicable:'Kapsam dışı'};
const readinessTones:Record<Brand['readiness'],'green'|'yellow'|'red'|'gray'>={ready:'green',attention:'yellow',missing:'red',notApplicable:'gray'};
const stateLabels:Record<Source['state'],string>={entered:'Girildi',zero:'0 girildi',missing:'Kayıt yok',notApplicable:'Kapsam dışı'};
const expectationLabels:Record<string,string>={expected:'Bu ay anlaşma kapsamı içinde',unknownStart:'Anlaşma başlangıç tarihi girilmediği için kapsam belirlenemiyor',notExpected:'Bu ay anlaşma kapsamı dışında'};
const statusLabels:Record<string,string>={Draft:'Taslak',UnderReview:'İncelemede',Approved:'Onaylandı',Locked:'Kilitli',Invoiced:'Faturalandı',Paid:'Ödendi'};
const originLabels:Record<string,string>={manual:'Elle giriş',import:'Dosyadan aktarım',none:'Kaynak bilinmiyor'};
const months=['Ocak','Şubat','Mart','Nisan','Mayıs','Haziran','Temmuz','Ağustos','Eylül','Ekim','Kasım','Aralık'];
const monthInput=(year:number,month:number)=>`${year}-${String(month).padStart(2,'0')}`;

export default function Page(){return <Suspense fallback={<p role="status">Sayfa hazırlanıyor…</p>}><Board/></Suspense>;}

function Board(){
  const params=useSearchParams();
  const urlTerm=useMemo(()=>{
    const y=Number(params.get('year'));const m=Number(params.get('month'));
    return y>=2020&&y<=2100&&m>=1&&m<=12?monthInput(y,m):null;
  },[params]);
  const me=useQuery({queryKey:['session-user'],queryFn:()=>api<SessionUser>('/api/auth/me')});
  const canWrite=me.data?.role==='Admin'||me.data?.role==='Partner';
  const current=monthInput(new Date().getFullYear(),new Date().getMonth()+1);
  const[picked,setPicked]=useState<string|null>(null);
  const shown=picked??urlTerm??current;
  const term=/^\d{4}-(0[1-9]|1[0-2])$/.test(shown)?shown:urlTerm??current;
  const [year,month]=term.split('-').map(Number);
  const qs=`year=${year}&month=${month}`;
  const report=useQuery({queryKey:['data-quality',qs],queryFn:()=>api<Report>(`/api/data-quality?${qs}`),enabled:me.isSuccess,refetchInterval:60000});
  const qc=useQueryClient();const[busy,setBusy]=useState<string|null>(null);const[notice,setNotice]=useState('');const[error,setError]=useState('');
  async function createTask(item:Brand){
    if(busy||!confirm(`${item.brandName} için ${months[month-1]} ${year} dönemi takip işi açılsın mı?`))return;
    setBusy(item.brandId);setError('');setNotice('');
    try{
      await api('/api/data-quality/track-task',{method:'POST',body:JSON.stringify({brandId:item.brandId,year,month})});
      setNotice(`${item.brandName} için takip işi açıldı. Görev İşlerim listesinde görünür.`);
      await qc.invalidateQueries({queryKey:['data-quality']});await qc.invalidateQueries({queryKey:['tasks']});await qc.invalidateQueries({queryKey:['work-approvals']});
    }catch(e){setError(e instanceof Error?e.message:'Takip işi açılamadı.');}finally{setBusy(null);}
  }
  if(me.isPending)return <p role="status">Yetki kontrol ediliyor…</p>;
  if(me.isError)return <p role="alert">Hesap bilgisi alınamadı.</p>;
  const s=report.data?.summary;
  return <><PageHeader title="Kapanış hazırlığı ve veri kalitesi" description="Bu ay hangi markanın hangi kaynağı eksik, hangi kayıt incelenmeli ve ay kapanmaya hazır mı? Uyarılar sonucu kendiliğinden değiştirmez."/>
    <Card className="mb-4 space-y-3 p-5">
      <div className="flex flex-wrap items-end gap-3">
        <label className="text-sm">Dönem<input type="month" value={shown} onChange={e=>setPicked(e.target.value)} className="mt-1 rounded-lg border px-2 py-2"/></label>
        <button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" disabled={report.isFetching} onClick={()=>void report.refetch()}>Listeyi yenile</button>
        <span className="text-sm text-[#6d7175]">{report.data?`${report.data.label} · ${report.data.summary.total} marka`:'—'}</span>
      </div>
      {s&&<div className="flex flex-wrap gap-2">
        <Badge tone="green">Hazır: {s.ready}</Badge><Badge tone="yellow">İncelenecek: {s.attention}</Badge>
        <Badge tone="red">Kayıt yok: {s.missing}</Badge><Badge tone="gray">Kapsam dışı: {s.notApplicable}</Badge>
        <Badge tone="blue">Açık takip işi: {s.withOpenTask}</Badge></div>}
      <p className="text-sm">Uyarılar otomatik hata kararı değildir; aylık sonucu onaylamaz, kilitlemez veya değiştirmez. Tutar 0 görünüyorsa bu, verinin 0 olduğu değil, 0 girildiği anlamına gelir. İnceleme önerileri önceki aya göre en az %50 ve en az 5.000 birimlik farklarda çıkar.</p>
      <p className="text-sm">Bu ekranda şifre, bağlantı, ileti gövdesi ve kişisel finansal olmayan gizli alan gösterilmez. Kapanış için yine de iki kişi onayı ve kilitleme adımı gerekir.</p>
    </Card>
    {error&&<p role="alert" className="mb-3">{error}</p>}{notice&&<p role="status" className="mb-3">{notice}</p>}
    {report.isPending?<p role="status">Kayıtlar yükleniyor…</p>:report.isError?<p role="alert">Veri kalitesi listesi alınamadı. Yeniden deneyin.</p>:!report.data.brands.length?<p>Bu dönemde incelenecek marka bulunamadı. Marka ve anlaşma kayıtlarını kontrol edin.</p>:
    <div className="space-y-3">{report.data.brands.map(item=><Card key={item.brandId} className="space-y-3 p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div><h2 className="font-semibold">{item.brandName}</h2>
          <p className="text-sm text-[#6d7175]">{expectationLabels[item.expectation]??item.expectation} · Kaynak: {originLabels[item.origin]??item.origin}{item.origin==='import'&&item.originDetail?` (${item.originDetail})`:''} · Sorumlu: {item.responsible||'Atanmadı'}</p>
          {item.status&&<p className="text-sm text-[#6d7175]">{statusLabels[item.status]??item.status} dönem kaydı{item.approval?` · Hazırlayan: ${item.approval.preparedBy||'—'} · Onaylayan: ${item.approval.reviewedBy||'—'}`:''}</p>}
        </div>
        <Badge tone={readinessTones[item.readiness]}>{readinessLabels[item.readiness]}</Badge>
      </div>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">{item.sources.map(source=><div key={source.key} className="rounded-lg border p-3">
        <div className="text-sm font-semibold">{source.label}</div>
        <div className="mt-1 text-lg font-bold">{source.state==='missing'||source.state==='notApplicable'?'—':money(source.amount)}</div>
        <div className="text-xs text-[#6d7175]">{stateLabels[source.state]}</div>
        <p className="mt-1 text-xs text-[#4a4d50]">{source.note}</p></div>)}</div>
      {item.alerts.length>0&&<div className="space-y-2">{item.alerts.map(alert=><div key={alert.code} className={`rounded-lg border p-3 ${alert.severity==='warning'?'border-[#f4c7b0] bg-[#fff6f2]':'border-[#dfe3e8] bg-[#f7f7f8]'}`}>
        <p className="text-sm font-semibold"><Badge tone={alert.severity==='warning'?'red':'blue'}>{alert.severity==='warning'?'Uyarı':'İnceleme önerisi'}</Badge> <span className="ml-1">{alert.finding}</span></p>
        <p className="mt-1 text-sm">{alert.whyItMatters}</p><p className="text-sm text-[#4a4d50]">Yapılacak: {alert.nextStep}</p></div>)}</div>}
      <div className="flex flex-wrap items-center gap-3">
        {item.taskId?<span className="text-sm text-[#006e52]">{item.taskCompleted?'Takip işi tamamlanmış':'Takip işi açık'}</span>
          :canWrite&&item.readiness!=='ready'&&item.readiness!=='notApplicable'&&item.dealId?
            <button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-50" disabled={busy===item.brandId} onClick={()=>void createTask(item)}>{busy===item.brandId?'Açılıyor…':'Tek takip işi aç'}</button>:null}
        <Link className="text-sm underline" href={`/performance?search=${encodeURIComponent(item.brandName)}`}>Aylık sonucu aç</Link>
      </div>
    </Card>)}</div>}
  </>;
}
