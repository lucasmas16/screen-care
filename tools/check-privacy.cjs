// Local, dependency-free heuristic. Never prints matched values.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '..');
const reviewed = JSON.parse(fs.readFileSync(path.join(__dirname, 'privacy-reviewed.json'), 'utf8'));
const skip = new Set(['.git', 'node_modules', '.pnpm-store', '.runtime', 'dist', 'release', 'artifacts', 'test-output', 'test-results', 'coverage', 'bin', 'obj', '.gradle']);
const rules = {
  personalPath: /(?:[A-Z]:[\\/](?:Users|Documents)|\/Users\/|\/home\/)/i,
  email: /[\w.+-]+@[\w.-]+\.[a-z]{2,}/i,
  taxId: /\b\d{3}\.\d{3}\.\d{3}-\d{2}\b|\b\d{2}\.\d{3}\.\d{3}\/\d{4}-\d{2}\b/,
  telephone: /\(\d{2}\)\s*\d{4,5}[- ]?\d{4}/,
  longIdentifier: /(?<![\w.])\d{11,19}(?![\w.])/,
  privateKey: /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY/,
  token: /\bgh[pousr]_[A-Za-z0-9]{20,}|\bgithub_pat_[\w]{20,}|\bAKIA[0-9A-Z]{16}|\bsk-(?:proj-)?[\w-]{20,}/,
  credential: /(?:password|senha|api_?key|secret|access_?token)\s*[:=]\s*['"][^'"]{6,}['"]/i,
  bankAccount: /(?:Conta\s*;|<ACCTID>)\d{4,}/i,
};
const denied = /(?:^|\/)(?:privado|private|prompts|profile|profiles|backups|\.claude|\.agents|\.codex)(?:\/|$)|(?:^|\/)(?:\.env(?:\..+)?|local\.properties|keystore\.properties|Cookies|Login Data|Local State|Preferences)$|\.(?:pdf|xlsx?|csv|ofx|bgm|fgn|db|sqlite\d?|zip|7z|rar|exe|dll|lnk|pem|key|p12|pfx|jks|keystore)$/i;
const files = new Set(), issues = [];
function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name), rel = path.relative(root, full).replaceAll('\\', '/');
    if (entry.isSymbolicLink()) { issues.push({ file: rel, rule: 'link-forbidden' }); continue; }
    if (entry.isDirectory()) { if (!skip.has(entry.name)) walk(full); }
    else if (!rel.endsWith('.tsbuildinfo')) files.add(rel);
  }
}
walk(root);
// Include tracked files even when their directory is normally ignored.
if (fs.existsSync(path.join(root, '.git'))) {
  const res = cp.spawnSync('git', ['-C', root, 'ls-files', '-z'], { encoding: 'utf8' });
  if (res.status !== 0) { console.error('Não foi possível enumerar o índice Git.'); process.exit(2); }
  for (const file of res.stdout.split('\0').filter(Boolean)) files.add(file);
}
for (const file of [...files].sort()) {
  if (file === 'tools/privacy-reviewed.json') continue;
  const full = path.join(root, file);
  if (!fs.existsSync(full)) { issues.push({ file, rule: 'tracked-file-missing' }); continue; }
  if (fs.lstatSync(full).isSymbolicLink()) { issues.push({ file, rule: 'link-forbidden' }); continue; }
  if (denied.test(file) && !file.endsWith('/.env.example') && file !== '.env.example') issues.push({ file, rule: 'private-file-type' });
  const bytes = fs.readFileSync(full);
  const hash = crypto.createHash('sha256').update(bytes).digest('hex');
  const approved = reviewed.files[file];
  const same = approved && approved.sha256 === hash;
  if (approved && !same) issues.push({ file, rule: 'reviewed-content-changed' });
  if (bytes.includes(0)) {
    if (!(same && approved.binary)) issues.push({ file, rule: 'binary-needs-review' });
    continue;
  }
  const lines = bytes.toString('utf8').split(/\r?\n/);
  for (let n = 0; n < lines.length; n++) {
    for (const [rule, regex] of Object.entries(rules)) {
      if (regex.test(lines[n]) && !(same && approved.rules?.includes(rule))) issues.push({ file, line: n + 1, rule });
    }
  }
}
for (const issue of issues) console.error(JSON.stringify(issue));
console.log(JSON.stringify({ checkedFiles: files.size, issues: issues.length }));
process.exitCode = issues.length ? 1 : 0;
