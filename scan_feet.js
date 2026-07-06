const fs = require('fs'), zlib = require('zlib');
function decodePNG(path) {
  const buf = fs.readFileSync(path);
  let off = 8, w, h, bitDepth, colorType, idat = [];
  while (off < buf.length) {
    const len = buf.readUInt32BE(off), type = buf.toString('ascii', off + 4, off + 8);
    const data = buf.slice(off + 8, off + 8 + len);
    if (type === 'IHDR') { w = data.readUInt32BE(0); h = data.readUInt32BE(4); bitDepth = data[8]; colorType = data[9]; }
    else if (type === 'IDAT') idat.push(data);
    off += 12 + len;
  }
  if (bitDepth !== 8 || (colorType !== 6 && colorType !== 2)) throw new Error(`unsupported: depth=${bitDepth} color=${colorType}`);
  const bpp = colorType === 6 ? 4 : 3;
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * bpp, out = Buffer.alloc(h * stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)], row = raw.slice(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const prev = y > 0 ? out.slice((y - 1) * stride, y * stride) : Buffer.alloc(stride);
    const cur = out.slice(y * stride, (y + 1) * stride);
    for (let x = 0; x < stride; x++) {
      const a = x >= bpp ? cur[x - bpp] : 0, b = prev[x], c = x >= bpp ? prev[x - bpp] : 0;
      let v = row[x];
      if (f === 1) v += a; else if (f === 2) v += b; else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) { const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c); v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c; }
      cur[x] = v & 255;
    }
  }
  return { w, h, bpp, data: out };
}
for (const path of process.argv.slice(2)) {
  const { w, h, bpp, data } = decodePNG(path);
  const frames = Math.floor(w / 100);
  console.log(`\n=== ${path} (${w}x${h}) — per-frame feet (bottom-up rows) ===`);
  for (let f = 0; f < frames; f++) {
    const x0 = f * 100;
    // bottommost opaque row + widest row, per frame
    let bottom = -1, widestY = -1, widestW = 0;
    const rowW = [];
    for (let uy = 0; uy < h; uy++) {
      const py = h - 1 - uy;
      let mn = 1e9, mx = -1;
      for (let x = 0; x < 100; x++) {
        const a = bpp === 4 ? data[py * w * bpp + (x0 + x) * bpp + 3] : 255;
        if (a > 25) { if (x < mn) mn = x; if (x > mx) mx = x; }
      }
      const rw = mx >= 0 ? mx - mn + 1 : 0;
      rowW.push(rw);
      if (rw > 0 && bottom < 0) bottom = uy;
      if (rw > widestW) { widestW = rw; widestY = uy; }
    }
    // replicate SpriteAlphaBounds feet heuristic: from widest row scan down to gap
    let feetY = widestY;
    for (let y = widestY - 1; y >= 0; y--) { if (rowW[y] === 0) break; feetY = y; }
    console.log(`frame ${f}: bottomRow=${bottom} feetY=${feetY} (w=${rowW[feetY]}) widest=${widestW}@y=${widestY}`);
  }
}
