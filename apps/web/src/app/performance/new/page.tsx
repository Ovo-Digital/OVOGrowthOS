'use client';
import { useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { PerformanceForm } from '@/components/performance-form';
import { PageHeader } from '@/components/ui/core';

export default function Page() {
  const router = useRouter();
  const queryClient = useQueryClient();
  return <><PageHeader title="Aylık sonuç gir" description="Kaynak verileri girin, hesabı kontrol edin ve taslak olarak kaydedin."/>
    <PerformanceForm onSaved={id => {
      for (const key of ['performance', 'dashboard', 'commissions', 'tasks', 'brand-report']) void queryClient.invalidateQueries({ queryKey: [key] });
      router.push(`/performance/${id}`);
    }}/></>;
}
