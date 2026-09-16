'use client';
import { useParams } from 'next/navigation';
import { MonthlyTargets } from '@/components/monthly-targets';

export default function TargetsPage() {
  const { id } = useParams<{ id: string }>();
  return <MonthlyTargets brandId={id} />;
}
