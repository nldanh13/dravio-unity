/**
 * Đọc save do C# ghi (DRAVIO_EXPORT_DIR, từ CrossRuntimeExportTests) bằng parseSaveText của TypeScript gốc @ b33ec7a3.
 *   cp verify-csharp-saves.test.ts <dravio>/tests/unit/zz-verify-csharp-saves.test.ts
 *   DRAVIO_EXPORT_DIR=<dir> npx vitest run tests/unit/zz-verify-csharp-saves.test.ts
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, it } from 'vitest';
import { parseSaveText } from '../../src/persistence/saveFile';
import { canonicalJson } from '../../src/persistence/schema';

it('TypeScript reads every C#-written save with the same state', async () => {
  const dir = process.env.DRAVIO_EXPORT_DIR!;
  const saves = readdirSync(dir).filter((f) => f.endsWith('.save.json'));
  expect(saves.length).toBeGreaterThan(15);
  for (const f of saves) {
    const r = await parseSaveText(readFileSync(join(dir, f), 'utf8'));
    expect(r.ok, f).toBe(true);
    if (!r.ok) continue;
    expect(r.save.migratedFrom, f).toBeNull();
    expect(canonicalJson(r.save.state), f).toBe(readFileSync(join(dir, f.replace('.save.json', '.canonical.txt')), 'utf8'));
  }
  console.log(`verified ${saves.length} C# saves`);
});
