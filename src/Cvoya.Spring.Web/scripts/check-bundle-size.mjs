#!/usr/bin/env node
/**
 * Bundle-size budget for the dashboard's client JS payload.
 *
 * Run AFTER `npm run build` (or `next build`); fails CI if any of the
 * configured budgets are exceeded.
 *
 * Why this script and not @next/bundle-analyzer or size-limit?
 *   - `next build` under Turbopack doesn't print per-route First Load JS
 *     totals the way Webpack did, so we can't grep stdout.
 *   - `@next/bundle-analyzer` produces an interactive HTML report — great
 *     for humans, useless as a CI gate without bespoke parsing.
 *   - `size-limit` doesn't grok Next's chunked output without an entry
 *     wrapper per route.
 *   - Computing the on-disk sum of `.next/static/chunks/*.js` (raw and
 *     gzipped) plus the largest single chunk catches the regressions we
 *     actually care about (a fat dependency creeping into the client
 *     bundle, or a single route ballooning).
 *
 * Tighten budgets here as the suite shrinks; relax with explicit
 * justification in the PR description.
 */

import { readdir, readFile, stat } from "node:fs/promises";
import { gzipSync } from "node:zlib";
import path from "node:path";
import process from "node:process";

const KB = 1024;

// Budgets — current measured values plus ~30% headroom (2026-04-21).
//   Total uncompressed: ~1290 KB → cap 1700 KB
//   Total gzipped:      ~ 371 KB → cap  500 KB
//   Largest chunk (uncompressed): ~224 KB → cap 300 KB
const BUDGETS = {
  totalUncompressedKb: 1700,
  totalGzippedKb: 500,
  maxChunkUncompressedKb: 300,
};

const projectRoot = path.resolve(import.meta.dirname, "..");
const chunksDir = path.join(projectRoot, ".next", "static", "chunks");

async function ensureChunksDir() {
  try {
    const s = await stat(chunksDir);
    if (!s.isDirectory()) {
      throw new Error(`${chunksDir} is not a directory`);
    }
  } catch (err) {
    console.error(
      `bundle-budget: ${chunksDir} not found. Run \`npm run build\` first.`,
    );
    console.error(err instanceof Error ? err.message : err);
    process.exit(2);
  }
}

async function* walkJs(dir) {
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      yield* walkJs(full);
    } else if (entry.isFile() && entry.name.endsWith(".js")) {
      yield full;
    }
  }
}

async function main() {
  await ensureChunksDir();

  let total = 0;
  let totalGz = 0;
  let largest = { file: "", size: 0 };

  for await (const file of walkJs(chunksDir)) {
    const buf = await readFile(file);
    total += buf.byteLength;
    totalGz += gzipSync(buf).byteLength;
    if (buf.byteLength > largest.size) {
      largest = { file: path.relative(projectRoot, file), size: buf.byteLength };
    }
  }

  const totalKb = Math.round(total / KB);
  const totalGzKb = Math.round(totalGz / KB);
  const largestKb = Math.round(largest.size / KB);

  console.log("bundle-budget report");
  console.log("--------------------");
  console.log(
    `total client JS (uncompressed): ${totalKb} KB / ${BUDGETS.totalUncompressedKb} KB`,
  );
  console.log(
    `total client JS (gzipped):      ${totalGzKb} KB / ${BUDGETS.totalGzippedKb} KB`,
  );
  console.log(
    `largest single chunk:           ${largestKb} KB / ${BUDGETS.maxChunkUncompressedKb} KB (${largest.file})`,
  );

  const failures = [];
  if (totalKb > BUDGETS.totalUncompressedKb) {
    failures.push(
      `total client JS (uncompressed) ${totalKb} KB > ${BUDGETS.totalUncompressedKb} KB budget`,
    );
  }
  if (totalGzKb > BUDGETS.totalGzippedKb) {
    failures.push(
      `total client JS (gzipped) ${totalGzKb} KB > ${BUDGETS.totalGzippedKb} KB budget`,
    );
  }
  if (largestKb > BUDGETS.maxChunkUncompressedKb) {
    failures.push(
      `largest single chunk ${largestKb} KB > ${BUDGETS.maxChunkUncompressedKb} KB budget (${largest.file})`,
    );
  }

  if (failures.length > 0) {
    console.error("");
    console.error("bundle-budget FAILED:");
    for (const f of failures) console.error(`  - ${f}`);
    console.error("");
    console.error(
      "Investigate with `npx next-bundle-analyzer` or by inspecting `.next/static/chunks/`.",
    );
    console.error(
      "If the increase is intentional, raise the budget in scripts/check-bundle-size.mjs and call it out in the PR description.",
    );
    process.exit(1);
  }

  console.log("");
  console.log("bundle-budget OK");
}

await main();
