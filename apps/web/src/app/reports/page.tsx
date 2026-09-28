import { PortfolioReportView } from '@/components/portfolio-report';
import { BrandReportPicker } from '@/components/brand-report';
import { SectorComparisonCard } from '@/components/sector-comparison';

export default function Page() {
  return <><BrandReportPicker /><SectorComparisonCard /><PortfolioReportView title="Portföy raporu" /></>;
}
