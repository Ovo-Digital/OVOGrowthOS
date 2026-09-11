import { PageHeader } from '@/components/ui/core';
import { WorkTasks } from '@/components/work-tasks';

export default function Page() {
  return <><PageHeader title="İşlerim" description="Sorumluluğunuzdaki görevleri, geciken işleri ve onay bekleyen aylık sonuçları takip edin." /><WorkTasks /></>;
}
