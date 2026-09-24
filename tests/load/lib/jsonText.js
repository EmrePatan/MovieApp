/**
 * k6 open() returns file text; Windows PowerShell often writes UTF-8 with BOM,
 * which breaks JSON.parse (invalid character 'ï' at position 0).
 */
export function stripUtf8Bom(text) {
  if (!text || text.length === 0) {
    return text;
  }
  if (text.charCodeAt(0) === 0xfeff) {
    return text.slice(1);
  }
  return text;
}

export function normalizeOpenPath(path) {
  if (!path) {
    return path;
  }
  return path.replace(/\\/g, '/');
}

export function parseJsonOpen(path) {
  const normalized = normalizeOpenPath(path);
  const raw = open(normalized);
  const trimmed = stripUtf8Bom(raw);
  return JSON.parse(trimmed);
}
