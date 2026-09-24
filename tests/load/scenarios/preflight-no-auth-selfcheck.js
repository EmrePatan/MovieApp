/**
 * Ensures health preflight module graph does not require identity pool.
 * Import chain: http -> config (no identities.js).
 */
import { apiGet, Expectation } from '../lib/http.js';

export const options = {
  vus: 0,
  iterations: 0,
};

export default function noop() {}

export function setup() {
  const hasIdentityModule = false;
  return { hasIdentityModule };
}
