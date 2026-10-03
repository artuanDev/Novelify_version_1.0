import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

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
const sampleReferences = [];
const generatedSamples = 'Samples~/Generated Samples';
for (const file of ['Characters/Daisy.asset', 'Characters/Hoki.asset',
  'Characters/Template.asset', 'NovelGraphs/Example.novelgraph', 'Scenes/TestScene.unity']) {
  if (!existsSync(join(root, generatedSamples, file))) throw new Error(`Missing packaged sample: ${file}`);
}
function visit(dir) {
  for (const item of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, item.name);
    const packageFolder = relative(root, path).split(sep)[0];
    if (relative(root, path).split(sep).join('/').startsWith(generatedSamples + '/') && item.isFile()) {
      const content = readFileSync(path);
      if (content.subarray(0, 80).toString().startsWith('version https://git-lfs.github.com/spec/v1')) {
        throw new Error(`Packaged sample contains a Git LFS pointer instead of media: ${path}`);
      }
      if (/\.(meta|asset|novelgraph|prefab|unity)$/.test(item.name)) {
        for (const match of content.toString('utf8').matchAll(/guid:\s*([a-f0-9]{32})/g)) {
          sampleReferences.push({ path, guid: match[1] });
        }
      }
    }
    if (item.isDirectory()) visit(path);
    else if (item.name.endsWith('.cs') && packageFolder !== 'Samples~' &&
             readFileSync(path, 'utf8').includes('Assets/Novelify/Samples/')) {
      throw new Error(`Package code depends on development sample assets: ${path}`);
    }
    else if (item.name.endsWith('.meta')) {
      const guid = /^guid:\s*([a-f0-9]{32})$/m.exec(readFileSync(path, 'utf8'))?.[1];
      if (!guid) throw new Error(`Missing GUID in ${path}`);
      if (guids.has(guid)) throw new Error(`Duplicate GUID: ${path} and ${guids.get(guid)}`);
      guids.set(guid, path);
    }
  }
}
visit(root);
// Samples may reference the package, Unity built-ins and the required UGUI package only.
const unityGuids = new Set([
  '0000000000000000e000000000000000', '0000000000000000f000000000000000',
  'fe87c0e1cc204ed48ad3b37840f39efc', // Image
  '4e29b1a8efbd4b44bb3f3716e73f07ff', // Button
  '4f231c4fb786f3946a6b90b886c48677', // StandaloneInputModule
  '76c392e42b5098c458856cdf6ecaaaa1', // EventSystem
  'dc42784cf147c0c48a680349fa168899', // GraphicRaycaster
  '0cd44c1031e13a943bb63640046fad76', // CanvasScaler
]);
for (const { path, guid } of sampleReferences) {
  if (!guids.has(guid) && !unityGuids.has(guid)) {
    throw new Error(`Packaged sample has an unresolved asset reference ${guid}: ${path}`);
  }
}
console.log(`Validated ${manifest.name}@${manifest.version} and ${guids.size} asset GUIDs.`);
