# Database

PostgreSQL 17 is mapped with EF Core Code First under schema `growth`. Core tables are Brands, BrandEconomics, Evaluations, RuleSets, RuleDefinitions, Scenarios, PartnershipDeals, PartnershipConditions, MonthlyPerformances, CommissionAdjustments, GeneralSettings, and AuditTrail. Money/rate columns use precision 18, scale 4.

Indexes cover brand name/status, evaluation history, ruleset name/version uniqueness, deal status, scenario evaluation/name, monthly brand/year/month uniqueness, and audit lookup.

`OperatingWorkflow` is a compatibility migration from the MVP schema. It renames deal/audit tables, converts text deal statuses through an explicit PostgreSQL `USING CASE`, copies the former evaluation score into `PartnershipScore`, preserves audit field meaning, and adds the operating entities. The former unversioned MVP rules table is retained as `LegacyRules`; the application reads the new versioned rulesets.

The API applies migrations on startup for local/Compose use. Production should run them once as a release job before scaling instances and should back up the database first.
