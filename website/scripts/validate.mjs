import { existsSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));

const requiredPaths = [
  "index.html",
  "privacy/index.html",
  "terms/index.html",
  "delete-account/index.html",
  "auth/verify-email/index.html",
  "auth/reset-password/index.html",
  "assets/css/site.css",
  "assets/images/movie-cave-logo.png",
  "wrangler.toml",
  "scripts/build.mjs",
];

const missing = requiredPaths.filter((relativePath) => !existsSync(join(root, relativePath)));

if (missing.length > 0) {
  console.error("Website validation failed. Missing files:");
  for (const path of missing) {
    console.error(` - ${path}`);
  }
  process.exit(1);
}

console.log("Website validation passed.");
