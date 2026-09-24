/**
 * Pure helpers for k6 handleSummary — no k6 runtime imports (testable via selfcheck scenario).
 */

const TRACKED_GROUPS = [
  'home',
  'discover',
  'personalized',
  'movie-detail',
  'tv-detail',
  'detail-status',
  'library',
  'insights',
  'reviews',
  'ratings-me',
];

/** k6 `name` tag — finer-grained than `group` (e.g. TV show detail vs season-1). */
const TRACKED_REQUEST_NAMES = [
  'tv-detail',
  'tv-season-1',
  'discover',
  'explore-preview',
  'personalized',
  'recommendations-home',
];

const LATENCY_STATS = ['med', 'p(90)', 'p(95)', 'p(99)', 'max'];

export function pickMetricValues(metrics, name) {
  const entry = metrics?.[name];
  if (!entry) {
    return null;
  }
  return entry.values ?? entry;
}

export function pickLatency(values) {
  if (!values) {
    return null;
  }
  const out = {};
  for (const key of LATENCY_STATS) {
    if (values[key] !== undefined && values[key] !== null) {
      out[key] = values[key];
    }
  }
  if (values.med !== undefined && values.med !== null) {
    out.p50 = values.med;
  }
  if (values.avg !== undefined) {
    out.avg = values.avg;
  }
  if (values.min !== undefined) {
    out.min = values.min;
  }
  return Object.keys(out).length > 0 ? out : null;
}

export function parseTaggedMetricName(metricName) {
  const brace = metricName.indexOf('{');
  if (brace < 0) {
    return { base: metricName, tags: {} };
  }
  const base = metricName.slice(0, brace);
  const inner = metricName.slice(brace + 1, metricName.lastIndexOf('}'));
  const tags = {};
  for (const part of inner.split(',')) {
    const colon = part.indexOf(':');
    if (colon > 0) {
      const key = part.slice(0, colon).trim();
      const value = part.slice(colon + 1).trim();
      tags[key] = value;
    }
  }
  return { base, tags };
}

export function collectTaggedMetrics(metrics, baseName, tagKey) {
  const byTag = {};
  if (!metrics) {
    return byTag;
  }
  for (const name of Object.keys(metrics)) {
    const parsed = parseTaggedMetricName(name);
    if (parsed.base !== baseName) {
      continue;
    }
    const tagValue = parsed.tags[tagKey];
    if (!tagValue) {
      continue;
    }
    byTag[tagValue] = pickMetricValues(metrics, name);
  }
  return byTag;
}

export function walkCheckGroups(rootGroup, acc = {}) {
  if (!rootGroup) {
    return acc;
  }
  for (const check of rootGroup.checks || []) {
    const semanticSuffix = ' semantic_success';
    const unexpectedSuffix = ' not_unexpected_status';
    if (check.name?.endsWith(semanticSuffix)) {
      const group = check.name.slice(0, -semanticSuffix.length);
      if (!acc[group]) {
        acc[group] = { semanticSuccess: null, notUnexpected: null };
      }
      const total = (check.passes || 0) + (check.fails || 0);
      acc[group].semanticSuccess = {
        passes: check.passes || 0,
        fails: check.fails || 0,
        total,
        rate: total > 0 ? (check.passes || 0) / total : null,
      };
    } else if (check.name?.endsWith(unexpectedSuffix)) {
      const group = check.name.slice(0, -unexpectedSuffix.length);
      if (!acc[group]) {
        acc[group] = { semanticSuccess: null, notUnexpected: null };
      }
      const total = (check.passes || 0) + (check.fails || 0);
      acc[group].notUnexpected = {
        passes: check.passes || 0,
        fails: check.fails || 0,
        total,
        rate: total > 0 ? (check.passes || 0) / total : null,
      };
    }
  }
  for (const child of rootGroup.groups || []) {
    walkCheckGroups(child, acc);
  }
  return acc;
}

export function buildGroupReport(metrics, rootGroup) {
  const durationByGroup = collectTaggedMetrics(metrics, 'http_req_duration', 'group');
  const reqsByGroup = collectTaggedMetrics(metrics, 'http_reqs', 'group');
  const failedByGroup = collectTaggedMetrics(metrics, 'http_req_failed', 'group');
  const checksByGroup = walkCheckGroups(rootGroup);

  const groups = {};
  for (const group of TRACKED_GROUPS) {
    const duration = pickLatency(durationByGroup[group]);
    const reqs = reqsByGroup[group];
    const failed = failedByGroup[group];
    const checks = checksByGroup[group];
    const hasData =
      duration || reqs || failed || checks?.semanticSuccess || checks?.notUnexpected;
    if (!hasData) {
      continue;
    }
    groups[group] = {
      http_reqs: reqs
        ? { count: reqs.count, rate: reqs.rate }
        : null,
      http_req_failed: failed
        ? {
            rate: failed.rate,
            passes: failed.passes,
            fails: failed.fails,
          }
        : null,
      latency: duration,
      checks: checks || null,
    };
  }
  return groups;
}

