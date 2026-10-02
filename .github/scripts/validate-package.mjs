import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const root = 'Packages/com.artuandev.novelify';
const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
if (manifest.name !== 'com.artuandev.novelify') throw new Error('Unexpected package name');
if (!/^\d+\.\d+\.\d+$/.test(manifest.version)) throw new Error('Invalid package version');
if (!manifest.dependencies?.['com.unity.ugui']) throw new Error('Missing UGUI dependency');
if (manifest.license !== 'See LICENSE.md') throw new Error('Unexpected package license reference');
for (const file of ['README.md', 'CHANGELOG.md', 'LICENSE.md', 'THIRD_PARTY_NOTICES.md',
  'Runtime/Novelify.Runtime.asmdef', 'Editor/Novelify.Editor.asmdef']) {
  if (!existsSync(join(root, file))) throw new Error(`Missing ${file}`);
}
if (!readFileSync(join(root, 'LICENSE.md'), 'utf8').includes('Novelify Free Use License 1.0')) {
  throw new Error('Package license is missing or unfinished');
}
for (const sample of manifest.samples ?? []) {
  if (!sample.path?.startsWith('Samples~/') || !existsSync(join(root, sample.path))) {
    throw new Error(`Invalid sample path: ${sample.path}`);
  }
}
const guids = new Map();
function visit(dir) {
  for (const item of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, item.name);
    if (item.isDirectory()) visit(path);
    else if (item.name.endsWith('.meta')) {
      const guid = /^guid:\s*([a-f0-9]{32})$/m.exec(readFileSync(path, 'utf8'))?.[1];
      if (!guid) throw new Error(`Missing GUID in ${path}`);
      if (guids.has(guid)) throw new Error(`Duplicate GUID: ${path} and ${guids.get(guid)}`);
      guids.set(guid, path);
    }
  }
}
visit(root);
console.log(`Validated ${manifest.name}@${manifest.version} and ${guids.size} asset GUIDs.`);
