import http from 'k6/http';
import { check } from 'k6';
import { baseUrl, acceptLanguage, requestTimeout } from './config.js';

const defaultHeaders = {
  Accept: 'application/json',
  'Accept-Language': acceptLanguage(),
};

export function apiGet(path, { group, token, tags = {}, name } = {}) {
  const headers = { ...defaultHeaders };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${baseUrl()}${path}`;
  const params = {
    tags: { group, name: name || group, ...tags },
    timeout: requestTimeout(),
  };

  const res = http.get(url, { headers, ...params });
  const ok = check(res, {
    [`${group} status 2xx`]: (r) => r.status >= 200 && r.status < 300,
  });
  return { res, ok };
}

export function apiPostJson(path, body, { group, token, tags = {}, name } = {}) {
  const headers = {
    ...defaultHeaders,
    'Content-Type': 'application/json',
  };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const url = `${baseUrl()}${path}`;
  const params = {
    tags: { group, name: name || group, ...tags },
    timeout: requestTimeout(),
  };

  const res = http.post(url, JSON.stringify(body), { headers, ...params });
  const ok = check(res, {
    [`${group} status 2xx`]: (r) => r.status >= 200 && r.status < 300,
  });
  return { res, ok };
}