export function buildRequestNameReport(metrics) {
  const durationByName = collectTaggedMetrics(metrics, 'http_req_duration', 'name');
  const reqsByName = collectTaggedMetrics(metrics, 'http_reqs', 'name');
  const failedByName = collectTaggedMetrics(metrics, 'http_req_failed', 'name');

  const requestNames = {};
  for (const requestName of TRACKED_REQUEST_NAMES) {
    const duration = pickLatency(durationByName[requestName]);
    const reqs = reqsByName[requestName];
    const failed = failedByName[requestName];
    const hasData = duration || reqs || failed;
    if (!hasData) {
      continue;
    }
    requestNames[requestName] = {
      http_reqs: reqs ? { count: reqs.count, rate: reqs.rate } : null,
      http_req_failed: failed
        ? {
            rate: failed.rate,
            passes: failed.passes,
            fails: failed.fails,
          }
        : null,
      latency: duration,
    };
  }
  return requestNames;
}

export function buildOutcomeCounters(metrics) {
  const counterNames = {
    success2xx: 'http_outcome_2xx_success',
    stateAbsent404: 'http_outcome_state_absent_404',
    unexpected4xx: 'http_outcome_unexpected_4xx',
    status401: 'http_outcome_401',
    status403: 'http_outcome_403',
    status429: 'http_outcome_429',
    status5xx: 'http_outcome_5xx',
    transportTimeout: 'http_outcome_transport_timeout',
  };

  const outcomes = {};
  let totalCounted = 0;
  for (const [key, metricName] of Object.entries(counterNames)) {
    const values = pickMetricValues(metrics, metricName);
    const count = values?.count ?? 0;
    outcomes[key] = { count };
    totalCounted += count;
  }

  const httpReqs = pickMetricValues(metrics, 'http_reqs');
  const totalRequests = httpReqs?.count ?? null;
  outcomes._meta = {
    totalRequests,
    totalOutcomeEvents: totalCounted,
  };
  return outcomes;
}

export function buildIterationMetrics(metrics) {
  const iterations = pickMetricValues(metrics, 'iterations');
  const interrupted = pickMetricValues(metrics, 'interrupted_iterations');
  const dropped = pickMetricValues(metrics, 'dropped_iterations');
  return {
    completed: iterations
      ? { count: iterations.count, rate: iterations.rate }
      : null,
    interrupted: interrupted
      ? { count: interrupted.count, rate: interrupted.rate }
      : null,
    dropped: dropped
      ? { count: dropped.count, rate: dropped.rate }
      : null,
  };
}

export function buildApplicationTraffic(metrics) {
  const scoped = collectTaggedMetrics(metrics, 'http_reqs', 'workload_scope');
  const application = scoped.application || pickMetricValues(metrics, 'http_reqs');
  const durationScoped = collectTaggedMetrics(metrics, 'http_req_duration', 'workload_scope');
  const applicationDuration = durationScoped.application;

  return {
    http_reqs: application
      ? { count: application.count, rate: application.rate }
      : null,
    http_req_duration: pickLatency(applicationDuration),
    note:
      scoped.application
        ? 'Filtered by workload_scope=application tag.'
        : 'workload_scope submetric unavailable; using aggregate http_reqs.',
  };
}

export function buildEnhancedReport(data, metadata = {}) {
  const metrics = data?.metrics ?? {};
  const globalDuration = pickLatency(pickMetricValues(metrics, 'http_req_duration'));

  return {
    metadata,
    application: buildApplicationTraffic(metrics),
    iterations: buildIterationMetrics(metrics),
    outcomes: buildOutcomeCounters(metrics),
    metrics: {
      http_reqs: pickMetricValues(metrics, 'http_reqs'),
      http_req_failed: pickMetricValues(metrics, 'http_req_failed'),
      http_req_duration: globalDuration,
      vus: pickMetricValues(metrics, 'vus'),
      vus_max: pickMetricValues(metrics, 'vus_max'),
      iteration_duration: pickLatency(pickMetricValues(metrics, 'iteration_duration')),
      checks: pickMetricValues(metrics, 'checks'),
      semantic_success: pickMetricValues(metrics, 'semantic_success'),
      unexpected_status: pickMetricValues(metrics, 'unexpected_status'),
      rate_limited: pickMetricValues(metrics, 'rate_limited'),
      rate_limited_count: pickMetricValues(metrics, 'rate_limited_count'),
    },
    groups: buildGroupReport(metrics, data?.root_group),
    requestNames: buildRequestNameReport(metrics),
    root_group: data?.root_group,
  };
}

export { TRACKED_GROUPS, TRACKED_REQUEST_NAMES };
