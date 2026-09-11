'use client';
import { Area, AreaChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { money } from '@/lib/api';

export type ReportTrend = { year: number; month: number; recordCount: number; netRevenue: number | null; ovoRevenue: number | null; ovoGrossProfit: number | null; ovoMargin: number | null; mer: number | null };

export function DashboardChart({ data, currency = 'TRY' }: { data: ReportTrend[]; currency?: string }) {
  const values = data.map(x => ({ ...x, label: `${String(x.month).padStart(2, '0')}/${String(x.year).slice(-2)}` }));
  if (!data.some(x => x.recordCount > 0)) return <p className="py-8 text-center text-sm">Seçili kapsamda grafik için veri yok.</p>;
  return <ResponsiveContainer width="100%" height={245}><AreaChart data={values} margin={{ left: 15, right: 10, top: 8 }}><CartesianGrid stroke="#eef0f1" vertical={false} /><XAxis dataKey="label" axisLine={false} tickLine={false} /><YAxis axisLine={false} tickLine={false} tickFormatter={v => `${Number(v) / 1000} bin`} /><Tooltip formatter={v => v == null ? 'Veri yok' : money(Number(v), currency)} /><Legend /><Area type="monotone" connectNulls={false} dataKey="netRevenue" name="Net ciro" stroke="#2c6ecb" fill="#ebf5fa" /><Area type="monotone" connectNulls={false} dataKey="ovoRevenue" name="OVO hakedişi" stroke="#008060" fill="#e3f1df" /><Area type="monotone" connectNulls={false} dataKey="ovoGrossProfit" name="OVO brüt kârı" stroke="#916a00" fill="#fff5d9" /></AreaChart></ResponsiveContainer>;
}

export function DashboardRatioChart({ data }: { data: ReportTrend[] }) {
  const values = data.map(x => ({ ...x, label: `${String(x.month).padStart(2, '0')}/${String(x.year).slice(-2)}`, marginPercent: x.ovoMargin === null ? null : x.ovoMargin * 100 }));
  if (!data.some(x => x.mer !== null || x.ovoMargin !== null)) return <p className="py-8 text-center text-sm">Oranları hesaplamak için yeterli veri yok.</p>;
  return <ResponsiveContainer width="100%" height={210}><LineChart data={values}><CartesianGrid stroke="#eef0f1" vertical={false} /><XAxis dataKey="label" axisLine={false} tickLine={false} /><YAxis yAxisId="mer" axisLine={false} tickLine={false} /><YAxis yAxisId="margin" orientation="right" axisLine={false} tickLine={false} tickFormatter={v => `${v}%`} /><Tooltip /><Legend /><Line yAxisId="mer" connectNulls={false} dataKey="mer" name="Reklam verimliliği (MER)" stroke="#2c6ecb" strokeWidth={2} /><Line yAxisId="margin" connectNulls={false} dataKey="marginPercent" name="OVO brüt kâr marjı %" stroke="#008060" strokeWidth={2} /></LineChart></ResponsiveContainer>;
}
