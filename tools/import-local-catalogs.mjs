// Independently reads the unencrypted English GMD string payloads in the user's RomFS.
// It does not load a save, external repository data, archives, executables, or keys.
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const romfs = process.argv[2];
if (!romfs) throw new Error('Usage: node tools/import-local-catalogs.mjs <romfs folder> [output folder]');
const output = resolve(process.argv[3] ?? join(root, 'src/MhguSave.Format/Data/Local'));

async function strings(relative) {
  const bytes = await readFile(join(romfs, relative));
  if (bytes.length < 40 || bytes.toString('ascii', 0, 4) !== 'GMD\0' || bytes.readUInt32LE(4) !== 0x00010302)
    throw new Error(`Unsupported GMD header: ${relative}`);
  const count = bytes.readUInt32LE(24);
  const length = bytes.readUInt32LE(32);
  if (!count || count > 100000 || !length || length > bytes.length - 40)
    throw new Error(`Invalid GMD string section: ${relative}`);
  const payload = bytes.subarray(bytes.length - length);
  if (payload.at(-1) !== 0) throw new Error(`Unterminated GMD string section: ${relative}`);
  const values = new TextDecoder('utf-8', { fatal: true }).decode(payload).split('\0');
  values.pop();
  if (values.length !== count) throw new Error(`GMD count mismatch: ${relative}: ${values.length}/${count}`);
  return { values, source: relative.replaceAll('\\', '/'), sha256: createHash('sha256').update(bytes).digest('hex') };
}

const monster = await strings('nativeNX/eng/GUI/06_msg/monsterName_eng.gmd');
if (monster.values.length < 137 || monster.values[0] !== 'Rathian' || monster.values[136] !== 'Conga')
  throw new Error('The monster table does not match the supported MHGU index layout.');
const arts = await strings('nativeNX/eng/table/hunterArtsData_eng.gmd');
const awards = await strings('nativeNX/eng/table/GC_Medal_eng.gmd');
if (arts.values.length !== 191 * 4 || awards.values.length !== 132 * 2)
  throw new Error('Hunter Art or award string counts do not match the supported MHGU tables.');
const data = { schemaVersion: 1, monsters: monster.values.slice(0, 137), hunterArtMessages: arts.values, awardMessages: awards.values };
await mkdir(output, { recursive: true });
await writeFile(join(output, 'game-catalog.json'), JSON.stringify(data, null, 2) + '\n', 'utf8');
await writeFile(join(output, 'provenance.json'), JSON.stringify({
  description: 'Generated from the user-local MHGU RomFS; no reference-editor datasets are used.',
  sources: [monster, arts, awards].map(({ source, sha256 }) => ({ source, sha256 })),
  counts: { monsters: data.monsters.length, hunterArtMessages: arts.values.length }
}, null, 2) + '\n', 'utf8');
process.stdout.write(JSON.stringify({ output, monsters: data.monsters.length, artMessages: arts.values.length,
  artSample: arts.values.slice(0, 8), awardCount: awards.values.length, awardSample: awards.values.slice(0, 8) }, null, 2) + '\n');
