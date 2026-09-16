import { PageHeader } from '@/components/ui/core';
import { WorkTasks } from '@/components/work-tasks';
import Link from 'next/link';

export default function Page() {
  return <><PageHeader title="İşlerim" description="Sorumluluğunuzdaki görevleri, geciken işleri ve onay bekleyen aylık sonuçları takip edin." /><Link href="/work/planning" className="mb-4 inline-block rounded-lg border px-4 py-2 text-sm font-semibold">Haftalık ekip kapasitesi</Link><WorkTasks /></>;
}
