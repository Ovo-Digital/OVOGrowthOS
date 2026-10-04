import { PortfolioReportView } from '@/components/portfolio-report';
import { BrandReportPicker } from '@/components/brand-report';
import { SectorComparisonCard } from '@/components/sector-comparison';
import { BrandProfitabilityCard } from '@/components/brand-profitability';

export default function Page() {
  return <><BrandReportPicker /><BrandProfitabilityCard /><SectorComparisonCard /><PortfolioReportView title="Portföy raporu" /></>;
}
