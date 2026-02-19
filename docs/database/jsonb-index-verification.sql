-- JSONB index verification script for issue #17.
-- Run against a database with representative data volume.
-- Expected: planner uses GIN indexes for selective @> predicates.

-- Verify JSONB GIN indexes exist
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND indexname IN (
    'ix_reports_results_gin',
    'ix_reports_metadata_gin',
    'ix_report_parameters_raw_parameter_gin',
    'ix_access_grants_scope_gin',
    'ix_audit_logs_details_gin'
  )
ORDER BY indexname;

-- Common selective query pattern: report source lookup in metadata
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)
FROM reports
WHERE metadata @> '{"source":"lab-rare"}'::jsonb;

-- Common selective query pattern: specific parameter code from report results
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)
FROM reports
WHERE results @> '{"parameters":[{"code":"TSH"}]}'::jsonb;

-- Selective query pattern: raw parsed parameter lookup
EXPLAIN (ANALYZE, BUFFERS)
SELECT count(*)
FROM report_parameters
WHERE raw_parameter @> '{"code":"NON_EXISTENT_CODE"}'::jsonb;
