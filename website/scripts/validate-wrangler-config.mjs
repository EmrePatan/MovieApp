import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const config = readFileSync(join(root, "wrangler.toml"), "utf8");

const errors = [];

if (!config.includes('CATALOG_SHARE_API_ORIGIN = "https://movieapp-fpkg.onrender.com"')) {
  errors.push("CATALOG_SHARE_API_ORIGIN must be https://movieapp-fpkg.onrender.com for production deploys.");
}

if (config.includes('CATALOG_SHARE_API_ORIGIN = ""')) {
  errors.push("CATALOG_SHARE_API_ORIGIN must not be empty in wrangler.toml.");
}

const requiredPatterns = [
  "moviecaveapp.com/movie/*",
  "moviecaveapp.com/tv/*",
  "moviecaveapp.com/watchlist/*",
  "moviecaveapp.com/.well-known/*",
  "open.moviecaveapp.com/movie/*",
  "open.moviecaveapp.com/tv/*",
  "open.moviecaveapp.com/watchlist/*",
  "open.moviecaveapp.com/.well-known/*",
];

for (const pattern of requiredPatterns) {
  if (!config.includes(`pattern = "${pattern}"`)) {
    errors.push(`Missing route pattern: ${pattern}`);
  }
}

if (!config.includes('pattern = "moviecaveapp.com"') || !config.includes("custom_domain = true")) {
  errors.push("Missing custom domain route for moviecaveapp.com");
}

if (!config.includes('CATALOG_SHARE_CANONICAL_ORIGIN = "https://moviecaveapp.com"')) {
  errors.push("CATALOG_SHARE_CANONICAL_ORIGIN must be https://moviecaveapp.com");
}

if (!config.includes('CATALOG_SHARE_APP_OPEN_HOST = "open.moviecaveapp.com"')) {
  errors.push("CATALOG_SHARE_APP_OPEN_HOST must be open.moviecaveapp.com");
}

if (!config.includes('run_worker_first = ["/"]')) {
  errors.push('assets.run_worker_first must include "/" so the API landing wins over dist/index.html');
}

if (errors.length > 0) {
  console.error("Wrangler config validation failed:");
  for (const message of errors) {
    console.error(` - ${message}`);
  }
  process.exit(1);
}

console.log("Wrangler config validation passed.");
