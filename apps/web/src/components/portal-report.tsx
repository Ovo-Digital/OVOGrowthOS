'use client';
import { moneyPrecise, percent, API_URL, token, logout } from '@/lib/api';
import { Card } from '@/components/ui/core';
import { turkce } from '@/lib/turkish';

export type PortalSnapshot = { brandName:string; currency:string; version:number; publishedAt:string; metrics:{year:number;month:number;status:string;netRevenue:number;ovoFee:number;adSpend:number;mer:number|null;refundRate:number|null;brandContribution:number;paid:number;outstanding:number}; explanations:{whatHappened:string;whyItMatters:string;nextStep:string}[] };
export async function downloadPortal(path:string,name:string){
  const response=await fetch(API_URL+path,{headers:{authorization:'Bearer '+token()},cache:'no-store'});
  if(response.status===401){logout();return;}
  if(!response.ok)throw new Error('Dosya artık paylaşılmıyor veya erişiminiz yok. Sayfayı yenileyin.');
  const url=URL.createObjectURL(await response.blob());const link=document.createElement('a');link.href=url;link.download=name;link.click();URL.revokeObjectURL(url);
}
export function PortalReportView({data}:{data:PortalSnapshot}){
  const m=data.metrics;const values:[string,string][]=[['Net ciro',moneyPrecise(m.netRevenue,data.currency)],['OVO hakedişi',moneyPrecise(m.ovoFee,data.currency)],['Reklam gideri',moneyPrecise(m.adSpend,data.currency)],['Markaya kalan katkı',moneyPrecise(m.brandContribution,data.currency)],['Hakedişe bağlı ödenen',moneyPrecise(m.paid,data.currency)],['Kalan alacak',moneyPrecise(m.outstanding,data.currency)],['Reklam verimliliği',m.mer===null?'Hesaplanamadı':m.mer.toLocaleString('tr-TR',{maximumFractionDigits:2})+'x'],['İade oranı',m.refundRate===null?'Hesaplanamadı':percent(m.refundRate)]];
  return <Card className="my-4 space-y-4 p-5"><h2 className="text-xl font-semibold">{data.brandName} · {m.month}/{m.year}</h2><p className="text-sm">Sürüm {data.version} · {turkce(m.status)} · Yayımlanma: {new Date(data.publishedAt).toLocaleString('tr-TR',{timeZone:'Europe/Istanbul'})} (Türkiye saati)</p><p className="text-sm">Bu rapor yayımlandığı andaki bilgileri gösterir; daha sonraki ödemeler veya düzeltmeler kendiliğinden yansımaz. Tutarlar KDV hariçtir. Markaya kalan katkı, vergi sonrası net kâr veya banka bakiyesi değildir.</p><dl className="grid gap-4 sm:grid-cols-2">{values.map(([label,value])=><div key={label} className="rounded-lg border p-3"><dt className="text-sm text-gray-600">{label}</dt><dd className="mt-1 text-lg font-semibold">{value}</dd></div>)}</dl><h3 className="font-semibold">Rakamlar ne anlatıyor?</h3>{data.explanations.map((e,i)=><div key={i} className="border-t pt-3 text-sm"><p className="font-semibold">{e.whatHappened}</p><p className="my-1">{e.whyItMatters}</p><p>{e.nextStep}</p></div>)}</Card>;
}
