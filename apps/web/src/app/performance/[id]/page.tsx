'use client';
import { useParams } from 'next/navigation';
import { PerformanceDetail } from '@/components/performance-detail';

export default function Page() {
  const { id } = useParams<{ id: string }>();
  return <PerformanceDetail key={id} id={id}/>;
}
