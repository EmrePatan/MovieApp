import { scenario } from 'k6/execution';

/** k6 v2 ramping-vus may leave scenario.vuIdInTest undefined; __VU is stable. */
export function currentVu() {
  const id = scenario.vuIdInTest;
  return typeof id === 'number' && id > 0 ? id : __VU;
}

export function currentIteration() {
  const iter = scenario.iterationInTest;
  return typeof iter === 'number' && iter >= 0 ? iter : __ITER;
}
