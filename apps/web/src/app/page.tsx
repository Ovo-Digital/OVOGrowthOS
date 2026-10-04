import { PortfolioReportView } from '@/components/portfolio-report';
import { AlertsPanel } from '@/components/alerts-panel';

export default function Page() {
  return <><AlertsPanel /><PortfolioReportView title="Portföy özeti" showCharts /></>;
}
