/**
 * Đọc save do C# ghi (DRAVIO_EXPORT_DIR, từ CrossRuntimeExportTests / WorldSaveCompatTests / ProgressSaveTests / BreedingSaveTests)
 * bằng parseSaveText của TypeScript gốc @ b33ec7a3.
 *   - *.save.json: ảnh v4 của save C# (tổ trống) — TS phải đọc được với state trùng *.canonical.txt.
 *   - *.v5.json: save Schema v5 thật của bản Unity (U2.5) — TS phải từ chối "newer-version" (chỉ đọc, không ghi đè).
 *   cp verify-csharp-saves.test.ts <dravio>/tests/unit/zz-verify-csharp-saves.test.ts
 *   DRAVIO_EXPORT_DIR=<dir> npx vitest run tests/unit/zz-verify-csharp-saves.test.ts
 */
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { expect, it } from 'vitest';
import { parseSaveText } from '../../src/persistence/saveFile';
import { canonicalJson } from '../../src/persistence/schema';

it('TypeScript reads every C#-written save (v4 projection) with the same state and refuses v5 as newer', async () => {
  const dir = process.env.DRAVIO_EXPORT_DIR!;
  const files = readdirSync(dir);
  const saves = files.filter((f) => f.endsWith('.save.json'));
  const v5 = files.filter((f) => f.endsWith('.v5.json'));
  expect(saves.length).toBeGreaterThan(15);
  expect(v5.length).toBeGreaterThanOrEqual(saves.length);
  for (const f of saves) {
    const r = await parseSaveText(readFileSync(join(dir, f), 'utf8'));
    expect(r.ok, f).toBe(true);
    if (!r.ok) continue;
    expect(r.save.migratedFrom, f).toBeNull();
    expect(canonicalJson(r.save.state), f).toBe(readFileSync(join(dir, f.replace('.save.json', '.canonical.txt')), 'utf8'));
  }
  for (const f of v5) {
    const r = await parseSaveText(readFileSync(join(dir, f), 'utf8'));
    expect(r.ok, f).toBe(false);
    if (!r.ok) expect(r.code, f).toBe('newer-version');
  }
  console.log(`verified ${saves.length} C# saves (v4 projection) and ${v5.length} v5 saves refused as newer`);
});
