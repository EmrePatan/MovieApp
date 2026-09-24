import http from 'k6/http';
import { check } from 'k6';
import { baseUrl, acceptLanguage, requestTimeout } from './config.js';
import { Expectation, responseCallbackFor, classifyResponse } from './expectations.js';
import { recordHttpSemantics } from './metrics.js';

const defaultHeaders = {
  Accept: 'application/json',
  'Accept-Language': acceptLanguage(),
};

function buildTags({ group, name, tags, expectation, workloadScope }) {
  return {
    group,
    name: name || group,
    expectation,
    workload_scope: workloadScope || 'application',
    ...tags,
  };
}

function executeRequest(method, url, { headers, body, group, expectation, tags, name, workloadScope }) {
  const tagSet = buildTags({ group, name, tags, expectation, workloadScope });
  const params = {
    tags: tagSet,
    timeout: requestTimeout(),
    responseCallback: responseCallbackFor(expectation),
  };

  const res =
    method === 'GET'
      ? http.get(url, { headers, ...params })
      : http.post(url, body, { headers, ...params });

  const classification = classifyResponse(res.status, expectation);
  recordHttpSemantics(classification);

  check(res, {
    [`${group} semantic_success`]: () => classification.semanticSuccess,
    [`${group} not_unexpected_status`]: () => !classification.unexpected,
  });

  if (classification.rateLimited) {
    check(res, {
      [`${group} rate_limited`]: (r) => r.status === 429,
    });
  }

  return { res, classification };
}

export function apiGet(
  path,
  {
    group,
    token,
    tags = {},
    name,
    expectation = Expectation.API_SUCCESS,
    workloadScope = 'application',
  } = {},
) {
  const headers = { ...defaultHeaders };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${baseUrl()}${path}`;
  return executeRequest('GET', url, {
    headers,
    group,
    expectation,
    tags,
    name,
    workloadScope,
  });
}

export function apiPostJson(
  path,
  body,
  {
    group,
    token,
    tags = {},
    name,
    expectation = Expectation.API_SUCCESS,
    workloadScope = 'application',
  } = {},
) {
  const headers = {
    ...defaultHeaders,
    'Content-Type': 'application/json',
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${baseUrl()}${path}`;
  return executeRequest('POST', url, {
    headers,
    body: JSON.stringify(body),
    group,
    expectation,
    tags,
    name,
    workloadScope,
  });
}

export { Expectation };
