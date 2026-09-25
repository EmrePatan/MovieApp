import { check } from 'k6';
import { assertSupportedContentDataset, SUPPORTED_CONTENT_DATASETS } from '../lib/config.js';

function expectThrows(fn) {
  try {
    fn();
    return false;
  } catch (error) {
    return (
      String(error.message).includes('Unsupported LOAD_TEST_CONTENT_DATASET') &&
      SUPPORTED_CONTENT_DATASETS.every((value) => String(error.message).includes(value))
    );
  }
}

export const options = {
  vus: 1,
  iterations: 1,
};

export default function contentDatasetSelfCheck() {
  check(null, {
    'hot is supported': () => assertSupportedContentDataset('hot') === 'hot',
    'varied is supported': () => assertSupportedContentDataset('VARIED') === 'varied',
    'default when empty is hot': () => assertSupportedContentDataset('') === 'hot',
    'cold is rejected': () => expectThrows(() => assertSupportedContentDataset('cold')),
    'unknown is rejected': () => expectThrows(() => assertSupportedContentDataset('warm')),
  });
}
