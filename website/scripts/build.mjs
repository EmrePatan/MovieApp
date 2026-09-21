import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const dist = join(root, "dist");

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
];

const publicPaths = [
  "index.html",
  "robots.txt",
  "privacy",
  "terms",
  "delete-account",
  "auth",
  "assets",
];

const missing = requiredPaths.filter((relativePath) => !existsSync(join(root, relativePath)));

if (missing.length > 0) {
  console.error("Website validation failed. Missing files:");
  for (const path of missing) {
    console.error(` - ${path}`);
  }
  process.exit(1);
}

rmSync(dist, { recursive: true, force: true });
mkdirSync(dist, { recursive: true });

for (const relativePath of publicPaths) {
  const source = join(root, relativePath);
  const target = join(dist, relativePath);

  if (!existsSync(source)) {
    continue;
  }

  cpSync(source, target, { recursive: true });
}

console.log("Website build passed. Static assets staged in dist/.");
