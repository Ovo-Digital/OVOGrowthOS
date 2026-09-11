'use client';
import { useParams } from 'next/navigation';
import { BrandReportView } from '@/components/brand-report';
export default function Page() { const { id } = useParams<{ id: string }>(); return <BrandReportView id={id} />; }
