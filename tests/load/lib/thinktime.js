import { sleep } from 'k6';

function randBetween(minSec, maxSec) {
  return minSec + Math.random() * (maxSec - minSec);
}

export function thinkScrollFeed() {
  sleep(randBetween(3, 12));
}

export function thinkDetailPage() {
  sleep(randBetween(5, 25));
}

export function thinkShortGlance() {
  sleep(randBetween(1, 4));
}

export function thinkSearchTyping() {
  sleep(randBetween(2, 8));
}

export function thinkLibraryBrowse() {
  sleep(randBetween(4, 15));
}

export function thinkInsights() {
  sleep(randBetween(6, 20));
}

export function thinkBetweenIterations() {
  sleep(randBetween(2, 6));
}
